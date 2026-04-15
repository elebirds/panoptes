// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现回合结算报告模块的结算报告映射逻辑。

package report

import (
	"reflect"
	"strconv"
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/event"
	gamequery "github.com/elebirds/panoptes/internal/game/query"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
)

func BuildTurnSettlement(
	state *domain.GameState,
	playerID string,
	turn int32,
	phase string,
	nextPhase string,
	unitEvents []event.Event,
	mapEvents []*pb.TurnEvent,
	economyEvents []event.Event,
) *pb.MsgTurnSettlement {
	msg := &pb.MsgTurnSettlement{
		Sections:  SettlementSections(unitEvents, mapEvents, economyEvents),
		Turn:      turn,
		Phase:     phase,
		NextPhase: nextPhase,
	}
	if state == nil {
		return msg
	}

	msg.Nodes = gamequery.BuildNodeViews(state, playerID)
	msg.Units = gamequery.BuildUnitViews(state)
	msg.MyPlayerAfter = gamequery.BuildPlayerView(state, playerID)
	return msg
}

func SettlementSections(unitEvents []event.Event, mapEvents []*pb.TurnEvent, economyEvents []event.Event) []*pb.SettlementSection {
	sections := make([]*pb.SettlementSection, 0, 3)
	if events := TurnEvents(unitEvents); len(events) > 0 {
		sections = append(sections, &pb.SettlementSection{Section: "unit", Events: events})
	}
	if len(mapEvents) > 0 {
		sections = append(sections, &pb.SettlementSection{
			Section: "map",
			Events:  append([]*pb.TurnEvent(nil), mapEvents...),
		})
	}
	if events := TurnEvents(economyEvents); len(events) > 0 {
		sections = append(sections, &pb.SettlementSection{Section: "economy", Events: events})
	}
	return sections
}

func TurnEvents(events []event.Event) []*pb.TurnEvent {
	out := make([]*pb.TurnEvent, 0, len(events))
	for _, evt := range events {
		out = append(out, TurnEventFromEvent(evt))
	}
	return out
}

func TurnEventFromEvent(evt event.Event) *pb.TurnEvent {
	if evt == nil {
		return &pb.TurnEvent{Type: "unknown", Data: map[string]string{}}
	}

	switch e := evt.(type) {
	case event.BuildingBuiltEvent:
		return &pb.TurnEvent{
			Type: e.Kind(),
			Data: map[string]string{
				"node_id":       strings.TrimSpace(e.NodeID),
				"building_type": strings.TrimSpace(e.BuildingType),
				"owner":         strings.TrimSpace(e.Owner),
				"city_id":       strings.TrimSpace(e.CityID),
				"building_hp":   strconv.Itoa(resolveBuiltBuildingHP(e.BuildingType)),
			},
		}
	case event.ResourceProducedEvent:
		return &pb.TurnEvent{
			Type: e.Kind(),
			Data: map[string]string{
				"node_id":       strings.TrimSpace(e.NodeID),
				"resource_type": strings.TrimSpace(e.ResourceType),
				"amount":        strconv.Itoa(e.Amount),
				"owner":         strings.TrimSpace(e.Owner),
				"city_id":       strings.TrimSpace(e.CityID),
			},
		}
	case event.ResourceFlowedEvent:
		data := map[string]string{
			"from_node_id": strings.TrimSpace(e.FromNodeID),
			"to_node_id":   strings.TrimSpace(e.ToNodeID),
		}
		for key, amount := range resourceBagData(e.Resources) {
			data[key] = amount
		}
		return &pb.TurnEvent{Type: e.Kind(), Data: data}
	case event.RoadBuiltEvent:
		return &pb.TurnEvent{
			Type: e.Kind(),
			Data: map[string]string{
				"from_node": strings.TrimSpace(e.FromNode),
				"to_node":   strings.TrimSpace(e.ToNode),
				"owner":     strings.TrimSpace(e.Owner),
				"cost":      strconv.Itoa(e.Cost),
			},
		}
	case event.UnitProducedEvent:
		return &pb.TurnEvent{
			Type: e.Kind(),
			Data: map[string]string{
				"node_id":   strings.TrimSpace(e.NodeID),
				"unit_type": strings.TrimSpace(e.UnitType),
				"faction":   strings.TrimSpace(e.Faction),
				"city_id":   strings.TrimSpace(e.CityID),
				"count":     strconv.Itoa(e.Count),
			},
		}
	case event.IndustryOutputRefreshedEvent:
		return &pb.TurnEvent{
			Type: e.Kind(),
			Data: map[string]string{
				"player_id": strings.TrimSpace(e.PlayerID),
				"amount":    strconv.Itoa(e.Amount),
			},
		}
	case event.UpkeepPaidEvent:
		return &pb.TurnEvent{
			Type: e.Kind(),
			Data: map[string]string{
				"player_id":     strings.TrimSpace(e.PlayerID),
				"food_consumed": strconv.Itoa(e.FoodConsumed),
			},
		}
	case event.UnitStarvingEvent:
		return &pb.TurnEvent{
			Type: e.Kind(),
			Data: map[string]string{
				"unit_id":         strings.TrimSpace(e.UnitID),
				"damage_per_turn": strconv.Itoa(e.DamagePerTurn),
			},
		}
	case event.BuildingDeactivatedEvent:
		return &pb.TurnEvent{
			Type: e.Kind(),
			Data: map[string]string{
				"node_id": strings.TrimSpace(e.NodeID),
				"reason":  strings.TrimSpace(e.Reason),
			},
		}
	case event.TechnologyUnlockedEvent:
		return &pb.TurnEvent{
			Type: e.Kind(),
			Data: map[string]string{
				"player_id":     strings.TrimSpace(e.PlayerID),
				"technology_id": strings.TrimSpace(e.TechnologyID),
			},
		}
	case event.ResearchProgressAppliedEvent:
		return &pb.TurnEvent{
			Type: e.Kind(),
			Data: map[string]string{
				"player_id": strings.TrimSpace(e.PlayerID),
				"amount":    strconv.Itoa(e.Amount),
			},
		}
	case event.TechnologyGrantAppliedEvent:
		return &pb.TurnEvent{
			Type: e.Kind(),
			Data: map[string]string{
				"player_id":     strings.TrimSpace(e.PlayerID),
				"technology_id": strings.TrimSpace(e.SourceTech),
			},
		}
	case event.RecipeSelectionChangedEvent:
		return &pb.TurnEvent{
			Type: e.Kind(),
			Data: map[string]string{
				"node_id":   strings.TrimSpace(e.NodeID),
				"recipe_id": strings.TrimSpace(e.RecipeID),
			},
		}
	case event.RecipeProgressedEvent:
		return &pb.TurnEvent{
			Type: e.Kind(),
			Data: map[string]string{
				"node_id":        strings.TrimSpace(e.NodeID),
				"progress_turns": strconv.Itoa(e.ProgressTurns),
			},
		}
	case event.RecipeDelayedEvent:
		return &pb.TurnEvent{
			Type: e.Kind(),
			Data: map[string]string{
				"node_id":     strings.TrimSpace(e.NodeID),
				"delay_turns": strconv.Itoa(e.DelayTurns),
				"reason":      strings.TrimSpace(e.Reason),
			},
		}
	case event.RecipeCompletedEvent:
		return &pb.TurnEvent{
			Type: e.Kind(),
			Data: map[string]string{
				"node_id": strings.TrimSpace(e.NodeID),
				"owner":   strings.TrimSpace(e.Owner),
			},
		}
	case event.MinisterActedEvent:
		return &pb.TurnEvent{
			Type: e.Kind(),
			Data: map[string]string{
				"player_id":     strings.TrimSpace(e.PlayerID),
				"minister_role": strings.TrimSpace(e.MinisterRole),
				"action_id":     strings.TrimSpace(e.ActionID),
				"report":        strings.TrimSpace(e.Report),
			},
		}
	case event.PolicyChangedEvent:
		return &pb.TurnEvent{
			Type: e.Kind(),
			Data: map[string]string{
				"player_id":              strings.TrimSpace(e.PlayerID),
				"old_national_policy_id": strings.TrimSpace(e.OldPolicy),
				"new_national_policy_id": strings.TrimSpace(e.NewPolicy),
			},
		}
	case event.TokenUsedEvent:
		return &pb.TurnEvent{
			Type: e.Kind(),
			Data: map[string]string{
				"player_id":   strings.TrimSpace(e.PlayerID),
				"action":      strings.TrimSpace(e.Action),
				"tokens_left": strconv.Itoa(e.TokensLeft),
			},
		}
	case event.UnitMovedEvent:
		return &pb.TurnEvent{Type: e.Kind(), Data: map[string]string{
			"unit_id": e.UnitID,
			"from_x":  strconv.Itoa(e.From.X),
			"from_y":  strconv.Itoa(e.From.Y),
			"to_x":    strconv.Itoa(e.To.X),
			"to_y":    strconv.Itoa(e.To.Y),
		}}
	case event.UnitDamagedEvent:
		return &pb.TurnEvent{Type: e.Kind(), Data: map[string]string{
			"unit_id":  e.UnitID,
			"damage":   strconv.Itoa(e.Damage),
			"hp_after": strconv.Itoa(e.HPAfter),
			"source":   e.Source,
		}}
	case event.UnitDiedEvent:
		return &pb.TurnEvent{Type: e.Kind(), Data: map[string]string{
			"unit_id":   e.UnitID,
			"killer_id": e.KillerID,
			"pos_x":     strconv.Itoa(e.Pos.X),
			"pos_y":     strconv.Itoa(e.Pos.Y),
		}}
	case event.CityCoreDamagedEvent:
		return &pb.TurnEvent{Type: e.Kind(), Data: map[string]string{
			"node_id":  e.NodeID,
			"damage":   strconv.Itoa(e.Damage),
			"hp_after": strconv.Itoa(e.HPAfter),
			"attacker": e.AttackerID,
		}}
	case event.CityCoreDestroyedEvent:
		return &pb.TurnEvent{Type: e.Kind(), Data: map[string]string{
			"node_id":           e.NodeID,
			"conqueror_faction": e.ConquerorFaction,
		}}
	case event.ConflictResolvedEvent:
		return &pb.TurnEvent{Type: e.Kind(), Data: map[string]string{
			"unit_a_id":     e.UnitAID,
			"unit_b_id":     e.UnitBID,
			"location_x":    strconv.Itoa(e.Location.X),
			"location_y":    strconv.Itoa(e.Location.Y),
			"conflict_type": e.ConflictType,
		}}
	case event.RoadDestroyedEvent:
		return &pb.TurnEvent{Type: e.Kind(), Data: map[string]string{
			"from_node":    e.FromNode,
			"to_node":      e.ToNode,
			"destroyer_id": e.DestroyerID,
		}}
	case event.BuildingDamagedEvent:
		return &pb.TurnEvent{Type: e.Kind(), Data: map[string]string{
			"node_id":  e.NodeID,
			"damage":   strconv.Itoa(e.Damage),
			"hp_after": strconv.Itoa(e.HPAfter),
		}}
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
	return &pb.TurnEvent{
		Type: typeName,
		Data: map[string]string{
			"detail": evt.String(),
		},
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
