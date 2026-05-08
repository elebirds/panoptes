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
	gameorders "github.com/elebirds/panoptes/internal/game/orders"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	transportproblem "github.com/elebirds/panoptes/internal/transport/problem"
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
		result, err := s.handleInstitutionLoadout(delivery, room, playerID, playerState, draft.PolicyIDs)
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
