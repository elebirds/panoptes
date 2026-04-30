// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
package economy

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/staticdata"
)

func logisticsPriorityForRecipe(state *domain.GameState, playerID string, recipe staticdata.RecipeDefinition) int {
	if state == nil || playerID == "" {
		return 0
	}
	playerState := state.Players[playerID]
	if playerState == nil {
		return 0
	}
	priority := 0
	applyPolicyPriority := func(policyID string) {
		if policyID == "" {
			return
		}
		policy, ok := staticdata.Default().GetPolicy(policyID)
		if !ok {
			return
		}
		for _, candidate := range policy.LogisticsPriority {
			if candidate.TargetID != "" && candidate.TargetID == recipe.ID {
				priority = maxInt(priority, candidate.Priority)
				continue
			}
			if candidate.Tag != "" && recipeHasTag(recipe, candidate.Tag) {
				priority = maxInt(priority, candidate.Priority)
			}
		}
	}
	applyPolicyPriority(string(playerState.Policy))
	for _, policyID := range playerState.Institutions.ActivePolicyIDs {
		applyPolicyPriority(policyID)
	}
	return priority
}

func recipeHasTag(recipe staticdata.RecipeDefinition, tag string) bool {
	for _, candidate := range recipe.Tags {
		if candidate == tag {
			return true
		}
	}
	return false
}

func maxInt(a int, b int) int {
	if a > b {
		return a
	}
	return b
}
