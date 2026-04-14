// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现对局会话模块的运行时状态与生命周期。

package session

import (
	"context"
	"fmt"
	"strings"
	"time"

	"github.com/elebirds/panoptes/internal/config"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/engine/maploader"
	gamequery "github.com/elebirds/panoptes/internal/game/query"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/elebirds/panoptes/internal/transport"
	"github.com/yohamta/donburi"
	"google.golang.org/protobuf/proto"
)

type Player interface {
	PlayerID() string
	Username() string
	IsBot() bool
	Send(msg proto.Message) error
}

type Runtime struct {
	ID        string
	players   []Player
	cfg       *config.Config
	transport transport.GameTransport
	submitCh  chan string
	cancelFn  context.CancelFunc
	state     *domain.GameState
}

func NewRuntime(id string, players []Player, t transport.GameTransport, cfg *config.Config) *Runtime {
	return &Runtime{
		ID:        id,
		players:   append([]Player(nil), players...),
		cfg:       cfg,
		transport: t,
		submitCh:  make(chan string, len(players)*4+16),
	}
}

func (r *Runtime) Initialize() error {
	catalog := staticdata.Default()
	mapID := ""
	if r.cfg != nil {
		mapID = r.cfg.MapID
	}
	if mapID == "" {
		mapID = catalog.DefaultMapID()
	}
	baseMap, err := maploader.LoadMap(catalog, mapID)
	if err != nil {
		return fmt.Errorf("load map %s: %w", mapID, err)
	}

	world := donburi.NewWorld()
	playerIDs := r.humanPlayerIDs()
	usernames := r.humanUsernames()
	seed := time.Now().UnixNano()
	runtimeMap := maploader.GenerateProceduralMap(baseMap, len(playerIDs), seed)
	if runtimeMap == nil {
		return fmt.Errorf("generate procedural map for %s", mapID)
	}
	mapData := maploader.InitWorldFromMap(world, runtimeMap, playerIDs)
	r.state = domain.NewGameState(r.ID, playerIDs, usernames, mapData)
	r.state.World = world
	r.spawnInitialBaseVehicles()
	r.grantDevStartingResources()
	r.initializeCastleStates()

	for _, player := range r.players {
		if player.IsBot() {
			continue
		}
		r.sendStaticCatalogManifest(player)
		r.sendGameInit(player)
	}

	return nil
}

func (r *Runtime) State() *domain.GameState {
	return r.state
}

func (r *Runtime) SetState(state *domain.GameState) {
	r.state = state
}

func (r *Runtime) SubmitChannel() chan string {
	return r.submitCh
}

func (r *Runtime) SetCancelFunc(cancel context.CancelFunc) {
	r.cancelFn = cancel
}

func (r *Runtime) Cancel() {
	if r.cancelFn != nil {
		r.cancelFn()
	}
}

func (r *Runtime) IsDevMode() bool {
	return r != nil && r.cfg != nil && r.cfg.DevMode
}

func (r *Runtime) PlayerIDs() []string {
	ids := make([]string, 0, len(r.players))
	for _, player := range r.players {
		ids = append(ids, player.PlayerID())
	}
	return ids
}

func (r *Runtime) PlayerCount() int {
	return len(r.players)
}

func (r *Runtime) SendToPlayer(playerID string, msg proto.Message) error {
	for _, player := range r.players {
		if player.PlayerID() == playerID {
			return player.Send(msg)
		}
	}
	return fmt.Errorf("player %s not found", playerID)
}

func (r *Runtime) Broadcast(msg proto.Message) {
	for _, player := range r.players {
		_ = player.Send(msg)
	}
}

func (r *Runtime) humanPlayerIDs() []string {
	ids := make([]string, 0, len(r.players))
	for _, player := range r.players {
		if !player.IsBot() {
			ids = append(ids, player.PlayerID())
		}
	}
	return ids
}

func (r *Runtime) humanUsernames() []string {
	usernames := make([]string, 0, len(r.players))
	for _, player := range r.players {
		if !player.IsBot() {
			usernames = append(usernames, player.Username())
		}
	}
	return usernames
}

func (r *Runtime) spawnInitialBaseVehicles() {
	if r == nil || r.state == nil || r.state.World == nil || r.state.Map == nil {
		return
	}

	for playerID := range r.state.Players {
		if strings.TrimSpace(playerID) == "" {
			continue
		}
		if hasTerritoryExpansionUnit(r.state.World, playerID) {
			continue
		}
		spawnPos, ok := r.state.Map.PlayerSpawns[playerID]
		if !ok {
			continue
		}
		ecs.CreateUnit(r.state.World, string(domain.UnitTypeSettler), playerID, spawnPos)
	}
}

func hasTerritoryExpansionUnit(world donburi.World, playerID string) bool {
	found := false
	ecs.AllUnits(world).Each(world, func(entry *donburi.Entry) {
		if found || entry == nil {
			return
		}

		stats := ecs.UnitStatsC.Get(entry)
		if stats.Faction != playerID {
			return
		}

		switch strings.ToLower(strings.TrimSpace(string(stats.Type))) {
		case "settler", "pioneer", "expander", "engineer":
			found = true
		}
	})
	return found
}

func (r *Runtime) grantDevStartingResources() {
	if r == nil || r.state == nil || !r.IsDevMode() {
		return
	}

	for _, player := range r.state.Players {
		if player == nil {
			continue
		}
		player.Resources.Set(domain.ResourceOre, 200)
		player.Resources.Set(domain.ResourceWood, 200)
		player.Resources.Set(domain.ResourceFood, 200)
		player.Resources.Set(domain.ResourceRefinedOre, 100)
		player.Resources.Set(domain.ResourceEngineerMat, 100)
	}
}

func (r *Runtime) initializeCastleStates() {
	if r == nil || r.state == nil || r.state.World == nil {
		return
	}

	primaryCastleByPlayer := make(map[string]string, len(r.state.Players))
	if r.state.Map != nil {
		for playerID, spawnPos := range r.state.Map.PlayerSpawns {
			entry, ok := domain.GetNodeAt(r.state.World, spawnPos)
			if !ok || entry == nil || !entry.HasComponent(ecs.BuildingC) {
				continue
			}

			building := ecs.BuildingC.Get(entry)
			if !strings.EqualFold(string(building.Type), "castle") {
				continue
			}

			primaryCastleByPlayer[playerID] = ecs.NodeC.Get(entry).ID
		}
	}

	ecs.NodesWithBuilding(r.state.World).Each(r.state.World, func(entry *donburi.Entry) {
		if entry == nil {
			return
		}

		building := ecs.BuildingC.Get(entry)
		if !strings.EqualFold(string(building.Type), "castle") {
			return
		}

		node := ecs.NodeC.Get(entry)
		playerID := strings.TrimSpace(building.Owner)
		if playerID == "" {
			playerID = strings.TrimSpace(node.Owner)
		}
		if playerID == "" {
			playerID = strings.TrimSpace(node.TerritoryOwner)
		}
		if playerID == "" {
			return
		}

		r.state.EnsureCastleState(playerID, node.ID)
	})

	for playerID, playerState := range r.state.Players {
		if playerState == nil || len(playerState.Castles) == 0 {
			continue
		}

		primaryCastleID := strings.TrimSpace(primaryCastleByPlayer[playerID])
		if primaryCastleID == "" {
			for castleID := range playerState.Castles {
				primaryCastleID = castleID
				break
			}
		}
		if primaryCastleID == "" {
			continue
		}

		castle := playerState.Castles[primaryCastleID]
		if castle == nil || !castle.Resources.IsZero() {
			continue
		}

		castle.Resources = playerState.Resources.Clone()
	}
}

func (r *Runtime) sendGameInit(p Player) {
	if r.state == nil {
		return
	}
	msg := &pb.MsgGameInit{
		GameId:       r.state.GameID,
		YourPlayerId: p.PlayerID(),
		Turn:         int32(r.state.Turn),
		Phase:        r.state.Phase,
		MapWidth:     int32(r.state.Map.Width),
		MapHeight:    int32(r.state.Map.Height),
		MyPlayer:     gamequery.BuildPlayerView(r.state, p.PlayerID()),
		Ministers:    nil,
		Nodes:        gamequery.BuildNodeViews(r.state, p.PlayerID()),
		Units:        gamequery.BuildUnitViews(r.state),
	}
	_ = p.Send(msg)
}

func (r *Runtime) sendStaticCatalogManifest(p Player) {
	manifest := staticdata.Default().Manifest()
	msg := &pb.MsgStaticCatalogManifest{
		Manifest: &pb.StaticCatalogManifest{
			SchemaVersion:  manifest.SchemaVersion,
			ContentVersion: manifest.ContentVersion,
			BundleHash:     manifest.BundleHash,
			DefaultLocale:  manifest.DefaultLocale,
			DefaultMapId:   manifest.DefaultMapID,
		},
	}
	_ = p.Send(msg)
}
