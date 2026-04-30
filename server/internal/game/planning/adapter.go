// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载 planning 命令处理、端口拆分与响应投递相关逻辑。

package planning

import (
	"encoding/json"
	"strings"

	pb "github.com/elebirds/panoptes/internal/gen/proto"
	cmddispatch "github.com/elebirds/panoptes/internal/transport/dispatch"
	transportproblem "github.com/elebirds/panoptes/internal/transport/problem"
)

type BatchPlanningCommand struct {
	Context cmddispatch.InboundContext
	Command *pb.PlanningCommand
}

func AdaptCommandBatch(inbound cmddispatch.InboundContext, batch *pb.MsgGameCommandBatch) ([]BatchPlanningCommand, bool) {
	if batch == nil {
		return nil, false
	}
	commands := make([]BatchPlanningCommand, 0, len(batch.GetCommands()))
	for _, envelope := range batch.GetCommands() {
		command, ok := AdaptCommandEnvelope(inbound, envelope)
		if !ok {
			return nil, false
		}
		commands = append(commands, command)
	}
	return commands, true
}

func AdaptCommandEnvelope(inbound cmddispatch.InboundContext, envelope *pb.CommandEnvelope) (BatchPlanningCommand, bool) {
	planningCommand := planningCommandFromCommandEnvelope(envelope)
	if planningCommand == nil {
		return BatchPlanningCommand{}, false
	}
	nextCtx := inbound
	if envelope.GetParticipantId() != "" {
		nextCtx.PlayerID = envelope.GetParticipantId()
	}
	if envelope.GetCommandId() != "" {
		nextCtx.RequestID = envelope.GetCommandId()
	}
	return BatchPlanningCommand{
		Context: nextCtx,
		Command: planningCommand,
	}, true
}

func planningCommandFromCommandEnvelope(envelope *pb.CommandEnvelope) *pb.PlanningCommand {
	if envelope == nil || envelope.GetBody() == nil {
		return nil
	}
	switch body := envelope.GetBody().(type) {
	case *pb.CommandEnvelope_SetPolicy:
		return &pb.PlanningCommand{Body: &pb.PlanningCommand_SetPolicy{SetPolicy: body.SetPolicy}}
	case *pb.CommandEnvelope_SetInstitutionLoadout:
		return &pb.PlanningCommand{Body: &pb.PlanningCommand_SetInstitutionLoadout{SetInstitutionLoadout: body.SetInstitutionLoadout}}
	case *pb.CommandEnvelope_SetResearchTarget:
		return &pb.PlanningCommand{Body: &pb.PlanningCommand_SetResearchTarget{SetResearchTarget: body.SetResearchTarget}}
	case *pb.CommandEnvelope_SetBuildingRecipe:
		return &pb.PlanningCommand{Body: &pb.PlanningCommand_SetBuildingRecipe{SetBuildingRecipe: body.SetBuildingRecipe}}
	case *pb.CommandEnvelope_BuildStructure:
		return &pb.PlanningCommand{Body: &pb.PlanningCommand_BuildStructure{BuildStructure: body.BuildStructure}}
	case *pb.CommandEnvelope_RevealNode:
		return &pb.PlanningCommand{Body: &pb.PlanningCommand_RevealNode{RevealNode: body.RevealNode}}
	case *pb.CommandEnvelope_SetWarZone:
		return &pb.PlanningCommand{Body: &pb.PlanningCommand_SetWarZone{SetWarZone: body.SetWarZone}}
	case *pb.CommandEnvelope_WarZoneDirective:
		return &pb.PlanningCommand{Body: &pb.PlanningCommand_WarZoneDirective{WarZoneDirective: body.WarZoneDirective}}
	case *pb.CommandEnvelope_SetMinisterDirective:
		return &pb.PlanningCommand{Body: &pb.PlanningCommand_SetMinisterDirective{SetMinisterDirective: body.SetMinisterDirective}}
	case *pb.CommandEnvelope_IssueUnitOrder:
		return &pb.PlanningCommand{Body: &pb.PlanningCommand_IssueUnitOrder{IssueUnitOrder: body.IssueUnitOrder}}
	case *pb.CommandEnvelope_CancelUnitOrder:
		return &pb.PlanningCommand{Body: &pb.PlanningCommand_CancelUnitOrder{CancelUnitOrder: body.CancelUnitOrder}}
	case *pb.CommandEnvelope_SubmitTurn:
		return &pb.PlanningCommand{Body: &pb.PlanningCommand_SubmitTurn{SubmitTurn: body.SubmitTurn}}
	default:
		return nil
	}
}

func EnvelopeFromPlanningCommand(inbound cmddispatch.InboundContext, cmd *pb.PlanningCommand) (IntentEnvelope, bool, error) {
	if cmd == nil || cmd.GetBody() == nil {
		return IntentEnvelope{}, false, transportproblem.InvalidRequest("planning command is nil")
	}

	envelope := IntentEnvelope{
		ParticipantID: inbound.PlayerID,
		RequestID:     inbound.RequestID,
		TraceID:       inbound.TraceID,
	}

	switch body := cmd.GetBody().(type) {
	case *pb.PlanningCommand_SetPolicy:
		envelope.Intent = SetPolicyIntent{NationalPolicyID: strings.TrimSpace(body.SetPolicy.GetNationalPolicyId())}
	case *pb.PlanningCommand_SetInstitutionLoadout:
		envelope.Intent = SetInstitutionLoadoutIntent{PolicyIDs: append([]string(nil), body.SetInstitutionLoadout.GetPolicyIds()...)}
	case *pb.PlanningCommand_BuildStructure:
		envelope.Intent = BuildStructureIntent{
			NodeID:         strings.TrimSpace(body.BuildStructure.GetNodeId()),
			BuildingTypeID: strings.TrimSpace(body.BuildStructure.GetBuildingTypeId()),
			CityID:         strings.TrimSpace(body.BuildStructure.GetCityId()),
		}
	case *pb.PlanningCommand_RevealNode:
		envelope.Intent = RevealNodeIntent{NodeID: strings.TrimSpace(body.RevealNode.GetNodeId())}
	case *pb.PlanningCommand_SetResearchTarget:
		envelope.Intent = SetResearchTargetIntent{TechnologyID: strings.TrimSpace(body.SetResearchTarget.GetTechnologyId())}
	case *pb.PlanningCommand_SetBuildingRecipe:
		envelope.Intent = SetBuildingRecipeIntent{
			NodeID:   strings.TrimSpace(body.SetBuildingRecipe.GetNodeId()),
			RecipeID: strings.TrimSpace(body.SetBuildingRecipe.GetRecipeId()),
		}
	case *pb.PlanningCommand_SetMinisterDirective:
		intent, err := ministerDirectiveIntent(body.SetMinisterDirective)
		if err != nil {
			return IntentEnvelope{}, false, err
		}
		envelope.Intent = intent
	case *pb.PlanningCommand_SetWarZone:
		return IntentEnvelope{}, false, transportproblem.New("invalid_directive", "war zone is not part of current MVP")
	case *pb.PlanningCommand_WarZoneDirective:
		return IntentEnvelope{}, false, transportproblem.New("invalid_directive", "war zone is not part of current MVP")
	case *pb.PlanningCommand_IssueUnitOrder:
		envelope.Intent = IssueUnitOrderIntent{
			UnitID:          strings.TrimSpace(body.IssueUnitOrder.GetUnitId()),
			Action:          strings.TrimSpace(body.IssueUnitOrder.GetAction()),
			TargetNodeID:    strings.TrimSpace(body.IssueUnitOrder.GetTargetNodeId()),
			TargetUnitID:    strings.TrimSpace(body.IssueUnitOrder.GetTargetUnitId()),
			SecondaryNodeID: strings.TrimSpace(body.IssueUnitOrder.GetSecondaryNodeId()),
			Params:          cloneParams(body.IssueUnitOrder.GetParams()),
		}
	case *pb.PlanningCommand_CancelUnitOrder:
		envelope.Intent = CancelUnitOrderIntent{UnitID: strings.TrimSpace(body.CancelUnitOrder.GetUnitId())}
	case *pb.PlanningCommand_PlanningPathPreviewRequest:
		return IntentEnvelope{}, false, nil
	case *pb.PlanningCommand_BuildStructurePreview:
		return IntentEnvelope{}, false, nil
	case *pb.PlanningCommand_SetBuildingRecipePreview:
		return IntentEnvelope{}, false, nil
	case *pb.PlanningCommand_SubmitTurn:
		envelope.Intent = SubmitTurnIntent{}
	default:
		return IntentEnvelope{}, false, transportproblem.InvalidRequest("unsupported planning command")
	}

	return envelope, true, nil
}

func ministerDirectiveIntent(msg *pb.MsgSetMinisterDirective) (SetMinisterDirectiveIntent, error) {
	if msg == nil {
		return SetMinisterDirectiveIntent{}, transportproblem.New("invalid_directive", "minister directive is nil")
	}
	if strings.TrimSpace(msg.GetMinisterRole()) != "domestic" {
		return SetMinisterDirectiveIntent{}, transportproblem.New("invalid_directive", "unsupported minister role")
	}
	var payload struct {
		DirectiveType string `json:"directive_type"`
		DraftID       string `json:"draft_id"`
	}
	if err := json.Unmarshal([]byte(msg.GetContent()), &payload); err != nil {
		return SetMinisterDirectiveIntent{}, transportproblem.New("invalid_directive", "invalid minister directive payload")
	}
	payload.DirectiveType = strings.TrimSpace(payload.DirectiveType)
	payload.DraftID = strings.TrimSpace(payload.DraftID)
	switch payload.DirectiveType {
	case "accept", "reject":
	default:
		return SetMinisterDirectiveIntent{}, transportproblem.New("invalid_directive", "unsupported minister directive type")
	}
	if payload.DraftID == "" {
		return SetMinisterDirectiveIntent{}, transportproblem.New("invalid_directive", "draft_id is required")
	}
	return SetMinisterDirectiveIntent{
		MinisterRole:  "domestic",
		DirectiveType: payload.DirectiveType,
		DraftID:       payload.DraftID,
	}, nil
}
