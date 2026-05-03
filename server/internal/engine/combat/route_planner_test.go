// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 验证单位结算引擎的路径规划逻辑。

package combat

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
)

func TestWeightedRoutePlanner_BuildPreviewUsesTerrainCostAndTurnStops(t *testing.T) {
	state := newCombatTestState(t, 5)
	unitID := spawnTestUnit(state.World, "infantry", "player-a", 0, 0)

	planner := NewWeightedRoutePlanner(DefaultTerrainCostPolicy{})
	preview, ok := planner.BuildPreview(state.World, state, unitID, "N4_0")
	if !ok {
		t.Fatalf("BuildPreview() = false, want true")
	}

	if got := preview.FirstTurnNodeID; got != "N2_0" {
		t.Fatalf("FirstTurnNodeID = %q, want %q", got, "N2_0")
	}
	if got := preview.TotalTurns; got != 2 {
		t.Fatalf("TotalTurns = %d, want 2", got)
	}
	if len(preview.PathNodeIDs) != 5 {
		t.Fatalf("PathNodeIDs len = %d, want 5", len(preview.PathNodeIDs))
	}
	if len(preview.TurnStops) != 2 {
		t.Fatalf("TurnStops len = %d, want 2", len(preview.TurnStops))
	}
	if got := preview.TurnStops[0].NodeID; got != "N2_0" {
		t.Fatalf("turn 1 stop = %q, want %q", got, "N2_0")
	}
	if got := preview.TurnStops[1].NodeID; got != "N4_0" {
		t.Fatalf("turn 2 stop = %q, want %q", got, "N4_0")
	}
}

func TestWeightedRoutePlanner_BuildPreviewAllowsRiverWithRoad(t *testing.T) {
	state := newCombatTestState(t, 3)
	unitID := spawnTestUnit(state.World, "infantry", "player-a", 0, 0)
	setNodeTerrain(t, state, "N1_0", "river", true)

	planner := NewWeightedRoutePlanner(DefaultTerrainCostPolicy{})
	preview, ok := planner.BuildPreview(state.World, state, unitID, "N2_0")
	if !ok {
		t.Fatalf("BuildPreview() = false, want true")
	}

	if got := preview.FirstTurnNodeID; got != "N2_0" {
		t.Fatalf("FirstTurnNodeID = %q, want %q", got, "N2_0")
	}
	if got := preview.TotalTurns; got != 1 {
		t.Fatalf("TotalTurns = %d, want 1", got)
	}
}

func TestWeightedRoutePlanner_BuildPreviewBlocksCavalryOnMountainWithoutRoad(t *testing.T) {
	state := newCombatTestState(t, 3)
	unitID := spawnTestUnit(state.World, "cavalry", "player-a", 0, 0)
	setNodeTerrain(t, state, "N1_0", "mountain", false)

	planner := NewWeightedRoutePlanner(DefaultTerrainCostPolicy{})
	if _, ok := planner.BuildPreview(state.World, state, unitID, "N2_0"); ok {
		t.Fatalf("BuildPreview() = true, want false")
	}
}

func TestSingleStepResolver_MoveBudgetUsesTerrainCost(t *testing.T) {
	state := newCombatTestState(t, 4)
	unitID := spawnTestUnit(state.World, "infantry", "player-a", 0, 0)
	setNodeTerrain(t, state, "N1_0", "forest", false)

	state.TurnRuntime.Resolving.UnitOrders = map[string]domain.UnitResolutionOrder{
		unitID: {PlayerID: "player-a", UnitID: unitID, Action: domain.UnitResolutionActionMove, TargetNodeID: "N3_0"},
	}

	resolver := NewSingleStepResolver()
	events := resolver.Run(state.World, state)
	applyCombatEvents(state, events)

	if got := unitPosition(t, state.World, unitID); got != (domain.Position{Q: 1, R: 0}) {
		t.Fatalf("unit position = %#v, want stop at forest tile", got)
	}
}

func setNodeTerrain(t *testing.T, state *domain.GameState, nodeID string, terrain string, hasRoad bool) {
	t.Helper()

	entry, ok := state.GetNode(nodeID)
	if !ok {
		t.Fatalf("node %s not found", nodeID)
	}
	node := ecs.NodeC.Get(entry)
	node.Terrain = domain.Terrain(terrain)
	node.HasRoad = hasRoad
	ecs.NodeC.SetValue(entry, *node)
}
