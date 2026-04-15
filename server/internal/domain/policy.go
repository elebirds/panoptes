// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-15 22:30:00 +0800
// Description: 定义 planning 阶段的政策与研究目标草案辅助。

package domain

import "github.com/elebirds/panoptes/internal/staticdata"

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

func (p *PlanningInputs) HasBuildOrder(playerID string, nodeID string) bool {
	if p == nil {
		return false
	}
	for _, order := range p.BuildOrders {
		if order.PlayerID == playerID && order.NodeID == nodeID {
			return true
		}
	}
	return false
}

func (p *PlanningInputs) UpsertBuildOrder(order BuildOrder) bool {
	if p == nil {
		return false
	}
	for idx, existing := range p.BuildOrders {
		if existing.PlayerID == order.PlayerID && existing.NodeID == order.NodeID {
			p.BuildOrders[idx] = order
			return true
		}
	}
	p.BuildOrders = append(p.BuildOrders, order)
	return false
}

func (p *PlanningInputs) UpsertRecipeSelection(order RecipeSelectionOrder) bool {
	if p == nil {
		return false
	}
	for idx, existing := range p.RecipeSelections {
		if existing.PlayerID == order.PlayerID && existing.NodeID == order.NodeID {
			p.RecipeSelections[idx] = order
			return true
		}
	}
	p.RecipeSelections = append(p.RecipeSelections, order)
	return false
}

func (p *PlanningInputs) UpsertWarDirective(playerID string, directive WarZoneDirective) bool {
	p.EnsureDraftMaps()
	directives := p.WarDirectives[playerID]
	for idx, existing := range directives {
		if existing.ZoneID == directive.ZoneID {
			directives[idx] = directive
			p.WarDirectives[playerID] = directives
			return true
		}
	}
	p.WarDirectives[playerID] = append(directives, directive)
	return false
}

type ResolvedExplicitEffects struct {
	UnlockBuildingIDs []string
	UnlockRecipeIDs   []string
	GrantResources    ResourceBag
	GrantUnitTypes    []string
}

func ResolveExplicitEffects(effects []staticdata.ExplicitEffect) ResolvedExplicitEffects {
	resolved := ResolvedExplicitEffects{
		GrantResources: NewResourceBag(),
	}
	for _, effect := range effects {
		switch effect.Type {
		case "unlock_building":
			if effect.TargetID != "" {
				resolved.UnlockBuildingIDs = append(resolved.UnlockBuildingIDs, effect.TargetID)
			}
		case "unlock_recipe":
			if effect.TargetID != "" {
				resolved.UnlockRecipeIDs = append(resolved.UnlockRecipeIDs, effect.TargetID)
			}
		case "grant":
			for key, value := range effect.GrantResources {
				resolved.GrantResources.AddAmount(ResourceKey(key), value)
			}
			resolved.GrantUnitTypes = append(resolved.GrantUnitTypes, effect.GrantUnits...)
		}
	}
	return resolved
}
