// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 验证单位结算引擎的修正器集成行为。

package combat

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func TestSnapshotPhaseAppliesUnitAttackModifier(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Units: []staticdata.UnitDefinition{
			{ID: "infantry", Class: "melee", MaxHP: 30, Attack: 10, AttackRange: 1, MoveRange: 2, VisionRange: 3, TrainCost: staticdata.ResourceAmounts{}, Upkeep: staticdata.ResourceAmounts{}},
		},
		Technologies: []staticdata.TechnologyDefinition{
			{
				ID:           "weapon_mastery",
				Branch:       "military",
				Tier:         1,
				ResearchCost: 1,
				ModifierEffects: []staticdata.ModifierEffect{
					{Trigger: "unit.attack", TargetID: "infantry", ModifierType: "flat", Value: 3},
				},
			},
		},
	}))

	world := donburi.NewWorld()
	entity := ecs.CreateUnit(world, "infantry", "player-1", domain.Position{Q: 0, R: 0})
	entry := world.Entry(entity)
	unitID := ecs.UnitStatsC.Get(entry).ID

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	state.World = world
	state.Players["player-1"].Research.UnlockTechnology("weapon_mastery")
	ctx := &ResolutionContext{
		World:     world,
		State:     state,
		CurrentHP: make(map[string]int),
	}

	(SnapshotPhase{}).Apply(ctx)

	if got := ctx.Snapshot.Units[unitID].Attack; got != 13 {
		t.Fatalf("snapshot attack = %d, want 13", got)
	}
}

func TestSnapshotPhaseUsesUnifiedModifierOrderAcrossTypes(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Units: []staticdata.UnitDefinition{
			{ID: "infantry", Class: "melee", MaxHP: 30, Attack: 10, AttackRange: 1, MoveRange: 2, VisionRange: 3, TrainCost: staticdata.ResourceAmounts{}, Upkeep: staticdata.ResourceAmounts{}},
		},
		Technologies: []staticdata.TechnologyDefinition{
			{
				ID:           "combined_arms",
				Branch:       "military",
				Tier:         1,
				ResearchCost: 1,
				ModifierEffects: []staticdata.ModifierEffect{
					{Trigger: "unit.attack", TargetID: "infantry", ModifierType: "percent", Value: 0.5},
					{Trigger: "unit.attack", TargetID: "infantry", ModifierType: "flat", Value: 2},
					{Trigger: "unit.attack", TargetID: "infantry", ModifierType: "multiplier", Value: 2},
				},
			},
		},
	}))

	world := donburi.NewWorld()
	entity := ecs.CreateUnit(world, "infantry", "player-1", domain.Position{Q: 0, R: 0})
	entry := world.Entry(entity)
	unitID := ecs.UnitStatsC.Get(entry).ID

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	state.World = world
	state.Players["player-1"].Research.UnlockTechnology("combined_arms")
	ctx := &ResolutionContext{
		World:     world,
		State:     state,
		CurrentHP: make(map[string]int),
	}

	(SnapshotPhase{}).Apply(ctx)

	if got := ctx.Snapshot.Units[unitID].Attack; got != 34 {
		t.Fatalf("snapshot attack = %d, want 34", got)
	}
}
