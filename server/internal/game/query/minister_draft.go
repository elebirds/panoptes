package query

import (
	"encoding/json"
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
)

func BuildMinisterDraftViews(state *domain.GameState, playerID string) []*pb.MinisterDraftView {
	if state == nil {
		return nil
	}
	drafts := state.TurnRuntime.Planning.MinisterDraftsForPlayer(playerID)
	if len(drafts) == 0 {
		return nil
	}
	out := make([]*pb.MinisterDraftView, 0, len(drafts))
	for _, draft := range drafts {
		payload, err := json.Marshal(draft)
		if err != nil {
			continue
		}
		out = append(out, &pb.MinisterDraftView{
			MinisterRole: strings.TrimSpace(draft.MinisterRole),
			JsonPayload:  string(payload),
			Available:    draft.Available,
		})
	}
	return out
}

func BuildMinisterProposalViews(state *domain.GameState, playerID string) []*pb.MinisterProposalView {
	if state == nil {
		return nil
	}
	drafts := state.TurnRuntime.Planning.MinisterDraftsForPlayer(playerID)
	if len(drafts) == 0 {
		return nil
	}
	out := make([]*pb.MinisterProposalView, 0, len(drafts))
	for _, draft := range drafts {
		if !draft.Available || draft.Status != domain.MinisterDraftStatusPending {
			continue
		}
		payload, err := json.Marshal(draft)
		if err != nil {
			continue
		}
		out = append(out, &pb.MinisterProposalView{
			ProposalId:      strings.TrimSpace(draft.DraftID),
			MinisterRole:    strings.TrimSpace(draft.MinisterRole),
			Kind:            string(draft.Kind),
			Title:           strings.TrimSpace(draft.Title),
			Summary:         strings.TrimSpace(draft.Summary),
			Rationale:       strings.TrimSpace(draft.Rationale),
			RiskNote:        strings.TrimSpace(draft.RiskNote),
			ProposedCommand: commandEnvelopeForMinisterDraft(draft),
			RawJson:         string(payload),
		})
	}
	return out
}

func commandEnvelopeForMinisterDraft(draft domain.MinisterDraft) *pb.CommandEnvelope {
	envelope := &pb.CommandEnvelope{
		CommandId:     strings.TrimSpace(draft.DraftID),
		ParticipantId: strings.TrimSpace(draft.PlayerID),
		Source:        "minister:" + strings.TrimSpace(draft.MinisterRole),
		Turn:          int32(draft.Turn),
	}
	switch draft.Kind {
	case domain.MinisterDraftKindResearch:
		envelope.Body = &pb.CommandEnvelope_SetResearchTarget{
			SetResearchTarget: &pb.MsgSetResearchTarget{TechnologyId: strings.TrimSpace(draft.TargetID)},
		}
	case domain.MinisterDraftKindPolicy:
		envelope.Body = &pb.CommandEnvelope_SetPolicy{
			SetPolicy: &pb.MsgSetPolicy{NationalPolicyId: strings.TrimSpace(draft.TargetID)},
		}
	case domain.MinisterDraftKindInstitution:
		envelope.Body = &pb.CommandEnvelope_SetInstitutionLoadout{
			SetInstitutionLoadout: &pb.MsgSetInstitutionLoadout{PolicyIds: append([]string(nil), draft.PolicyIDs...)},
		}
	case domain.MinisterDraftKindBuild:
		envelope.Body = &pb.CommandEnvelope_BuildStructure{
			BuildStructure: &pb.MsgBuildStructure{
				NodeId:         strings.TrimSpace(draft.NodeID),
				BuildingTypeId: strings.TrimSpace(draft.BuildingTypeID),
				CityId:         strings.TrimSpace(draft.CityID),
			},
		}
	case domain.MinisterDraftKindRecipe:
		envelope.Body = &pb.CommandEnvelope_SetBuildingRecipe{
			SetBuildingRecipe: &pb.MsgSetBuildingRecipe{
				NodeId:   strings.TrimSpace(draft.NodeID),
				RecipeId: strings.TrimSpace(draft.RecipeID),
			},
		}
	case domain.MinisterDraftKindUnitOrder:
		envelope.Body = &pb.CommandEnvelope_IssueUnitOrder{
			IssueUnitOrder: &pb.MsgIssueUnitOrder{
				UnitId:          strings.TrimSpace(draft.UnitID),
				Action:          strings.TrimSpace(draft.Action),
				TargetNodeId:    strings.TrimSpace(draft.TargetNodeID),
				TargetUnitId:    strings.TrimSpace(draft.TargetUnitID),
				SecondaryNodeId: strings.TrimSpace(draft.SecondaryNodeID),
				Params:          cloneDraftParams(draft.Params),
			},
		}
	default:
		return nil
	}
	return envelope
}

func cloneDraftParams(src map[string]string) map[string]string {
	if len(src) == 0 {
		return nil
	}
	dst := make(map[string]string, len(src))
	for key, value := range src {
		dst[key] = value
	}
	return dst
}

func BuildMinisterRosterViews() []*pb.MinisterView {
	ministers := staticdata.Default().Ministers()
	if len(ministers) == 0 {
		return nil
	}
	out := make([]*pb.MinisterView, 0, len(ministers))
	for _, minister := range ministers {
		role := strings.TrimSpace(minister.Role)
		if role == "" {
			continue
		}
		out = append(out, &pb.MinisterView{
			Role:        role,
			Name:        strings.TrimSpace(minister.Name),
			Ability:     int32(minister.Ability),
			Personality: strings.TrimSpace(minister.Personality),
		})
	}
	return out
}
