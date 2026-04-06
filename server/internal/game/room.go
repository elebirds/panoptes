package game

import (
	"context"
	"log/slog"
	"time"

	"github.com/elebirds/panoptes/internal/config"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/transport"
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
	r.Turn = 1
	r.Phase = "domestic"
	Registry.Register(r)
	r.sendInitialGameState()
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

func (r *GameRoom) sendInitialGameState() {
	for idx, player := range r.Players {
		if player.IsBot() {
			continue
		}
		if err := player.Send(r.buildInitialMessage(idx, player)); err != nil {
			slog.Warn("发送游戏初始化消息失败", "room_id", r.ID, "player_id", player.PlayerID(), "error", err)
		}
	}
}

func (r *GameRoom) buildInitialMessage(playerIndex int, player Player) *pb.MsgGameInit {
	spawn1Owner := ""
	spawn2Owner := ""
	if len(r.Players) > 0 {
		spawn1Owner = r.Players[0].PlayerID()
	}
	if len(r.Players) > 1 {
		spawn2Owner = r.Players[1].PlayerID()
	}

	return &pb.MsgGameInit{
		GameId:       r.ID,
		YourPlayerId: player.PlayerID(),
		Turn:         1,
		Phase:        "domestic",
		MapWidth:     20,
		MapHeight:    20,
		MyPlayer: &pb.PlayerView{
			Id:            player.PlayerID(),
			Username:      player.Username(),
			Resources:     &pb.Resources{},
			TokensLeft:    int32(r.cfg.TokensPerTurn),
			MainCastleHp:  100,
			MaxCastleHp:   100,
			WarZones:      nil,
			CurrentPolicy: "",
		},
		Ministers: nil,
		Nodes: []*pb.NodeView{
			{
				Id:      "spawn_p1",
				Pos:     &pb.Position{X: 2, Y: 10},
				Terrain: "plain",
				Owner:   spawn1Owner,
			},
			{
				Id:      "spawn_p2",
				Pos:     &pb.Position{X: 17, Y: 10},
				Terrain: "plain",
				Owner:   spawn2Owner,
			},
			{
				Id:              "res_ore",
				Pos:             &pb.Position{X: 10, Y: 8},
				Terrain:         "mountain",
				IsResourcePoint: true,
				ResourceType:    "ore",
			},
			{
				Id:              "res_wood",
				Pos:             &pb.Position{X: 10, Y: 10},
				Terrain:         "forest",
				IsResourcePoint: true,
				ResourceType:    "wood",
			},
			{
				Id:              "res_food",
				Pos:             &pb.Position{X: 10, Y: 12},
				Terrain:         "plain",
				IsResourcePoint: true,
				ResourceType:    "food",
			},
		},
		Units: nil,
	}
}
