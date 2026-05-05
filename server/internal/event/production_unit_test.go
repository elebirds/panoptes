// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载领域事件写入逻辑拆分后的事件族或事件辅助逻辑。

package event

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func TestUnitStarvingEventApplyRemovesDeadUnitViaSharedMutation(t *testing.T) {
	world := donburi.NewWorld()
	unitEntity := ecs.CreateUnit(world, string(domain.UnitTypeSettler), "player-1", domain.Position{Q: 0, R: 0})
	unitEntry := world.Entry(unitEntity)
	unitID := ecs.UnitStatsC.Get(unitEntry).ID

	UnitStarvingEvent{UnitID: unitID, DamagePerTurn: 100}.Apply(world, nil)

	if _, ok := findUnitByID(world, unitID); ok {
		t.Fatalf("unit %s should be removed after lethal starvation damage", unitID)
	}
}

func TestUnitProducedEventApplyUsesSharedSpawnResolver(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
		Buildings: []staticdata.BuildingDefinition{
			{ID: "barracks", MaxHP: 80},
			{ID: "farm", MaxHP: 60},
		},
		Units: []staticdata.UnitDefinition{
			{ID: "infantry", Class: "melee", MaxHP: 30, Attack: 10, AttackRange: 1, MoveRange: 2, VisionRange: 3},
		},
	}))

	world := donburi.NewWorld()
	originEntity := ecs.CreateNode(world, ecs.MapNode{ID: "C1", Q: 0, R: 0, Terrain: "plain"})
	firstRingEntity := ecs.CreateNode(world, ecs.MapNode{ID: "N1", Q: 1, R: 0, Terrain: "plain"})
	secondRingEntity := ecs.CreateNode(world, ecs.MapNode{ID: "N2", Q: 2, R: 0, Terrain: "plain"})
	thirdRingEntity := ecs.CreateNode(world, ecs.MapNode{ID: "N3", Q: 3, R: 0, Terrain: "plain"})
	fourthRingEntity := ecs.CreateNode(world, ecs.MapNode{ID: "N4", Q: 4, R: 0, Terrain: "plain"})
	originEntry := world.Entry(originEntity)
	firstRingEntry := world.Entry(firstRingEntity)
	ecs.CreateBuilding(world, "barracks", "player-1", "C1", originEntry)
	ecs.CreateBuilding(world, "farm", "player-2", "", firstRingEntry)

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{
		ID: "default",
		NodeIndex: map[string]donburi.Entity{
			"C1": originEntity,
			"N1": firstRingEntity,
			"N2": secondRingEntity,
			"N3": thirdRingEntity,
			"N4": fourthRingEntity,
		},
	})
	state.World = world

	UnitProducedEvent{NodeID: "C1", UnitType: "infantry", Faction: "player-1", Count: 1}.Apply(world, state)

	if got := domain.GetUnitsByNode(world, domain.Position{Q: 0, R: 0}); len(got) != 0 {
		t.Fatalf("units at origin = %d, want 0", len(got))
	}
	if got := domain.GetUnitsByNode(world, domain.Position{Q: 1, R: 0}); len(got) != 0 {
		t.Fatalf("units on blocked first ring = %d, want 0", len(got))
	}
	if got := domain.GetUnitsByNode(world, domain.Position{Q: 2, R: 0}); len(got) != 1 {
		t.Fatalf("units on resolved spawn tile = %d, want 1", len(got))
	}
}

func TestRecipeCompletedEventApplySkipsUnitWhenNoSafeSpawnExists(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
		Buildings: []staticdata.BuildingDefinition{
			{ID: "workshop", MaxHP: 80},
		},
		Units: []staticdata.UnitDefinition{
			{ID: "infantry", Class: "melee", MaxHP: 30, Attack: 10, AttackRange: 1, MoveRange: 2, VisionRange: 3},
		},
	}))

	world := donburi.NewWorld()
	originEntity := ecs.CreateNode(world, ecs.MapNode{ID: "C1", Q: 0, R: 0, Terrain: "plain"})
	originEntry := world.Entry(originEntity)
	ecs.CreateBuilding(world, "workshop", "player-1", "C1", originEntry)

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{
		ID: "default",
		NodeIndex: map[string]donburi.Entity{
			"C1": originEntity,
		},
	})
	state.World = world

	RecipeCompletedEvent{NodeID: "C1", Owner: "player-1", Units: []string{"infantry"}}.Apply(world, state)

	count := 0
	ecs.AllUnits(world).Each(world, func(entry *donburi.Entry) {
		if entry != nil {
			count++
		}
	})
	if count != 0 {
		t.Fatalf("spawned unit count = %d, want 0 when no safe tile exists", count)
	}
}
