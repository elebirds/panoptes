// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 验证回合结算报告模块的结算报告映射逻辑。

package report

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/event"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
)

func TestTurnEventFromEventMapsKnownEvents(t *testing.T) {
	t.Parallel()

	turnEvent := TurnEventFromEvent(event.UnitMovedEvent{
		UnitID: "unit-1",
		From:   domain.Position{X: 1, Y: 2},
		To:     domain.Position{X: 3, Y: 4},
	})

	if turnEvent.GetType() != "unit_moved" {
		t.Fatalf("type = %q, want unit_moved", turnEvent.GetType())
	}
	if got := turnEvent.GetData()["unit_id"]; got != "unit-1" {
		t.Fatalf("unit_id = %q, want unit-1", got)
	}
	if got := turnEvent.GetData()["to_x"]; got != "3" {
		t.Fatalf("to_x = %q, want 3", got)
	}
}

func TestSettlementSectionsGroupsNonEmptyDomains(t *testing.T) {
	t.Parallel()

	sections := SettlementSections(
		[]event.Event{event.UnitDamagedEvent{UnitID: "unit-1", Damage: 2, HPAfter: 8, Source: "combat"}},
		[]*pb.TurnEvent{{Type: "settle_city"}},
		[]event.Event{event.UpkeepPaidEvent{PlayerID: "player-1", FoodConsumed: 3}},
	)

	if len(sections) != 3 {
		t.Fatalf("sections len = %d, want 3", len(sections))
	}
	if sections[0].GetSection() != "unit" {
		t.Fatalf("sections[0] = %q, want unit", sections[0].GetSection())
	}
	if sections[1].GetSection() != "map" {
		t.Fatalf("sections[1] = %q, want map", sections[1].GetSection())
	}
	if sections[2].GetSection() != "economy" {
		t.Fatalf("sections[2] = %q, want economy", sections[2].GetSection())
	}
}

func TestBuildTurnSettlementHandlesNilState(t *testing.T) {
	t.Parallel()

	msg := BuildTurnSettlement(nil, "player-1", 5, domain.PhaseResolving.String(), domain.PhasePlanning.String(),
		[]event.Event{event.UnitDiedEvent{UnitID: "unit-1"}},
		nil,
		nil,
	)

	if msg.GetTurn() != 5 {
		t.Fatalf("turn = %d, want 5", msg.GetTurn())
	}
	if msg.GetPhase() != domain.PhaseResolving.String() {
		t.Fatalf("phase = %q, want %q", msg.GetPhase(), domain.PhaseResolving.String())
	}
	if msg.GetNextPhase() != domain.PhasePlanning.String() {
		t.Fatalf("next_phase = %q, want %q", msg.GetNextPhase(), domain.PhasePlanning.String())
	}
	if len(msg.GetSections()) != 1 {
		t.Fatalf("sections len = %d, want 1", len(msg.GetSections()))
	}
}
