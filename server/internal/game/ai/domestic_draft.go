package ai

import (
	"context"
	"strings"

	"github.com/elebirds/panoptes/internal/game/planning"
	"github.com/elebirds/panoptes/internal/staticdata"
)

type DomesticDraftCandidate struct {
	Kind        string
	TargetID    string
	TargetLabel string
}

func BuildDomesticDraftCandidates(_ context.Context, req Request) []DomesticDraftCandidate {
	if req.State == nil || strings.TrimSpace(req.Participant.ID) == "" {
		return nil
	}

	planner := newRuleBotPlanner(req)
	out := make([]DomesticDraftCandidate, 0, 2)

	if intent, ok := planner.chooseResearchIntent(); ok {
		if typed, ok := intent.(planning.SetResearchTargetIntent); ok {
			out = append(out, DomesticDraftCandidate{
				Kind:        "research",
				TargetID:    typed.TechnologyID,
				TargetLabel: technologyLabel(typed.TechnologyID),
			})
		}
	}
	if intent, ok := planner.chooseNationalPolicyIntent(); ok {
		if typed, ok := intent.(planning.SetPolicyIntent); ok {
			out = append(out, DomesticDraftCandidate{
				Kind:        "policy",
				TargetID:    typed.NationalPolicyID,
				TargetLabel: policyLabel(typed.NationalPolicyID),
			})
		}
	}

	return out
}

func technologyLabel(technologyID string) string {
	technologyID = strings.TrimSpace(technologyID)
	if technologyID == "" {
		return ""
	}
	if tech, ok := staticdata.Default().GetTechnology(technologyID); ok && strings.TrimSpace(tech.Name) != "" {
		return strings.TrimSpace(tech.Name)
	}
	return technologyID
}

func policyLabel(policyID string) string {
	policyID = strings.TrimSpace(policyID)
	if policyID == "" {
		return ""
	}
	if policy, ok := staticdata.Default().GetPolicy(policyID); ok && strings.TrimSpace(policy.Name) != "" {
		return strings.TrimSpace(policy.Name)
	}
	return policyID
}
