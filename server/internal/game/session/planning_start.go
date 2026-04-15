package session

import (
	"github.com/elebirds/panoptes/internal/domain"
	gamequery "github.com/elebirds/panoptes/internal/game/query"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
)

func BuildPlanningStartMessage(state *domain.GameState, playerID string, phase string) *pb.MsgPlanningStart {
	if state == nil || phase != domain.PhasePlanning.String() {
		return nil
	}

	rules := staticdata.Default().Rules()
	currentPolicy := ""
	tokens := int32(rules.TokensPerTurn)
	if playerState := state.Players[playerID]; playerState != nil {
		currentPolicy = string(playerState.Policy)
		tokens = int32(playerState.TokensLeft)
	}

	msg := &pb.MsgPlanningStart{
		Timeout:                int32(rules.TurnTimeLimitPlanning),
		Turn:                   int32(state.Turn),
		Tokens:                 tokens,
		Phase:                  phase,
		ActiveNationalPolicyId: currentPolicy,
	}
	snapshot := gamequery.BuildPlanningSnapshot(state, playerID)
	snapshot.Phase = phase
	msg.Snapshot = snapshot
	return msg
}
