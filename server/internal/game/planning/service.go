// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现规划输入模块的服务编排逻辑。

package planning

import (
	"context"
	"errors"
	"log/slog"
	"strings"

	"github.com/elebirds/panoptes/internal/game/participant"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	coretransport "github.com/elebirds/panoptes/internal/transport"
	cmddispatch "github.com/elebirds/panoptes/internal/transport/dispatch"
	transportproblem "github.com/elebirds/panoptes/internal/transport/problem"
)

type Service struct{}

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
		return handlePreviewCommand(room, inbound, cmd)
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
	delivery := newCommandDelivery(eventCtx, playerID, room)
	result := acceptedHandleIntentResult()
	var err error
	switch intent := envelope.Intent.(type) {
	case SetPolicyIntent:
		result, err = s.handleSetPolicy(delivery, room, playerID, strings.TrimSpace(intent.NationalPolicyID))
	case SetInstitutionLoadoutIntent:
		result, err = s.handleInstitutionLoadout(delivery, room, playerID, playerState, intent.PolicyIDs)
	case BuildStructureIntent:
		result, err = s.handleBuildRequest(delivery, room, playerID, playerState, intent.NodeID, intent.BuildingTypeID, intent.CityID)
	case RevealNodeIntent:
		result, err = s.handleRevealNode(delivery, room, playerID, playerState, intent.NodeID)
	case SetResearchTargetIntent:
		result, err = s.handleResearchRequest(delivery, room, playerID, playerState, strings.TrimSpace(intent.TechnologyID))
	case SetBuildingRecipeIntent:
		result, err = s.handleSetBuildingRecipe(delivery, room, playerID, strings.TrimSpace(intent.NodeID), strings.TrimSpace(intent.RecipeID))
	case SetMinisterDirectiveIntent:
		result, err = s.handleMinisterDirective(delivery, room, playerID, intent)
	case IssueUnitOrderIntent:
		result, err = s.handleIssueUnitOrder(delivery, room, playerID, &pb.MsgIssueUnitOrder{
			UnitId:          intent.UnitID,
			Action:          intent.Action,
			TargetNodeId:    intent.TargetNodeID,
			TargetUnitId:    intent.TargetUnitID,
			SecondaryNodeId: intent.SecondaryNodeID,
			Params:          cloneParams(intent.Params),
		})
	case CancelUnitOrderIntent:
		result, err = s.handleCancelUnitOrder(delivery, room, playerID, intent.UnitID)
	case SubmitTurnIntent:
		result, err = s.handleSubmitTurn(room, playerID)
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
