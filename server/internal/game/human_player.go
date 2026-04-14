package game

import (
	"context"
	"log/slog"

	"github.com/elebirds/panoptes/internal/domain"
	gamequery "github.com/elebirds/panoptes/internal/game/query"
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
	if phase != domain.PhasePlanning.String() {
		slog.Warn("未知阶段通知", "phase", phase, "player_id", p.playerID)
		return
	}
	rules := staticdata.Default().Rules()
	timeout := rules.PlanningTimeoutSeconds()
	if timeout <= 0 {
		timeout = 35
	}
	currentPolicy := ""
	if room != nil && room.State() != nil {
		if playerState := room.State().Players[p.playerID]; playerState != nil {
			currentPolicy = string(playerState.Policy)
		}
	}
	msg := &pb.MsgPlanningStart{
		Timeout:       int32(timeout),
		Tokens:        int32(rules.TokensPerTurn),
		CurrentPolicy: currentPolicy,
		Phase:         phase,
	}
	if room != nil && room.State() != nil {
		msg.Turn = int32(room.State().Turn)
		snapshot := gamequery.BuildPlanningSnapshot(room.State(), p.playerID)
		snapshot.Phase = phase
		msg.Snapshot = snapshot
	}
	_ = p.Send(msg)
}
