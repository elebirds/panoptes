// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载规则 AI 规划器拆分后的候选、评分与意图生成逻辑。

package ai

import (
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
)

func (p *ruleBotPlanner) needsBarracksTechPath() bool {
	if p.req.State == nil {
		return false
	}
	if p.req.State.IsBuildingUnlocked(p.playerID, "barracks") || p.hasOwnedBuildingType("barracks") {
		return false
	}
	return true
}

func (p *ruleBotPlanner) needsFirstBarracks() bool {
	if p.req.State == nil {
		return false
	}
	if !p.req.State.IsBuildingUnlocked(p.playerID, "barracks") {
		return false
	}
	return p.countOwnedCombatProductionBuildings() == 0
}

func (p *ruleBotPlanner) needsMoreMilitaryProduction() bool {
	return p.countOwnedCombatProductionBuildings() > 0 && p.countOwnedCombatProductionBuildings() < 2 && p.countOwnedCombatUnits() <= 1
}

func (p *ruleBotPlanner) shouldPauseExpansionForMilitaryBuildout() bool {
	return p.needsBarracksTechPath() || p.needsFirstBarracks() || p.needsMoreMilitaryProduction()
}

func (p *ruleBotPlanner) needsSettlerProductionBuilding() bool {
	if p.shouldPauseExpansionForMilitaryBuildout() {
		return false
	}
	return len(p.player.Cities) < 2 && p.countOwnedSettlerProductionBuildings() == 0
}

func (p *ruleBotPlanner) isCombatProductionBuilding(buildingID string) bool {
	return p.buildingProducesNonCivilianUnits(buildingID)
}

func (p *ruleBotPlanner) isSettlerProductionBuilding(buildingID string) bool {
	building, ok := staticdata.Default().GetBuilding(strings.TrimSpace(buildingID))
	if !ok {
		return false
	}
	for _, recipeID := range building.RecipeIDs {
		recipe, ok := staticdata.Default().GetRecipe(strings.TrimSpace(recipeID))
		if !ok {
			continue
		}
		for _, unitID := range recipe.Outputs.Units {
			if isCivilianUnit(unitID) {
				return true
			}
		}
	}
	return false
}

func (p *ruleBotPlanner) buildingProducesNonCivilianUnits(buildingID string) bool {
	building, ok := staticdata.Default().GetBuilding(strings.TrimSpace(buildingID))
	if !ok {
		return false
	}
	for _, recipeID := range building.RecipeIDs {
		recipe, ok := staticdata.Default().GetRecipe(strings.TrimSpace(recipeID))
		if !ok {
			continue
		}
		for _, unitID := range recipe.Outputs.Units {
			if !isCivilianUnit(unitID) {
				return true
			}
		}
	}
	return false
}

func protoPosition(pos *pb.Position) domain.Position {
	if pos == nil {
		return domain.Position{}
	}
	return domain.Position{Q: int(pos.GetQ()), R: int(pos.GetR())}
}

func prerequisitesMet(state *domain.GameState, playerID string, prerequisites []staticdata.Prerequisite) bool {
	for _, prereq := range prerequisites {
		switch prereq.Type {
		case "technology_unlocked":
			if !state.HasTechnologyUnlocked(playerID, prereq.TargetID) {
				return false
			}
		case "policy_active":
			if !state.IsPolicyActive(playerID, prereq.TargetID) {
				return false
			}
		}
	}
	return true
}

func isCivilianUnit(unitType string) bool {
	normalized := strings.ToLower(strings.TrimSpace(unitType))
	switch normalized {
	case "settler", "pioneer", "expander", "engineer":
		return true
	default:
		return false
	}
}

func min(a int, b int) int {
	if a < b {
		return a
	}
	return b
}
