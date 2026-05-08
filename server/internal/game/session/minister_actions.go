// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-05-08 00:00:00 +0800
// Description: Applies minister LLM actions through the normal planning rules.

package session

import (
	"fmt"
	"log/slog"
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/engine/economy"
	ministerengine "github.com/elebirds/panoptes/internal/engine/minister"
	gameorders "github.com/elebirds/panoptes/internal/game/orders"
)

func (r *Runtime) ApplyMinisterActions(playerID string, actions []ministerengine.MinisterActionItem) error {
	if r == nil || r.state == nil || len(actions) == 0 {
		return nil
	}
	playerID = strings.TrimSpace(playerID)
	if playerID == "" {
		return nil
	}

	for _, action := range actions {
		r.applyMinisterAction(playerID, action)
	}
	return nil
}

func (r *Runtime) applyMinisterAction(playerID string, action ministerengine.MinisterActionItem) {
	switch strings.TrimSpace(action.Type) {
	case "build":
		r.applyMinisterBuildAction(playerID, action.Params)
	case "move_units":
		r.applyMinisterMoveAction(playerID, action.Params)
	default:
		slog.Warn("unsupported minister action type", "player_id", playerID, "type", strings.TrimSpace(action.Type))
	}
}

func (r *Runtime) applyMinisterBuildAction(playerID string, params map[string]any) {
	nodeID := ministerActionStringParam(params, "node_id", "target_node", "target_node_id")
	buildingType := ministerActionStringParam(params, "building_type", "building_type_id")
	cityID := ministerActionStringParam(params, "city_id")
	if nodeID == "" || buildingType == "" {
		return
	}

	validation := economy.ValidateBuildOrder(r.state, playerID, nodeID, buildingType, cityID)
	if !validation.OK {
		slog.Warn("minister build action rejected", "player_id", playerID, "node_id", nodeID, "building_type", buildingType, "city_id", cityID, "error_code", validation.ErrorCode)
		return
	}

	r.state.TurnRuntime.Planning.UpsertBuildOrder(domain.BuildOrder{
		PlayerID:     playerID,
		NodeID:       nodeID,
		BuildingType: buildingType,
		CityID:       cityID,
	})
}

func (r *Runtime) applyMinisterMoveAction(playerID string, params map[string]any) {
	unitID := ministerActionStringParam(params, "unit_id")
	targetNodeID := ministerActionStringParam(params, "target_node", "target_node_id", "destination_node_id")
	if unitID == "" || targetNodeID == "" {
		return
	}

	order := gameorders.UnitOrder{
		PlayerID:     playerID,
		UnitID:       unitID,
		Action:       gameorders.ActionMove,
		TargetNodeID: targetNodeID,
	}
	if errCode := gameorders.ValidatePlanningUnitOrder(r.state, playerID, order); errCode != "" {
		slog.Warn("minister move action rejected", "player_id", playerID, "unit_id", unitID, "target_node_id", targetNodeID, "error_code", errCode)
		return
	}

	gameorders.ApplyPlanningUnitOrder(r.state, order, gameorders.RoutePreviewCallbacks{})
}

func ministerActionStringParam(params map[string]any, keys ...string) string {
	for _, key := range keys {
		value, ok := params[key]
		if !ok || value == nil {
			continue
		}
		switch typed := value.(type) {
		case string:
			return strings.TrimSpace(typed)
		case fmt.Stringer:
			return strings.TrimSpace(typed.String())
		default:
			text := strings.TrimSpace(fmt.Sprint(value))
			if text != "" && text != "<nil>" {
				return text
			}
		}
	}
	return ""
}
