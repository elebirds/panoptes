package game

import (
	"context"
	"log/slog"
	"time"

	"fmt"
	"github.com/elebirds/panoptes/internal/config"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/engine/maploader"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/transport"
	"github.com/yohamta/donburi"
	"google.golang.org/protobuf/proto"
)

type GameRoom struct {
	ID        string
	Players   []Player
	Phase     string
	Turn      int
	cfg       *config.Config
	transport transport.GameTransport
	submitCh  chan string
	cancelFn  context.CancelFunc
	state     *domain.GameState
}

type Room = GameRoom

var _ transport.GameRoom = (*GameRoom)(nil)

func NewRoom(id string, players []Player, t transport.GameTransport, cfg *config.Config) *GameRoom {
	return &GameRoom{
		ID:        id,
		Players:   players,
		cfg:       cfg,
		transport: t,
		submitCh:  make(chan string, len(players)*4+1),
	}
}

func (r *GameRoom) Start() {
	mapFile, err := maploader.LoadMap(r.cfg.MapPath)
	if err != nil {
		slog.Error("地图加载失败", "room_id", r.ID, "error", err)
		return
	}

	world := donburi.NewWorld()
	playerIDs := r.humanPlayerIDs()
	usernames := r.humanUsernames()
	mapData := maploader.InitWorldFromMap(world, mapFile, playerIDs)
	r.state = domain.NewGameState(r.ID, playerIDs, usernames, mapData)
	r.state.World = world

	r.Turn = r.state.Turn
	r.Phase = r.state.Phase

	slog.Info("地图加载成功", "room_id", r.ID, "map_id", mapFile.ID, "nodes", len(mapFile.Nodes))

	for _, player := range r.Players {
		if player.IsBot() {
			continue
		}
		r.sendGameInit(player)
	}

	Registry.Register(r)
	go r.runLoop()
}

func (r *GameRoom) runLoop() {
	ctx, cancel := context.WithCancel(context.Background())
	r.cancelFn = cancel
	defer Registry.Unregister(r.ID)

	for {
		if ctx.Err() != nil {
			return
		}
		r.notifyTurnStart(ctx)
		r.waitAllSubmit(ctx)
		if ctx.Err() != nil {
			return
		}
		r.settle()
		r.advancePhase()
	}
}

func (r *GameRoom) notifyTurnStart(ctx context.Context) {
	for _, player := range r.Players {
		player.NotifyTurn(ctx, r, r.Phase)
	}
}

func (r *GameRoom) waitAllSubmit(ctx context.Context) {
	submitted := make(map[string]bool, len(r.Players))
	timeout := time.Duration(r.cfg.TurnTimeLimitDomestic) * time.Second
	if r.Phase == "combat" {
		timeout = time.Duration(r.cfg.TurnTimeLimitCombat) * time.Second
	}

	timer := time.NewTimer(timeout)
	defer timer.Stop()

	for {
		select {
		case playerID := <-r.submitCh:
			submitted[playerID] = true
			if len(submitted) == len(r.Players) {
				return
			}
		case <-timer.C:
			missing := make([]string, 0, len(r.Players)-len(submitted))
			for _, player := range r.Players {
				if !submitted[player.PlayerID()] {
					missing = append(missing, player.PlayerID())
				}
			}
			slog.Warn("玩家提交超时", "room_id", r.ID, "turn", r.Turn, "phase", r.Phase, "missing_players", missing)
			return
		case <-ctx.Done():
			return
		}
	}
}

func (r *GameRoom) settle() {
	slog.Info("回合结算", "turn", r.Turn, "phase", r.Phase)
	// TODO: 接入真实 Pipeline 结算，替换当前日志占位逻辑。
}

func (r *GameRoom) advancePhase() {
	switch r.Phase {
	case "domestic":
		r.Phase = "combat"
	case "combat":
		r.Phase = "domestic"
		r.Turn++
	default:
		r.Phase = "domestic"
	}
	if r.state != nil {
		r.state.Phase = r.Phase
		r.state.Turn = r.Turn
	}
}

func (r *GameRoom) submitDomestic(playerID string) {
	r.submitCh <- playerID
}

func (r *GameRoom) submitCombat(playerID string) {
	r.submitCh <- playerID
}

func (r *GameRoom) OnHumanSubmitDomestic(playerID string) {
	r.submitDomestic(playerID)
}

func (r *GameRoom) OnHumanSubmitCombat(playerID string) {
	r.submitCombat(playerID)
}

func (r *GameRoom) Broadcast(msg proto.Message) {
	for _, player := range r.Players {
		if err := player.Send(msg); err != nil {
			slog.Warn("广播消息失败", "room_id", r.ID, "player_id", player.PlayerID(), "error", err)
		}
	}
}

func (r *GameRoom) sendGameInit(p Player) {
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
		MyPlayer:     r.buildPlayerView(p.PlayerID()),
		Ministers:    nil,
		Nodes:        r.buildNodeViews(p.PlayerID()),
		Units:        r.buildUnitViews(),
	}
	if err := p.Send(msg); err != nil {
		slog.Warn("发送游戏初始化消息失败", "room_id", r.ID, "player_id", p.PlayerID(), "error", err)
	}
}

func (r *GameRoom) buildPlayerView(playerID string) *pb.PlayerView {
	playerState := r.state.Players[playerID]
	if playerState == nil {
		return &pb.PlayerView{Id: playerID}
	}

	warZones := make([]*pb.WarZone, 0, len(playerState.WarZones))
	for _, zone := range playerState.WarZones {
		warZones = append(warZones, &pb.WarZone{
			Id:         zone.ID,
			Name:       zone.Name,
			NodeIds:    zone.NodeIDs,
			Directive:  zone.Directive,
			TargetNode: zone.Target,
		})
	}

	return &pb.PlayerView{
		Id:       playerState.PlayerID,
		Username: playerState.Username,
		Resources: func() *pb.Resources {
			resources, unknown := toProtoResources(playerState.Resources)
			if len(unknown) > 0 {
				slog.Warn("存在未映射到协议的资源", "player_id", playerID, "keys", fmt.Sprint(unknown))
			}
			return resources
		}(),
		TokensLeft:    int32(playerState.TokensLeft),
		CurrentPolicy: string(playerState.Policy),
		MainCastleHp:  int32(playerState.MainCastleHP),
		MaxCastleHp:   int32(config.Data.Rules.CastleBaseHP),
		WarZones:      warZones,
	}
}

func (r *GameRoom) buildNodeViews(playerID string) []*pb.NodeView {
	nodes := make([]*pb.NodeView, 0, ecs.AllNodes(r.state.World).Count(r.state.World))
	ecs.AllNodes(r.state.World).Each(r.state.World, func(entry *donburi.Entry) {
		node := ecs.NodeC.Get(entry)
		pos := ecs.PositionC.Get(entry)
		unitsByFaction := domain.UnitsByFactionAtNode(r.state.World, domain.Position{X: pos.X, Y: pos.Y})

		myCount := len(unitsByFaction[playerID])
		enemyCount := 0
		for faction, units := range unitsByFaction {
			if faction == playerID {
				continue
			}
			enemyCount += len(units)
		}

		view := &pb.NodeView{
			Id:              node.ID,
			Pos:             &pb.Position{X: int32(pos.X), Y: int32(pos.Y)},
			Terrain:         string(node.Terrain),
			Owner:           node.Owner,
			MyUnitCount:     int32(myCount),
			EnemyUnitCount:  int32(enemyCount),
			HasRoad:         node.HasRoad,
			IsResourcePoint: node.IsResource,
			ResourceType:    node.ResourceType,
			IsSafeZone:      domain.IsInSafeZone(r.state.World, domain.Position{X: pos.X, Y: pos.Y}, playerID),
		}
		if entry.HasComponent(ecs.BuildingC) {
			building := ecs.BuildingC.Get(entry)
			view.BuildingType = string(building.Type)
			view.BuildingHp = int32(building.HP)
			view.WallLevel = int32(building.WallLevel)
		}
		nodes = append(nodes, view)
	})
	return nodes
}

func (r *GameRoom) buildUnitViews() []*pb.UnitView {
	units := make([]*pb.UnitView, 0, ecs.AllUnits(r.state.World).Count(r.state.World))
	ecs.AllUnits(r.state.World).Each(r.state.World, func(entry *donburi.Entry) {
		stats := ecs.UnitStatsC.Get(entry)
		pos := ecs.PositionC.Get(entry)
		units = append(units, &pb.UnitView{
			Id:       stats.ID,
			Faction:  stats.Faction,
			UnitType: string(stats.Type),
			Hp:       int32(stats.HP),
			MaxHp:    int32(stats.MaxHP),
			Pos:      &pb.Position{X: int32(pos.X), Y: int32(pos.Y)},
		})
	})
	return units
}

func (r *GameRoom) humanPlayerIDs() []string {
	ids := make([]string, 0, len(r.Players))
	for _, player := range r.Players {
		if !player.IsBot() {
			ids = append(ids, player.PlayerID())
		}
	}
	return ids
}

func (r *GameRoom) humanUsernames() []string {
	usernames := make([]string, 0, len(r.Players))
	for _, player := range r.Players {
		if !player.IsBot() {
			usernames = append(usernames, player.Username())
		}
	}
	return usernames
}

func toProtoResources(resources domain.ResourceBag) (*pb.Resources, []domain.ResourceKey) {
	mapped := &pb.Resources{
		Ore:              int32(resources.Get(domain.ResourceOre)),
		Wood:             int32(resources.Get(domain.ResourceWood)),
		Food:             int32(resources.Get(domain.ResourceFood)),
		RefinedOre:       int32(resources.Get(domain.ResourceRefinedOre)),
		EngineerMaterial: int32(resources.Get(domain.ResourceEngineerMat)),
		BuildPoints:      int32(resources.Get(domain.ResourceBuildPoints)),
	}

	unknown := make([]domain.ResourceKey, 0)
	for _, key := range resources.Keys() {
		if !config.IsProtoVisibleResourceKey(key) {
			unknown = append(unknown, key)
		}
	}

	return mapped, unknown
}
