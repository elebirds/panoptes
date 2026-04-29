// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现回合结算报告模块的结算报告映射逻辑。

package projection

import (
	"reflect"
	"strconv"
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/event"
	gamefeedback "github.com/elebirds/panoptes/internal/game/feedback"
	gameresolution "github.com/elebirds/panoptes/internal/game/resolution"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
)

func ProjectPlanningStartEvents(turn int32, events []event.Event) []*pb.DomainEventEnvelope {
	out := make([]*pb.DomainEventEnvelope, 0, len(events))
	for _, evt := range events {
		if shouldSkipProjectedEvent(evt) {
			continue
		}
		out = append(out, domainEventEnvelope(evt, gameresolution.ChannelPlanning, turn, domain.PhasePlanning.String(), len(out)))
	}
	return out
}

func EventPayloadFromEvent(evt event.Event) (string, map[string]string) {
	if evt == nil {
		return "unknown", map[string]string{}
	}

	// 这里是服务端对外事件字符串与 data 字段的唯一映射入口。
	// 规则层只负责产出领域事件，不直接关心 protobuf 文本口径。
	switch e := evt.(type) {
	case event.BuildingBuiltEvent:
		data := map[string]string{
			"node_id":       strings.TrimSpace(e.NodeID),
			"building_type": strings.TrimSpace(e.BuildingType),
			"owner":         strings.TrimSpace(e.Owner),
			"city_id":       strings.TrimSpace(e.CityID),
			"building_hp":   strconv.Itoa(resolveBuiltBuildingHP(e.BuildingType)),
		}
		if e.OnlineOnTurn > 0 {
			data["online_on_turn"] = strconv.Itoa(e.OnlineOnTurn)
		}
		return e.Kind(), data
	case event.ResourceProducedEvent:
		return e.Kind(), map[string]string{
			"node_id":       strings.TrimSpace(e.NodeID),
			"resource_type": strings.TrimSpace(e.ResourceType),
			"amount":        strconv.Itoa(e.Amount),
			"owner":         strings.TrimSpace(e.Owner),
			"city_id":       strings.TrimSpace(e.CityID),
		}
	case event.ResourceFlowedEvent:
		data := map[string]string{
			"from_node_id": strings.TrimSpace(e.FromNodeID),
			"to_node_id":   strings.TrimSpace(e.ToNodeID),
		}
		for key, amount := range resourceBagData(e.Resources) {
			data[key] = amount
		}
		return e.Kind(), data
	case event.RoadBuiltEvent:
		return e.Kind(), map[string]string{
			"from_node": strings.TrimSpace(e.FromNode),
			"to_node":   strings.TrimSpace(e.ToNode),
			"owner":     strings.TrimSpace(e.Owner),
			"cost":      strconv.Itoa(e.Cost),
		}
	case event.UnitProducedEvent:
		return e.Kind(), map[string]string{
			"node_id":   strings.TrimSpace(e.NodeID),
			"unit_type": strings.TrimSpace(e.UnitType),
			"faction":   strings.TrimSpace(e.Faction),
			"city_id":   strings.TrimSpace(e.CityID),
			"count":     strconv.Itoa(e.Count),
		}
	case event.PointBudgetRefreshedEvent:
		// 点数预算刷新是 settlement 里的“本回合经济输入”证据，
		// 它告诉客户端这一回合研究/工业预算被设到了多少。
		return e.Kind(), map[string]string{
			"player_id": strings.TrimSpace(e.PlayerID),
			"point_key": string(e.Key),
			"amount":    strconv.Itoa(e.Amount),
		}
	case event.PointSpentEvent:
		// point_spent 描述的是“本回合预算被用在了什么地方”，
		// 与玩家资源库存不同，它反映的是 resolving 内的临时预算消耗。
		return e.Kind(), map[string]string{
			"player_id": strings.TrimSpace(e.PlayerID),
			"point_key": string(e.Key),
			"amount":    strconv.Itoa(e.Amount),
			"reason":    strings.TrimSpace(e.Reason),
		}
	case event.BuildSkippedEvent:
		data := map[string]string{
			"player_id":     strings.TrimSpace(e.PlayerID),
			"node_id":       strings.TrimSpace(e.NodeID),
			"building_type": strings.TrimSpace(e.BuildingType),
			"reason":        strings.TrimSpace(e.Reason),
		}
		if reasonMessage := gamefeedback.BuildReasonMessage(e.Reason); reasonMessage != "" {
			data["reason_message"] = reasonMessage
		}
		return e.Kind(), data
	case event.IndustryOutputRefreshedEvent:
		return e.Kind(), map[string]string{
			"player_id": strings.TrimSpace(e.PlayerID),
			"amount":    strconv.Itoa(e.Amount),
		}
	case event.UpkeepPaidEvent:
		return e.Kind(), map[string]string{
			"player_id":     strings.TrimSpace(e.PlayerID),
			"food_consumed": strconv.Itoa(e.FoodConsumed),
		}
	case event.UnitStarvingEvent:
		return e.Kind(), map[string]string{
			"unit_id":         strings.TrimSpace(e.UnitID),
			"damage_per_turn": strconv.Itoa(e.DamagePerTurn),
		}
	case event.TechnologyCompletedEvent:
		return e.Kind(), map[string]string{
			"player_id":     strings.TrimSpace(e.PlayerID),
			"technology_id": strings.TrimSpace(e.TechnologyID),
		}
	case event.TechnologyActivatedEvent:
		return e.Kind(), map[string]string{
			"player_id":     strings.TrimSpace(e.PlayerID),
			"technology_id": strings.TrimSpace(e.TechnologyID),
		}
	case event.ResearchProgressAppliedEvent:
		return e.Kind(), map[string]string{
			"player_id": strings.TrimSpace(e.PlayerID),
			"amount":    strconv.Itoa(e.Amount),
		}
	case event.TechnologyGrantAppliedEvent:
		return e.Kind(), map[string]string{
			"player_id":     strings.TrimSpace(e.PlayerID),
			"technology_id": strings.TrimSpace(e.SourceTech),
		}
	case event.ResearchTargetChangedEvent:
		return e.Kind(), map[string]string{
			"player_id":     strings.TrimSpace(e.PlayerID),
			"technology_id": strings.TrimSpace(e.TechnologyID),
		}
	case event.RecipeSelectionChangedEvent:
		// 配方切换属于 operation state 的内部重置，不单独投影为 settlement 事件。
		// 客户端在结算后直接从 NodeView.Operation 看到新的 recipe 选择即可。
		return "unknown", map[string]string{}
	case event.RecipeSkippedEvent:
		data := map[string]string{
			"node_id":   strings.TrimSpace(e.NodeID),
			"recipe_id": strings.TrimSpace(e.RecipeID),
			"reason":    strings.TrimSpace(e.Reason),
		}
		if reasonMessage := gamefeedback.RecipeReasonMessage(e.Reason); reasonMessage != "" {
			data["reason_message"] = reasonMessage
		}
		return e.Kind(), data
	case event.RecipeProgressedEvent:
		// recipe_progressed 是经济链的关键反馈：
		// 它让客户端知道当前建筑推进到了哪里，以及是否因为 blocked reason 停在这里。
		data := map[string]string{
			"node_id":        strings.TrimSpace(e.NodeID),
			"progress_turns": strconv.Itoa(e.ProgressTurns),
			"required_turns": strconv.Itoa(e.RequiredTurns),
			"blocked_reason": strings.TrimSpace(e.BlockedReason),
		}
		if blockedReasonMessage := gamefeedback.RuntimeReasonMessage(e.BlockedReason); blockedReasonMessage != "" {
			data["blocked_reason_message"] = blockedReasonMessage
		}
		return e.Kind(), data
	case event.RecipeDelayedEvent:
		return e.Kind(), map[string]string{
			"node_id":     strings.TrimSpace(e.NodeID),
			"delay_turns": strconv.Itoa(e.DelayTurns),
			"reason":      strings.TrimSpace(e.Reason),
		}
	case event.RecipeCompletedEvent:
		// recipe_completed 只表达“这条 recipe 已完成”，
		// 真正产出的资源和单位已经体现在 settlement 后的权威快照里。
		return e.Kind(), map[string]string{
			"node_id": strings.TrimSpace(e.NodeID),
			"owner":   strings.TrimSpace(e.Owner),
		}
	case event.BuildingStatusChangedEvent:
		// 建筑运行态变化会进入 settlement 事件流，方便客户端解释
		// 为什么同一建筑这回合从 active 变成 blocked / idle / disabled。
		data := map[string]string{
			"node_id": strings.TrimSpace(e.NodeID),
			"status":  strings.TrimSpace(e.Status),
			"reason":  strings.TrimSpace(e.Reason),
		}
		if reasonMessage := gamefeedback.RuntimeReasonMessage(e.Reason); reasonMessage != "" {
			data["reason_message"] = reasonMessage
		}
		if e.OnlineOnTurn > 0 {
			data["online_on_turn"] = strconv.Itoa(e.OnlineOnTurn)
		}
		return e.Kind(), data
	case event.CityFoundedEvent:
		data := map[string]string{
			"player_id":      strings.TrimSpace(e.PlayerID),
			"city_id":        strings.TrimSpace(e.CityID),
			"center_node_id": strings.TrimSpace(e.CenterNodeID),
		}
		if len(e.TerritoryIDs) > 0 {
			data["territory_node_ids"] = strings.Join(e.TerritoryIDs, ",")
		}
		if e.OnlineOnTurn > 0 {
			data["online_on_turn"] = strconv.Itoa(e.OnlineOnTurn)
		}
		return e.Kind(), data
	case event.CityFoundingFailedEvent:
		return e.Kind(), map[string]string{
			"player_id": strings.TrimSpace(e.PlayerID),
			"unit_id":   strings.TrimSpace(e.UnitID),
			"reason":    strings.TrimSpace(e.Reason),
		}
	case event.MinisterActedEvent:
		return e.Kind(), map[string]string{
			"player_id":     strings.TrimSpace(e.PlayerID),
			"minister_role": strings.TrimSpace(e.MinisterRole),
			"action_id":     strings.TrimSpace(e.ActionID),
			"report":        strings.TrimSpace(e.Report),
		}
	case event.PolicyChangedEvent:
		return e.Kind(), map[string]string{
			"player_id":              strings.TrimSpace(e.PlayerID),
			"old_national_policy_id": strings.TrimSpace(e.OldPolicy),
			"new_national_policy_id": strings.TrimSpace(e.NewPolicy),
		}
	case event.InstitutionLoadoutChangedEvent:
		return e.Kind(), map[string]string{
			"player_id":       strings.TrimSpace(e.PlayerID),
			"activation_turn": strconv.Itoa(e.ActivationTurn),
		}
	case event.InstitutionLoadoutActivatedEvent:
		data := map[string]string{
			"player_id": strings.TrimSpace(e.PlayerID),
		}
		if len(e.PolicyIDs) > 0 {
			data["policy_ids"] = strings.Join(e.PolicyIDs, ",")
		}
		return e.Kind(), data
	case event.TokenUsedEvent:
		return e.Kind(), map[string]string{
			"player_id":   strings.TrimSpace(e.PlayerID),
			"action":      strings.TrimSpace(e.Action),
			"tokens_left": strconv.Itoa(e.TokensLeft),
		}
	case event.UnitMovedEvent:
		return e.Kind(), map[string]string{
			"unit_id": e.UnitID,
			"from_q":  strconv.Itoa(e.From.Q),
			"from_r":  strconv.Itoa(e.From.R),
			"to_q":    strconv.Itoa(e.To.Q),
			"to_r":    strconv.Itoa(e.To.R),
		}
	case event.UnitDamagedEvent:
		return e.Kind(), map[string]string{
			"unit_id":  e.UnitID,
			"damage":   strconv.Itoa(e.Damage),
			"hp_after": strconv.Itoa(e.HPAfter),
			"source":   e.Source,
		}
	case event.UnitDiedEvent:
		return e.Kind(), map[string]string{
			"unit_id":   e.UnitID,
			"killer_id": e.KillerID,
			"pos_q":     strconv.Itoa(e.Pos.Q),
			"pos_r":     strconv.Itoa(e.Pos.R),
		}
	case event.CityCoreDamagedEvent:
		return e.Kind(), map[string]string{
			"node_id":  e.NodeID,
			"damage":   strconv.Itoa(e.Damage),
			"hp_after": strconv.Itoa(e.HPAfter),
			"attacker": e.AttackerID,
		}
	case event.CityCoreDestroyedEvent:
		return e.Kind(), map[string]string{
			"node_id":           e.NodeID,
			"conqueror_faction": e.ConquerorFaction,
		}
	case event.ConflictResolvedEvent:
		return e.Kind(), map[string]string{
			"unit_a_id":     e.UnitAID,
			"unit_b_id":     e.UnitBID,
			"location_q":    strconv.Itoa(e.Location.Q),
			"location_r":    strconv.Itoa(e.Location.R),
			"conflict_type": e.ConflictType,
		}
	case event.RoadDestroyedEvent:
		return e.Kind(), map[string]string{
			"from_node":    e.FromNode,
			"to_node":      e.ToNode,
			"destroyer_id": e.DestroyerID,
		}
	case event.BuildingDamagedEvent:
		return e.Kind(), map[string]string{
			"node_id":  e.NodeID,
			"damage":   strconv.Itoa(e.Damage),
			"hp_after": strconv.Itoa(e.HPAfter),
		}
	case event.CityCapturedEvent:
		data := map[string]string{
			"node_id":      e.NodeID,
			"city_id":      e.CityID,
			"old_owner_id": e.OldOwnerID,
			"new_owner_id": e.NewOwnerID,
		}
		if e.OnlineOnTurn > 0 {
			data["online_on_turn"] = strconv.Itoa(e.OnlineOnTurn)
		}
		return e.Kind(), data
	case event.FacilityTakeoverProgressedEvent:
		data := map[string]string{
			"node_id":              e.NodeID,
			"controller_player_id": e.ControllerPlayerID,
			"progress":             strconv.Itoa(e.Progress),
			"required":             strconv.Itoa(e.Required),
			"status":               e.Status,
			"reason":               e.Reason,
		}
		if reasonMessage := gamefeedback.RuntimeReasonMessage(e.Reason); reasonMessage != "" {
			data["reason_message"] = reasonMessage
		}
		return e.Kind(), data
	case event.FacilityTakeoverCompletedEvent:
		data := map[string]string{
			"node_id":         e.NodeID,
			"new_owner_id":    e.NewOwnerID,
			"service_city_id": e.ServiceCityID,
		}
		if e.OnlineOnTurn > 0 {
			data["online_on_turn"] = strconv.Itoa(e.OnlineOnTurn)
		}
		return e.Kind(), data
	case event.BuildingRuinedEvent:
		return e.Kind(), map[string]string{
			"node_id":      e.NodeID,
			"new_owner_id": e.NewOwnerID,
			"reason":       e.Reason,
		}
	}

	typeName := strings.TrimSpace(evt.Kind())
	if typeName == "" {
		if eventType := reflect.TypeOf(evt); eventType != nil {
			typeName = eventType.Name()
		}
	}
	if typeName == "" {
		typeName = "unknown"
	}
	return typeName, map[string]string{
		"detail": evt.String(),
	}
}

func shouldSkipProjectedEvent(evt event.Event) bool {
	if evt == nil {
		return true
	}
	switch evt.(type) {
	case event.RecipeSelectionChangedEvent:
		return true
	default:
		return false
	}
}

func resolveBuiltBuildingHP(buildingType string) int {
	const fallback = 100

	catalog := staticdata.Default()
	if catalog == nil {
		return fallback
	}
	if cfg, ok := catalog.GetBuilding(strings.TrimSpace(buildingType)); ok && cfg.MaxHP > 0 {
		return cfg.MaxHP
	}
	if strings.EqualFold(strings.TrimSpace(buildingType), "city_core") {
		if hp := catalog.Rules().CityCoreMaxHP; hp > 0 {
			return hp
		}
	}
	return fallback
}

func resourceBagData(bag domain.ResourceBag) map[string]string {
	if len(bag) == 0 {
		return nil
	}
	data := make(map[string]string, len(bag))
	for _, key := range bag.Keys() {
		data["resource_"+string(key)] = strconv.Itoa(bag.Get(key))
	}
	return data
}
