// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载 planning 命令处理、端口拆分与响应投递相关逻辑。

package planning

import (
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
)

func (s *Service) handleSetPolicy(delivery commandDelivery, room Session, playerID string, policyID string) (handleIntentResult, error) {
	if policyID == "" {
		delivery.send(&pb.MsgSetPolicyResult{Success: false, NationalPolicyId: policyID, ErrorCode: "invalid_request"})
		return rejectedHandleIntentResult("invalid_request"), nil
	}

	if _, errCode := validatePolicySelection(room.State(), playerID, policyID, "national"); errCode != "" {
		delivery.send(&pb.MsgSetPolicyResult{Success: false, NationalPolicyId: policyID, ErrorCode: errCode})
		return rejectedHandleIntentResult(errCode), nil
	}

	room.State().TurnRuntime.Planning.SetPendingPolicy(playerID, domain.Policy(policyID))
	transitions := reconcileMinisterDraftBindings(room.State(), playerID, "")
	delivery.sendWithSnapshot(&pb.MsgSetPolicyResult{Success: true, NationalPolicyId: policyID})
	recordMinisterDraftTransitions(room, playerID, room.State().Turn, transitions)
	return acceptedHandleIntentResult(), nil
}

func validatePolicySelection(state *domain.GameState, playerID string, policyID string, requiredLayer string) (staticdata.PolicyDefinition, string) {
	return ValidatePolicySelection(state, playerID, policyID, requiredLayer)
}

func ValidatePolicySelection(state *domain.GameState, playerID string, policyID string, requiredLayer string) (staticdata.PolicyDefinition, string) {
	policy, ok := staticdata.Default().GetPolicy(policyID)
	if !ok {
		return staticdata.PolicyDefinition{}, "invalid_target"
	}
	if !strings.EqualFold(policy.Layer, requiredLayer) {
		return staticdata.PolicyDefinition{}, "invalid_directive"
	}
	if errCode := validatePrerequisites(state, playerID, policy.Prerequisites); errCode != "" {
		return staticdata.PolicyDefinition{}, errCode
	}
	return policy, ""
}

func validatePrerequisites(state *domain.GameState, playerID string, prerequisites []staticdata.Prerequisite) string {
	if state == nil {
		return "invalid_target"
	}
	for _, prereq := range prerequisites {
		switch prereq.Type {
		case "technology_unlocked":
			if !state.HasTechnologyUnlocked(playerID, prereq.TargetID) {
				return "invalid_directive"
			}
		case "policy_active":
			if !state.IsPolicyActive(playerID, prereq.TargetID) {
				return "invalid_directive"
			}
		}
	}
	return ""
}
