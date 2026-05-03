package debug

import (
	"fmt"
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/yohamta/donburi"
)

func AssertStateInvariants(t testing.TB, state *domain.GameState) {
	t.Helper()
	if state == nil {
		t.Fatalf("state is nil")
	}
	if state.World == nil {
		t.Fatalf("state world is nil")
	}
	if state.Map == nil {
		t.Fatalf("state map is nil")
	}
	if state.Players == nil {
		t.Fatalf("state players map is nil")
	}
	if state.NodeIndex == nil {
		t.Fatalf("state node index is nil")
	}

	assertPlayerInvariants(t, state)
	assertNodeIndexInvariants(t, state)
	assertBuildingInvariants(t, state)
	assertUnitInvariants(t, state)
}

func assertPlayerInvariants(t testing.TB, state *domain.GameState) {
	t.Helper()
	for playerID, player := range state.Players {
		if playerID == "" {
			t.Fatalf("players map contains empty player id")
		}
		if player == nil {
			t.Fatalf("player %q state is nil", playerID)
		}
		if player.PlayerID != "" && player.PlayerID != playerID {
			t.Fatalf("player %q has mismatched PlayerID %q", playerID, player.PlayerID)
		}
		if player.TokensLeft < 0 {
			t.Fatalf("player %q tokens left = %d, want non-negative", playerID, player.TokensLeft)
		}
		if player.CapitalCityCoreHP < 0 {
			t.Fatalf("player %q capital city core hp = %d, want non-negative", playerID, player.CapitalCityCoreHP)
		}
		assertResourceBagNonNegative(t, fmt.Sprintf("player %q resources", playerID), player.Resources)
		for cityID, city := range player.Cities {
			if cityID == "" {
				t.Fatalf("player %q cities map contains empty city id", playerID)
			}
			if city == nil {
				t.Fatalf("player %q city %q is nil", playerID, cityID)
			}
			if city.CityID != "" && city.CityID != cityID {
				t.Fatalf("player %q city map key %q has mismatched CityID %q", playerID, cityID, city.CityID)
			}
			if city.OwnerID != "" && city.OwnerID != playerID {
				t.Fatalf("player %q city %q has mismatched owner %q", playerID, cityID, city.OwnerID)
			}
			if city.CoreNodeID != "" {
				if _, ok := state.GetNode(city.CoreNodeID); !ok {
					t.Fatalf("player %q city %q core node %q missing from state", playerID, cityID, city.CoreNodeID)
				}
			}
			assertResourceBagNonNegative(t, fmt.Sprintf("player %q city %q storage", playerID, cityID), city.Storage)
		}
	}
}

func assertNodeIndexInvariants(t testing.TB, state *domain.GameState) {
	t.Helper()
	for nodeID, entity := range state.NodeIndex {
		if nodeID == "" {
			t.Fatalf("node index contains empty node id")
		}
		if !state.World.Valid(entity) {
			t.Fatalf("node index %q points to invalid entity", nodeID)
		}
		entry := state.World.Entry(entity)
		if entry == nil || !entry.HasComponent(ecs.NodeC) {
			t.Fatalf("node index %q points to entity without node component", nodeID)
		}
		if got := ecs.NodeC.Get(entry).ID; got != nodeID {
			t.Fatalf("node index %q points to node %q", nodeID, got)
		}
	}
}

func assertBuildingInvariants(t testing.TB, state *domain.GameState) {
	t.Helper()
	ecs.NodesWithBuilding(state.World).Each(state.World, func(entry *donburi.Entry) {
		if entry == nil {
			t.Fatalf("building query returned nil entry")
		}
		node := ecs.NodeC.Get(entry)
		building := ecs.BuildingC.Get(entry)
		if node.ID == "" {
			t.Fatalf("building node has empty id")
		}
		if _, ok := state.GetNode(node.ID); !ok {
			t.Fatalf("building node %q missing from state node index", node.ID)
		}
		if building.Type == "" {
			t.Fatalf("building at node %q has empty type", node.ID)
		}
		if building.Owner != "" && state.Players[building.Owner] == nil {
			t.Fatalf("building at node %q references unknown owner %q", node.ID, building.Owner)
		}
		if building.HP < 0 {
			t.Fatalf("building at node %q hp = %d, want non-negative", node.ID, building.HP)
		}
		if building.MaxHP < 0 {
			t.Fatalf("building at node %q max hp = %d, want non-negative", node.ID, building.MaxHP)
		}
		if building.MaxHP > 0 && building.HP > building.MaxHP {
			t.Fatalf("building at node %q hp = %d exceeds max hp %d", node.ID, building.HP, building.MaxHP)
		}
		if entry.HasComponent(ecs.BuildingOperationC) {
			op := ecs.BuildingOperationC.Get(entry)
			assertResourceBagNonNegative(t, fmt.Sprintf("building %q consumed resources", node.ID), op.ConsumedResources)
		}
	})
}

func assertUnitInvariants(t testing.TB, state *domain.GameState) {
	t.Helper()
	unitIDs := map[string]struct{}{}
	ecs.AllUnits(state.World).Each(state.World, func(entry *donburi.Entry) {
		if entry == nil {
			t.Fatalf("unit query returned nil entry")
		}
		stats := ecs.UnitStatsC.Get(entry)
		pos := ecs.PositionC.Get(entry)
		if stats.ID == "" {
			t.Fatalf("unit at {%d,%d} has empty id", pos.Q, pos.R)
		}
		if _, ok := unitIDs[stats.ID]; ok {
			t.Fatalf("duplicate unit id %q", stats.ID)
		}
		unitIDs[stats.ID] = struct{}{}
		if stats.Faction == "" {
			t.Fatalf("unit %q has empty faction", stats.ID)
		}
		if state.Players[stats.Faction] == nil {
			t.Fatalf("unit %q references unknown faction %q", stats.ID, stats.Faction)
		}
		if stats.Type == "" {
			t.Fatalf("unit %q has empty type", stats.ID)
		}
		if stats.HP < 0 {
			t.Fatalf("unit %q hp = %d, want non-negative", stats.ID, stats.HP)
		}
		if stats.MaxHP < 0 {
			t.Fatalf("unit %q max hp = %d, want non-negative", stats.ID, stats.MaxHP)
		}
		if stats.MaxHP > 0 && stats.HP > stats.MaxHP {
			t.Fatalf("unit %q hp = %d exceeds max hp %d", stats.ID, stats.HP, stats.MaxHP)
		}
		if nodeID := nodeIDAt(state.World, domain.Position{Q: pos.Q, R: pos.R}); nodeID == "" {
			t.Fatalf("unit %q position {%d,%d} does not map to a node", stats.ID, pos.Q, pos.R)
		}
	})
}

func assertResourceBagNonNegative(t testing.TB, label string, bag domain.ResourceBag) {
	t.Helper()
	for key, amount := range bag {
		if amount < 0 {
			t.Fatalf("%s has negative %s amount %d", label, key, amount)
		}
	}
}
