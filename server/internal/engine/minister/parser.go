// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现部长引擎的输出解析逻辑。

package minister

import (
	"encoding/json"
	"log/slog"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/event"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
)

type MinisterOutput struct {
	Report   string
	Metrics  []*pb.MetricItem
	Actions  []MinisterActionItem
	ActionID string
}

type MinisterActionItem struct {
	Type   string
	Params map[string]any
}

func ParseMinisterResponse(response string) (*MinisterOutput, error) {
	var raw struct {
		Report  string `json:"report"`
		Metrics []struct {
			Label      string `json:"label"`
			Value      string `json:"value"`
			Trend      string `json:"trend"`
			Confidence string `json:"confidence"`
			IsDelayed  bool   `json:"is_delayed"`
		} `json:"metrics"`
		Actions []struct {
			Type   string         `json:"type"`
			Params map[string]any `json:"params"`
		} `json:"actions"`
		ActionID string `json:"action_id"`
	}
	if err := json.Unmarshal([]byte(response), &raw); err != nil {
		return nil, err
	}

	out := &MinisterOutput{Report: raw.Report, ActionID: raw.ActionID}
	out.Metrics = make([]*pb.MetricItem, 0, len(raw.Metrics))
	for _, m := range raw.Metrics {
		out.Metrics = append(out.Metrics, &pb.MetricItem{
			Label:      m.Label,
			Value:      m.Value,
			Trend:      m.Trend,
			Confidence: m.Confidence,
			IsDelayed:  m.IsDelayed,
		})
	}
	out.Actions = make([]MinisterActionItem, 0, len(raw.Actions))
	for _, a := range raw.Actions {
		out.Actions = append(out.Actions, MinisterActionItem{Type: a.Type, Params: a.Params})
	}
	return out, nil
}

type ActionRoom interface {
	State() *domain.GameState
}

func ExecuteActions(actions []MinisterActionItem, room ActionRoom, playerID string) []event.Event {
	state := room.State()
	events := make([]event.Event, 0)
	for _, action := range actions {
		switch action.Type {
		case "build":
			nodeID, _ := asString(action.Params["node_id"])
			buildingType, _ := asString(action.Params["building_type"])
			if nodeID == "" || buildingType == "" {
				continue
			}
			state.TurnRuntime.Planning.MinisterBuilds = append(state.TurnRuntime.Planning.MinisterBuilds, domain.BuildOrder{PlayerID: playerID, NodeID: nodeID, BuildingType: buildingType})
		case "repair_road":
			// 道路当前仍未接入 Chunk 3 统一预算与 map action 结算，
			// 这里禁止部长直接落图，避免绕过点数账本。
			continue
		case "move_units":
			unitID, _ := asString(action.Params["unit_id"])
			targetNode, _ := asString(action.Params["target_node"])
			if unitID == "" || targetNode == "" {
				continue
			}
			nodeEntry, ok := state.GetNode(targetNode)
			if !ok {
				continue
			}
			p := ecs.PositionC.Get(nodeEntry)
			pos := domain.Position{X: p.X, Y: p.Y}
			state.TurnRuntime.Planning.MinisterMoves = append(state.TurnRuntime.Planning.MinisterMoves, domain.MoveOrder{PlayerID: playerID, UnitID: unitID, Target: pos})
		case "redirect_flow":
			// redirect_flow 暂时只记录，不直接修改持久配置。
		default:
			slog.Warn("unknown minister action", "type", action.Type)
		}
	}
	return events
}

func asString(v any) (string, bool) {
	s, ok := v.(string)
	return s, ok
}

func asInt(v any) (int, bool) {
	switch t := v.(type) {
	case float64:
		return int(t), true
	case int:
		return t, true
	default:
		return 0, false
	}
}
