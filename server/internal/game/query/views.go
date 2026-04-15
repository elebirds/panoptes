// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现对局查询模块的视图构建逻辑。

package query

import (
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func BuildPlayerView(state *domain.GameState, playerID string) *pb.PlayerView {
	if state == nil {
		return &pb.PlayerView{Id: playerID}
	}
	playerState := state.Players[playerID]
	if playerState == nil {
		return &pb.PlayerView{Id: playerID}
	}

	warZones := make([]*pb.WarZone, 0, len(playerState.WarZones))
	for _, zone := range playerState.WarZones {
		warZones = append(warZones, &pb.WarZone{
			Id:         zone.ID,
			Name:       zone.Name,
			NodeIds:    zone.NodeIDs,
			Directive:  zone.Directive,
			TargetNode: zone.Target,
		})
	}

	return &pb.PlayerView{
		Id:       playerState.PlayerID,
		Username: playerState.Username,
		Resources: func() *pb.ResourceBag {
			return ToProtoResourceBag(playerState.Resources)
		}(),
		Points: func() *pb.PointBag {
			return ToProtoPointBag(state, playerState.PlayerID)
		}(),
		TokensLeft:             int32(playerState.TokensLeft),
		ActiveNationalPolicyId: string(playerState.Policy),
		CapitalCityCoreHp:      int32(playerState.CapitalCityCoreHP),
		CapitalCityCoreMaxHp:   int32(staticdata.Default().Rules().CityCoreMaxHP),
		WarZones:               warZones,
		Research:               buildResearchStateView(playerState.Research),
		Institutions: &pb.InstitutionStateView{
			SlotCount:          int32(playerState.Institutions.SlotCount),
			CandidatePolicyIds: playerState.Institutions.CandidateIDs(),
			ActivePolicyIds:    append([]string(nil), playerState.Institutions.ActivePolicyIDs...),
		},
	}
}

func BuildNodeViews(state *domain.GameState, playerID string) []*pb.NodeView {
	if state == nil || state.World == nil {
		return nil
	}
	nodes := make([]*pb.NodeView, 0, ecs.AllNodes(state.World).Count(state.World))
	ecs.AllNodes(state.World).Each(state.World, func(entry *donburi.Entry) {
		nodes = append(nodes, BuildNodeView(state, entry, playerID))
	})
	return nodes
}

func BuildNodeView(state *domain.GameState, entry *donburi.Entry, playerID string) *pb.NodeView {
	if state == nil || entry == nil {
		return nil
	}
	node := ecs.NodeC.Get(entry)
	pos := ecs.PositionC.Get(entry)
	unitsByFaction := domain.UnitsByFactionAtNode(state.World, domain.Position{X: pos.X, Y: pos.Y})

	myCount := len(unitsByFaction[playerID])
	enemyCount := 0
	for faction, units := range unitsByFaction {
		if faction == playerID {
			continue
		}
		enemyCount += len(units)
	}

	view := &pb.NodeView{
		Id:                     node.ID,
		Pos:                    &pb.Position{X: int32(pos.X), Y: int32(pos.Y)},
		Terrain:                string(node.Terrain),
		ControllerPlayerId:     node.Owner,
		TerritoryOwnerPlayerId: node.TerritoryOwner,
		MyUnitCount:            int32(myCount),
		EnemyUnitCount:         int32(enemyCount),
		HasRoad:                node.HasRoad,
		IsResourcePoint:        node.IsResource,
		ResourceType:           node.ResourceType,
		IsSafeZone:             domain.IsInSafeZone(state, domain.Position{X: pos.X, Y: pos.Y}, playerID),
	}
	if entry.HasComponent(ecs.BuildingOperationC) {
		operation := ecs.BuildingOperationC.Get(entry)
		baseProgress := 0
		if recipeID := strings.TrimSpace(operation.SelectedRecipeID); recipeID != "" {
			if recipe, ok := staticdata.Default().GetRecipe(recipeID); ok {
				baseProgress = recipe.BaseProgress
			}
		}
		view.Operation = &pb.BuildingOperationView{
			SelectedRecipeId: operation.SelectedRecipeID,
			CurrentProgress:  int32(operation.ProgressTurns),
			RequiredProgress: int32(operation.RequiredTurns),
			BaseProgress:     int32(baseProgress),
			BlockedReason:    operation.BlockedReason,
		}
	}
	if entry.HasComponent(ecs.BuildingC) {
		building := ecs.BuildingC.Get(entry)
		view.BuildingTypeId = string(building.Type)
		view.BuildingHp = int32(building.HP)
		view.IsCityCore = entry.HasComponent(ecs.CityCoreC) || strings.EqualFold(string(building.Type), "city_core")
		view.CityId = ecs.ResolveCityID(entry)
		view.ServiceCityId = ecs.ResolveServiceCityID(entry)
		status, takeoverProgress, takeoverRequired := ecs.BuildingRuntimeState(entry, state.Turn)
		view.BuildingStatus = status
		view.TakeoverProgress = int32(takeoverProgress)
		view.TakeoverRequired = int32(takeoverRequired)
	} else {
		view.BuildingStatus = "empty"
	}
	return view
}

func BuildUnitViews(state *domain.GameState) []*pb.UnitView {
	if state == nil || state.World == nil {
		return nil
	}
	units := make([]*pb.UnitView, 0, ecs.AllUnits(state.World).Count(state.World))
	ecs.AllUnits(state.World).Each(state.World, func(entry *donburi.Entry) {
		stats := ecs.UnitStatsC.Get(entry)
		pos := ecs.PositionC.Get(entry)
		units = append(units, &pb.UnitView{
			Id:       stats.ID,
			Faction:  stats.Faction,
			UnitType: string(stats.Type),
			Hp:       int32(stats.HP),
			MaxHp:    int32(stats.MaxHP),
			Pos:      &pb.Position{X: int32(pos.X), Y: int32(pos.Y)},
		})
	})
	return units
}

func ToProtoResourceBag(resources domain.ResourceBag) *pb.ResourceBag {
	items := make([]*pb.ResourceValue, 0, len(resources))
	for _, key := range resources.Keys() {
		if _, ok := staticdata.Default().GetResource(string(key)); !ok {
			continue
		}
		items = append(items, &pb.ResourceValue{
			Key:    string(key),
			Amount: int32(resources.Get(key)),
		})
	}
	return &pb.ResourceBag{Items: items}
}

func ToProtoPointBag(state *domain.GameState, playerID string) *pb.PointBag {
	if state == nil {
		return &pb.PointBag{}
	}
	return &pb.PointBag{
		Items: []*pb.PointValue{
			{Key: "research_output", Amount: int32(state.EffectiveResearchOutput(playerID))},
			{Key: "industry_output", Amount: int32(state.EffectiveIndustryOutput(playerID))},
		},
	}
}

func buildResearchStateView(research domain.ResearchState) *pb.ResearchStateView {
	view := &pb.ResearchStateView{
		CurrentTargetTechnologyId:      research.CurrentTargetTechnologyID,
		CurrentProgress:                int32(research.CurrentTargetProgress()),
		RequiredProgress:               int32(researchRequiredProgress(research.CurrentTargetTechnologyID)),
		CompletedTechnologyIds:         research.CompletedTechnologyIDs(),
		ActiveTechnologyIds:            research.ActiveTechnologyIDs(),
		PendingActivationTechnologyIds: research.PendingActivationTechnologyIDs(),
	}
	for _, technologyID := range research.StoredProgressTechnologyIDs() {
		if technologyID == research.CurrentTargetTechnologyID {
			continue
		}
		view.SavedProgress = append(view.SavedProgress, &pb.ResearchProgressEntry{
			TechnologyId:     technologyID,
			CurrentProgress:  int32(research.ProgressForTechnology(technologyID)),
			RequiredProgress: int32(researchRequiredProgress(technologyID)),
		})
	}
	return view
}

func researchRequiredProgress(technologyID string) int {
	technologyID = strings.TrimSpace(technologyID)
	if technologyID == "" {
		return 0
	}
	technology, ok := staticdata.Default().GetTechnology(technologyID)
	if !ok {
		return 0
	}
	return technology.ResearchCost
}
