package session

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/elebirds/panoptes/internal/staticdata"
)

func PreparePlanningStartState(state *domain.GameState) *PlanningStartResult {
	return NewPlanningStartRunner().Run(state)
}

func activatePendingTechnologies(state *domain.GameState) []event.Event {
	events := make([]event.Event, 0)
	if state == nil {
		return events
	}
	for playerID, playerState := range state.Players {
		if playerState == nil {
			continue
		}
		for _, technologyID := range playerState.Research.PendingActivationTechnologyIDs() {
			completedTurn := playerState.Research.CompletedTechnologyTurns[technologyID]
			if completedTurn <= 0 || completedTurn >= state.Turn {
				continue
			}
			technology, ok := staticdata.Default().GetTechnology(technologyID)
			if !ok {
				continue
			}
			resolved := domain.ResolveExplicitEffects(technology.ExplicitEffects)
			events = append(events, event.TechnologyActivatedEvent{
				PlayerID:            playerID,
				TechnologyID:        technologyID,
				UnlockBuildingIDs:   append([]string(nil), resolved.UnlockBuildingIDs...),
				UnlockRecipeIDs:     append([]string(nil), resolved.UnlockRecipeIDs...),
				UnlockPolicyIDs:     append([]string(nil), resolved.UnlockPolicyIDs...),
				AddInstitutionSlots: resolved.AddInstitutionSlots,
			})
			if !resolved.GrantResources.IsZero() || len(resolved.GrantUnitTypes) > 0 {
				events = append(events, event.TechnologyGrantAppliedEvent{
					PlayerID:   playerID,
					Resources:  resolved.GrantResources,
					UnitTypes:  append([]string(nil), resolved.GrantUnitTypes...),
					SourceTech: technology.ID,
				})
			}
		}
	}
	return events
}

func promoteInstitutionLoadouts(state *domain.GameState) []event.Event {
	events := make([]event.Event, 0)
	if state == nil {
		return events
	}
	for playerID, playerState := range state.Players {
		if playerState == nil {
			continue
		}
		playerState.Institutions.EnsureMaps()
		if playerState.Institutions.PendingActivationTurn <= 0 || playerState.Institutions.PendingActivationTurn > state.Turn {
			continue
		}
		next := make([]string, 0, len(playerState.Institutions.PendingPolicyIDs))
		for _, policyID := range domain.NormalizePolicyIDList(playerState.Institutions.PendingPolicyIDs) {
			if len(next) >= playerState.Institutions.SlotCount {
				break
			}
			if !playerState.Institutions.HasCandidate(policyID) {
				continue
			}
			next = append(next, policyID)
		}
		events = append(events, event.InstitutionLoadoutActivatedEvent{
			PlayerID:  playerID,
			PolicyIDs: next,
		})
	}
	return events
}
