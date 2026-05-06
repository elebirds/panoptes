// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 验证回合结算报告模块的结算报告映射逻辑。

package projection

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/event"
	gameresolution "github.com/elebirds/panoptes/internal/game/resolution"
)

type projectedEventPayload struct {
	kind string
	data map[string]string
}

func projectEventPayload(evt event.Event) projectedEventPayload {
	kind, data := EventPayloadFromEvent(evt)
	return projectedEventPayload{kind: kind, data: data}
}

func (p projectedEventPayload) GetType() string {
	return p.kind
}

func (p projectedEventPayload) GetData() map[string]string {
	return p.data
}

func TestEventPayloadFromEventMapsKnownEvents(t *testing.T) {
	t.Parallel()

	turnEvent := projectEventPayload(event.UnitMovedEvent{
		UnitID: "unit-1",
		From:   domain.Position{Q: 1, R: 2},
		To:     domain.Position{Q: 3, R: 4},
	})

	if turnEvent.GetType() != "unit_moved" {
		t.Fatalf("type = %q, want unit_moved", turnEvent.GetType())
	}
	if got := turnEvent.GetData()["unit_id"]; got != "unit-1" {
		t.Fatalf("unit_id = %q, want unit-1", got)
	}
	if got := turnEvent.GetData()["to_q"]; got != "3" {
		t.Fatalf("to_q = %q, want 3", got)
	}
}

func TestEventPayloadFromEventMapsCombatAttackers(t *testing.T) {
	t.Parallel()

	unitDamage := projectEventPayload(event.UnitDamagedEvent{
		UnitID:     "defender-1",
		Damage:     4,
		HPAfter:    6,
		Source:     "combat",
		AttackerID: "attacker-1",
	})
	if got := unitDamage.GetData()["unit_id"]; got != "defender-1" {
		t.Fatalf("unit_damaged unit_id = %q, want defender-1", got)
	}
	if got := unitDamage.GetData()["attacker"]; got != "attacker-1" {
		t.Fatalf("unit_damaged attacker = %q, want attacker-1", got)
	}

	buildingDamage := projectEventPayload(event.BuildingDamagedEvent{
		NodeID:     "node-1",
		Damage:     5,
		HPAfter:    15,
		AttackerID: "attacker-2",
	})
	if got := buildingDamage.GetData()["attacker"]; got != "attacker-2" {
		t.Fatalf("building_damaged attacker = %q, want attacker-2", got)
	}

	cityCoreDamage := projectEventPayload(event.CityCoreDamagedEvent{
		NodeID:     "core-1",
		Damage:     6,
		HPAfter:    20,
		AttackerID: "attacker-3",
	})
	if got := cityCoreDamage.GetData()["attacker"]; got != "attacker-3" {
		t.Fatalf("city_core_damaged attacker = %q, want attacker-3", got)
	}
}

func TestEventPayloadFromEventMapsBuildingBuiltOnlineTurn(t *testing.T) {
	t.Parallel()

	turnEvent := projectEventPayload(event.BuildingBuiltEvent{
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

func TestEventPayloadFromEventMapsBuildingRepaired(t *testing.T) {
	t.Parallel()

	turnEvent := projectEventPayload(event.BuildingRepairedEvent{
		NodeID: "A2",
		Owner:  "player-1",
	})

	if turnEvent.GetType() != "building_repaired" {
		t.Fatalf("type = %q, want building_repaired", turnEvent.GetType())
	}
	if got := turnEvent.GetData()["node_id"]; got != "A2" {
		t.Fatalf("node_id = %q, want A2", got)
	}
	if got := turnEvent.GetData()["owner"]; got != "player-1" {
		t.Fatalf("owner = %q, want player-1", got)
	}
}

func TestEventPayloadFromEventMapsBuildingStatusChanged(t *testing.T) {
	t.Parallel()

	turnEvent := projectEventPayload(event.BuildingStatusChangedEvent{
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
	if got := turnEvent.GetData()["reason_message"]; got != "生产所需资源不足，本回合无法推进。" {
		t.Fatalf("reason_message = %q, want localized reason message", got)
	}
	if got := turnEvent.GetData()["online_on_turn"]; got != "3" {
		t.Fatalf("online_on_turn = %q, want 3", got)
	}
}

func TestEventPayloadFromEventMapsResearchTargetChangedEvent(t *testing.T) {
	t.Parallel()

	turnEvent := projectEventPayload(event.ResearchTargetChangedEvent{
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

func TestEventPayloadFromEventMapsPointBudgetEvents(t *testing.T) {
	t.Parallel()

	refreshed := projectEventPayload(event.PointBudgetRefreshedEvent{
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

	spent := projectEventPayload(event.PointSpentEvent{
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

func TestEventPayloadFromEventMapsRoadRepairedEvent(t *testing.T) {
	t.Parallel()

	turnEvent := projectEventPayload(event.RoadRepairedEvent{
		FromNode: "A1",
		ToNode:   "A2",
		Owner:    "player-1",
	})

	if turnEvent.GetType() != "road_repaired" {
		t.Fatalf("type = %q, want road_repaired", turnEvent.GetType())
	}
	if got := turnEvent.GetData()["from_node"]; got != "A1" {
		t.Fatalf("from_node = %q, want A1", got)
	}
	if got := turnEvent.GetData()["owner"]; got != "player-1" {
		t.Fatalf("owner = %q, want player-1", got)
	}
}

func TestEventPayloadFromEventMapsRecipeSkippedEvent(t *testing.T) {
	t.Parallel()

	turnEvent := projectEventPayload(event.RecipeSkippedEvent{
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
	if got := turnEvent.GetData()["reason_message"]; got != "建筑当前停摆，本回合不会生产。" {
		t.Fatalf("reason_message = %q, want localized reason message", got)
	}
}

func TestEventPayloadFromEventMapsRecipeProgressedBlockedReasonMessage(t *testing.T) {
	t.Parallel()

	turnEvent := projectEventPayload(event.RecipeProgressedEvent{
		NodeID:        "A1",
		ProgressTurns: 1,
		RequiredTurns: 3,
		BlockedReason: "insufficient_points",
	})

	if turnEvent.GetType() != "recipe_progressed" {
		t.Fatalf("type = %q, want recipe_progressed", turnEvent.GetType())
	}
	if got := turnEvent.GetData()["blocked_reason_message"]; got != "生产所需点数不足，本回合无法推进。" {
		t.Fatalf("blocked_reason_message = %q, want localized blocked message", got)
	}
}

func TestEventPayloadFromEventMapsBuildSkippedReasonMessage(t *testing.T) {
	t.Parallel()

	turnEvent := projectEventPayload(event.BuildSkippedEvent{
		PlayerID:     "player-1",
		NodeID:       "A2",
		BuildingType: "farm",
		Reason:       "outside_territory",
	})

	if turnEvent.GetType() != "building_skipped" {
		t.Fatalf("type = %q, want building_skipped", turnEvent.GetType())
	}
	if got := turnEvent.GetData()["reason_message"]; got != "该节点不在你的有效辖区内，当前不能建造。" {
		t.Fatalf("reason_message = %q, want localized build skip message", got)
	}
}

func TestEventPayloadFromEventMapsChunk4LifecycleEvents(t *testing.T) {
	t.Parallel()

	captured := projectEventPayload(event.CityCapturedEvent{
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

	progressed := projectEventPayload(event.FacilityTakeoverProgressedEvent{
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
	if got := progressed.GetData()["reason_message"]; got != "建筑正处于敌方控制下，当前无法正常运作。" {
		t.Fatalf("facility_takeover_progressed reason_message = %q, want localized reason message", got)
	}

	completed := projectEventPayload(event.FacilityTakeoverCompletedEvent{
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

	ruined := projectEventPayload(event.BuildingRuinedEvent{
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

func TestDomainEventEnvelopesPreserveChannelOrder(t *testing.T) {
	t.Parallel()

	collector := gameresolution.NewCollector()
	collector.AppendDeferred(gameresolution.ChannelUnit, event.UnitDamagedEvent{UnitID: "unit-1", Damage: 2, HPAfter: 8, Source: "combat"})
	collector.AppendDeferred(gameresolution.ChannelMap, event.CityFoundedEvent{PlayerID: "player-1", CityID: "B2", CenterNodeID: "B2"})
	collector.AppendDeferred(gameresolution.ChannelEconomy, event.UpkeepPaidEvent{PlayerID: "player-1", FoodConsumed: 3})
	events := DomainEventEnvelopes(collector, 2, domain.PhaseResolving.String())

	if len(events) != 3 {
		t.Fatalf("events len = %d, want 3", len(events))
	}
	if events[0].GetChannel() != "unit" {
		t.Fatalf("events[0].channel = %q, want unit", events[0].GetChannel())
	}
	if events[1].GetChannel() != "map" {
		t.Fatalf("events[1].channel = %q, want map", events[1].GetChannel())
	}
	if events[2].GetChannel() != "economy" {
		t.Fatalf("events[2].channel = %q, want economy", events[2].GetChannel())
	}
}

func TestDomainEventEnvelopesSkipsInternalStateOnlyEvents(t *testing.T) {
	t.Parallel()

	collector := gameresolution.NewCollector()
	collector.AppendDeferred(gameresolution.ChannelEconomy, event.RecipeSelectionChangedEvent{
		NodeID:        "A1",
		RecipeID:      "farm_food",
		RequiredTurns: 2,
	})
	collector.AppendDeferred(gameresolution.ChannelEconomy, &event.RecipeSelectionChangedEvent{
		NodeID:        "A2",
		RecipeID:      "farm_food",
		RequiredTurns: 2,
	})
	collector.AppendDeferred(gameresolution.ChannelEconomy, event.RecipeProgressedEvent{
		NodeID:        "A1",
		ProgressTurns: 1,
		RequiredTurns: 2,
	})

	events := DomainEventEnvelopes(collector, 2, domain.PhaseResolving.String())
	if len(events) != 1 {
		t.Fatalf("events len = %d, want 1", len(events))
	}
	if got := events[0].GetKind(); got != "recipe_progressed" {
		t.Fatalf("events[0].kind = %q, want recipe_progressed", got)
	}
}

func TestProjectGameSyncHandlesNilState(t *testing.T) {
	t.Parallel()

	collector := gameresolution.NewCollector()
	collector.AppendDeferred(gameresolution.ChannelUnit, event.UnitDiedEvent{UnitID: "unit-1"})
	msg := ProjectGameSync(nil, "player-1", 5, domain.PhaseResolving.String(), domain.PhasePlanning.String(), collector)

	if msg.GetTurn() != 5 {
		t.Fatalf("turn = %d, want 5", msg.GetTurn())
	}
	if msg.GetPhase() != domain.PhaseResolving.String() {
		t.Fatalf("phase = %q, want %q", msg.GetPhase(), domain.PhaseResolving.String())
	}
	if msg.GetNextPhase() != domain.PhasePlanning.String() {
		t.Fatalf("next_phase = %q, want %q", msg.GetNextPhase(), domain.PhasePlanning.String())
	}
	if len(msg.GetEvents()) != 1 {
		t.Fatalf("events len = %d, want 1", len(msg.GetEvents()))
	}
}

func TestProjectGameSyncKeepsPlanningAndEconomyChannelsSeparate(t *testing.T) {
	t.Parallel()

	collector := gameresolution.NewCollector()
	collector.AppendDeferred(gameresolution.ChannelPlanning, event.PolicyChangedEvent{
		PlayerID:  "player-1",
		OldPolicy: "",
		NewPolicy: "expansion",
	})
	collector.AppendDeferred(gameresolution.ChannelEconomy, event.PointSpentEvent{
		PlayerID: "player-1",
		Key:      domain.PointIndustryOutput,
		Amount:   1,
		Reason:   "build_structure",
	})
	collector.AppendDeferred(gameresolution.ChannelMap, event.CityFoundedEvent{
		PlayerID:     "player-1",
		CityID:       "B2",
		CenterNodeID: "B2",
	})

	msg := ProjectGameSync(nil, "player-1", 2, domain.PhaseResolving.String(), domain.PhasePlanning.String(), collector)

	if len(msg.GetEvents()) != 3 {
		t.Fatalf("events len = %d, want 3", len(msg.GetEvents()))
	}
	if msg.GetEvents()[0].GetChannel() != "planning" || msg.GetEvents()[0].GetKind() != "national_policy_changed" {
		t.Fatalf("events[0] = (%q,%q), want planning/national_policy_changed", msg.GetEvents()[0].GetChannel(), msg.GetEvents()[0].GetKind())
	}
	if msg.GetEvents()[1].GetChannel() != "map" || msg.GetEvents()[1].GetKind() != "city_founded" {
		t.Fatalf("events[1] = (%q,%q), want map/city_founded", msg.GetEvents()[1].GetChannel(), msg.GetEvents()[1].GetKind())
	}
	if msg.GetEvents()[2].GetChannel() != "economy" || msg.GetEvents()[2].GetKind() != "point_spent" {
		t.Fatalf("events[2] = (%q,%q), want economy/point_spent", msg.GetEvents()[2].GetChannel(), msg.GetEvents()[2].GetKind())
	}
}

func TestProjectPlanningStartEventsMapsTechnologyActivationOnly(t *testing.T) {
	t.Parallel()

	events := ProjectPlanningStartEvents(2, []event.Event{
		event.TechnologyActivatedEvent{PlayerID: "player-1", TechnologyID: "agrarian_foundations"},
		event.TechnologyGrantAppliedEvent{PlayerID: "player-1", SourceTech: "agrarian_foundations"},
	})

	if len(events) != 2 {
		t.Fatalf("events len = %d, want 2", len(events))
	}
	if events[0].GetKind() != "technology_activated" {
		t.Fatalf("events[0].kind = %q, want technology_activated", events[0].GetKind())
	}
	if events[1].GetKind() != "technology_grant_applied" {
		t.Fatalf("events[1].kind = %q, want technology_grant_applied", events[1].GetKind())
	}
	if events[0].GetTurn() != 2 {
		t.Fatalf("events[0].turn = %d, want 2", events[0].GetTurn())
	}
	nextTurnEvents := ProjectPlanningStartEvents(3, []event.Event{
		event.TechnologyActivatedEvent{PlayerID: "player-1", TechnologyID: "agrarian_foundations"},
	})
	if events[0].GetEventId() == nextTurnEvents[0].GetEventId() {
		t.Fatalf("planning start event ids should differ across turns: %q", events[0].GetEventId())
	}
}

func TestProjectGameSyncDoesNotContainPlanningStartActivationEvents(t *testing.T) {
	t.Parallel()

	collector := gameresolution.NewCollector()
	collector.AppendDeferred(gameresolution.ChannelEconomy, event.TechnologyCompletedEvent{
		PlayerID:     "player-1",
		TechnologyID: "agrarian_foundations",
	})

	msg := ProjectGameSync(nil, "player-1", 2, domain.PhaseResolving.String(), domain.PhasePlanning.String(), collector)
	if len(msg.GetEvents()) != 1 {
		t.Fatalf("events len = %d, want 1", len(msg.GetEvents()))
	}
	if got := msg.GetEvents()[0].GetKind(); got != "technology_completed" {
		t.Fatalf("events[0].kind = %q, want technology_completed", got)
	}
}
