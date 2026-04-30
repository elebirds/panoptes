// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载 planning 命令处理、端口拆分与响应投递相关逻辑。

package planning

import (
	"github.com/elebirds/panoptes/internal/domain"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
)

func (s *Service) handleInstitutionLoadout(delivery commandDelivery, room Session, playerID string, playerState *domain.PlayerState, policyIDs []string) (handleIntentResult, error) {
	if playerState == nil {
		delivery.send(&pb.MsgSetInstitutionLoadoutResult{Success: false, ErrorCode: "invalid_request"})
		return rejectedHandleIntentResult("invalid_request"), nil
	}
	state := room.State()
	normalized := domain.NormalizePolicyIDList(policyIDs)
	if len(normalized) > playerState.Institutions.SlotCount {
		delivery.send(&pb.MsgSetInstitutionLoadoutResult{Success: false, PolicyIds: normalized, ErrorCode: "invalid_directive"})
		return rejectedHandleIntentResult("invalid_directive"), nil
	}
	for _, policyID := range normalized {
		if _, errCode := validatePolicySelection(state, playerID, policyID, "institutional"); errCode != "" {
			delivery.send(&pb.MsgSetInstitutionLoadoutResult{Success: false, PolicyIds: normalized, ErrorCode: errCode})
			return rejectedHandleIntentResult(errCode), nil
		}
		if !playerState.Institutions.HasCandidate(policyID) {
			delivery.send(&pb.MsgSetInstitutionLoadoutResult{Success: false, PolicyIds: normalized, ErrorCode: "invalid_directive"})
			return rejectedHandleIntentResult("invalid_directive"), nil
		}
	}
	room.SetInstitutionLoadout(playerID, normalized)
	delivery.sendWithSnapshot(&pb.MsgSetInstitutionLoadoutResult{Success: true, PolicyIds: normalized})
	return acceptedHandleIntentResult(), nil
}
