// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-15 22:30:00 +0800
// Description: 定义 planning 阶段的政策与研究目标草案辅助。

package domain

func (p *PlanningInputs) EnsureDraftMaps() {
	if p == nil {
		return
	}
	if p.MinisterDirectives == nil {
		p.MinisterDirectives = make(map[string]string)
	}
	if p.PendingPolicies == nil {
		p.PendingPolicies = make(map[string]Policy)
	}
	if p.PendingResearch == nil {
		p.PendingResearch = make(map[string]string)
	}
	if p.WarDirectives == nil {
		p.WarDirectives = make(map[string][]WarZoneDirective)
	}
	if p.UnitOrders == nil {
		p.UnitOrders = make(map[string]UnitDirective)
	}
}

func (p *PlanningInputs) SetPendingPolicy(playerID string, policyID Policy) {
	p.EnsureDraftMaps()
	p.PendingPolicies[playerID] = policyID
}

func (p *PlanningInputs) PendingPolicy(playerID string) Policy {
	if p == nil || p.PendingPolicies == nil {
		return ""
	}
	return p.PendingPolicies[playerID]
}

func (p *PlanningInputs) SetPendingResearchTarget(playerID string, technologyID string) {
	p.EnsureDraftMaps()
	if technologyID == "" {
		delete(p.PendingResearch, playerID)
		return
	}
	p.PendingResearch[playerID] = technologyID
}

func (p *PlanningInputs) PendingResearchTarget(playerID string) string {
	if p == nil || p.PendingResearch == nil {
		return ""
	}
	return p.PendingResearch[playerID]
}
