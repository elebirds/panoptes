// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载规则 AI 规划器拆分后的候选、评分与意图生成逻辑。

package ai

import (
	"slices"
	"sort"
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
	slotCount := p.player.Institutions.SlotCount
	if slotCount <= 0 {
		return nil, false
	}

	candidates := make([]struct {
		key   string
		score int
	}, 0)
	for _, policyID := range p.player.Institutions.CandidateIDs() {
		policy, ok := staticdata.Default().GetPolicy(policyID)
		if !ok || !strings.EqualFold(policy.Layer, "institutional") || !prerequisitesMet(p.req.State, p.playerID, policy.Prerequisites) {
			continue
		}
		candidates = append(candidates, struct {
			key   string
			score int
		}{key: policyID, score: p.scoreInstitutionPolicy(policy)})
	}
	if len(candidates) == 0 {
		return nil, false
	}
	sort.Slice(candidates, func(i, j int) bool {
		if candidates[i].score == candidates[j].score {
			return candidates[i].key < candidates[j].key
		}
		return candidates[i].score > candidates[j].score
	})
	threshold := candidates[0].score
	tied := candidates[:0]
	for _, candidate := range candidates {
		if candidate.score < threshold {
			break
		}
		tied = append(tied, candidate)
	}
	if len(tied) > slotCount {
		p.rng.Shuffle(len(tied), func(i, j int) {
			tied[i], tied[j] = tied[j], tied[i]
		})
		tied = tied[:slotCount]
	}
	selected := make([]string, 0, min(slotCount, len(candidates)))
	if len(tied) > 0 {
		for _, candidate := range tied {
			selected = append(selected, candidate.key)
		}
	}
	if len(selected) < slotCount {
		for _, candidate := range candidates[len(tied):] {
			selected = append(selected, candidate.key)
			if len(selected) >= slotCount {
				break
			}
		}
	}
	selected = domain.NormalizePolicyIDList(selected)
	if slices.Equal(selected, p.player.Institutions.ActivePolicyIDs) {
		return nil, false
	}
	return planning.SetInstitutionLoadoutIntent{PolicyIDs: selected}, true
}
