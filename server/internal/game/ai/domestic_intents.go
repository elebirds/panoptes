// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载规则 AI 规划器拆分后的候选、评分与意图生成逻辑。

package ai

import (
	"slices"
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/game/planning"
	"github.com/elebirds/panoptes/internal/staticdata"
)

func (p *ruleBotPlanner) chooseResearchIntent() (planning.Intent, bool) {
	if p.player == nil || p.req.State == nil {
		return nil, false
	}
	if strings.TrimSpace(p.req.State.TurnRuntime.Planning.PendingResearchTarget(p.playerID)) != "" {
		return nil, false
	}
	if strings.TrimSpace(p.player.Research.CurrentTargetTechnologyID) != "" {
		return nil, false
	}

	candidates := make([]scoredIntent, 0)
	for _, tech := range staticdata.Default().Technologies() {
		techID := strings.TrimSpace(tech.ID)
		if techID == "" || p.player.Research.HasTechnology(techID) || p.player.Research.HasCompletedTechnology(techID) {
			continue
		}
		if !prerequisitesMet(p.req.State, p.playerID, tech.Prerequisites) {
			continue
		}
		score := p.scoreTechnology(tech)
		candidates = append(candidates, scoredIntent{
			key:   techID,
			score: score,
			intent: planning.SetResearchTargetIntent{
				TechnologyID: techID,
			},
		})
	}
	intent, ok := pickBestScoredIntent(p.rng, candidates)
	return intent, ok
}

func (p *ruleBotPlanner) chooseNationalPolicyIntent() (planning.Intent, bool) {
	if p.player == nil || p.req.State == nil {
		return nil, false
	}
	if pending := strings.TrimSpace(string(p.req.State.TurnRuntime.Planning.PendingPolicy(p.playerID))); pending != "" {
		return nil, false
	}

	candidates := make([]scoredIntent, 0)
	for _, policy := range staticdata.Default().Policies() {
		if !strings.EqualFold(policy.Layer, "national") {
			continue
		}
		policyID := strings.TrimSpace(policy.ID)
		if policyID == "" || !prerequisitesMet(p.req.State, p.playerID, policy.Prerequisites) {
			continue
		}
		score := p.scoreNationalPolicy(policy)
		if domain.Policy(policyID) == p.player.Policy {
			score -= 20
		}
		candidates = append(candidates, scoredIntent{
			key:   policyID,
			score: score,
			intent: planning.SetPolicyIntent{
				NationalPolicyID: policyID,
			},
		})
	}
	intent, ok := pickBestScoredIntent(p.rng, candidates)
	if !ok {
		return nil, false
	}
	selected, ok := intent.(planning.SetPolicyIntent)
	if !ok || domain.Policy(selected.NationalPolicyID) == p.player.Policy {
		return nil, false
	}
	return intent, true
}

func (p *ruleBotPlanner) chooseInstitutionIntent() (planning.Intent, bool) {
	if p.player == nil || p.req.State == nil {
		return nil, false
	}
	if p.req.State.TurnRuntime.Planning.HasPendingInstitutionLoadout(p.playerID) {
		return nil, false
	}
	candidates := make([]scoredIntent, 0)
	for _, institutionID := range p.player.Institutions.CandidateIDs() {
		institution, ok := staticdata.Default().GetInstitution(institutionID)
		if !ok || !prerequisitesMet(p.req.State, p.playerID, institution.Prerequisites) {
			continue
		}
		selected := replaceInstitutionInCategory(p.player.Institutions.ActiveInstitutionIDs, institutionID, institution.Category)
		normalized, errCode := planning.ValidateInstitutionLoadout(p.req.State, p.playerID, p.player, selected)
		if errCode != "" || slices.Equal(normalized, p.player.Institutions.ActiveInstitutionIDs) {
			continue
		}
		candidates = append(candidates, scoredIntent{
			key:   institutionID,
			score: p.scoreInstitution(institution),
			intent: planning.SetInstitutionLoadoutIntent{
				InstitutionIDs: normalized,
			},
		})
	}
	return pickBestScoredIntent(p.rng, candidates)
}

func replaceInstitutionInCategory(active []string, institutionID string, category string) []string {
	selected := make([]string, 0, len(active)+1)
	replaced := false
	for _, activeID := range active {
		activeInstitution, ok := staticdata.Default().GetInstitution(activeID)
		if ok && strings.EqualFold(activeInstitution.Category, category) {
			if !replaced {
				selected = append(selected, institutionID)
				replaced = true
			}
			continue
		}
		selected = append(selected, activeID)
	}
	if !replaced {
		selected = append(selected, institutionID)
	}
	return domain.NormalizeInstitutionIDList(selected)
}
