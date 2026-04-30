// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: Defines domain unlock helper methods.

package domain

import "github.com/elebirds/panoptes/internal/staticdata"

func (s *GameState) HasTechnologyUnlocked(playerID string, technologyID string) bool {
	if s == nil || technologyID == "" {
		return false
	}
	playerState, ok := s.Players[playerID]
	if !ok || playerState == nil {
		return false
	}
	return playerState.Research.HasTechnology(technologyID)
}

func (s *GameState) IsBuildingUnlocked(playerID string, buildingID string) bool {
	if s == nil || buildingID == "" {
		return false
	}
	playerState, ok := s.Players[playerID]
	if !ok || playerState == nil {
		return false
	}
	if playerState.Research.HasBuilding(buildingID) {
		return true
	}
	return !technologyExplicitlyUnlocks("unlock_building", buildingID)
}

func (s *GameState) IsRecipeUnlocked(playerID string, recipeID string) bool {
	if s == nil || recipeID == "" {
		return false
	}
	playerState, ok := s.Players[playerID]
	if !ok || playerState == nil {
		return false
	}
	if playerState.Research.HasRecipe(recipeID) {
		return true
	}
	return !technologyExplicitlyUnlocks("unlock_recipe", recipeID)
}

func (s *GameState) IsPolicyActive(playerID string, policyID string) bool {
	if s == nil || policyID == "" {
		return false
	}
	playerState, ok := s.Players[playerID]
	if !ok || playerState == nil {
		return false
	}
	if string(playerState.Policy) == policyID {
		return true
	}
	for _, activeID := range playerState.Institutions.ActivePolicyIDs {
		if activeID == policyID {
			return true
		}
	}
	return false
}

func technologyExplicitlyUnlocks(effectType string, targetID string) bool {
	if targetID == "" {
		return false
	}
	catalog := staticdata.Default()
	if catalog == nil {
		return false
	}
	for _, technology := range catalog.Technologies() {
		for _, effect := range technology.ExplicitEffects {
			if effect.Type == effectType && effect.TargetID == targetID {
				return true
			}
		}
	}
	return false
}
