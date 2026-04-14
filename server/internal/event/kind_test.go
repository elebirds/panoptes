package event

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
)

func TestCombatEventKinds(t *testing.T) {
	t.Parallel()

	cases := []struct {
		name string
		kind string
		ev   Event
	}{
		{name: "unit moved", kind: "unit_moved", ev: UnitMovedEvent{}},
		{name: "unit damaged", kind: "unit_damaged", ev: UnitDamagedEvent{}},
		{name: "unit died", kind: "unit_died", ev: UnitDiedEvent{}},
		{name: "castle damaged", kind: "castle_damaged", ev: CastleDamagedEvent{}},
		{name: "castle destroyed", kind: "castle_destroyed", ev: CastleDestroyedEvent{}},
		{name: "road destroyed", kind: "road_destroyed", ev: RoadDestroyedEvent{}},
		{name: "building damaged", kind: "building_damaged", ev: BuildingDamagedEvent{}},
		{name: "conflict resolved", kind: "conflict", ev: ConflictResolvedEvent{Location: domain.Position{}}},
	}

	for _, tc := range cases {
		tc := tc
		t.Run(tc.name, func(t *testing.T) {
			t.Parallel()
			if got := tc.ev.Kind(); got != tc.kind {
				t.Fatalf("Kind() = %q, want %q", got, tc.kind)
			}
		})
	}
}

func TestProductionEventKinds(t *testing.T) {
	t.Parallel()

	cases := []struct {
		name string
		kind string
		ev   Event
	}{
		{name: "building built", kind: "building_built", ev: BuildingBuiltEvent{}},
		{name: "resource produced", kind: "resource_produced", ev: ResourceProducedEvent{}},
		{name: "resource flowed", kind: "resource_flowed", ev: ResourceFlowedEvent{}},
		{name: "road built", kind: "road_built", ev: RoadBuiltEvent{}},
		{name: "unit produced", kind: "unit_produced", ev: UnitProducedEvent{}},
		{name: "build points recharged", kind: "build_points_recharged", ev: BuildPointsRechargedEvent{}},
		{name: "upkeep paid", kind: "upkeep_paid", ev: UpkeepPaidEvent{}},
		{name: "unit starving", kind: "unit_starving", ev: UnitStarvingEvent{}},
		{name: "building deactivated", kind: "building_deactivated", ev: BuildingDeactivatedEvent{}},
	}

	for _, tc := range cases {
		tc := tc
		t.Run(tc.name, func(t *testing.T) {
			t.Parallel()
			if got := tc.ev.Kind(); got != tc.kind {
				t.Fatalf("Kind() = %q, want %q", got, tc.kind)
			}
		})
	}
}
