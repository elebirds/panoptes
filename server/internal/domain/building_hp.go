// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: Defines building hit point refresh helpers.

package domain

import (
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func (s *GameState) RefreshBuildingMaxHPForPlayer(playerID string) {
	if s == nil || s.World == nil || playerID == "" {
		return
	}
	newNodeQuery().Each(s.World, func(entry *donburi.Entry) {
		if entry == nil || !entry.HasComponent(BuildingC) {
			return
		}
		building := BuildingC.Get(entry)
		if building.Owner != playerID {
			return
		}
		s.RefreshBuildingMaxHPAtEntry(entry)
	})
}

func (s *GameState) RefreshBuildingMaxHPAtEntry(entry *donburi.Entry) {
	if s == nil || entry == nil || !entry.HasComponent(BuildingC) {
		return
	}
	building := BuildingC.Get(entry)
	cfg, ok := staticdata.Default().GetBuilding(string(building.Type))
	if !ok {
		return
	}
	base := cfg.MaxHP
	if base <= 0 {
		return
	}
	maxHP := s.ApplyScalarModifier(building.Owner, string(staticdata.ModifierTriggerBuildingMaxHP), string(building.Type), "", base)
	if maxHP <= 0 {
		maxHP = 1
	}
	if maxHP == building.MaxHP {
		if building.HP > building.MaxHP {
			building.HP = building.MaxHP
		}
		return
	}
	delta := maxHP - building.MaxHP
	building.MaxHP = maxHP
	building.HP += delta
	if building.HP > building.MaxHP {
		building.HP = building.MaxHP
	}
	if building.HP < 0 {
		building.HP = 0
	}
}
