// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现对局查询模块的视图构建逻辑。

package query

import (
	"sort"
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
		TokensLeft:    int32(playerState.TokensLeft),
		CurrentPolicy: string(playerState.Policy),
		MainCastleHp:  int32(playerState.MainCastleHP),
		MaxCastleHp:   int32(staticdata.Default().Rules().CastleBaseHP),
		WarZones:      warZones,
		Research: &pb.PlayerResearchView{
			TechPoints:            int32(playerState.Research.TechPoints),
			TechPointsIncome:      int32(state.EffectiveTechPointIncome(playerID)),
			TechPointsCap:         int32(state.EffectiveTechPointCap(playerID)),
			UnlockedTechnologyIds: sortedUnlockedTechnologyIDs(playerState.Research),
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
		Id:              node.ID,
		Pos:             &pb.Position{X: int32(pos.X), Y: int32(pos.Y)},
		Terrain:         string(node.Terrain),
		Owner:           node.Owner,
		TerritoryOwner:  node.TerritoryOwner,
		MyUnitCount:     int32(myCount),
		EnemyUnitCount:  int32(enemyCount),
		HasRoad:         node.HasRoad,
		IsResourcePoint: node.IsResource,
		ResourceType:    node.ResourceType,
		IsSafeZone:      domain.IsInSafeZone(state, domain.Position{X: pos.X, Y: pos.Y}, playerID),
	}
	if entry.HasComponent(ecs.BuildingOperationC) {
		operation := ecs.BuildingOperationC.Get(entry)
		view.Operation = &pb.BuildingOperationView{
			SelectedRecipeId: operation.SelectedRecipeID,
			ProgressTurns:    int32(operation.ProgressTurns),
			RequiredTurns:    int32(operation.RequiredTurns),
			DelayTurns:       int32(operation.DelayTurns),
			BlockedReason:    operation.BlockedReason,
		}
	}
	if entry.HasComponent(ecs.BuildingC) {
		building := ecs.BuildingC.Get(entry)
		view.BuildingType = string(building.Type)
		view.BuildingHp = int32(building.HP)
		view.WallLevel = int32(building.WallLevel)
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
		items = append(items, &pb.ResourceValue{
			Key:    string(key),
			Amount: int32(resources.Get(key)),
		})
	}
	return &pb.ResourceBag{Items: items}
}

func sortedUnlockedTechnologyIDs(research domain.ResearchState) []string {
	ids := make([]string, 0, len(research.UnlockedTechnologies))
	for technologyID := range research.UnlockedTechnologies {
		if strings.TrimSpace(technologyID) != "" {
			ids = append(ids, strings.TrimSpace(technologyID))
		}
	}
	sort.Strings(ids)
	return ids
}
