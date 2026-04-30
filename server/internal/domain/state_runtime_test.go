// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 验证领域模型的状态运行时行为。

package domain

import "testing"

func TestNewGameStateInitializesTurnRuntimeContainers(t *testing.T) {
	t.Parallel()

	state := NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &MapData{})

	if state.TurnRuntime.Planning.UnitOrders == nil {
		t.Fatalf("planning unit orders map is nil")
	}
	if state.TurnRuntime.Planning.MinisterDirectives == nil {
		t.Fatalf("planning minister directives map is nil")
	}
	if state.TurnRuntime.Planning.MinisterDrafts == nil {
		t.Fatalf("planning minister drafts map is nil")
	}
	if state.TurnRuntime.Planning.WarDirectives == nil {
		t.Fatalf("planning war directives map is nil")
	}
	if state.TurnRuntime.Resolving.UnitOrders == nil {
		t.Fatalf("resolving unit orders map is nil")
	}
	if state.TurnRuntime.Resolving.ActiveMarches == nil {
		t.Fatalf("resolving active marches map is nil")
	}
}

func TestTurnRuntimeClearPostResolutionScratchClearsTransientPlanningAndResolvingState(t *testing.T) {
	t.Parallel()

	runtime := TurnRuntime{
		Planning: PlanningInputs{
			BuildOrders:      []BuildOrder{{PlayerID: "player-1", NodeID: "N1", BuildingType: "farm"}},
			RecipeSelections: []RecipeSelectionOrder{{PlayerID: "player-1", NodeID: "N1", RecipeID: "farm_food"}},
			MinisterBuilds:   []BuildOrder{{PlayerID: "player-1", NodeID: "N2", BuildingType: "barracks"}},
			MinisterMoves:    []MoveOrder{{PlayerID: "player-1", UnitID: "unit-1", Target: Position{Q: 1, R: 0}}},
			MinisterDrafts: map[string][]MinisterDraft{
				"player-1": {{DraftID: "draft-1"}},
			},
			MinisterDirectives: map[string]string{
				"player-1": "focus industry",
			},
			PendingPolicies: map[string]Policy{
				"player-1": PolicyExpansion,
			},
			PendingResearch: map[string]string{
				"player-1": "agrarian_foundations",
			},
			PendingInstitutions: map[string][]string{
				"player-1": {"academy_charter"},
			},
			WarDirectives: map[string][]WarZoneDirective{
				"player-1": {{ZoneID: "front-1", Directive: "attack"}},
			},
			UnitOrders: map[string]UnitDirective{
				"unit-1": {PlayerID: "player-1", UnitID: "unit-1", Action: "move"},
			},
		},
		Resolving: ResolvingState{
			UnitOrders: map[string]UnitResolutionOrder{
				"unit-1": {PlayerID: "player-1", UnitID: "unit-1", Action: UnitResolutionActionMove},
			},
			ActiveMarches: map[string]ActiveMarch{
				"unit-1": {PlayerID: "player-1", UnitID: "unit-1", DestinationNodeID: "N2"},
			},
			PointBudgets: map[string]PointBag{
				"player-1": NewPointBag(),
			},
		},
	}

	runtime.ClearPostResolutionScratch()

	if len(runtime.Resolving.UnitOrders) != 0 {
		t.Fatalf("resolving unit orders = %#v, want empty", runtime.Resolving.UnitOrders)
	}
	if len(runtime.Planning.BuildOrders) != 0 {
		t.Fatalf("build orders = %#v, want empty", runtime.Planning.BuildOrders)
	}
	if len(runtime.Planning.RecipeSelections) != 0 {
		t.Fatalf("recipe selections = %#v, want empty", runtime.Planning.RecipeSelections)
	}
	if len(runtime.Planning.MinisterBuilds) != 0 {
		t.Fatalf("minister builds = %#v, want empty", runtime.Planning.MinisterBuilds)
	}
	if len(runtime.Planning.MinisterMoves) != 0 {
		t.Fatalf("minister moves = %#v, want empty", runtime.Planning.MinisterMoves)
	}
	if len(runtime.Planning.MinisterDrafts) != 0 {
		t.Fatalf("minister drafts = %#v, want empty", runtime.Planning.MinisterDrafts)
	}
	if len(runtime.Planning.UnitOrders) != 0 {
		t.Fatalf("unit orders = %#v, want empty", runtime.Planning.UnitOrders)
	}
	if len(runtime.Planning.MinisterDirectives) != 0 {
		t.Fatalf("minister directives = %#v, want empty", runtime.Planning.MinisterDirectives)
	}
	if len(runtime.Planning.PendingPolicies) != 0 {
		t.Fatalf("pending policies = %#v, want empty", runtime.Planning.PendingPolicies)
	}
	if len(runtime.Planning.PendingResearch) != 0 {
		t.Fatalf("pending research = %#v, want empty", runtime.Planning.PendingResearch)
	}
	if len(runtime.Planning.PendingInstitutions) != 0 {
		t.Fatalf("pending institutions = %#v, want empty", runtime.Planning.PendingInstitutions)
	}
	if len(runtime.Planning.WarDirectives) != 0 {
		t.Fatalf("war directives = %#v, want empty", runtime.Planning.WarDirectives)
	}
	if len(runtime.Resolving.ActiveMarches) != 1 {
		t.Fatalf("active marches = %#v, want preserved", runtime.Resolving.ActiveMarches)
	}
	if len(runtime.Resolving.PointBudgets) != 1 {
		t.Fatalf("point budgets = %#v, want preserved", runtime.Resolving.PointBudgets)
	}
}
