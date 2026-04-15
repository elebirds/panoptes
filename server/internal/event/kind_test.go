// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 验证事件模型的事件 Kind 标识。

package event

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
)

func TestResolutionEventKinds(t *testing.T) {
	t.Parallel()

	cases := []struct {
		name string
		kind string
		ev   Event
	}{
		{name: "unit moved", kind: "unit_moved", ev: UnitMovedEvent{}},
		{name: "unit damaged", kind: "unit_damaged", ev: UnitDamagedEvent{}},
		{name: "unit died", kind: "unit_died", ev: UnitDiedEvent{}},
		{name: "city core damaged", kind: "city_core_damaged", ev: CityCoreDamagedEvent{}},
		{name: "city core destroyed", kind: "city_core_destroyed", ev: CityCoreDestroyedEvent{}},
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

func TestEconomyEventKinds(t *testing.T) {
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
		{name: "industry output refreshed", kind: "industry_output_refreshed", ev: IndustryOutputRefreshedEvent{}},
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
