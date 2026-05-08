// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载 planning 命令处理、端口拆分与响应投递相关逻辑。

package planning

import (
	"fmt"
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/engine/economy"
	ministerengine "github.com/elebirds/panoptes/internal/engine/minister"
	ministerskills "github.com/elebirds/panoptes/internal/engine/minister/skills"
	gameorders "github.com/elebirds/panoptes/internal/game/orders"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/ministerroles"
	transportproblem "github.com/elebirds/panoptes/internal/transport/problem"
	"google.golang.org/protobuf/proto"
)

type ministerMemoryRecorder interface {
	RecordMinisterMemory(playerID string, role string, entry ministerengine.MemoryEntry)
}

type ministerDraftTransition struct {
	Draft      domain.MinisterDraft
	FromStatus domain.MinisterDraftStatus
	ToStatus   domain.MinisterDraftStatus
}

func (s *Service) handleMinisterDirective(delivery commandDelivery, room Session, playerID string, intent SetMinisterDirectiveIntent) (handleIntentResult, error) {
	state := room.State()
	if state == nil {
		return rejectedHandleIntentResult("invalid_directive"), transportproblem.New("invalid_directive", "state is nil")
	}
	switch intent.DirectiveType {
	case "mandate_override":
		playerState := state.Players[playerID]
		return s.handleMandateAction(delivery, room, playerID, playerState, MandateActionOverride)
	case "direct_command":
		playerState := state.Players[playerID]
		return s.handleMandateAction(delivery, room, playerID, playerState, MandateActionDirectCommand)
	case "accept_role":
		return s.acceptMinisterRoleDrafts(delivery, room, playerID, intent.MinisterRole)
	case "reject_role":
		return s.rejectMinisterRoleDrafts(delivery, room, playerID, intent.MinisterRole)
	case "activate_skill":
		if _, ok := ministerskills.Activate(state, playerID, intent.MinisterRole, intent.SkillCardID); !ok {
			return rejectedHandleIntentResult("invalid_directive"), transportproblem.New("invalid_directive", "minister skill activation failed")
		}
		delivery.snapshot()
		return acceptedHandleIntentResult(), nil
	case "refresh_candidates":
		state.RefreshMinisterCandidatesForPlayer(playerID)
		delivery.snapshot()
		return acceptedHandleIntentResult(), nil
	case "fire":
		if !s.fireMinister(delivery, room, playerID, intent.MinisterRole) {
			return rejectedHandleIntentResult("invalid_directive"), transportproblem.New("invalid_directive", "minister not found")
		}
		state.RefreshMinisterCandidatesForPlayer(playerID)
		delivery.snapshot()
		return acceptedHandleIntentResult(), nil
	case "hire":
		if !s.hireMinister(delivery, room, playerID, intent.MinisterRole, intent.CandidateID) {
			return rejectedHandleIntentResult("invalid_directive"), transportproblem.New("invalid_directive", "minister candidate not found")
		}
		state.RefreshMinisterCandidatesForPlayer(playerID)
		delivery.snapshot()
		return acceptedHandleIntentResult(), nil
	case "replace":
		if !s.replaceMinister(delivery, room, playerID, intent.MinisterRole, intent.CandidateID) {
			return rejectedHandleIntentResult("invalid_directive"), transportproblem.New("invalid_directive", "minister replacement failed")
		}
		state.RefreshMinisterCandidatesForPlayer(playerID)
		delivery.snapshot()
		return acceptedHandleIntentResult(), nil
	case "accept":
		draft, _, ok := state.TurnRuntime.Planning.FindMinisterDraft(playerID, intent.DraftID)
		if !ok || draft.MinisterRole != intent.MinisterRole || draft.Turn != state.Turn {
			return rejectedHandleIntentResult("invalid_directive"), transportproblem.New("invalid_directive", "minister draft not found")
		}
		if !draft.Available {
			return rejectedHandleIntentResult("invalid_directive"), transportproblem.New("invalid_directive", "minister draft is unavailable")
		}
		return s.acceptMinisterDraft(delivery, room, playerID, draft)
	case "reject":
		draft, idx, ok := state.TurnRuntime.Planning.FindMinisterDraft(playerID, intent.DraftID)
		if !ok || draft.MinisterRole != intent.MinisterRole || draft.Turn != state.Turn {
			return rejectedHandleIntentResult("invalid_directive"), transportproblem.New("invalid_directive", "minister draft not found")
		}
		before := draft.Status
		draft.Status = domain.MinisterDraftStatusRejected
		draft.Available = false
		state.TurnRuntime.Planning.ReplaceMinisterDraft(playerID, idx, draft)
		delivery.snapshot()
		recordMinisterDraftTransitions(room, playerID, state.Turn, []ministerDraftTransition{{
			Draft:      draft,
			FromStatus: before,
			ToStatus:   draft.Status,
		}})
		return acceptedHandleIntentResult(), nil
	default:
		return rejectedHandleIntentResult("invalid_directive"), transportproblem.New("invalid_directive", "unsupported minister directive type")
	}
}

func (s *Service) fireMinister(delivery commandDelivery, room Session, playerID string, role string) bool {
	state := room.State()
	if state == nil {
		return false
	}
	playerState := state.Players[playerID]
	if playerState == nil {
		return false
	}
	role = ministerroles.Canonical(role)
	if role == "" {
		return false
	}
	if _, ok := playerState.MinisterForRole(role); !ok {
		return false
	}
	playerState.ClearMinisterForRole(role)
	return true
}

func (s *Service) hireMinister(delivery commandDelivery, room Session, playerID string, role string, candidateID string) bool {
	state := room.State()
	if state == nil {
		return false
	}
	playerState := state.Players[playerID]
	if playerState == nil {
		return false
	}
	role = ministerroles.Canonical(role)
	candidateID = strings.TrimSpace(candidateID)
	if role == "" || candidateID == "" {
		return false
	}
	if _, ok := playerState.MinisterForRole(role); ok {
		return false
	}
	candidate, ok := playerState.MinisterCandidateForRole(role)
	if !ok || !strings.EqualFold(strings.TrimSpace(candidate.ID), candidateID) {
		return false
	}
	playerState.SetMinisterForRole(role, candidate)
	delete(playerState.MinisterCandidates, role)
	return true
}

func (s *Service) replaceMinister(delivery commandDelivery, room Session, playerID string, role string, candidateID string) bool {
	state := room.State()
	if state == nil {
		return false
	}
	playerState := state.Players[playerID]
	if playerState == nil {
		return false
	}
	role = ministerroles.Canonical(role)
	candidateID = strings.TrimSpace(candidateID)
	if role == "" || candidateID == "" {
		return false
	}
	candidate, ok := playerState.MinisterCandidateForRole(role)
	if !ok || !strings.EqualFold(strings.TrimSpace(candidate.ID), candidateID) {
		return false
	}
	playerState.SetMinisterForRole(role, candidate)
	delete(playerState.MinisterCandidates, role)
	return true
}

func (s *Service) acceptMinisterRoleDrafts(delivery commandDelivery, room Session, playerID string, role string) (handleIntentResult, error) {
	state := room.State()
	if state == nil {
		return rejectedHandleIntentResult("invalid_directive"), transportproblem.New("invalid_directive", "state is nil")
	}
	role = strings.TrimSpace(role)
	applied := 0
	for _, draft := range state.TurnRuntime.Planning.MinisterDraftsForPlayer(playerID) {
		if draft.Turn != state.Turn || !isMinisterDraftInteractive(draft) || strings.TrimSpace(draft.MinisterRole) != role {
			continue
		}
		result, err := s.acceptMinisterDraft(delivery, room, playerID, draft)
		if err != nil {
			return result, err
		}
		if result.accepted {
			applied++
		}
	}
	if applied == 0 {
		return rejectedHandleIntentResult("invalid_directive"), transportproblem.New("invalid_directive", "no available minister drafts")
	}
	return acceptedHandleIntentResult(), nil
}

func (s *Service) rejectMinisterRoleDrafts(delivery commandDelivery, room Session, playerID string, role string) (handleIntentResult, error) {
	state := room.State()
	if state == nil {
		return rejectedHandleIntentResult("invalid_directive"), transportproblem.New("invalid_directive", "state is nil")
	}
	role = strings.TrimSpace(role)
	drafts := state.TurnRuntime.Planning.MinisterDraftsForPlayer(playerID)
	transitions := make([]ministerDraftTransition, 0)
	changed := false
	for idx := range drafts {
		draft := drafts[idx]
		if draft.Turn != state.Turn || !isMinisterDraftInteractive(draft) || strings.TrimSpace(draft.MinisterRole) != role {
			continue
		}
		before := draft.Status
		draft.Status = domain.MinisterDraftStatusRejected
		draft.Available = false
		drafts[idx] = draft
		changed = true
		transitions = append(transitions, ministerDraftTransition{Draft: draft, FromStatus: before, ToStatus: draft.Status})
	}
	if !changed {
		return rejectedHandleIntentResult("invalid_directive"), transportproblem.New("invalid_directive", "no available minister drafts")
	}
	state.TurnRuntime.Planning.SetMinisterDrafts(playerID, drafts)
	delivery.snapshot()
	recordMinisterDraftTransitions(room, playerID, state.Turn, transitions)
	return acceptedHandleIntentResult(), nil
}

func isMinisterDraftInteractive(draft domain.MinisterDraft) bool {
	return draft.Available &&
		draft.Status != domain.MinisterDraftStatusAccepted &&
		draft.Status != domain.MinisterDraftStatusRejected
}

func (s *Service) acceptMinisterDraft(delivery commandDelivery, room Session, playerID string, draft domain.MinisterDraft) (handleIntentResult, error) {
	state := room.State()
	if state == nil {
		return rejectedHandleIntentResult("invalid_directive"), transportproblem.New("invalid_directive", "state is nil")
	}
	playerState := state.Players[playerID]
	if playerState == nil {
		return rejectedHandleIntentResult("invalid_directive"), transportproblem.New("invalid_directive", "player not found")
	}

	switch draft.Kind {
	case domain.MinisterDraftKindResearch:
		technologyID := strings.TrimSpace(draft.TargetID)
		validation := economy.ValidateResearchTarget(state, playerID, technologyID)
		if !validation.OK {
			delivery.send(&pb.MsgResearchResult{Success: false, TechnologyId: technologyID, ErrorCode: validation.ErrorCode})
			return rejectedHandleIntentResult(validation.ErrorCode), nil
		}
		state.TurnRuntime.Planning.SetPendingResearchTarget(playerID, technologyID)
		transitions := reconcileMinisterDraftBindings(state, playerID, strings.TrimSpace(draft.DraftID))
		delivery.sendWithSnapshot(&pb.MsgResearchResult{Success: true, TechnologyId: technologyID})
		recordMinisterDraftTransitions(room, playerID, state.Turn, transitions)
		return acceptedHandleIntentResult(), nil
	case domain.MinisterDraftKindPolicy:
		policyID := strings.TrimSpace(draft.TargetID)
		if _, errCode := validatePolicySelection(state, playerID, policyID, "national"); errCode != "" {
			delivery.send(&pb.MsgSetPolicyResult{Success: false, NationalPolicyId: policyID, ErrorCode: errCode})
			return rejectedHandleIntentResult(errCode), nil
		}
		state.TurnRuntime.Planning.SetPendingPolicy(playerID, domain.Policy(policyID))
		transitions := reconcileMinisterDraftBindings(state, playerID, strings.TrimSpace(draft.DraftID))
		delivery.sendWithSnapshot(&pb.MsgSetPolicyResult{Success: true, NationalPolicyId: policyID})
		recordMinisterDraftTransitions(room, playerID, state.Turn, transitions)
		return acceptedHandleIntentResult(), nil
	case domain.MinisterDraftKindInstitution:
		result, err := s.handleInstitutionLoadout(delivery, room, playerID, playerState, draft.InstitutionIDs)
		if err != nil || !result.accepted {
			return result, err
		}
		transitions := reconcileMinisterDraftBindings(state, playerID, strings.TrimSpace(draft.DraftID))
		recordMinisterDraftTransitions(room, playerID, state.Turn, transitions)
		return acceptedHandleIntentResult(), nil
	case domain.MinisterDraftKindBuild:
		eval := evaluateBuildCommand(room, playerID, playerState, draft.NodeID, draft.BuildingTypeID, draft.CityID)
		if !eval.OK {
			delivery.send(&pb.MsgBuildStructureResult{
				Success:         false,
				NodeId:          strings.TrimSpace(draft.NodeID),
				BuildingTypeId:  strings.TrimSpace(draft.BuildingTypeID),
				CityId:          strings.TrimSpace(draft.CityID),
				ErrorCode:       eval.ErrorCode,
				FeedbackMessage: eval.FeedbackMessage,
				FeedbackDetails: eval.FeedbackDetails,
			})
			return rejectedHandleIntentResult(eval.ErrorCode), nil
		}
		room.QueueBuildOrder(domain.BuildOrder{
			PlayerID:     playerID,
			NodeID:       strings.TrimSpace(draft.NodeID),
			BuildingType: strings.TrimSpace(draft.BuildingTypeID),
			CityID:       strings.TrimSpace(draft.CityID),
		})
		transitions := reconcileMinisterDraftBindings(state, playerID, strings.TrimSpace(draft.DraftID))
		delivery.sendWithSnapshot(&pb.MsgBuildStructureResult{
			Success:        true,
			NodeId:         strings.TrimSpace(draft.NodeID),
			BuildingTypeId: strings.TrimSpace(draft.BuildingTypeID),
			CityId:         strings.TrimSpace(draft.CityID),
		})
		recordMinisterDraftTransitions(room, playerID, state.Turn, transitions)
		return acceptedHandleIntentResult(), nil
	case domain.MinisterDraftKindRecipe:
		result, err := s.handleSetBuildingRecipe(delivery, room, playerID, draft.NodeID, draft.RecipeID)
		if err != nil || !result.accepted {
			return result, err
		}
		transitions := reconcileMinisterDraftBindings(state, playerID, strings.TrimSpace(draft.DraftID))
		recordMinisterDraftTransitions(room, playerID, state.Turn, transitions)
		return acceptedHandleIntentResult(), nil
	case domain.MinisterDraftKindOperation:
		if len(draft.OperationSteps) == 0 {
			return rejectedHandleIntentResult("invalid_directive"), transportproblem.New("invalid_directive", "minister operation has no commands")
		}
		if errCode, failure := validateMinisterOperationSteps(room, playerID, playerState, draft.OperationSteps); errCode != "" {
			delivery.send(failure)
			return rejectedHandleIntentResult(errCode), nil
		}
		results := make([]proto.Message, 0, len(draft.OperationSteps))
		for _, step := range draft.OperationSteps {
			if result := applyMinisterDraftStep(room, playerID, step); result != nil {
				results = append(results, result)
			}
		}
		transitions := reconcileMinisterDraftBindings(state, playerID, strings.TrimSpace(draft.DraftID))
		delivery.sendAllWithSnapshot(results...)
		recordMinisterDraftTransitions(room, playerID, state.Turn, transitions)
		return acceptedHandleIntentResult(), nil
	case domain.MinisterDraftKindUnitOrder:
		order := gameorders.UnitOrder{
			PlayerID:        playerID,
			UnitID:          strings.TrimSpace(draft.UnitID),
			Action:          gameorders.UnitAction(strings.TrimSpace(draft.Action)),
			TargetNodeID:    strings.TrimSpace(draft.TargetNodeID),
			TargetUnitID:    strings.TrimSpace(draft.TargetUnitID),
			SecondaryNodeID: strings.TrimSpace(draft.SecondaryNodeID),
			Params:          cloneParams(draft.Params),
		}
		if errCode := gameorders.ValidatePlanningUnitOrder(state, playerID, order); errCode != "" {
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
		transitions := reconcileMinisterDraftBindings(state, playerID, strings.TrimSpace(draft.DraftID))
		delivery.sendWithSnapshot(&pb.MsgIssueUnitOrderResult{
			Success:      true,
			UnitId:       order.UnitID,
			Action:       string(order.Action),
			TargetNodeId: order.TargetNodeID,
			TargetUnitId: order.TargetUnitID,
		})
		recordMinisterDraftTransitions(room, playerID, state.Turn, transitions)
		return acceptedHandleIntentResult(), nil
	default:
		return rejectedHandleIntentResult("invalid_directive"), transportproblem.New("invalid_directive", "unsupported minister draft kind")
	}
}

func validateMinisterOperationSteps(room Session, playerID string, playerState *domain.PlayerState, steps []domain.MinisterDraft) (string, proto.Message) {
	seen := make(map[string]struct{}, len(steps))
	for _, step := range steps {
		key := ministerOperationStepConflictKey(step)
		if key != "" {
			if _, exists := seen[key]; exists {
				return "invalid_directive", nil
			}
			seen[key] = struct{}{}
		}
		if errCode, failure := validateMinisterDraftStep(room, playerID, playerState, step); errCode != "" {
			return errCode, failure
		}
	}
	return "", nil
}

func ministerOperationStepConflictKey(draft domain.MinisterDraft) string {
	switch draft.Kind {
	case domain.MinisterDraftKindResearch:
		return "research"
	case domain.MinisterDraftKindPolicy:
		return "policy"
	case domain.MinisterDraftKindInstitution:
		return "institution"
	case domain.MinisterDraftKindBuild:
		if nodeID := strings.TrimSpace(draft.NodeID); nodeID != "" {
			return "build:" + nodeID
		}
	case domain.MinisterDraftKindRecipe:
		if nodeID := strings.TrimSpace(draft.NodeID); nodeID != "" {
			return "recipe:" + nodeID
		}
	case domain.MinisterDraftKindUnitOrder:
		if unitID := strings.TrimSpace(draft.UnitID); unitID != "" {
			return "unit:" + unitID
		}
	}
	return ""
}

func validateMinisterDraftStep(room Session, playerID string, playerState *domain.PlayerState, draft domain.MinisterDraft) (string, proto.Message) {
	state := room.State()
	switch draft.Kind {
	case domain.MinisterDraftKindResearch:
		technologyID := strings.TrimSpace(draft.TargetID)
		validation := economy.ValidateResearchTarget(state, playerID, technologyID)
		if !validation.OK {
			return validation.ErrorCode, &pb.MsgResearchResult{Success: false, TechnologyId: technologyID, ErrorCode: validation.ErrorCode}
		}
	case domain.MinisterDraftKindPolicy:
		policyID := strings.TrimSpace(draft.TargetID)
		if _, errCode := validatePolicySelection(state, playerID, policyID, "national"); errCode != "" {
			return errCode, &pb.MsgSetPolicyResult{Success: false, NationalPolicyId: policyID, ErrorCode: errCode}
		}
	case domain.MinisterDraftKindInstitution:
		normalized, errCode := ValidateInstitutionLoadout(state, playerID, playerState, draft.InstitutionIDs)
		if errCode != "" || len(normalized) == 0 {
			if errCode == "" {
				errCode = "invalid_directive"
			}
			return errCode, &pb.MsgSetInstitutionLoadoutResult{Success: false, InstitutionIds: append([]string(nil), draft.InstitutionIDs...), ErrorCode: errCode}
		}
	case domain.MinisterDraftKindBuild:
		eval := evaluateBuildCommand(room, playerID, playerState, draft.NodeID, draft.BuildingTypeID, draft.CityID)
		if !eval.OK {
			return eval.ErrorCode, &pb.MsgBuildStructureResult{
				Success:         false,
				NodeId:          strings.TrimSpace(draft.NodeID),
				BuildingTypeId:  strings.TrimSpace(draft.BuildingTypeID),
				CityId:          strings.TrimSpace(draft.CityID),
				ErrorCode:       eval.ErrorCode,
				FeedbackMessage: eval.FeedbackMessage,
				FeedbackDetails: eval.FeedbackDetails,
			}
		}
	case domain.MinisterDraftKindRecipe:
		eval := evaluateRecipeCommand(state, playerID, draft.NodeID, draft.RecipeID)
		if !eval.OK {
			return eval.ErrorCode, &pb.MsgSetBuildingRecipeResult{
				Success:         false,
				NodeId:          strings.TrimSpace(draft.NodeID),
				RecipeId:        strings.TrimSpace(draft.RecipeID),
				ErrorCode:       eval.ErrorCode,
				FeedbackMessage: eval.FeedbackMessage,
				FeedbackDetails: eval.FeedbackDetails,
			}
		}
	case domain.MinisterDraftKindUnitOrder:
		order := gameorders.UnitOrder{
			PlayerID:        playerID,
			UnitID:          strings.TrimSpace(draft.UnitID),
			Action:          gameorders.UnitAction(strings.TrimSpace(draft.Action)),
			TargetNodeID:    strings.TrimSpace(draft.TargetNodeID),
			TargetUnitID:    strings.TrimSpace(draft.TargetUnitID),
			SecondaryNodeID: strings.TrimSpace(draft.SecondaryNodeID),
			Params:          cloneParams(draft.Params),
		}
		if errCode := gameorders.ValidatePlanningUnitOrder(state, playerID, order); errCode != "" {
			return errCode, &pb.MsgIssueUnitOrderResult{
				Success:      false,
				UnitId:       order.UnitID,
				Action:       string(order.Action),
				TargetNodeId: order.TargetNodeID,
				TargetUnitId: order.TargetUnitID,
				ErrorCode:    errCode,
			}
		}
	default:
		return "invalid_directive", nil
	}
	return "", nil
}

func applyMinisterDraftStep(room Session, playerID string, draft domain.MinisterDraft) proto.Message {
	switch draft.Kind {
	case domain.MinisterDraftKindResearch:
		technologyID := strings.TrimSpace(draft.TargetID)
		room.State().TurnRuntime.Planning.SetPendingResearchTarget(playerID, technologyID)
		return &pb.MsgResearchResult{Success: true, TechnologyId: technologyID}
	case domain.MinisterDraftKindPolicy:
		policyID := strings.TrimSpace(draft.TargetID)
		room.State().TurnRuntime.Planning.SetPendingPolicy(playerID, domain.Policy(policyID))
		return &pb.MsgSetPolicyResult{Success: true, NationalPolicyId: policyID}
	case domain.MinisterDraftKindInstitution:
		normalized, _ := ValidateInstitutionLoadout(room.State(), playerID, room.State().Players[playerID], draft.InstitutionIDs)
		room.SetInstitutionLoadout(playerID, normalized)
		return &pb.MsgSetInstitutionLoadoutResult{Success: true, InstitutionIds: normalized}
	case domain.MinisterDraftKindBuild:
		nodeID := strings.TrimSpace(draft.NodeID)
		buildingTypeID := strings.TrimSpace(draft.BuildingTypeID)
		cityID := strings.TrimSpace(draft.CityID)
		room.QueueBuildOrder(domain.BuildOrder{PlayerID: playerID, NodeID: nodeID, BuildingType: buildingTypeID, CityID: cityID})
		return &pb.MsgBuildStructureResult{Success: true, NodeId: nodeID, BuildingTypeId: buildingTypeID, CityId: cityID}
	case domain.MinisterDraftKindRecipe:
		nodeID := strings.TrimSpace(draft.NodeID)
		recipeID := strings.TrimSpace(draft.RecipeID)
		room.QueueRecipeSelection(domain.RecipeSelectionOrder{PlayerID: playerID, NodeID: nodeID, RecipeID: recipeID})
		return &pb.MsgSetBuildingRecipeResult{Success: true, NodeId: nodeID, RecipeId: recipeID}
	case domain.MinisterDraftKindUnitOrder:
		order := gameorders.UnitOrder{
			PlayerID:        playerID,
			UnitID:          strings.TrimSpace(draft.UnitID),
			Action:          gameorders.UnitAction(strings.TrimSpace(draft.Action)),
			TargetNodeID:    strings.TrimSpace(draft.TargetNodeID),
			TargetUnitID:    strings.TrimSpace(draft.TargetUnitID),
			SecondaryNodeID: strings.TrimSpace(draft.SecondaryNodeID),
			Params:          cloneParams(draft.Params),
		}
		room.SetUnitOrder(order)
		return &pb.MsgIssueUnitOrderResult{
			Success:      true,
			UnitId:       order.UnitID,
			Action:       string(order.Action),
			TargetNodeId: order.TargetNodeID,
			TargetUnitId: order.TargetUnitID,
		}
	default:
		return nil
	}
}

func reconcileMinisterDraftBindings(state *domain.GameState, playerID string, acceptedDraftID string) []ministerDraftTransition {
	if state == nil {
		return nil
	}
	drafts := state.TurnRuntime.Planning.MinisterDraftsForPlayer(playerID)
	if len(drafts) == 0 {
		return nil
	}
	currentResearch := strings.TrimSpace(state.TurnRuntime.Planning.PendingResearchTarget(playerID))
	currentPolicy := strings.TrimSpace(string(state.TurnRuntime.Planning.PendingPolicy(playerID)))
	acceptedDraftID = strings.TrimSpace(acceptedDraftID)
	transitions := make([]ministerDraftTransition, 0, len(drafts))

	for idx := range drafts {
		draft := drafts[idx]
		before := draft.Status
		if draft.Status == domain.MinisterDraftStatusRejected {
			draft.Available = false
			drafts[idx] = draft
			continue
		}
		draft.Available = true
		if acceptedDraftID != "" && strings.TrimSpace(draft.DraftID) == acceptedDraftID {
			draft.Status = domain.MinisterDraftStatusAccepted
			if before != draft.Status {
				transitions = append(transitions, ministerDraftTransition{
					Draft:      draft,
					FromStatus: before,
					ToStatus:   draft.Status,
				})
			}
			drafts[idx] = draft
			continue
		}
		switch draft.Kind {
		case domain.MinisterDraftKindResearch:
			if draft.Status == domain.MinisterDraftStatusAccepted && strings.TrimSpace(draft.TargetID) != currentResearch {
				draft.Status = domain.MinisterDraftStatusStale
			}
		case domain.MinisterDraftKindPolicy:
			if draft.Status == domain.MinisterDraftStatusAccepted && strings.TrimSpace(draft.TargetID) != currentPolicy {
				draft.Status = domain.MinisterDraftStatusStale
			}
		}
		if before != draft.Status {
			transitions = append(transitions, ministerDraftTransition{
				Draft:      draft,
				FromStatus: before,
				ToStatus:   draft.Status,
			})
		}
		drafts[idx] = draft
	}
	state.TurnRuntime.Planning.SetMinisterDrafts(playerID, drafts)
	return transitions
}

func recordMinisterDraftTransitions(room Session, playerID string, turn int, transitions []ministerDraftTransition) {
	recorder, ok := room.(ministerMemoryRecorder)
	if !ok || recorder == nil || len(transitions) == 0 {
		return
	}
	for _, transition := range transitions {
		role := strings.TrimSpace(transition.Draft.MinisterRole)
		if role == "" {
			continue
		}
		content := fmt.Sprintf("%s:%s", strings.TrimSpace(string(transition.Draft.Kind)), strings.TrimSpace(transition.Draft.TargetLabel))
		recorder.RecordMinisterMemory(playerID, role, ministerengine.MemoryEntry{
			Turn:       turn,
			Type:       "draft_feedback",
			Content:    content,
			Outcome:    string(transition.ToStatus),
			PlayerResp: string(transition.ToStatus),
		})
	}
}
