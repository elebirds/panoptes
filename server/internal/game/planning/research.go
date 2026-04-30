// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载 planning 命令处理、端口拆分与响应投递相关逻辑。

package planning

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/engine/economy"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
)

func (s *Service) handleResearchRequest(delivery commandDelivery, room Session, playerID string, playerState *domain.PlayerState, technologyID string) (handleIntentResult, error) {
	if playerState == nil || technologyID == "" {
		delivery.send(&pb.MsgResearchResult{Success: false, TechnologyId: technologyID, ErrorCode: "invalid_request"})
		return rejectedHandleIntentResult("invalid_request"), nil
	}

	state := room.State()
	validation := economy.ValidateResearchTarget(state, playerID, technologyID)
	if !validation.OK {
		delivery.send(&pb.MsgResearchResult{Success: false, TechnologyId: technologyID, ErrorCode: validation.ErrorCode})
		return rejectedHandleIntentResult(validation.ErrorCode), nil
	}

	state.TurnRuntime.Planning.SetPendingResearchTarget(playerID, technologyID)
	transitions := reconcileMinisterDraftBindings(state, playerID, "")
	delivery.sendWithSnapshot(&pb.MsgResearchResult{Success: true, TechnologyId: technologyID})
	recordMinisterDraftTransitions(room, playerID, state.Turn, transitions)
	return acceptedHandleIntentResult(), nil
}
