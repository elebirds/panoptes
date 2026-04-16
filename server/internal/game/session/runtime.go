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
	"github.com/elebirds/panoptes/internal/event"
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
	Send(ctx context.Context, msg proto.Message) error
}

type Runtime struct {
	ID        string
	players   []Player
	cfg       *config.Config
	transport transport.GameTransport
	submitCh  chan string
	cancelFn  context.CancelFunc
	state     *domain.GameState

	bootstrapPlanningStartSent bool
	// 同一回合内，bootstrap 消息与正式 planning 广播都必须看到同一份 planning-start 结果，
	// 不能因为重复 Prepare 而重复激活 technology / institution。
	planningStartPreparedTurn int
	planningStartResult       *PlanningStartResult
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
	r.bootstrapStartingCapitals()
	r.spawnInitialBaseVehicles()
	r.grantDevStartingResources()
	r.initializeCityStates()
	return r.sendBootstrapMessages()
}

func (r *Runtime) InitializePrepared(state *domain.GameState) error {
	if state == nil {
		return fmt.Errorf("prepared state is nil")
	}
	if state.World == nil {
		return fmt.Errorf("prepared state world is nil")
	}
	if state.Map == nil {
		return fmt.Errorf("prepared state map is nil")
	}

	r.state = state
	if r.state.GameID == "" {
		r.state.GameID = r.ID
	}
	if r.state.NodeIndex == nil {
		r.state.NodeIndex = make(map[string]donburi.Entity)
	}
	if r.state.Map != nil && r.state.Map.NodeIndex != nil && len(r.state.NodeIndex) == 0 {
		for nodeID, entity := range r.state.Map.NodeIndex {
			r.state.NodeIndex[nodeID] = entity
		}
	}
	r.initializeCityStates()
	return r.sendBootstrapMessages()
}

func (r *Runtime) State() *domain.GameState {
	return r.state
}

func (r *Runtime) SetState(state *domain.GameState) {
	r.state = state
	r.planningStartPreparedTurn = 0
	r.planningStartResult = nil
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

func (r *Runtime) ConsumeBootstrapPlanningStart() bool {
	if r == nil || !r.bootstrapPlanningStartSent {
		return false
	}
	r.bootstrapPlanningStartSent = false
	return true
}

func (r *Runtime) SendToPlayer(ctx context.Context, playerID string, msg proto.Message) error {
	for _, player := range r.players {
		if player.PlayerID() == playerID {
			return player.Send(ctx, msg)
		}
	}
	return fmt.Errorf("player %s not found", playerID)
}

func (r *Runtime) PreparePlanningStartStateIfNeeded() {
	if r == nil || r.state == nil || r.state.Phase != domain.PhasePlanning.String() {
		return
	}
	if r.planningStartPreparedTurn == r.state.Turn {
		return
	}
	// 这里是 planning-start 状态推进的唯一受控入口。
	// 其它调用方只读取缓存结果，不再各自直接推进状态。
	r.planningStartResult = PreparePlanningStartState(r.state)
	r.planningStartPreparedTurn = r.state.Turn
}

func (r *Runtime) PlanningStartResult() *PlanningStartResult {
	if r == nil {
		return nil
	}
	return r.planningStartResult
}

func (r *Runtime) Broadcast(ctx context.Context, msg proto.Message) {
	for _, player := range r.players {
		_ = player.Send(ctx, msg)
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
	}
}

func (r *Runtime) bootstrapStartingCapitals() {
	if r == nil || r.state == nil || r.state.World == nil || r.state.Map == nil {
		return
	}

	for playerID, spawnPos := range r.state.Map.PlayerSpawns {
		playerID = strings.TrimSpace(playerID)
		if playerID == "" {
			continue
		}
		spawnEntry, ok := domain.GetNodeAt(r.state.World, spawnPos)
		if !ok || spawnEntry == nil {
			continue
		}

		if !spawnEntry.HasComponent(ecs.BuildingC) {
			ecs.CreateBuilding(r.state.World, "city_core", playerID, ecs.NodeC.Get(spawnEntry).ID, spawnEntry)
		}

		if !spawnEntry.HasComponent(ecs.BuildingC) {
			continue
		}
		building := ecs.BuildingC.Get(spawnEntry)
		if !strings.EqualFold(string(building.Type), "city_core") {
			continue
		}

		cityID := ecs.NodeC.Get(spawnEntry).ID
		footprintEntries, _, reason := ecs.TerritoryFootprint(r.state, spawnEntry)
		if reason == "" {
			for _, entry := range footprintEntries {
				if entry == nil {
					continue
				}
				node := ecs.NodeC.Get(entry)
				node.Owner = playerID
				node.TerritoryOwner = playerID
			}
		} else {
			node := ecs.NodeC.Get(spawnEntry)
			node.Owner = playerID
			node.TerritoryOwner = playerID
		}

		building.Owner = playerID
		building.CityID = cityID
		playerState := r.state.Players[playerID]
		if playerState == nil {
			continue
		}
		playerState.CapitalCityID = cityID
		playerState.CapitalCityCoreHP = building.HP
		cityState := r.state.EnsureCityState(playerID, cityID)
		if cityState != nil {
			cityState.CoreNodeID = cityID
			cityState.OwnerID = playerID
			cityState.OnlineOnTurn = 0
		}
	}
}

func (r *Runtime) initializeCityStates() {
	if r == nil || r.state == nil || r.state.World == nil {
		return
	}

	ecs.NodesWithBuilding(r.state.World).Each(r.state.World, func(entry *donburi.Entry) {
		if entry == nil {
			return
		}

		building := ecs.BuildingC.Get(entry)
		if !strings.EqualFold(string(building.Type), "city_core") {
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

		playerState := r.state.Players[playerID]
		if playerState == nil {
			return
		}
		cityState := r.state.EnsureCityState(playerID, node.ID)
		if cityState != nil {
			cityState.CoreNodeID = node.ID
			cityState.OwnerID = playerID
		}
		if r.state.Map != nil {
			if spawnPos, ok := r.state.Map.PlayerSpawns[playerID]; ok {
				pos := ecs.PositionC.Get(entry)
				if pos.X == spawnPos.X && pos.Y == spawnPos.Y {
					playerState.CapitalCityID = node.ID
					playerState.CapitalCityCoreHP = building.HP
				}
			}
		}
	})
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
	_ = p.Send(context.Background(), msg)
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
	_ = p.Send(context.Background(), msg)
}

func (r *Runtime) sendBootstrapMessages() error {
	r.bootstrapPlanningStartSent = false
	r.PreparePlanningStartStateIfNeeded()
	for _, player := range r.players {
		if player.IsBot() {
			continue
		}
		r.sendStaticCatalogManifest(player)
		r.sendGameInit(player)
		var planningStartEvents []event.Event
		if r.planningStartResult != nil {
			planningStartEvents = r.planningStartResult.Events
		}
		if msg := BuildPlanningStartMessage(r.state, player.PlayerID(), r.state.Phase, planningStartEvents); msg != nil {
			_ = player.Send(context.Background(), msg)
			r.bootstrapPlanningStartSent = true
		}
	}
	return nil
}
