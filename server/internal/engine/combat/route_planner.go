// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现单位结算引擎的路径规划逻辑。

package combat

import (
	"github.com/elebirds/panoptes/internal/algo/pathfinding"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

type DefaultTerrainCostPolicy struct{}

func (DefaultTerrainCostPolicy) StepCost(world donburi.World, pos domain.Position, profile domain.MovementProfile) int {
	entry, ok := domain.GetNodeAt(world, pos)
	if !ok {
		return 99
	}
	node := ecs.NodeC.Get(entry)
	if node.HasRoad {
		return maxMovementInt(1, profile.RoadCost)
	}
	terrain, ok := staticdata.Default().GetTerrain(string(node.Terrain))
	if !ok || terrain.MoveCostNoRoad <= 0 {
		return 99
	}
	return terrain.MoveCostNoRoad
}

func (DefaultTerrainCostPolicy) IsBlocked(world donburi.World, pos domain.Position, profile domain.MovementProfile) bool {
	entry, ok := domain.GetNodeAt(world, pos)
	if !ok {
		return true
	}
	node := ecs.NodeC.Get(entry)
	terrain, ok := staticdata.Default().GetTerrain(string(node.Terrain))
	if !ok {
		return true
	}
	if node.HasRoad && terrain.PassableWithRoad {
		return false
	}
	if !terrain.Passable {
		return true
	}
	if profile.Mounted && terrain.BlocksCavalry && !node.HasRoad {
		return true
	}
	return DefaultTerrainCostPolicy{}.StepCost(world, pos, profile) > profile.TurnBudget()
}

type WeightedRoutePlanner struct {
	CostPolicy TerrainCostPolicy
	Turns      TurnSegmentPlanner
}

func NewWeightedRoutePlanner(costPolicy TerrainCostPolicy) WeightedRoutePlanner {
	if costPolicy == nil {
		costPolicy = DefaultTerrainCostPolicy{}
	}
	return WeightedRoutePlanner{
		CostPolicy: costPolicy,
		Turns:      DefaultTurnSegmentPlanner{CostPolicy: costPolicy},
	}
}

func (p WeightedRoutePlanner) FindPath(world donburi.World, start, goal domain.Position, profile domain.MovementProfile) ([]domain.Position, bool) {
	return pathfinding.FindPath(weightedMovementGrid{world: world, profile: profile, costPolicy: p.CostPolicy}, start, goal)
}

func (p WeightedRoutePlanner) BuildPreview(world donburi.World, state *domain.GameState, unitID, destinationNodeID string) (domain.RoutePreview, bool) {
	entry, ok := findUnit(world, unitID)
	if !ok {
		return domain.RoutePreview{}, false
	}
	if state == nil {
		return domain.RoutePreview{}, false
	}
	targetEntry, ok := state.GetNode(destinationNodeID)
	if !ok {
		return domain.RoutePreview{}, false
	}

	stats := ecs.UnitStatsC.Get(entry)
	pos := ecs.PositionC.Get(entry)
	caps := domain.UnitCapabilities{}
	if entry.HasComponent(ecs.UnitCapabilitiesC) {
		caps = *ecs.UnitCapabilitiesC.Get(entry)
	}
	moveRange := effectiveUnitMoveRange(state, stats.Faction, stats.Type, stats.Speed)
	profile := buildMovementProfile(stats.Type, caps, moveRange)
	goalPos := ecs.PositionC.Get(targetEntry)
	path, ok := p.FindPath(world, domain.Position{X: pos.X, Y: pos.Y}, domain.Position{X: goalPos.X, Y: goalPos.Y}, profile)
	if !ok || len(path) == 0 {
		return domain.RoutePreview{}, false
	}

	return buildRoutePreview(world, path, profile, p.Turns), true
}

func (p WeightedRoutePlanner) BuildPreviewFromPathNodeIDs(world donburi.World, state *domain.GameState, unitID string, pathNodeIDs []string) (domain.RoutePreview, bool) {
	path, profile, ok := resolvePreviewPathFromNodeIDs(world, state, unitID, pathNodeIDs)
	if !ok || len(path) == 0 {
		return domain.RoutePreview{}, false
	}
	return buildRoutePreview(world, path, profile, p.Turns), true
}

type DefaultTurnSegmentPlanner struct {
	CostPolicy TerrainCostPolicy
}

func (p DefaultTurnSegmentPlanner) Reachable(world donburi.World, path []domain.Position, profile domain.MovementProfile) (domain.Position, domain.Position) {
	if len(path) == 0 {
		return domain.Position{}, domain.Position{}
	}
	reachableIndex := reachablePathIndex(world, path, profile, p.CostPolicy)
	candidate := path[reachableIndex]
	fallback := path[0]
	if reachableIndex > 0 {
		fallback = path[reachableIndex-1]
	}
	return candidate, fallback
}

func (p DefaultTurnSegmentPlanner) BuildStops(world donburi.World, path []domain.Position, profile domain.MovementProfile) []domain.Position {
	return buildTurnStops(world, path, profile, p.CostPolicy)
}

func resolvePreviewPathFromNodeIDs(world donburi.World, state *domain.GameState, unitID string, pathNodeIDs []string) ([]domain.Position, domain.MovementProfile, bool) {
	entry, ok := findUnit(world, unitID)
	if !ok || state == nil || len(pathNodeIDs) == 0 {
		return nil, domain.MovementProfile{}, false
	}

	stats := ecs.UnitStatsC.Get(entry)
	caps := domain.UnitCapabilities{}
	if entry.HasComponent(ecs.UnitCapabilitiesC) {
		caps = *ecs.UnitCapabilitiesC.Get(entry)
	}
	moveRange := effectiveUnitMoveRange(state, stats.Faction, stats.Type, stats.Speed)
	profile := buildMovementProfile(stats.Type, caps, moveRange)

	path := make([]domain.Position, 0, len(pathNodeIDs))
	for _, nodeID := range pathNodeIDs {
		nodeEntry, ok := state.GetNode(nodeID)
		if !ok {
			return nil, domain.MovementProfile{}, false
		}
		pos := ecs.PositionC.Get(nodeEntry)
		path = append(path, domain.Position{X: pos.X, Y: pos.Y})
	}

	return path, profile, len(path) > 0
}

type weightedMovementGrid struct {
	world      donburi.World
	profile    domain.MovementProfile
	costPolicy TerrainCostPolicy
}

func (g weightedMovementGrid) InBounds(pos domain.Position) bool {
	_, ok := domain.GetNodeAt(g.world, pos)
	return ok
}

func (g weightedMovementGrid) Neighbors(pos domain.Position) []domain.Position {
	neighbors := pos.Neighbors()
	out := make([]domain.Position, 0, len(neighbors))
	for _, next := range neighbors {
		if g.InBounds(next) {
			out = append(out, next)
		}
	}
	return out
}

func (g weightedMovementGrid) Cost(_, to domain.Position) int {
	return g.costPolicy.StepCost(g.world, to, g.profile)
}

func (g weightedMovementGrid) IsBlocked(pos domain.Position) bool {
	return g.costPolicy.IsBlocked(g.world, pos, g.profile)
}
