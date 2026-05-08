package query

import (
	"encoding/json"
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/ministerroles"
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
			MinisterRole: ministerroles.Canonical(draft.MinisterRole),
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
			ProposalId:        strings.TrimSpace(draft.DraftID),
			MinisterRole:      ministerroles.Canonical(draft.MinisterRole),
			Kind:              string(draft.Kind),
			Title:             strings.TrimSpace(draft.Title),
			Summary:           strings.TrimSpace(draft.Summary),
			Rationale:         strings.TrimSpace(draft.Rationale),
			RiskNote:          strings.TrimSpace(draft.RiskNote),
			ProposedCommand:   commandEnvelopeForMinisterDraft(draft),
			OperationCommands: operationCommandViewsForMinisterDraft(draft),
			OperationId:       strings.TrimSpace(draft.OperationID),
			Objective:         strings.TrimSpace(draft.Objective),
			RawJson:           string(payload),
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
			SetInstitutionLoadout: &pb.MsgSetInstitutionLoadout{InstitutionIds: append([]string(nil), draft.InstitutionIDs...)},
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
	case domain.MinisterDraftKindOperation:
		return nil
	default:
		return nil
	}
	return envelope
}

func operationCommandViewsForMinisterDraft(draft domain.MinisterDraft) []*pb.MinisterOperationCommandView {
	if draft.Kind != domain.MinisterDraftKindOperation || len(draft.OperationSteps) == 0 {
		return nil
	}
	out := make([]*pb.MinisterOperationCommandView, 0, len(draft.OperationSteps))
	for _, step := range draft.OperationSteps {
		envelope := commandEnvelopeForMinisterDraft(step)
		if envelope == nil {
			continue
		}
		out = append(out, &pb.MinisterOperationCommandView{
			Label:   strings.TrimSpace(step.TargetLabel),
			Kind:    string(step.Kind),
			Command: envelope,
			RawJson: marshalDraftJSON(step),
		})
	}
	return out
}

func marshalDraftJSON(draft domain.MinisterDraft) string {
	payload, err := json.Marshal(draft)
	if err != nil {
		return ""
	}
	return string(payload)
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
	return BuildMinisterRosterViewsForPlayer(nil, "")
}

func BuildMinisterRosterViewsForPlayer(state *domain.GameState, playerID string) []*pb.MinisterView {
	ministers := ministerRosterForPlayer(state, playerID)
	out := make([]*pb.MinisterView, 0, len(ministerroles.OrderedRoles()))
	for _, role := range ministerroles.OrderedRoles() {
		minister, ok := ministers[role]
		if !ok {
			out = append(out, ministerViewFromMinister(role, staticdata.Minister{}, true))
			continue
		}
		out = append(out, ministerViewFromMinister(role, minister, false))
	}
	return out
}

func BuildMinisterCandidateViewsForPlayer(state *domain.GameState, playerID string) []*pb.MinisterCandidateView {
	candidates := ministerCandidateForPlayer(state, playerID)
	if len(candidates) == 0 {
		return nil
	}
	out := make([]*pb.MinisterCandidateView, 0, len(candidates))
	for _, role := range ministerroles.OrderedRoles() {
		minister, ok := candidates[role]
		if !ok {
			continue
		}
		out = append(out, ministerCandidateViewFromMinister(role, minister))
	}
	return out
}

func ministerRosterForPlayer(state *domain.GameState, playerID string) map[string]staticdata.Minister {
	if state != nil {
		if playerState := state.Players[playerID]; playerState != nil && playerState.MinisterRoster != nil {
			return playerState.MinisterRoster
		}
	}
	return normalizeMinisterRoster(staticdata.Default().Ministers())
}

func ministerCandidateForPlayer(state *domain.GameState, playerID string) map[string]staticdata.Minister {
	if state == nil {
		return nil
	}
	if playerState := state.Players[playerID]; playerState != nil && len(playerState.MinisterCandidates) > 0 {
		return playerState.MinisterCandidates
	}
	return nil
}

func normalizeMinisterRoster(pool []staticdata.Minister) map[string]staticdata.Minister {
	if len(pool) == 0 {
		return nil
	}
	ministers := ministerroles.NormalizeMinisters(pool)
	out := make(map[string]staticdata.Minister, len(ministerroles.OrderedRoles()))
	for _, minister := range ministers {
		role := ministerroles.Canonical(minister.Role)
		if role == "" {
			continue
		}
		out[role] = minister
	}
	return out
}

func ministerViewFromMinister(role string, minister staticdata.Minister, vacant bool) *pb.MinisterView {
	role = ministerroles.Canonical(role)
	if role == "" {
		return nil
	}
	minister.Role = role
	if strings.TrimSpace(minister.IconKey) == "" {
		minister.IconKey = role
	}
	ministerID := strings.TrimSpace(minister.ID)
	ministerName := strings.TrimSpace(minister.Name)
	vacated := vacant || ministerID == ""
	if vacated {
		ministerID = ""
		ministerName = "空缺"
	}
	return &pb.MinisterView{
		MinisterId:      ministerID,
		Role:            role,
		Name:            ministerName,
		IconKey:         strings.TrimSpace(minister.IconKey),
		Personality:     strings.TrimSpace(minister.Personality),
		PersonalityDesc: strings.TrimSpace(minister.PersonalityDesc),
		Ability:         int32(minister.Ability),
		Loyalty:         int32(minister.Loyalty),
		Ambition:        int32(minister.Ambition),
		Cautiousness:    int32(minister.Cautiousness),
		Decisiveness:    int32(minister.Decisiveness),
		LoyaltyTendency: int32(minister.LoyaltyTendency),
		AmbitionStyle:   int32(minister.AmbitionStyle),
		Vacant:          vacated,
	}
}

func ministerCandidateViewFromMinister(role string, minister staticdata.Minister) *pb.MinisterCandidateView {
	role = ministerroles.Canonical(role)
	if role == "" {
		return nil
	}
	minister.Role = role
	if strings.TrimSpace(minister.IconKey) == "" {
		minister.IconKey = role
	}
	return &pb.MinisterCandidateView{
		MinisterId:      strings.TrimSpace(minister.ID),
		Role:            role,
		Name:            strings.TrimSpace(minister.Name),
		IconKey:         strings.TrimSpace(minister.IconKey),
		Personality:     strings.TrimSpace(minister.Personality),
		PersonalityDesc: strings.TrimSpace(minister.PersonalityDesc),
		Ability:         int32(minister.Ability),
		Loyalty:         int32(minister.Loyalty),
		Ambition:        int32(minister.Ambition),
		Cautiousness:    int32(minister.Cautiousness),
		Decisiveness:    int32(minister.Decisiveness),
		LoyaltyTendency: int32(minister.LoyaltyTendency),
		AmbitionStyle:   int32(minister.AmbitionStyle),
	}
}
