// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: Defines domain state modifier helpers.

package domain

import (
	"math"

	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

// ActiveModifierEffects 只读取“已正式解锁”的科技/政策修正。
//
// 科技研究在当前语义下是回合末完成、下一回合生效，因此这里不暴露任何本回合
// 尚未 Apply 的临时解锁状态。
func (s *GameState) ActiveModifierEffects(playerID string) []staticdata.ModifierEffect {
	if s == nil {
		return nil
	}
	playerState, ok := s.Players[playerID]
	if !ok || playerState == nil {
		return nil
	}
	technologyIDs := make(map[string]struct{}, len(playerState.Research.UnlockedTechnologies))
	for technologyID := range playerState.Research.UnlockedTechnologies {
		technologyIDs[technologyID] = struct{}{}
	}
	effects := make([]staticdata.ModifierEffect, 0)
	for technologyID := range technologyIDs {
		technology, ok := staticdata.Default().GetTechnology(technologyID)
		if !ok {
			continue
		}
		effects = append(effects, technology.ModifierEffects...)
	}
	if playerState.Policy != "" {
		if policy, ok := staticdata.Default().GetPolicy(string(playerState.Policy)); ok {
			effects = append(effects, policy.ModifierEffects...)
		}
	}
	for _, policyID := range playerState.Institutions.ActivePolicyIDs {
		if policy, ok := staticdata.Default().GetPolicy(policyID); ok {
			effects = append(effects, policy.ModifierEffects...)
		}
	}
	if s.World != nil {
		newNodeQuery().Each(s.World, func(entry *donburi.Entry) {
			if entry == nil || !entry.HasComponent(BuildingC) {
				return
			}
			building := BuildingC.Get(entry)
			if building.Owner != playerID {
				return
			}
			if !BuildingOperationalAtTurn(entry, s.Turn) {
				return
			}
			cfg, ok := staticdata.Default().GetBuilding(string(building.Type))
			if !ok || len(cfg.ModifierEffects) == 0 {
				return
			}
			effects = append(effects, cfg.ModifierEffects...)
		})
	}
	return effects
}

// ApplyFloatModifier 实现统一的 percent -> flat -> multiplier 聚合顺序。
//
// 所有 trigger/key 型修正都通过这里读时计算，而不是把最终值预写回 ECS 或静态表。
func (s *GameState) ApplyFloatModifier(playerID string, trigger string, targetID string, modifierKey string, base float64) float64 {
	flat := 0.0
	percent := 0.0
	multiplier := 1.0
	for _, effect := range s.ActiveModifierEffects(playerID) {
		if effect.Trigger != trigger {
			continue
		}
		if effect.TargetID != "" && effect.TargetID != targetID {
			continue
		}
		if effect.ResourceKey != "" && effect.ResourceKey != modifierKey {
			continue
		}
		if effect.PointKey != "" && effect.PointKey != modifierKey {
			continue
		}
		switch effect.ModifierType {
		case "flat":
			flat += effect.Value
		case "percent":
			percent += effect.Value
		case "multiplier":
			multiplier *= effect.Value
		}
	}
	value := ((base * (1 + percent)) + flat) * multiplier
	if value < 0 {
		value = 0
	}
	return value
}

func (s *GameState) ApplyScalarModifier(playerID string, trigger string, targetID string, resourceKey string, base int) int {
	return int(math.Round(s.ApplyFloatModifier(playerID, trigger, targetID, resourceKey, float64(base))))
}

func (s *GameState) ApplyResourceModifiers(playerID string, trigger string, targetID string, base ResourceBag) ResourceBag {
	if base == nil {
		return nil
	}
	out := NewResourceBag()
	for _, key := range base.Keys() {
		out.Set(key, s.ApplyScalarModifier(playerID, trigger, targetID, string(key), base.Get(key)))
	}
	return out
}

func (s *GameState) ApplyPointModifiers(playerID string, trigger string, targetID string, base PointBag) PointBag {
	if base == nil {
		return nil
	}
	out := NewPointBag()
	for _, key := range base.Keys() {
		out.Set(key, s.ApplyScalarModifier(playerID, trigger, targetID, string(key), base.Get(key)))
	}
	return out
}

// 研究推进数值也走同一套 modifier 入口，避免研究系统和展示层再各自复制一份
// “研究产出增益”逻辑。
func (s *GameState) EffectiveResearchOutput(playerID string) int {
	if s == nil {
		return 0
	}
	playerState, ok := s.Players[playerID]
	if !ok || playerState == nil {
		return 0
	}
	base := playerState.Research.OutputPerTurn
	if base <= 0 {
		base = staticdata.Default().Rules().BaseResearchOutputPerTurn
	}
	return s.ApplyScalarModifier(playerID, string(staticdata.ModifierTriggerPointOutput), "", "research_output", base)
}

func (s *GameState) EffectiveIndustryOutput(playerID string) int {
	if s == nil {
		return 0
	}
	if _, ok := s.Players[playerID]; !ok {
		return 0
	}
	return s.ApplyScalarModifier(playerID, string(staticdata.ModifierTriggerPointOutput), "", "industry_output", staticdata.Default().Rules().BaseIndustryOutputPerTurn)
}

func (s *GameState) EffectiveRoadBaseCapacity(playerID string) int {
	if s == nil {
		return 0
	}
	if _, ok := s.Players[playerID]; !ok {
		return 0
	}
	return s.ApplyScalarModifier(playerID, string(staticdata.ModifierTriggerLogisticsRoadCapacity), "", "", staticdata.Default().Rules().RoadBaseCapacity)
}

func (s *GameState) EffectiveResearchCap(playerID string) int {
	if s == nil {
		return 0
	}
	playerState, ok := s.Players[playerID]
	if !ok || playerState == nil {
		return 0
	}
	if playerState.Research.ProgressCap > 0 {
		return playerState.Research.ProgressCap
	}
	return math.MaxInt / 4
}
