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

func TestTurnEventFromEventMapsBuildingBuiltOnlineTurn(t *testing.T) {
	t.Parallel()

	turnEvent := TurnEventFromEvent(event.BuildingBuiltEvent{
		NodeID:       "A2",
		BuildingType: "farm",
		Owner:        "player-1",
		CityID:       "A1",
		OnlineOnTurn: 4,
	})

	if turnEvent.GetType() != "building_built" {
		t.Fatalf("type = %q, want building_built", turnEvent.GetType())
	}
	if got := turnEvent.GetData()["online_on_turn"]; got != "4" {
		t.Fatalf("online_on_turn = %q, want 4", got)
	}
}

func TestTurnEventFromEventMapsBuildingStatusChanged(t *testing.T) {
	t.Parallel()

	turnEvent := TurnEventFromEvent(event.BuildingStatusChangedEvent{
		NodeID:       "C2",
		Status:       "blocked",
		Reason:       "insufficient_resources",
		OnlineOnTurn: 3,
	})

	if turnEvent.GetType() != "building_status_changed" {
		t.Fatalf("type = %q, want building_status_changed", turnEvent.GetType())
	}
	if got := turnEvent.GetData()["node_id"]; got != "C2" {
		t.Fatalf("node_id = %q, want C2", got)
	}
	if got := turnEvent.GetData()["status"]; got != "blocked" {
		t.Fatalf("status = %q, want blocked", got)
	}
	if got := turnEvent.GetData()["reason"]; got != "insufficient_resources" {
		t.Fatalf("reason = %q, want insufficient_resources", got)
	}
	if got := turnEvent.GetData()["online_on_turn"]; got != "3" {
		t.Fatalf("online_on_turn = %q, want 3", got)
	}
}

func TestTurnEventFromEventMapsResearchTargetChangedEvent(t *testing.T) {
	t.Parallel()

	turnEvent := TurnEventFromEvent(event.ResearchTargetChangedEvent{
		PlayerID:     "player-1",
		TechnologyID: "agrarian_foundations",
	})

	if turnEvent.GetType() != "research_target_changed" {
		t.Fatalf("type = %q, want research_target_changed", turnEvent.GetType())
	}
	if got := turnEvent.GetData()["player_id"]; got != "player-1" {
		t.Fatalf("player_id = %q, want player-1", got)
	}
	if got := turnEvent.GetData()["technology_id"]; got != "agrarian_foundations" {
		t.Fatalf("technology_id = %q, want agrarian_foundations", got)
	}
}

func TestTurnEventFromEventMapsPointBudgetEvents(t *testing.T) {
	t.Parallel()

	refreshed := TurnEventFromEvent(event.PointBudgetRefreshedEvent{
		PlayerID: "player-1",
		Key:      domain.PointIndustryOutput,
		Amount:   2,
	})
	if refreshed.GetType() != "point_budget_refreshed" {
		t.Fatalf("type = %q, want point_budget_refreshed", refreshed.GetType())
	}
	if got := refreshed.GetData()["point_key"]; got != "industry_output" {
		t.Fatalf("point_key = %q, want industry_output", got)
	}

	spent := TurnEventFromEvent(event.PointSpentEvent{
		PlayerID: "player-1",
		Key:      domain.PointIndustryOutput,
		Amount:   1,
		Reason:   "build_structure",
	})
	if spent.GetType() != "point_spent" {
		t.Fatalf("type = %q, want point_spent", spent.GetType())
	}
	if got := spent.GetData()["reason"]; got != "build_structure" {
		t.Fatalf("reason = %q, want build_structure", got)
	}
}

func TestTurnEventFromEventMapsRecipeSkippedEvent(t *testing.T) {
	t.Parallel()

	turnEvent := TurnEventFromEvent(event.RecipeSkippedEvent{
		NodeID:   "A1",
		RecipeID: "farm_food",
		Reason:   "building_disabled",
	})

	if turnEvent.GetType() != "recipe_skipped" {
		t.Fatalf("type = %q, want recipe_skipped", turnEvent.GetType())
	}
	if got := turnEvent.GetData()["node_id"]; got != "A1" {
		t.Fatalf("node_id = %q, want A1", got)
	}
	if got := turnEvent.GetData()["recipe_id"]; got != "farm_food" {
		t.Fatalf("recipe_id = %q, want farm_food", got)
	}
	if got := turnEvent.GetData()["reason"]; got != "building_disabled" {
		t.Fatalf("reason = %q, want building_disabled", got)
	}
}

func TestTurnEventFromEventMapsChunk4LifecycleEvents(t *testing.T) {
	t.Parallel()

	captured := TurnEventFromEvent(event.CityCapturedEvent{
		NodeID:       "C3",
		CityID:       "C3",
		OldOwnerID:   "player-1",
		NewOwnerID:   "player-2",
		OnlineOnTurn: 5,
	})
	if captured.GetType() != "city_captured" {
		t.Fatalf("type = %q, want city_captured", captured.GetType())
	}
	if got := captured.GetData()["online_on_turn"]; got != "5" {
		t.Fatalf("city_captured online_on_turn = %q, want 5", got)
	}

	progressed := TurnEventFromEvent(event.FacilityTakeoverProgressedEvent{
		NodeID:             "B2",
		ControllerPlayerID: "player-2",
		Progress:           1,
		Required:           2,
		Status:             "takeover",
		Reason:             "enemy_control",
	})
	if progressed.GetType() != "facility_takeover_progressed" {
		t.Fatalf("type = %q, want facility_takeover_progressed", progressed.GetType())
	}
	if got := progressed.GetData()["status"]; got != "takeover" {
		t.Fatalf("facility_takeover_progressed status = %q, want takeover", got)
	}

	completed := TurnEventFromEvent(event.FacilityTakeoverCompletedEvent{
		NodeID:        "B2",
		NewOwnerID:    "player-2",
		ServiceCityID: "E5",
		OnlineOnTurn:  6,
	})
	if completed.GetType() != "facility_takeover_completed" {
		t.Fatalf("type = %q, want facility_takeover_completed", completed.GetType())
	}
	if got := completed.GetData()["online_on_turn"]; got != "6" {
		t.Fatalf("facility_takeover_completed online_on_turn = %q, want 6", got)
	}

	ruined := TurnEventFromEvent(event.BuildingRuinedEvent{
		NodeID:     "C2",
		NewOwnerID: "player-2",
		Reason:     "city_captured",
	})
	if ruined.GetType() != "building_ruined" {
		t.Fatalf("type = %q, want building_ruined", ruined.GetType())
	}
	if got := ruined.GetData()["reason"]; got != "city_captured" {
		t.Fatalf("building_ruined reason = %q, want city_captured", got)
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
