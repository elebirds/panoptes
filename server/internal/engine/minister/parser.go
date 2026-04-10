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
		Report   string `json:"report"`
		Metrics  []struct {
			Label      string `json:"label"`
			Value      string `json:"value"`
			Trend      string `json:"trend"`
			Confidence string `json:"confidence"`
			IsDelayed  bool   `json:"is_delayed"`
		} `json:"metrics"`
		Actions  []struct {
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
			state.MinisterBuildOrders = append(state.MinisterBuildOrders, domain.BuildOrder{PlayerID: playerID, NodeID: nodeID, BuildingType: buildingType})
		case "repair_road":
			fromNode, _ := asString(action.Params["from_node"])
			toNode, _ := asString(action.Params["to_node"])
			cost, _ := asInt(action.Params["cost"])
			events = append(events, event.RoadBuiltEvent{FromNode: fromNode, ToNode: toNode, Owner: playerID, Cost: cost})
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
			state.MinisterMoveOrders = append(state.MinisterMoveOrders, domain.MoveOrder{PlayerID: playerID, UnitID: unitID, Target: pos})
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
