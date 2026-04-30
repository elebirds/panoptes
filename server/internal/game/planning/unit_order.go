// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载 planning 命令处理、端口拆分与响应投递相关逻辑。

package planning

import (
	"encoding/json"
	"strings"

	gameorders "github.com/elebirds/panoptes/internal/game/orders"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
)

func (s *Service) handleIssueUnitOrder(delivery commandDelivery, room Session, playerID string, msg *pb.MsgIssueUnitOrder) (handleIntentResult, error) {
	order := gameorders.UnitOrder{
		PlayerID:        playerID,
		UnitID:          strings.TrimSpace(msg.GetUnitId()),
		Action:          gameorders.UnitAction(strings.TrimSpace(msg.GetAction())),
		TargetNodeID:    strings.TrimSpace(msg.GetTargetNodeId()),
		TargetUnitID:    strings.TrimSpace(msg.GetTargetUnitId()),
		SecondaryNodeID: strings.TrimSpace(msg.GetSecondaryNodeId()),
		Params:          cloneParams(msg.GetParams()),
	}
	if errCode := gameorders.ValidatePlanningUnitOrder(room.State(), playerID, order); errCode != "" {
		delivery.send(&pb.MsgIssueUnitOrderResult{
			Success:      false,
			UnitId:       order.UnitID,
			Action:       string(order.Action),
			TargetNodeId: order.TargetNodeID,
			TargetUnitId: order.TargetUnitID,
			ErrorCode:    errCode,
		})
		return rejectedHandleIntentResult(errCode), nil
	}

	room.SetUnitOrder(order)
	delivery.sendWithSnapshot(&pb.MsgIssueUnitOrderResult{
		Success:      true,
		UnitId:       order.UnitID,
		Action:       string(order.Action),
		TargetNodeId: order.TargetNodeID,
		TargetUnitId: order.TargetUnitID,
	})
	return acceptedHandleIntentResult(), nil
}

func (s *Service) handleCancelUnitOrder(delivery commandDelivery, room Session, playerID string, unitID string) (handleIntentResult, error) {
	room.CancelUnitOrder(playerID, strings.TrimSpace(unitID))
	delivery.snapshot()
	return acceptedHandleIntentResult(), nil
}

func cloneParams(src map[string]string) map[string]string {
	if len(src) == 0 {
		return nil
	}
	dst := make(map[string]string, len(src))
	for k, v := range src {
		dst[k] = v
	}
	return dst
}

type expandPayload struct {
	UnitID       string `json:"unit_id"`
	CenterNodeID string `json:"center_node_id"`
}

func MarshalExpandParams(unitID, centerNodeID string) ([]byte, error) {
	return json.Marshal(expandPayload{UnitID: unitID, CenterNodeID: centerNodeID})
}
