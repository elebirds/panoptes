// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载规则 AI 规划器拆分后的候选、评分与意图生成逻辑。

package ai

import (
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/game/planning"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
)

func (p *ruleBotPlanner) chooseBuildIntent() (planning.Intent, bool) {
	if p.req.State == nil || p.player == nil {
		return nil, false
	}
	candidates := make([]buildCandidate, 0)
	for _, node := range p.observation.VisibleNodes {
		if node == nil || node.GetBuildingTypeId() != "" || node.GetTerritoryOwnerPlayerId() != p.playerID {
			continue
		}
		for _, building := range p.availableBuildingsForNode(node) {
			score := p.scoreBuild(node, building)
			if score <= 0 {
				continue
			}
			candidates = append(candidates, buildCandidate{
				key:            node.GetId() + ":" + building.ID,
				score:          score,
				nodeID:         node.GetId(),
				buildingTypeID: building.ID,
				cityID:         p.closestCityID(node.GetId()),
			})
		}
	}
	chosen, ok := pickBestBuildCandidate(p.rng, candidates)
	if !ok {
		return nil, false
	}
	return planning.BuildStructureIntent{
		NodeID:         chosen.nodeID,
		BuildingTypeID: chosen.buildingTypeID,
		CityID:         chosen.cityID,
	}, true
}

func (p *ruleBotPlanner) chooseRecipeIntents() []planning.Intent {
	if p.req.State == nil {
		return nil
	}
	intents := make([]planning.Intent, 0)
	for _, node := range p.observation.VisibleNodes {
		if node == nil || node.GetControllerPlayerId() != p.playerID || node.GetBuildingTypeId() == "" {
			continue
		}
		recipe, ok := p.chooseRecipeForNode(node)
		if !ok {
			continue
		}
		current := ""
		if node.GetOperation() != nil {
			current = node.GetOperation().GetSelectedRecipeId()
		}
		if recipe.recipeID == current {
			continue
		}
		intents = append(intents, planning.SetBuildingRecipeIntent{
			NodeID:   node.GetId(),
			RecipeID: recipe.recipeID,
		})
	}
	return intents
}

func (p *ruleBotPlanner) chooseRecipeForNode(node *pb.NodeView) (recipeCandidate, bool) {
	building, ok := staticdata.Default().GetBuilding(node.GetBuildingTypeId())
	if !ok {
		return recipeCandidate{}, false
	}
	candidates := make([]recipeCandidate, 0)
	for _, recipeID := range building.RecipeIDs {
		recipeID = strings.TrimSpace(recipeID)
		if recipeID == "" || !p.req.State.IsRecipeUnlocked(p.playerID, recipeID) {
			continue
		}
		recipe, ok := staticdata.Default().GetRecipe(recipeID)
		if !ok {
			continue
		}
		score := p.scoreRecipe(node, recipe)
		if score <= 0 {
			continue
		}
		candidates = append(candidates, recipeCandidate{
			key:      node.GetId() + ":" + recipeID,
			score:    score,
			nodeID:   node.GetId(),
			recipeID: recipeID,
		})
	}
	return pickBestRecipeCandidate(p.rng, candidates)
}

func (p *ruleBotPlanner) availableBuildingsForNode(node *pb.NodeView) []staticdata.BuildingDefinition {
	available := make([]staticdata.BuildingDefinition, 0)
	for _, building := range staticdata.Default().Buildings() {
		if building.ID == "" || !p.req.State.IsBuildingUnlocked(p.playerID, building.ID) {
			continue
		}
		if building.ID == "city_core" {
			continue
		}
		if node.GetIsResourcePoint() && strings.TrimSpace(building.RequiredResourceType) != "" && !strings.EqualFold(building.RequiredResourceType, node.GetResourceType()) {
			continue
		}
		if !node.GetIsResourcePoint() && strings.EqualFold(building.PlacementKind, "resource_node") {
			continue
		}
		available = append(available, building)
	}
	return available
}

func (p *ruleBotPlanner) closestCityID(nodeID string) string {
	if p.player == nil || len(p.player.Cities) == 0 {
		return ""
	}
	targetEntry, ok := p.req.State.GetNode(nodeID)
	if !ok || targetEntry == nil {
		if primary := p.req.State.PrimaryCityState(p.playerID); primary != nil {
			return primary.CityID
		}
		return ""
	}
	targetPos := ecs.PositionC.Get(targetEntry)
	bestCityID := ""
	bestDistance := intMax
	for _, city := range p.player.Cities {
		if city == nil {
			continue
		}
		cityEntry, ok := p.req.State.GetNode(city.CoreNodeID)
		if !ok || cityEntry == nil {
			continue
		}
		cityPos := ecs.PositionC.Get(cityEntry)
		distance := (domain.Position{Q: targetPos.Q, R: targetPos.R}).DistanceTo(domain.Position{Q: cityPos.Q, R: cityPos.R})
		if distance < bestDistance {
			bestDistance = distance
			bestCityID = city.CityID
		}
	}
	if bestCityID == "" {
		if primary := p.req.State.PrimaryCityState(p.playerID); primary != nil {
			return primary.CityID
		}
	}
	return bestCityID
}
