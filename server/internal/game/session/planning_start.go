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
	return BuildPlanningStartMessageFromObservation(state, gamequery.NewObservationStore().BuildObservation(state, playerID), phase, planningStartEvents)
}

func BuildPlanningStartMessageFromObservation(state *domain.GameState, observation *gamequery.ObservationSnapshot, phase string, planningStartEvents []event.Event) *pb.MsgPlanningStart {
	if state == nil || phase != domain.PhasePlanning.String() {
		return nil
	}
	playerID := resolvePlanningStartPlayerID(state, observation)

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
		MinisterDrafts:         gamequery.BuildMinisterDraftViews(state, playerID),
		MyPlayer:               resolveObservationPlayerView(state, playerID, observation),
		Nodes:                  resolveObservationNodes(state, playerID, observation),
		Units:                  resolveObservationUnits(state, playerID, observation),
		// planning_start_events 是本轮改造新增的正式事件面。
		// 它只承载“开回合才正式生效”的事件，例如 technology_activated。
		PlanningStartEvents: gameprojection.ProjectPlanningStartEvents(int32(state.Turn), planningStartEvents),
	}
	snapshot := gamequery.BuildPlanningSnapshot(state, playerID)
	snapshot.Phase = phase
	msg.Snapshot = snapshot
	return msg
}

func resolveObservationPlayerView(state *domain.GameState, playerID string, observation *gamequery.ObservationSnapshot) *pb.PlayerView {
	if observation != nil && observation.MyPlayer != nil {
		return observation.MyPlayer
	}
	return gamequery.BuildPlayerView(state, playerID)
}

func resolveObservationNodes(state *domain.GameState, playerID string, observation *gamequery.ObservationSnapshot) []*pb.NodeView {
	if observation != nil {
		return observation.Nodes
	}
	return gamequery.BuildNodeViews(state, playerID)
}

func resolveObservationUnits(state *domain.GameState, playerID string, observation *gamequery.ObservationSnapshot) []*pb.UnitView {
	if observation != nil {
		return observation.Units
	}
	return gamequery.BuildUnitViews(state)
}

func resolvePlanningStartPlayerID(state *domain.GameState, observation *gamequery.ObservationSnapshot) string {
	if observation != nil && observation.ViewerID != "" {
		return observation.ViewerID
	}
	if state == nil || len(state.Players) != 1 {
		return ""
	}
	for playerID := range state.Players {
		return playerID
	}
	return ""
}
