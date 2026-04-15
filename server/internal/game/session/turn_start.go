package session

import (
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/elebirds/panoptes/internal/staticdata"
)

func PreparePlanningStartState(state *domain.GameState) {
	if state == nil {
		return
	}
	for _, playerState := range state.Players {
		if playerState == nil {
			continue
		}
		playerState.Research.EnsureProgressMaps()
		playerState.Institutions.EnsureMaps()
	}
	activatePendingTechnologies(state)
	promoteInstitutionLoadouts(state)
}

func activatePendingTechnologies(state *domain.GameState) {
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
			playerState.Research.MarkTechnologyActive(technologyID, state.Turn)
			resolved := domain.ResolveExplicitEffects(technology.ExplicitEffects)
			for _, buildingID := range resolved.UnlockBuildingIDs {
				playerState.Research.UnlockBuilding(buildingID)
			}
			for _, recipeID := range resolved.UnlockRecipeIDs {
				playerState.Research.UnlockRecipe(recipeID)
			}
			for _, policyID := range resolved.UnlockPolicyIDs {
				playerState.Research.UnlockPolicyCandidate(policyID)
				if policy, ok := staticdata.Default().GetPolicy(policyID); ok && strings.EqualFold(policy.Layer, "institutional") {
					playerState.Institutions.UnlockCandidate(policyID)
				}
			}
			playerState.Institutions.SlotCount += resolved.AddInstitutionSlots
			if !resolved.GrantResources.IsZero() || len(resolved.GrantUnitTypes) > 0 {
				event.TechnologyGrantAppliedEvent{
					PlayerID:   playerID,
					Resources:  resolved.GrantResources,
					UnitTypes:  append([]string(nil), resolved.GrantUnitTypes...),
					SourceTech: technology.ID,
				}.Apply(state.World, state)
			}
		}
	}
}

func promoteInstitutionLoadouts(state *domain.GameState) {
	for _, playerState := range state.Players {
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
		playerState.Institutions.ActivePolicyIDs = next
		playerState.Institutions.PendingPolicyIDs = nil
		playerState.Institutions.PendingActivationTurn = 0
	}
}
