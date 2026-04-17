// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现规划输入模块的服务编排逻辑。

package planning

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"log/slog"
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/engine/combat"
	"github.com/elebirds/panoptes/internal/engine/economy"
	ministerengine "github.com/elebirds/panoptes/internal/engine/minister"
	gameorders "github.com/elebirds/panoptes/internal/game/orders"
	"github.com/elebirds/panoptes/internal/game/participant"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	coretransport "github.com/elebirds/panoptes/internal/transport"
	cmddispatch "github.com/elebirds/panoptes/internal/transport/dispatch"
	transportproblem "github.com/elebirds/panoptes/internal/transport/problem"
	"github.com/yohamta/donburi"
	"google.golang.org/protobuf/proto"
)

type Session interface {
	State() *domain.GameState
	Participant(participantID string) (participant.Participant, bool)
	Submit(playerID string)
	SendToPlayer(ctx context.Context, playerID string, msg proto.Message) error
	IsDevMode() bool
	QueueBuildOrder(order domain.BuildOrder)
	QueueRecipeSelection(order domain.RecipeSelectionOrder)
	SetInstitutionLoadout(playerID string, policyIDs []string)
	SetMinisterDirective(playerID string, directive string)
	SetWarDirectives(playerID string, directives []domain.WarZoneDirective)
	SetUnitOrder(order gameorders.UnitOrder)
	CancelUnitOrder(playerID string, unitID string)
	SendPlanningSnapshot(ctx context.Context, playerID string) error
	BuildNodeViewForPlayer(nodeID string, viewerID string) *pb.NodeView
	NodeByID(nodeID string) (*donburi.Entry, bool)
}

type Service struct{}

type ministerMemoryRecorder interface {
	RecordMinisterMemory(playerID string, role string, entry ministerengine.MemoryEntry)
}

type ministerDraftTransition struct {
	Draft      domain.MinisterDraft
	FromStatus domain.MinisterDraftStatus
	ToStatus   domain.MinisterDraftStatus
}

type handleIntentResult struct {
	accepted  bool
	errorCode string
}

func acceptedHandleIntentResult() handleIntentResult {
	return handleIntentResult{accepted: true}
}

func rejectedHandleIntentResult(errorCode string) handleIntentResult {
	return handleIntentResult{accepted: false, errorCode: errorCode}
}

func (s *Service) Enter(room Session) {
	if room == nil || room.State() == nil {
		return
	}
	room.State().TurnRuntime.Planning.EnsureDraftMaps()
}

func (s *Service) HandleCommand(room Session, inbound cmddispatch.InboundContext, cmd *pb.PlanningCommand) error {
	if cmd == nil || cmd.GetBody() == nil {
		return errors.New("planning command is nil")
	}

	envelope, handled, err := EnvelopeFromPlanningCommand(inbound, cmd)
	if err != nil {
		return err
	}
	if !handled {
		eventCtx := coretransport.ContextWithEventMeta(context.Background(), coretransport.EventMetaFromInbound(inbound))
		switch body := cmd.GetBody().(type) {
		case *pb.PlanningCommand_PlanningPathPreviewRequest:
			_ = room.SendToPlayer(eventCtx, inbound.PlayerID, buildPlanningPathPreviewResponse(room.State(), inbound.PlayerID, body.PlanningPathPreviewRequest))
		case *pb.PlanningCommand_BuildStructurePreview:
			_ = room.SendToPlayer(eventCtx, inbound.PlayerID, buildStructurePreviewResponse(room, inbound.PlayerID, body.BuildStructurePreview))
		case *pb.PlanningCommand_SetBuildingRecipePreview:
			_ = room.SendToPlayer(eventCtx, inbound.PlayerID, setBuildingRecipePreviewResponse(room.State(), inbound.PlayerID, body.SetBuildingRecipePreview))
		default:
			return transportproblem.InvalidRequest("unsupported planning preview command")
		}
		return nil
	}
	return s.HandleIntent(room, envelope)
}

func (s *Service) HandleIntent(room Session, envelope IntentEnvelope) error {
	if room == nil {
		return errors.New("session is nil")
	}
	playerID := envelope.ParticipantID
	state := room.State()
	if state == nil {
		return errors.New("state is nil")
	}

	currentParticipant, ok := room.Participant(playerID)
	if !ok {
		currentParticipant = participant.Participant{ID: playerID, Kind: participant.KindHuman}
	}
	record := DebugIntentRecordFor(currentParticipant.Kind, playerID, envelope.Intent)
	baseAttrs := []any{
		"component", "planning_intent",
		"participant_id", playerID,
		"participant_kind", string(currentParticipant.Kind),
		"source", record.Source,
		"turn", state.Turn,
		"phase", state.Phase,
		"intent_type", record.IntentType,
		"intent_label", record.IntentLabel,
	}
	baseAttrs = append(baseAttrs, record.FieldAttrs()...)
	slog.Debug("planning 操作尝试", append(append([]any{}, baseAttrs...), "summary", record.AttemptSummary(), "outcome", "attempt")...)

	playerState, ok := state.Players[playerID]
	if !ok || playerState == nil {
		s.logIntentResult(baseAttrs, record, rejectedHandleIntentResult(""), errors.New("player not found"))
		return errors.New("player not found")
	}

	eventCtx := intentContext(envelope)
	result := acceptedHandleIntentResult()
	var err error
	switch intent := envelope.Intent.(type) {
	case SetPolicyIntent:
		result, err = s.handleSetPolicy(eventCtx, room, playerID, strings.TrimSpace(intent.NationalPolicyID))
	case SetInstitutionLoadoutIntent:
		result, err = s.handleInstitutionLoadout(eventCtx, room, playerID, playerState, intent.PolicyIDs)
	case BuildStructureIntent:
		result, err = s.handleBuildRequest(eventCtx, room, playerID, playerState, intent.NodeID, intent.BuildingTypeID, intent.CityID)
	case RevealNodeIntent:
		if playerState.TokensLeft <= 0 {
			_ = room.SendToPlayer(eventCtx, playerID, &pb.MsgTokenResult{Success: false, Action: "reveal", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "no_tokens_left"})
			result = rejectedHandleIntentResult("no_tokens_left")
			break
		}
		nodeView := room.BuildNodeViewForPlayer(intent.NodeID, playerID)
		if nodeView == nil {
			_ = room.SendToPlayer(eventCtx, playerID, &pb.MsgTokenResult{Success: false, Action: "reveal", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "invalid_target"})
			result = rejectedHandleIntentResult("invalid_target")
			break
		}
		playerState.TokensLeft--
		_ = room.SendToPlayer(eventCtx, playerID, &pb.MsgRevealResult{NodeId: intent.NodeID, TrueState: nodeView, TokensLeft: int32(playerState.TokensLeft)})
		result = acceptedHandleIntentResult()
	case SetResearchTargetIntent:
		result, err = s.handleResearchRequest(eventCtx, room, playerID, playerState, strings.TrimSpace(intent.TechnologyID))
	case SetBuildingRecipeIntent:
		result, err = s.handleSetBuildingRecipe(eventCtx, room, playerID, strings.TrimSpace(intent.NodeID), strings.TrimSpace(intent.RecipeID))
	case SetMinisterDirectiveIntent:
		result, err = s.handleMinisterDirective(eventCtx, room, playerID, intent)
	case IssueUnitOrderIntent:
		result, err = s.handleIssueUnitOrder(eventCtx, room, playerID, &pb.MsgIssueUnitOrder{
			UnitId:          intent.UnitID,
			Action:          intent.Action,
			TargetNodeId:    intent.TargetNodeID,
			TargetUnitId:    intent.TargetUnitID,
			SecondaryNodeId: intent.SecondaryNodeID,
			Params:          cloneParams(intent.Params),
		})
	case CancelUnitOrderIntent:
		room.CancelUnitOrder(playerID, strings.TrimSpace(intent.UnitID))
		_ = room.SendPlanningSnapshot(eventCtx, playerID)
		result = acceptedHandleIntentResult()
	case SubmitTurnIntent:
		room.Submit(playerID)
		result = acceptedHandleIntentResult()
	case nil:
		err = transportproblem.InvalidRequest("planning intent is nil")
	default:
		err = transportproblem.InvalidRequest("unsupported planning intent")
	}

	s.logIntentResult(baseAttrs, record, result, err)
	return err
}

func intentContext(envelope IntentEnvelope) context.Context {
	meta := &pb.EventMeta{
		RequestId: envelope.RequestID,
		TraceId:   envelope.TraceID,
	}
	if meta.RequestId == "" && meta.TraceId == "" {
		return context.Background()
	}
	return coretransport.ContextWithEventMeta(context.Background(), meta)
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

func (s *Service) handleIssueUnitOrder(ctx context.Context, room Session, playerID string, msg *pb.MsgIssueUnitOrder) (handleIntentResult, error) {
	order := gameorders.UnitOrder{
		PlayerID:        playerID,
		UnitID:          strings.TrimSpace(msg.GetUnitId()),
		Action:          gameorders.UnitAction(strings.TrimSpace(msg.GetAction())),
		TargetNodeID:    strings.TrimSpace(msg.GetTargetNodeId()),
		TargetUnitID:    strings.TrimSpace(msg.GetTargetUnitId()),
		SecondaryNodeID: strings.TrimSpace(msg.GetSecondaryNodeId()),
		Params:          cloneParams(msg.GetParams()),
	}
	if errCode := validateUnitOrder(room.State(), playerID, order); errCode != "" {
		_ = room.SendToPlayer(ctx, playerID, &pb.MsgIssueUnitOrderResult{
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
	_ = room.SendToPlayer(ctx, playerID, &pb.MsgIssueUnitOrderResult{
		Success:      true,
		UnitId:       order.UnitID,
		Action:       string(order.Action),
		TargetNodeId: order.TargetNodeID,
		TargetUnitId: order.TargetUnitID,
	})
	_ = room.SendPlanningSnapshot(ctx, playerID)
	return acceptedHandleIntentResult(), nil
}

func validateUnitOrder(state *domain.GameState, playerID string, order gameorders.UnitOrder) string {
	if state == nil || order.UnitID == "" || order.Action == "" {
		return "invalid_request"
	}
	unitEntry, ok := findPreviewUnit(state, order.UnitID, playerID)
	if !ok {
		return "unit_not_found"
	}
	stats := ecs.UnitStatsC.Get(unitEntry)

	switch order.Action {
	case gameorders.ActionHold:
		return ""
	case gameorders.ActionMove:
		if order.TargetNodeID == "" {
			return "invalid_request"
		}
		if _, ok := state.GetNode(order.TargetNodeID); !ok {
			return "invalid_target"
		}
		return ""
	case gameorders.ActionAttack:
		hasUnitTarget := order.TargetUnitID != ""
		hasNodeTarget := order.TargetNodeID != ""
		if hasUnitTarget == hasNodeTarget || !unitCanAttack(unitEntry, stats.Type) {
			return "invalid_directive"
		}
		if hasUnitTarget {
			targetEntry, ok := findAnyUnit(state, order.TargetUnitID)
			if !ok {
				return "invalid_target"
			}
			targetStats := ecs.UnitStatsC.Get(targetEntry)
			if targetStats.Faction == playerID {
				return "invalid_target"
			}
			return ""
		}
		nodeEntry, ok := state.GetNode(order.TargetNodeID)
		if !ok || nodeEntry == nil || !nodeEntry.HasComponent(ecs.BuildingC) {
			return "invalid_target"
		}
		building := ecs.BuildingC.Get(nodeEntry)
		if building.Owner == "" || building.Owner == playerID {
			return "invalid_target"
		}
		if !unitCanAttackStructures(unitEntry, stats.Type) {
			return "invalid_directive"
		}
		if !isStructureTargetInRange(unitEntry, nodeEntry, stats.AttackRange) {
			// Allow "move then attack" drafts: attack range can be validated from planned move destination.
			if state != nil && state.TurnRuntime.Resolving.ActiveMarches != nil {
				if march, ok := state.TurnRuntime.Resolving.ActiveMarches[order.UnitID]; ok && march.DestinationNodeID != "" {
					if marchNodeEntry, ok := state.GetNode(march.DestinationNodeID); ok && isStructureTargetInRangeFromNode(marchNodeEntry, nodeEntry, stats.AttackRange) {
						return ""
					}
				}
			}
			if order.SecondaryNodeID != "" {
				if secondaryNodeEntry, ok := state.GetNode(order.SecondaryNodeID); ok && isStructureTargetInRangeFromNode(secondaryNodeEntry, nodeEntry, stats.AttackRange) {
					return ""
				}
			}
			return "invalid_target"
		}
		return ""
	case gameorders.ActionCharge:
		if !unitCanCharge(unitEntry) {
			return "invalid_directive"
		}
		return ""
	case gameorders.ActionSettleCity:
		return ""
	default:
		return "invalid_directive"
	}
}

func unitCanAttack(entry *donburi.Entry, unitType domain.UnitType) bool {
	if entry != nil && entry.HasComponent(ecs.UnitCapabilitiesC) {
		return ecs.UnitCapabilitiesC.Get(entry).CanAttack()
	}
	if cfg, ok := staticdata.Default().GetUnit(string(unitType)); ok {
		return cfg.Attack > 0 && cfg.AttackRange > 0 && cfg.Class != "civilian"
	}
	return false
}

func unitCanAttackStructures(entry *donburi.Entry, unitType domain.UnitType) bool {
	if entry != nil && entry.HasComponent(ecs.UnitCapabilitiesC) {
		return ecs.UnitCapabilitiesC.Get(entry).CanAttackStructures
	}
	if cfg, ok := staticdata.Default().GetUnit(string(unitType)); ok {
		return cfg.Flags.CanAttackStructures
	}
	return false
}

func unitCanCharge(entry *donburi.Entry) bool {
	return entry != nil && entry.HasComponent(ecs.UnitCapabilitiesC) && ecs.UnitCapabilitiesC.Get(entry).Charge
}

func isStructureTargetInRange(unitEntry *donburi.Entry, nodeEntry *donburi.Entry, attackRange int) bool {
	if unitEntry == nil || nodeEntry == nil || attackRange <= 0 {
		return false
	}
	unitPos := ecs.PositionC.Get(unitEntry)
	nodePos := ecs.PositionC.Get(nodeEntry)
	return domain.Position{X: unitPos.X, Y: unitPos.Y}.DistanceTo(domain.Position{X: nodePos.X, Y: nodePos.Y}) <= attackRange
}

func isStructureTargetInRangeFromNode(fromNodeEntry *donburi.Entry, targetNodeEntry *donburi.Entry, attackRange int) bool {
	if fromNodeEntry == nil || targetNodeEntry == nil || attackRange <= 0 {
		return false
	}
	fromPos := ecs.PositionC.Get(fromNodeEntry)
	targetPos := ecs.PositionC.Get(targetNodeEntry)
	return domain.Position{X: fromPos.X, Y: fromPos.Y}.DistanceTo(domain.Position{X: targetPos.X, Y: targetPos.Y}) <= attackRange
}

func findAnyUnit(state *domain.GameState, unitID string) (*donburi.Entry, bool) {
	if state == nil || state.World == nil {
		return nil, false
	}
	var found *donburi.Entry
	ecs.AllUnits(state.World).Each(state.World, func(entry *donburi.Entry) {
		if found != nil {
			return
		}
		stats := ecs.UnitStatsC.Get(entry)
		if stats.ID == unitID {
			found = entry
		}
	})
	return found, found != nil
}

func (s *Service) handleSetPolicy(ctx context.Context, room Session, playerID string, policyID string) (handleIntentResult, error) {
	if policyID == "" {
		_ = room.SendToPlayer(ctx, playerID, &pb.MsgSetPolicyResult{Success: false, NationalPolicyId: policyID, ErrorCode: "invalid_request"})
		return rejectedHandleIntentResult("invalid_request"), nil
	}

	if _, errCode := validatePolicySelection(room.State(), playerID, policyID, "national"); errCode != "" {
		_ = room.SendToPlayer(ctx, playerID, &pb.MsgSetPolicyResult{Success: false, NationalPolicyId: policyID, ErrorCode: errCode})
		return rejectedHandleIntentResult(errCode), nil
	}

	room.State().TurnRuntime.Planning.SetPendingPolicy(playerID, domain.Policy(policyID))
	transitions := reconcileMinisterDraftBindings(room.State(), playerID, "")
	_ = room.SendToPlayer(ctx, playerID, &pb.MsgSetPolicyResult{Success: true, NationalPolicyId: policyID})
	_ = room.SendPlanningSnapshot(ctx, playerID)
	recordMinisterDraftTransitions(room, playerID, room.State().Turn, transitions)
	return acceptedHandleIntentResult(), nil
}

func (s *Service) handleResearchRequest(ctx context.Context, room Session, playerID string, playerState *domain.PlayerState, technologyID string) (handleIntentResult, error) {
	if playerState == nil || technologyID == "" {
		_ = room.SendToPlayer(ctx, playerID, &pb.MsgResearchResult{Success: false, TechnologyId: technologyID, ErrorCode: "invalid_request"})
		return rejectedHandleIntentResult("invalid_request"), nil
	}

	state := room.State()
	validation := economy.ValidateResearchTarget(state, playerID, technologyID)
	if !validation.OK {
		_ = room.SendToPlayer(ctx, playerID, &pb.MsgResearchResult{Success: false, TechnologyId: technologyID, ErrorCode: validation.ErrorCode})
		return rejectedHandleIntentResult(validation.ErrorCode), nil
	}

	state.TurnRuntime.Planning.SetPendingResearchTarget(playerID, technologyID)
	transitions := reconcileMinisterDraftBindings(state, playerID, "")
	_ = room.SendToPlayer(ctx, playerID, &pb.MsgResearchResult{Success: true, TechnologyId: technologyID})
	_ = room.SendPlanningSnapshot(ctx, playerID)
	recordMinisterDraftTransitions(room, playerID, state.Turn, transitions)
	return acceptedHandleIntentResult(), nil
}

func (s *Service) handleMinisterDirective(ctx context.Context, room Session, playerID string, intent SetMinisterDirectiveIntent) (handleIntentResult, error) {
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
		return s.acceptMinisterDraft(ctx, room, playerID, draft)
	case "reject":
		before := draft.Status
		draft.Status = domain.MinisterDraftStatusRejected
		draft.Available = false
		state.TurnRuntime.Planning.ReplaceMinisterDraft(playerID, idx, draft)
		_ = room.SendPlanningSnapshot(ctx, playerID)
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

func (s *Service) acceptMinisterDraft(ctx context.Context, room Session, playerID string, draft domain.MinisterDraft) (handleIntentResult, error) {
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
			_ = room.SendToPlayer(ctx, playerID, &pb.MsgResearchResult{Success: false, TechnologyId: technologyID, ErrorCode: validation.ErrorCode})
			return rejectedHandleIntentResult(validation.ErrorCode), nil
		}
		state.TurnRuntime.Planning.SetPendingResearchTarget(playerID, technologyID)
		transitions := reconcileMinisterDraftBindings(state, playerID, strings.TrimSpace(draft.DraftID))
		_ = room.SendToPlayer(ctx, playerID, &pb.MsgResearchResult{Success: true, TechnologyId: technologyID})
		_ = room.SendPlanningSnapshot(ctx, playerID)
		recordMinisterDraftTransitions(room, playerID, state.Turn, transitions)
		return acceptedHandleIntentResult(), nil
	case domain.MinisterDraftKindPolicy:
		policyID := strings.TrimSpace(draft.TargetID)
		if _, errCode := validatePolicySelection(state, playerID, policyID, "national"); errCode != "" {
			_ = room.SendToPlayer(ctx, playerID, &pb.MsgSetPolicyResult{Success: false, NationalPolicyId: policyID, ErrorCode: errCode})
			return rejectedHandleIntentResult(errCode), nil
		}
		state.TurnRuntime.Planning.SetPendingPolicy(playerID, domain.Policy(policyID))
		transitions := reconcileMinisterDraftBindings(state, playerID, strings.TrimSpace(draft.DraftID))
		_ = room.SendToPlayer(ctx, playerID, &pb.MsgSetPolicyResult{Success: true, NationalPolicyId: policyID})
		_ = room.SendPlanningSnapshot(ctx, playerID)
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

func (s *Service) handleInstitutionLoadout(ctx context.Context, room Session, playerID string, playerState *domain.PlayerState, policyIDs []string) (handleIntentResult, error) {
	if playerState == nil {
		_ = room.SendToPlayer(ctx, playerID, &pb.MsgSetInstitutionLoadoutResult{Success: false, ErrorCode: "invalid_request"})
		return rejectedHandleIntentResult("invalid_request"), nil
	}
	state := room.State()
	normalized := domain.NormalizePolicyIDList(policyIDs)
	if len(normalized) > playerState.Institutions.SlotCount {
		_ = room.SendToPlayer(ctx, playerID, &pb.MsgSetInstitutionLoadoutResult{Success: false, PolicyIds: normalized, ErrorCode: "invalid_directive"})
		return rejectedHandleIntentResult("invalid_directive"), nil
	}
	for _, policyID := range normalized {
		if _, errCode := validatePolicySelection(state, playerID, policyID, "institutional"); errCode != "" {
			_ = room.SendToPlayer(ctx, playerID, &pb.MsgSetInstitutionLoadoutResult{Success: false, PolicyIds: normalized, ErrorCode: errCode})
			return rejectedHandleIntentResult(errCode), nil
		}
		if !playerState.Institutions.HasCandidate(policyID) {
			_ = room.SendToPlayer(ctx, playerID, &pb.MsgSetInstitutionLoadoutResult{Success: false, PolicyIds: normalized, ErrorCode: "invalid_directive"})
			return rejectedHandleIntentResult("invalid_directive"), nil
		}
	}
	room.SetInstitutionLoadout(playerID, normalized)
	_ = room.SendToPlayer(ctx, playerID, &pb.MsgSetInstitutionLoadoutResult{Success: true, PolicyIds: normalized})
	_ = room.SendPlanningSnapshot(ctx, playerID)
	return acceptedHandleIntentResult(), nil
}

func validatePolicySelection(state *domain.GameState, playerID string, policyID string, requiredLayer string) (staticdata.PolicyDefinition, string) {
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

func (s *Service) handleSetBuildingRecipe(ctx context.Context, room Session, playerID string, nodeID string, recipeID string) (handleIntentResult, error) {
	eval := evaluateRecipeCommand(room.State(), playerID, nodeID, recipeID)
	if !eval.OK {
		_ = room.SendToPlayer(ctx, playerID, &pb.MsgSetBuildingRecipeResult{
			Success:         false,
			NodeId:          strings.TrimSpace(nodeID),
			RecipeId:        strings.TrimSpace(recipeID),
			ErrorCode:       eval.ErrorCode,
			FeedbackMessage: eval.FeedbackMessage,
			FeedbackDetails: eval.FeedbackDetails,
		})
		return rejectedHandleIntentResult(eval.ErrorCode), nil
	}

	room.QueueRecipeSelection(domain.RecipeSelectionOrder{
		PlayerID: playerID,
		NodeID:   strings.TrimSpace(nodeID),
		RecipeID: strings.TrimSpace(recipeID),
	})
	_ = room.SendToPlayer(ctx, playerID, &pb.MsgSetBuildingRecipeResult{
		Success:  true,
		NodeId:   strings.TrimSpace(nodeID),
		RecipeId: strings.TrimSpace(recipeID),
	})
	_ = room.SendPlanningSnapshot(ctx, playerID)
	return acceptedHandleIntentResult(), nil
}

func (s *Service) handleBuildRequest(ctx context.Context, room Session, playerID string, playerState *domain.PlayerState, nodeID string, buildingType string, cityID string) (handleIntentResult, error) {
	if playerState == nil {
		return handleIntentResult{}, errors.New("player not found")
	}
	eval := evaluateBuildCommand(room, playerID, playerState, nodeID, buildingType, cityID)
	if !eval.OK {
		_ = room.SendToPlayer(ctx, playerID, &pb.MsgBuildStructureResult{
			Success:         false,
			NodeId:          strings.TrimSpace(nodeID),
			BuildingTypeId:  strings.TrimSpace(buildingType),
			CityId:          strings.TrimSpace(cityID),
			ErrorCode:       eval.ErrorCode,
			FeedbackMessage: eval.FeedbackMessage,
			FeedbackDetails: eval.FeedbackDetails,
		})
		return rejectedHandleIntentResult(eval.ErrorCode), nil
	}

	nodeID = strings.TrimSpace(nodeID)
	buildingType = strings.TrimSpace(buildingType)
	cityID = strings.TrimSpace(cityID)
	room.QueueBuildOrder(domain.BuildOrder{PlayerID: playerID, NodeID: nodeID, BuildingType: buildingType, CityID: cityID})
	_ = room.SendToPlayer(ctx, playerID, &pb.MsgBuildStructureResult{
		Success:        true,
		NodeId:         nodeID,
		BuildingTypeId: buildingType,
		CityId:         cityID,
	})
	if !eval.ReplacingDraft {
		playerState.TokensLeft--
		_ = room.SendToPlayer(ctx, playerID, &pb.MsgTokenResult{Success: true, Action: "build", TokensLeft: int32(playerState.TokensLeft)})
	}
	_ = room.SendPlanningSnapshot(ctx, playerID)
	return acceptedHandleIntentResult(), nil
}

func (s *Service) logIntentResult(baseAttrs []any, record DebugIntentRecord, result handleIntentResult, err error) {
	attrs := append([]any{}, baseAttrs...)
	if err != nil {
		attrs = append(attrs, "summary", record.ResultSummary(false), "outcome", "rejected", "outcome_label", "失败")
		if problem, ok := transportproblem.AsProblem(err); ok && problem.GetCode() != "" {
			attrs = append(attrs, "error_code", problem.GetCode())
		}
		slog.Debug("planning 操作结果", attrs...)
		return
	}
	if result.accepted {
		attrs = append(attrs, "summary", record.ResultSummary(true), "outcome", "accepted", "outcome_label", "成功")
		slog.Debug("planning 操作结果", attrs...)
		return
	}
	attrs = append(attrs, "summary", record.ResultSummary(false), "outcome", "rejected", "outcome_label", "失败")
	if result.errorCode != "" {
		attrs = append(attrs, "error_code", result.errorCode)
	}
	slog.Debug("planning 操作结果", attrs...)
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

func buildPlanningPathPreviewResponse(state *domain.GameState, playerID string, msg *pb.MsgPlanningPathPreviewRequest) *pb.MsgPlanningPathPreviewResponse {
	resp := &pb.MsgPlanningPathPreviewResponse{
		RequestId:    msg.GetRequestId(),
		UnitId:       msg.GetUnitId(),
		Action:       msg.GetAction(),
		TargetNodeId: msg.GetTargetNodeId(),
		Valid:        false,
	}
	if state == nil || msg == nil {
		resp.ErrorCode = "invalid_request"
		return resp
	}
	if gameorders.UnitAction(msg.GetAction()) != gameorders.ActionMove {
		resp.ErrorCode = "invalid_directive"
		return resp
	}
	entry, ok := findPreviewUnit(state, msg.GetUnitId(), playerID)
	if !ok {
		resp.ErrorCode = "unit_not_found"
		return resp
	}
	if _, ok := state.GetNode(msg.GetTargetNodeId()); !ok {
		resp.ErrorCode = "invalid_target"
		return resp
	}

	planner := combat.NewWeightedRoutePlanner(combat.DefaultTerrainCostPolicy{})
	preview, ok := planner.BuildPreview(state.World, state, ecs.UnitStatsC.Get(entry).ID, msg.GetTargetNodeId())
	if !ok {
		resp.ErrorCode = "invalid_target"
		return resp
	}

	resp.Valid = true
	resp.PathNodeIds = append(resp.PathNodeIds, preview.PathNodeIDs...)
	resp.FirstTurnNodeId = preview.FirstTurnNodeID
	resp.TotalTurns = int32(preview.TotalTurns)
	for _, stop := range preview.TurnStops {
		resp.TurnStops = append(resp.TurnStops, &pb.MarchTurnStop{
			TurnIndex: int32(stop.TurnIndex),
			NodeId:    stop.NodeID,
		})
	}
	return resp
}

func findPreviewUnit(state *domain.GameState, unitID string, ownerID string) (*donburi.Entry, bool) {
	if state == nil || state.World == nil {
		return nil, false
	}
	var found *donburi.Entry
	ecs.AllUnits(state.World).Each(state.World, func(entry *donburi.Entry) {
		if found != nil {
			return
		}
		stats := ecs.UnitStatsC.Get(entry)
		if stats.ID == unitID && stats.Faction == ownerID {
			found = entry
		}
	})
	return found, found != nil
}

type expandPayload struct {
	UnitID       string `json:"unit_id"`
	CenterNodeID string `json:"center_node_id"`
}

func MarshalExpandParams(unitID, centerNodeID string) ([]byte, error) {
	return json.Marshal(expandPayload{UnitID: unitID, CenterNodeID: centerNodeID})
}
