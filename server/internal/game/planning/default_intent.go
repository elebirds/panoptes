// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-05-01 00:00:00 +0800
// Description: Applies minister default planning intents without client delivery.

package planning

import (
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/engine/economy"
	gameorders "github.com/elebirds/panoptes/internal/game/orders"
)

type DefaultIntentResult struct {
	Applied   bool
	ErrorCode string
}

func ApplyMinisterDefaultIntent(room Session, playerID string, intent Intent) DefaultIntentResult {
	if room == nil || room.State() == nil || strings.TrimSpace(playerID) == "" || intent == nil {
		return DefaultIntentResult{}
	}
	state := room.State()
	playerState := state.Players[playerID]
	if playerState == nil {
		return DefaultIntentResult{ErrorCode: "user_not_found"}
	}
	state.TurnRuntime.Planning.EnsureDraftMaps()

	switch typed := intent.(type) {
	case SetResearchTargetIntent:
		return applyDefaultResearch(room, playerID, strings.TrimSpace(typed.TechnologyID))
	case SetPolicyIntent:
		return applyDefaultPolicy(room, playerID, strings.TrimSpace(typed.NationalPolicyID))
	case SetInstitutionLoadoutIntent:
		return applyDefaultInstitutionLoadout(room, playerID, playerState, typed.InstitutionIDs)
	case BuildStructureIntent:
		return applyDefaultBuild(room, playerID, playerState, typed)
	case SetBuildingRecipeIntent:
		return applyDefaultRecipe(room, playerID, typed)
	case IssueUnitOrderIntent:
		return applyDefaultUnitOrder(room, playerID, typed)
	default:
		return DefaultIntentResult{}
	}
}

func applyDefaultResearch(room Session, playerID string, technologyID string) DefaultIntentResult {
	state := room.State()
	if technologyID == "" || strings.TrimSpace(state.TurnRuntime.Planning.PendingResearchTarget(playerID)) != "" {
		return DefaultIntentResult{}
	}
	validation := economy.ValidateResearchTarget(state, playerID, technologyID)
	if !validation.OK {
		return DefaultIntentResult{ErrorCode: validation.ErrorCode}
	}
	state.TurnRuntime.Planning.SetPendingResearchTarget(playerID, technologyID)
	acceptedDraftID := matchingMinisterDraftID(state, playerID, domain.MinisterDraftKindResearch, technologyID)
	recordMinisterDraftTransitions(room, playerID, state.Turn, reconcileMinisterDraftBindings(state, playerID, acceptedDraftID))
	return DefaultIntentResult{Applied: true}
}

func applyDefaultPolicy(room Session, playerID string, policyID string) DefaultIntentResult {
	state := room.State()
	if policyID == "" || strings.TrimSpace(string(state.TurnRuntime.Planning.PendingPolicy(playerID))) != "" {
		return DefaultIntentResult{}
	}
	if _, errCode := validatePolicySelection(state, playerID, policyID, "national"); errCode != "" {
		return DefaultIntentResult{ErrorCode: errCode}
	}
	state.TurnRuntime.Planning.SetPendingPolicy(playerID, domain.Policy(policyID))
	acceptedDraftID := matchingMinisterDraftID(state, playerID, domain.MinisterDraftKindPolicy, policyID)
	recordMinisterDraftTransitions(room, playerID, state.Turn, reconcileMinisterDraftBindings(state, playerID, acceptedDraftID))
	return DefaultIntentResult{Applied: true}
}

func applyDefaultInstitutionLoadout(room Session, playerID string, playerState *domain.PlayerState, institutionIDs []string) DefaultIntentResult {
	state := room.State()
	if state.TurnRuntime.Planning.HasPendingInstitutionLoadout(playerID) {
		return DefaultIntentResult{}
	}
	normalized, errCode := ValidateInstitutionLoadout(state, playerID, playerState, institutionIDs)
	if len(normalized) == 0 {
		return DefaultIntentResult{}
	}
	if errCode != "" {
		return DefaultIntentResult{ErrorCode: errCode}
	}
	room.SetInstitutionLoadout(playerID, normalized)
	return DefaultIntentResult{Applied: true}
}

func applyDefaultBuild(room Session, playerID string, playerState *domain.PlayerState, intent BuildStructureIntent) DefaultIntentResult {
	nodeID := strings.TrimSpace(intent.NodeID)
	if nodeID == "" || room.State().TurnRuntime.Planning.HasBuildOrder(playerID, nodeID) {
		return DefaultIntentResult{}
	}
	eval := evaluateBuildCommand(room, playerID, playerState, nodeID, intent.BuildingTypeID, intent.CityID)
	if !eval.OK {
		return DefaultIntentResult{ErrorCode: eval.ErrorCode}
	}
	room.QueueBuildOrder(domain.BuildOrder{
		PlayerID:     playerID,
		NodeID:       nodeID,
		BuildingType: strings.TrimSpace(intent.BuildingTypeID),
		CityID:       strings.TrimSpace(intent.CityID),
	})
	return DefaultIntentResult{Applied: true}
}

func applyDefaultRecipe(room Session, playerID string, intent SetBuildingRecipeIntent) DefaultIntentResult {
	nodeID := strings.TrimSpace(intent.NodeID)
	recipeID := strings.TrimSpace(intent.RecipeID)
	if nodeID == "" || recipeID == "" || hasRecipeSelection(room.State(), playerID, nodeID) {
		return DefaultIntentResult{}
	}
	eval := evaluateRecipeCommand(room.State(), playerID, nodeID, recipeID)
	if !eval.OK {
		return DefaultIntentResult{ErrorCode: eval.ErrorCode}
	}
	room.QueueRecipeSelection(domain.RecipeSelectionOrder{PlayerID: playerID, NodeID: nodeID, RecipeID: recipeID})
	return DefaultIntentResult{Applied: true}
}

func applyDefaultUnitOrder(room Session, playerID string, intent IssueUnitOrderIntent) DefaultIntentResult {
	order := gameorders.UnitOrder{
		PlayerID:        playerID,
		UnitID:          strings.TrimSpace(intent.UnitID),
		Action:          gameorders.UnitAction(strings.TrimSpace(intent.Action)),
		TargetNodeID:    strings.TrimSpace(intent.TargetNodeID),
		TargetUnitID:    strings.TrimSpace(intent.TargetUnitID),
		SecondaryNodeID: strings.TrimSpace(intent.SecondaryNodeID),
		Params:          cloneParams(intent.Params),
	}
	if order.UnitID == "" {
		return DefaultIntentResult{}
	}
	if _, exists := room.State().TurnRuntime.Planning.UnitOrders[order.UnitID]; exists {
		return DefaultIntentResult{}
	}
	if errCode := gameorders.ValidatePlanningUnitOrder(room.State(), playerID, order); errCode != "" {
		return DefaultIntentResult{ErrorCode: errCode}
	}
	room.SetUnitOrder(order)
	return DefaultIntentResult{Applied: true}
}

func matchingMinisterDraftID(state *domain.GameState, playerID string, kind domain.MinisterDraftKind, targetID string) string {
	for _, draft := range state.TurnRuntime.Planning.MinisterDraftsForPlayer(playerID) {
		if draft.Kind == kind && strings.TrimSpace(draft.TargetID) == strings.TrimSpace(targetID) {
			return strings.TrimSpace(draft.DraftID)
		}
	}
	return ""
}

func hasRecipeSelection(state *domain.GameState, playerID string, nodeID string) bool {
	if state == nil {
		return false
	}
	return state.TurnRuntime.Planning.HasRecipeSelection(playerID, nodeID)
}
