package game

import (
	"context"
	"log/slog"

	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/elebirds/panoptes/internal/transport"
	"google.golang.org/protobuf/proto"
)

type HumanPlayer struct {
	playerID  string
	username  string
	transport transport.GameTransport
}

func NewHumanPlayer(playerID, username string, t transport.GameTransport) *HumanPlayer {
	return &HumanPlayer{
		playerID:  playerID,
		username:  username,
		transport: t,
	}
}

func (p *HumanPlayer) PlayerID() string {
	return p.playerID
}

func (p *HumanPlayer) Username() string {
	return p.username
}

func (p *HumanPlayer) IsBot() bool {
	return false
}

func (p *HumanPlayer) Send(msg proto.Message) error {
	return p.transport.Send(p.playerID, msg)
}

func (p *HumanPlayer) NotifyTurn(_ context.Context, room *Room, phase string) {
	rules := staticdata.Default().Rules()
	switch phase {
	case "domestic":
		_ = p.Send(&pb.MsgDomesticPhaseStart{
			Timeout: int32(rules.TurnTimeLimitDomestic),
			Turn:    int32(room.Turn),
			Tokens:  int32(rules.TokensPerTurn),
		})
	case "combat":
		_ = p.Send(&pb.MsgCombatPhaseStart{
			Timeout: int32(rules.TurnTimeLimitCombat),
			Tokens:  int32(rules.TokensPerTurn),
		})
	default:
		slog.Warn("未知阶段通知", "phase", phase, "player_id", p.playerID)
	}
}
