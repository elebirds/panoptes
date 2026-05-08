// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载 planning 命令处理、端口拆分与响应投递相关逻辑。

package planning

import (
	"github.com/elebirds/panoptes/internal/domain"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
)

func (s *Service) handleInstitutionLoadout(delivery commandDelivery, room Session, playerID string, playerState *domain.PlayerState, institutionIDs []string) (handleIntentResult, error) {
	if playerState == nil {
		delivery.send(&pb.MsgSetInstitutionLoadoutResult{Success: false, ErrorCode: "invalid_request"})
		return rejectedHandleIntentResult("invalid_request"), nil
	}
	state := room.State()
	normalized, errCode := ValidateInstitutionLoadout(state, playerID, playerState, institutionIDs)
	if errCode != "" {
		delivery.send(&pb.MsgSetInstitutionLoadoutResult{Success: false, InstitutionIds: normalized, ErrorCode: errCode})
		return rejectedHandleIntentResult(errCode), nil
	}
	room.SetInstitutionLoadout(playerID, normalized)
	delivery.sendWithSnapshot(&pb.MsgSetInstitutionLoadoutResult{Success: true, InstitutionIds: normalized})
	return acceptedHandleIntentResult(), nil
}

func ValidateInstitutionLoadout(state *domain.GameState, playerID string, playerState *domain.PlayerState, institutionIDs []string) ([]string, string) {
	if playerState == nil {
		return nil, "invalid_request"
	}
	normalized := domain.NormalizeInstitutionIDList(institutionIDs)
	categories := make(map[string]string, len(normalized))
	for _, institutionID := range normalized {
		institution, ok := staticdata.Default().GetInstitution(institutionID)
		if !ok {
			return normalized, "invalid_target"
		}
		if errCode := validatePrerequisites(state, playerID, institution.Prerequisites); errCode != "" {
			return normalized, errCode
		}
		if !playerState.Institutions.HasCandidate(institutionID) {
			return normalized, "invalid_directive"
		}
		if existing := categories[institution.Category]; existing != "" && existing != institutionID {
			return normalized, "invalid_directive"
		}
		categories[institution.Category] = institutionID
	}
	return normalized, ""
}
