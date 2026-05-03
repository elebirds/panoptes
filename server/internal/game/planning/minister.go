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
	draft, idx, ok := state.TurnRuntime.Planning.FindMinisterDraft(playerID, intent.DraftID)
	if !ok || draft.MinisterRole != intent.MinisterRole || draft.Turn != state.Turn {
		return rejectedHandleIntentResult("invalid_directive"), transportproblem.New("invalid_directive", "minister draft not found")
	}
	if intent.DirectiveType == "accept" && !draft.Available {
		return rejectedHandleIntentResult("invalid_directive"), transportproblem.New("invalid_directive", "minister draft is unavailable")
	}

	switch intent.DirectiveType {
	case "accept":
		return s.acceptMinisterDraft(delivery, room, playerID, draft)
	case "reject":
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
