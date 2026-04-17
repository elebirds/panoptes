package planning

import (
	"strings"

	pb "github.com/elebirds/panoptes/internal/gen/proto"
	cmddispatch "github.com/elebirds/panoptes/internal/transport/dispatch"
	transportproblem "github.com/elebirds/panoptes/internal/transport/problem"
)

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
		return IntentEnvelope{}, false, transportproblem.New("invalid_directive", "minister is not part of current MVP")
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
