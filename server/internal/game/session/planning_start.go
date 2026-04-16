package session

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/event"
	gameprojection "github.com/elebirds/panoptes/internal/game/projection"
	gamequery "github.com/elebirds/panoptes/internal/game/query"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
)

func BuildPlanningStartMessage(state *domain.GameState, playerID string, phase string, planningStartEvents []event.Event) *pb.MsgPlanningStart {
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
		MyPlayer:               gamequery.BuildPlayerView(state, playerID),
		Nodes:                  gamequery.BuildNodeViews(state, playerID),
		Units:                  gamequery.BuildUnitViews(state),
		// planning_start_events 是本轮改造新增的正式事件面。
		// 它只承载“开回合才正式生效”的事件，例如 technology_activated。
		PlanningStartEvents: gameprojection.ProjectPlanningStartEvents(planningStartEvents),
	}
	snapshot := gamequery.BuildPlanningSnapshot(state, playerID)
	snapshot.Phase = phase
	msg.Snapshot = snapshot
	return msg
}
