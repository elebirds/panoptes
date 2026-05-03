// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载规划单位指令、地图动作与 resolving 单位订单的状态转换逻辑。

package orders

import (
	"reflect"
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func TestValidatePlanningUnitOrderAllowsAttackAfterActiveMarch(t *testing.T) {
	useUnitOrderTestCatalog(t)
	state := newUnitOrderTestState(t)
	infantry := state.World.Entry(ecs.CreateUnit(state.World, "infantry", "player-1", domain.Position{Q: 0, R: 0}))
	ecs.UnitStatsC.Get(infantry).ID = "infantry-1"
	enemyNode, _ := state.GetNode("A3")
	ecs.CreateBuilding(state.World, "farm", "player-2", "A3", enemyNode)
	state.TurnRuntime.Resolving.ActiveMarches["infantry-1"] = domain.ActiveMarch{
		PlayerID:          "player-1",
		UnitID:            "infantry-1",
		Action:            domain.UnitResolutionActionMove,
		DestinationNodeID: "A2",
	}

	errCode := ValidatePlanningUnitOrder(state, "player-1", UnitOrder{
		PlayerID:     "player-1",
		UnitID:       "infantry-1",
		Action:       ActionAttack,
		TargetNodeID: "A3",
	})

	if errCode != "" {
		t.Fatalf("validation error = %q, want accepted", errCode)
	}
}

func TestApplyPlanningUnitOrderSyncsMoveAndPreservesAttackPath(t *testing.T) {
	useUnitOrderTestCatalog(t)
	state := newUnitOrderTestState(t)
	unit := state.World.Entry(ecs.CreateUnit(state.World, "infantry", "player-1", domain.Position{Q: 0, R: 0}))
	ecs.UnitStatsC.Get(unit).ID = "infantry-1"
	routes := RoutePreviewCallbacks{
		ByDestination: func(unitID string, destinationNodeID string) (domain.RoutePreview, bool) {
			if unitID != "infantry-1" || destinationNodeID != "A3" {
				t.Fatalf("route preview request = %s/%s, want infantry-1/A3", unitID, destinationNodeID)
			}
			return domain.RoutePreview{PathNodeIDs: []string{"A1", "A2", "A3"}}, true
		},
	}

	ApplyPlanningUnitOrder(state, UnitOrder{
		UnitID:       "infantry-1",
		Action:       ActionMove,
		TargetNodeID: "A3",
	}, routes)

	march, ok := state.TurnRuntime.Resolving.ActiveMarches["infantry-1"]
	if !ok {
		t.Fatalf("active march missing after move order")
	}
	if march.PlayerID != "player-1" || march.DestinationNodeID != "A3" || !reflect.DeepEqual(march.LastPreview.PathNodeIDs, []string{"A1", "A2", "A3"}) {
		t.Fatalf("active march = %#v", march)
	}

	ApplyPlanningUnitOrder(state, UnitOrder{
		PlayerID:     "player-1",
		UnitID:       "infantry-1",
		Action:       ActionAttack,
		TargetNodeID: "A3",
	}, RoutePreviewCallbacks{})

	directive := state.TurnRuntime.Planning.UnitOrders["infantry-1"]
	if directive.Action != string(ActionAttack) || !reflect.DeepEqual(directive.PathNodeIDs, []string{"A1", "A2", "A3"}) {
		t.Fatalf("attack directive = %#v, want preserved march path", directive)
	}
	if _, ok := state.TurnRuntime.Resolving.ActiveMarches["infantry-1"]; ok {
		t.Fatalf("active march should be cleared after attack order")
	}
}

func TestValidatePlanningUnitOrderAllowsRoadActionsForCivilian(t *testing.T) {
	useUnitOrderTestCatalog(t)
	state := newUnitOrderTestState(t)
	unit := state.World.Entry(ecs.CreateUnit(state.World, "settler", "player-1", domain.Position{Q: 0, R: 0}))
	ecs.UnitStatsC.Get(unit).ID = "settler-1"

	errCode := ValidatePlanningUnitOrder(state, "player-1", UnitOrder{
		PlayerID:        "player-1",
		UnitID:          "settler-1",
		Action:          ActionBuildRoad,
		TargetNodeID:    "A1",
		SecondaryNodeID: "A2",
	})
	if errCode != "" {
		t.Fatalf("build_road validation error = %q, want accepted", errCode)
	}

	fromEntry, _ := state.GetNode("A1")
	toEntry, _ := state.GetNode("A2")
	ecs.NodeC.Get(fromEntry).HasRoad = true
	ecs.NodeC.Get(toEntry).HasRoad = true
	if errCode := ValidatePlanningUnitOrder(state, "player-1", UnitOrder{
		PlayerID:        "player-1",
		UnitID:          "settler-1",
		Action:          ActionRepairRoad,
		TargetNodeID:    "A1",
		SecondaryNodeID: "A2",
	}); errCode != "invalid_target" {
		t.Fatalf("repair_road validation error = %q, want invalid_target for intact road", errCode)
	}
	ecs.NodeC.Get(toEntry).HasRoad = false
	if errCode := ValidatePlanningUnitOrder(state, "player-1", UnitOrder{
		PlayerID:        "player-1",
		UnitID:          "settler-1",
		Action:          ActionRepairRoad,
		TargetNodeID:    "A1",
		SecondaryNodeID: "A2",
	}); errCode != "" {
		t.Fatalf("repair_road validation error = %q, want accepted for missing road endpoint", errCode)
	}
}

func TestValidatePlanningUnitOrderRejectsRoadActionsForNonEngineeringUnit(t *testing.T) {
	useUnitOrderTestCatalog(t)
	state := newUnitOrderTestState(t)
	unit := state.World.Entry(ecs.CreateUnit(state.World, "infantry", "player-1", domain.Position{Q: 0, R: 0}))
	ecs.UnitStatsC.Get(unit).ID = "infantry-1"

	errCode := ValidatePlanningUnitOrder(state, "player-1", UnitOrder{
		PlayerID:        "player-1",
		UnitID:          "infantry-1",
		Action:          ActionBuildRoad,
		TargetNodeID:    "A1",
		SecondaryNodeID: "A2",
	})
	if errCode != "invalid_directive" {
		t.Fatalf("validation error = %q, want invalid_directive", errCode)
	}
}

func TestValidatePlanningUnitOrderRejectsInvalidRoadEndpoints(t *testing.T) {
	useUnitOrderTestCatalog(t)
	state := newUnitOrderTestState(t)
	unit := state.World.Entry(ecs.CreateUnit(state.World, "settler", "player-1", domain.Position{Q: 0, R: 0}))
	ecs.UnitStatsC.Get(unit).ID = "settler-1"

	for _, tc := range []struct {
		name            string
		targetNodeID    string
		secondaryNodeID string
	}{
		{name: "missing endpoint", targetNodeID: "A1", secondaryNodeID: "missing"},
		{name: "non-adjacent endpoints", targetNodeID: "A1", secondaryNodeID: "A3"},
	} {
		t.Run(tc.name, func(t *testing.T) {
			errCode := ValidatePlanningUnitOrder(state, "player-1", UnitOrder{
				PlayerID:        "player-1",
				UnitID:          "settler-1",
				Action:          ActionBuildRoad,
				TargetNodeID:    tc.targetNodeID,
				SecondaryNodeID: tc.secondaryNodeID,
			})
			if errCode != "invalid_target" {
				t.Fatalf("validation error = %q, want invalid_target", errCode)
			}
		})
	}
}

func TestValidatePlanningUnitOrderAllowsImprovementActionsForCivilian(t *testing.T) {
	useUnitOrderTestCatalog(t)
	state := newUnitOrderTestState(t)
	unit := state.World.Entry(ecs.CreateUnit(state.World, "settler", "player-1", domain.Position{Q: 0, R: 0}))
	ecs.UnitStatsC.Get(unit).ID = "settler-1"
	core, _ := state.GetNode("A1")
	ecs.CreateBuilding(state.World, "city_core", "player-1", "A1", core)
	state.EnsureCityState("player-1", "A1")
	state.Players["player-1"].CapitalCityID = "A1"
	resource, _ := state.GetNode("A2")
	node := ecs.NodeC.Get(resource)
	node.IsResource = true
	node.ResourceType = "food"
	node.TerritoryOwner = "player-1"

	errCode := ValidatePlanningUnitOrder(state, "player-1", UnitOrder{
		PlayerID:     "player-1",
		UnitID:       "settler-1",
		Action:       ActionBuildImprovement,
		TargetNodeID: "A2",
		Params:       map[string]string{"city_id": "A1"},
	})
	if errCode != "" {
		t.Fatalf("build_improvement validation error = %q, want accepted", errCode)
	}

	ecs.CreateBuilding(state.World, "farm", "player-1", "A1", resource)
	ecs.BuildingC.Get(resource).HP = 1
	errCode = ValidatePlanningUnitOrder(state, "player-1", UnitOrder{
		PlayerID:     "player-1",
		UnitID:       "settler-1",
		Action:       ActionRepairImprovement,
		TargetNodeID: "A2",
	})
	if errCode != "" {
		t.Fatalf("repair_improvement validation error = %q, want accepted", errCode)
	}
}

func TestValidatePlanningUnitOrderRejectsInvalidImprovementWithoutStateWrites(t *testing.T) {
	useUnitOrderTestCatalog(t)
	state := newUnitOrderTestState(t)
	unit := state.World.Entry(ecs.CreateUnit(state.World, "settler", "player-1", domain.Position{Q: 0, R: 0}))
	ecs.UnitStatsC.Get(unit).ID = "settler-1"

	errCode := ValidatePlanningUnitOrder(state, "player-1", UnitOrder{
		PlayerID:     "player-1",
		UnitID:       "settler-1",
		Action:       ActionBuildImprovement,
		TargetNodeID: "A2",
		Params:       map[string]string{"city_id": "A1"},
	})
	if errCode != "invalid_target" {
		t.Fatalf("build_improvement validation error = %q, want invalid_target", errCode)
	}
	if _, ok := state.TurnRuntime.Planning.UnitOrders["settler-1"]; ok {
		t.Fatalf("planning order recorded after validation-only failure")
	}
}

func TestApplyPlanningUnitOrderAllowsM2MapActions(t *testing.T) {
	useUnitOrderTestCatalog(t)
	state := newUnitOrderTestState(t)
	unit := state.World.Entry(ecs.CreateUnit(state.World, "settler", "player-1", domain.Position{Q: 0, R: 0}))
	ecs.UnitStatsC.Get(unit).ID = "settler-1"

	for _, action := range []UnitAction{ActionBuildRoad, ActionRepairRoad, ActionBuildImprovement, ActionRepairImprovement} {
		t.Run(string(action), func(t *testing.T) {
			ApplyPlanningUnitOrder(state, UnitOrder{
				PlayerID:     "player-1",
				UnitID:       "settler-1",
				Action:       action,
				TargetNodeID: "A2",
			}, RoutePreviewCallbacks{})

			directive, ok := state.TurnRuntime.Planning.UnitOrders["settler-1"]
			if !ok || directive.Action != string(action) {
				t.Fatalf("planning unit order = %#v, ok=%v, want road action recorded", directive, ok)
			}
			if got := len(state.TurnRuntime.Resolving.ActiveMarches); got != 0 {
				t.Fatalf("active marches = %d, want 0", got)
			}
			delete(state.TurnRuntime.Planning.UnitOrders, "settler-1")
		})
	}

}

func TestApplyPlanningUnitOrderAllowsSettleCityMapAction(t *testing.T) {
	useUnitOrderTestCatalog(t)
	state := newUnitOrderTestState(t)
	unit := state.World.Entry(ecs.CreateUnit(state.World, "settler", "player-1", domain.Position{Q: 0, R: 0}))
	ecs.UnitStatsC.Get(unit).ID = "settler-1"
	state.TurnRuntime.Resolving.ActiveMarches["settler-1"] = domain.ActiveMarch{
		PlayerID:          "player-1",
		UnitID:            "settler-1",
		Action:            domain.UnitResolutionActionMove,
		DestinationNodeID: "A3",
	}

	ApplyPlanningUnitOrder(state, UnitOrder{
		PlayerID:     "player-1",
		UnitID:       "settler-1",
		Action:       ActionSettleCity,
		TargetNodeID: "A2",
	}, RoutePreviewCallbacks{})

	directive, ok := state.TurnRuntime.Planning.UnitOrders["settler-1"]
	if !ok {
		t.Fatalf("settle_city directive missing")
	}
	if directive.Action != string(ActionSettleCity) || directive.TargetNodeID != "A2" {
		t.Fatalf("settle_city directive = %#v", directive)
	}
	if _, ok := state.TurnRuntime.Resolving.ActiveMarches["settler-1"]; ok {
		t.Fatalf("active march should be cleared after settle_city order")
	}
}

func TestBuildResolvingUnitOrdersFreezesActiveMarchesAndSettleMove(t *testing.T) {
	state := newUnitOrderTestState(t)
	state.TurnRuntime.Resolving.ActiveMarches["infantry-1"] = domain.ActiveMarch{
		PlayerID:          "player-1",
		UnitID:            "infantry-1",
		Action:            domain.UnitResolutionActionMove,
		DestinationNodeID: "A3",
		LastPreview:       domain.RoutePreview{PathNodeIDs: []string{"A1", "A2", "A3"}},
	}
	state.TurnRuntime.Planning.UnitOrders["infantry-1"] = domain.UnitDirective{
		PlayerID:     "player-1",
		UnitID:       "infantry-1",
		Action:       string(ActionMove),
		TargetNodeID: "A2",
	}
	state.TurnRuntime.Planning.UnitOrders["settler-1"] = domain.UnitDirective{
		PlayerID:     "player-1",
		UnitID:       "settler-1",
		Action:       string(ActionSettleCity),
		TargetNodeID: "A2",
	}

	BuildResolvingUnitOrders(state, RoutePreviewCallbacks{})

	move := state.TurnRuntime.Resolving.UnitOrders["infantry-1"]
	if move.Action != domain.UnitResolutionActionMove || move.TargetNodeID != "A3" || !reflect.DeepEqual(move.PathNodeIDs, []string{"A1", "A2", "A3"}) {
		t.Fatalf("frozen move order = %#v", move)
	}
	settle := state.TurnRuntime.Resolving.UnitOrders["settler-1"]
	if settle.Action != domain.UnitResolutionActionMove || settle.TargetNodeID != "A2" {
		t.Fatalf("settle movement order = %#v", settle)
	}
}

func TestRefreshActiveMarchesAfterSettlementTrimsOrClearsMarches(t *testing.T) {
	useUnitOrderTestCatalog(t)
	state := newUnitOrderTestState(t)
	unit := state.World.Entry(ecs.CreateUnit(state.World, "infantry", "player-1", domain.Position{Q: 1, R: 0}))
	ecs.UnitStatsC.Get(unit).ID = "infantry-1"
	state.TurnRuntime.Resolving.ActiveMarches["infantry-1"] = domain.ActiveMarch{
		PlayerID:          "player-1",
		UnitID:            "infantry-1",
		Action:            domain.UnitResolutionActionMove,
		DestinationNodeID: "A3",
		LastPreview:       domain.RoutePreview{PathNodeIDs: []string{"A1", "A2", "A3"}},
	}
	routes := RoutePreviewCallbacks{
		ByPath: func(unitID string, pathNodeIDs []string) (domain.RoutePreview, bool) {
			if unitID != "infantry-1" || !reflect.DeepEqual(pathNodeIDs, []string{"A2", "A3"}) {
				t.Fatalf("path preview request = %s/%#v, want infantry-1/[A2 A3]", unitID, pathNodeIDs)
			}
			return domain.RoutePreview{PathNodeIDs: append([]string(nil), pathNodeIDs...)}, true
		},
	}

	RefreshActiveMarchesAfterSettlement(state, routes)

	march, ok := state.TurnRuntime.Resolving.ActiveMarches["infantry-1"]
	if !ok || !reflect.DeepEqual(march.LastPreview.PathNodeIDs, []string{"A2", "A3"}) {
		t.Fatalf("refreshed march = %#v, ok=%v", march, ok)
	}

	ecs.PositionC.SetValue(unit, ecs.PositionComp{Q: 2, R: 0})
	RefreshActiveMarchesAfterSettlement(state, routes)
	if _, ok := state.TurnRuntime.Resolving.ActiveMarches["infantry-1"]; ok {
		t.Fatalf("active march should be cleared after unit reaches destination")
	}
}

func useUnitOrderTestCatalog(t *testing.T) {
	t.Helper()
	previous := staticdata.Default()
	t.Cleanup(func() {
		staticdata.SetDefault(previous)
	})
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{CityCoreMaxHP: 100},
		Units: []staticdata.UnitDefinition{
			{ID: "infantry", Class: "melee", MaxHP: 30, Attack: 10, AttackRange: 1, MoveRange: 2, VisionRange: 3, Multipliers: map[string]float64{}, Flags: staticdata.UnitFlags{CanAttackStructures: true}},
			{ID: "settler", Class: "civilian", MaxHP: 12, MoveRange: 2, VisionRange: 2, Multipliers: map[string]float64{}, Flags: staticdata.UnitFlags{CanCapture: true}},
		},
		Buildings: []staticdata.BuildingDefinition{
			{ID: "city_core", PlacementKind: "city_foundation_center", BuildingScope: "city_core", MaxHP: 100, TakeoverMode: "disabled"},
			{ID: "farm", PlacementKind: "resource_node", BuildingScope: "out_of_city", RequiredResourceType: "food", MaxHP: 15, TakeoverMode: "delayed"},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
	}))
}

func newUnitOrderTestState(t *testing.T) *domain.GameState {
	t.Helper()
	world := donburi.NewWorld()
	nodeIndex := map[string]donburi.Entity{}
	for idx, nodeID := range []string{"A1", "A2", "A3"} {
		nodeIndex[nodeID] = ecs.CreateNode(world, ecs.MapNode{ID: nodeID, Q: idx, R: 0, Terrain: "plain"})
	}
	state := domain.NewGameState("game-1", []string{"player-1", "player-2"}, []string{"alice", "bob"}, &domain.MapData{
		ID:        "unit-order-test",
		Width:     3,
		Height:    1,
		NodeIndex: nodeIndex,
	})
	state.World = world
	state.NodeIndex = nodeIndex
	return state
}
