// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载规则 AI 规划器拆分后的候选、评分与意图生成逻辑。

package ai

import (
	"strings"

	"github.com/elebirds/panoptes/internal/ecs"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
)

func (p *ruleBotPlanner) scoreTechnology(tech staticdata.TechnologyDefinition) int {
	score := 20 - tech.Tier
	score += matchKeywordScore(tech.ID, tech.Name, tech.Description, []string{"farm", "food", "agri", "agrarian"}, 120)
	score += matchKeywordScore(tech.ID, tech.Name, tech.Description, []string{"settler", "expan", "city"}, 80)
	score += matchKeywordScore(tech.ID, tech.Name, tech.Description, []string{"barracks", "infantry", "archer", "cavalry", "military", "war"}, 60)
	if p.needsBarracksTechPath() {
		switch strings.TrimSpace(tech.ID) {
		case "organized_labor":
			score += 150
		case "mining_survey":
			score += 170
		case "militia_mobilization":
			score += 210
		}
	}
	if p.threatLevel > 0 {
		score += matchKeywordScore(tech.ID, tech.Name, tech.Description, []string{"barracks", "infantry", "archer", "cavalry", "military", "war"}, 60)
	}
	for _, effect := range tech.ExplicitEffects {
		switch effect.Type {
		case "unlock_building":
			score += matchKeywordScore(effect.TargetID, "", "", []string{"farm", "food"}, 100)
			score += matchKeywordScore(effect.TargetID, "", "", []string{"barracks", "stable", "military"}, 55)
		case "unlock_recipe":
			score += matchKeywordScore(effect.TargetID, "", "", []string{"food", "settler"}, 70)
			score += matchKeywordScore(effect.TargetID, "", "", []string{"infantry", "archer", "cavalry"}, 55)
		case "institution_slots":
			score += 35
		}
	}
	return score
}

func (p *ruleBotPlanner) scoreNationalPolicy(policy staticdata.PolicyDefinition) int {
	score := 10
	score += matchKeywordScore(policy.ID, policy.Name, policy.Description, []string{"expan", "frontier", "colon"}, 50)
	score += matchKeywordScore(policy.ID, policy.Name, policy.Description, []string{"recover", "food", "stability"}, 35)
	score += matchKeywordScore(policy.ID, policy.Name, policy.Description, []string{"war", "military", "prepared"}, 30)
	if p.canExpandSoon() {
		score += matchKeywordScore(policy.ID, policy.Name, policy.Description, []string{"expan", "frontier", "colon"}, 45)
	}
	if p.threatLevel > 0 {
		score += matchKeywordScore(policy.ID, policy.Name, policy.Description, []string{"war", "military", "prepared"}, 60)
	}
	if p.foodAmount() <= 0 {
		score += matchKeywordScore(policy.ID, policy.Name, policy.Description, []string{"recover", "food", "stability"}, 40)
	}
	return score
}

func (p *ruleBotPlanner) scoreInstitutionPolicy(policy staticdata.PolicyDefinition) int {
	score := 10
	score += matchKeywordScore(policy.ID, policy.Name, policy.Description, []string{"academy", "research", "science", "knowledge"}, 50)
	score += matchKeywordScore(policy.ID, policy.Name, policy.Description, []string{"industry", "forge", "craft"}, 30)
	if p.threatLevel > 0 {
		score += matchKeywordScore(policy.ID, policy.Name, policy.Description, []string{"military", "war", "drill"}, 40)
	}
	return score
}

func (p *ruleBotPlanner) scoreBuild(node *pb.NodeView, building staticdata.BuildingDefinition) int {
	score := 0
	if node.GetIsResourcePoint() {
		score += matchKeywordScore(building.ID, building.Name, building.Description, []string{node.GetResourceType()}, 120)
		if strings.EqualFold(node.GetResourceType(), "food") {
			score += matchKeywordScore(building.ID, building.Name, building.Description, []string{"farm", "food"}, 90)
			if p.foodAmount() <= 1 {
				score += 40
			}
		}
	}
	if p.threatLevel > 0 {
		score += matchKeywordScore(building.ID, building.Name, building.Description, []string{"barracks", "stable", "tower", "wall"}, 90)
	} else {
		score += matchKeywordScore(building.ID, building.Name, building.Description, []string{"farm", "mine", "lumber", "workshop"}, 50)
	}
	if len(p.player.Cities) < 2 {
		score += matchKeywordScore(building.ID, building.Name, building.Description, []string{"farm", "industry", "workshop"}, 30)
	}
	if p.needsSettlerProductionBuilding() && p.isSettlerProductionBuilding(building.ID) {
		score += 140
	}
	if p.needsFirstBarracks() && strings.EqualFold(building.ID, "barracks") {
		score += 180
	}
	if p.needsMoreMilitaryProduction() && p.isCombatProductionBuilding(building.ID) {
		score += 95
		if !p.hasOwnedBuildingType(building.ID) {
			score += 15
		}
	}
	return score
}

func (p *ruleBotPlanner) scoreRecipe(node *pb.NodeView, recipe staticdata.RecipeDefinition) int {
	score := 0
	if node.GetBuildingTypeId() == "city_core" {
		if p.threatLevel > 0 {
			score += matchKeywordScore(recipe.ID, recipe.Name, recipe.Description, []string{"infantry", "archer", "cavalry"}, 80)
		}
	}
	score += recipe.Outputs.Resources["food"] * 20
	score += recipe.Outputs.Resources["wood"] * 10
	score += recipe.Outputs.Resources["ore"] * 10
	for _, unitID := range recipe.Outputs.Units {
		score += matchKeywordScore(unitID, "", "", []string{"settler"}, 90)
		score += matchKeywordScore(unitID, "", "", []string{"infantry", "archer", "cavalry"}, 70)
	}
	if p.needsSettlerProductionBuilding() {
		for _, unitID := range recipe.Outputs.Units {
			score += matchKeywordScore(unitID, "", "", []string{"settler"}, 120)
		}
	}
	if p.threatLevel > 0 {
		for _, unitID := range recipe.Outputs.Units {
			score += matchKeywordScore(unitID, "", "", []string{"infantry", "archer", "cavalry"}, 50)
		}
	}
	return score
}

func (p *ruleBotPlanner) scoreExpansion(unitID string, nodeID string) int {
	nodeEntry, ok := p.req.State.GetNode(nodeID)
	if !ok || nodeEntry == nil {
		return 0
	}
	node := ecs.NodeC.Get(nodeEntry)
	score := 20
	if node.IsResource {
		score += 40
		if strings.EqualFold(node.ResourceType, "food") {
			score += 20
		}
	}
	if p.closestEnemyDistance(nodeID) <= 2 {
		score -= 40
	}
	if distance := p.closestCityDistance(nodeID); distance >= 2 && distance <= 6 {
		score += 20
	}
	return score
}

func matchKeywordScore(id string, name string, description string, keywords []string, value int) int {
	target := strings.ToLower(strings.Join([]string{id, name, description}, " "))
	for _, keyword := range keywords {
		if strings.Contains(target, strings.ToLower(keyword)) {
			return value
		}
	}
	return 0
}
