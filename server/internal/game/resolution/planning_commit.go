package resolution

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/event"
)

// BuildPlanningCommitEvents converts pending planning decisions into events
// without requiring a GameRoom.
func BuildPlanningCommitEvents(state *domain.GameState) []event.Event {
	if state == nil {
		return nil
	}
	events := make([]event.Event, 0, len(state.TurnRuntime.Planning.PendingPolicies)+len(state.TurnRuntime.Planning.PendingResearch)+len(state.TurnRuntime.Planning.PendingInstitutions))
	for playerID, policyID := range state.TurnRuntime.Planning.PendingPolicies {
		playerState, ok := state.Players[playerID]
		if !ok || playerState == nil || playerState.Policy == policyID {
			continue
		}
		evt := event.PolicyChangedEvent{
			PlayerID:  playerID,
			OldPolicy: string(playerState.Policy),
			NewPolicy: string(policyID),
		}
		events = append(events, evt)
	}
	for playerID, technologyID := range state.TurnRuntime.Planning.PendingResearch {
		playerState, ok := state.Players[playerID]
		if !ok || playerState == nil || playerState.Research.CurrentTargetTechnologyID == technologyID {
			continue
		}
		evt := event.ResearchTargetChangedEvent{
			PlayerID:     playerID,
			TechnologyID: technologyID,
		}
		events = append(events, evt)
	}
	for playerID := range state.Players {
		playerState := state.Players[playerID]
		if playerState == nil || !state.TurnRuntime.Planning.HasPendingInstitutionLoadout(playerID) {
			continue
		}
		institutionIDs := state.TurnRuntime.Planning.PendingInstitutionLoadout(playerID)
		if institutionSlicesEqual(playerState.Institutions.PendingInstitutionIDs, institutionIDs) && playerState.Institutions.PendingActivationTurn == state.Turn+1 {
			continue
		}
		evt := event.InstitutionLoadoutChangedEvent{
			PlayerID:       playerID,
			InstitutionIDs: institutionIDs,
			ActivationTurn: state.Turn + 1,
		}
		events = append(events, evt)
	}
	return events
}

func institutionSlicesEqual(a []string, b []string) bool {
	if len(a) != len(b) {
		return false
	}
	for idx := range a {
		if a[idx] != b[idx] {
			return false
		}
	}
	return true
}
