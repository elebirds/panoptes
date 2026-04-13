package combat

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/yohamta/donburi"
)

func buildRoutePreview(world donburi.World, path []domain.Position, profile domain.MovementProfile, turns TurnSegmentPlanner) domain.RoutePreview {
	preview := domain.RoutePreview{
		PathNodeIDs: make([]string, 0, len(path)),
	}
	for _, pos := range path {
		if entry, ok := domain.GetNodeAt(world, pos); ok {
			preview.PathNodeIDs = append(preview.PathNodeIDs, ecs.NodeC.Get(entry).ID)
		}
	}

	stops := turns.BuildStops(world, path, profile)
	preview.TotalTurns = len(stops)
	preview.TurnStops = make([]domain.MarchTurnStop, 0, len(stops))
	for idx, stop := range stops {
		if entry, ok := domain.GetNodeAt(world, stop); ok {
			nodeID := ecs.NodeC.Get(entry).ID
			if idx == 0 {
				preview.FirstTurnNodeID = nodeID
			}
			preview.TurnStops = append(preview.TurnStops, domain.MarchTurnStop{
				TurnIndex: idx + 1,
				NodeID:    nodeID,
			})
		}
	}
	return preview
}

func reachablePathIndex(world donburi.World, path []domain.Position, profile domain.MovementProfile, policy TerrainCostPolicy) int {
	if len(path) == 0 {
		return 0
	}
	budget := profile.TurnBudget()
	spent := 0
	reachable := 0
	for i := 1; i < len(path); i++ {
		cost := policy.StepCost(world, path[i], profile)
		if spent+cost > budget {
			break
		}
		spent += cost
		reachable = i
	}
	return reachable
}

func buildTurnStops(world donburi.World, path []domain.Position, profile domain.MovementProfile, policy TerrainCostPolicy) []domain.Position {
	if len(path) <= 1 {
		return nil
	}
	budget := profile.TurnBudget()
	spent := 0
	stops := make([]domain.Position, 0, len(path))
	lastStop := path[0]
	for i := 1; i < len(path); i++ {
		cost := policy.StepCost(world, path[i], profile)
		if spent+cost > budget {
			stops = append(stops, lastStop)
			spent = 0
		}
		spent += cost
		lastStop = path[i]
	}
	if lastStop != path[0] {
		stops = append(stops, lastStop)
	}
	return stops
}
