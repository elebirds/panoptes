// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 验证ECS 适配层的实体工厂与默认装配。

package ecs

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func TestCreateNodeAndFindNodeByID(t *testing.T) {
	world := donburi.NewWorld()

	entity := CreateNode(world, MapNode{
		ID:              "K10",
		X:               10,
		Y:               9,
		Terrain:         "mountain",
		IsResourcePoint: true,
		ResourceType:    "ore",
	})

	entry := world.Entry(entity)
	pos := donburi.Get[PositionComp](entry, PositionC)
	if pos.X != 10 || pos.Y != 9 {
		t.Fatalf("Position = %#v", pos)
	}

	node := donburi.Get[NodeComp](entry, NodeC)
	if node.ID != "K10" || node.Terrain != domain.TerrainMountain {
		t.Fatalf("Node = %#v", node)
	}
	if !node.IsResource || node.ResourceType != "ore" {
		t.Fatalf("resource node = %#v", node)
	}

	found, ok := FindNodeByID(world, "K10")
	if !ok {
		t.Fatalf("FindNodeByID() missed")
	}
	if found.Entity() != entity {
		t.Fatalf("FindNodeByID() entity mismatch")
	}
}

func TestCreateUnitAttachesAbilityComponents(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Units: []staticdata.UnitDefinition{
			{ID: "cavalry", MaxHP: 25, Attack: 12, MoveRange: 3, AttackRange: 1, ChargeBonus: 1.5},
			{ID: "archer", MaxHP: 20, Attack: 8, MoveRange: 1, AttackRange: 2},
			{ID: "siege", MaxHP: 35, Attack: 5, MoveRange: 1, AttackRange: 1, Flags: staticdata.UnitFlags{CanSiege: true, SiegeMultiplier: 3}},
			{ID: "saboteur", MaxHP: 20, Attack: 5, MoveRange: 2, AttackRange: 1, Flags: staticdata.UnitFlags{CanDestroyRoad: true, DestroyMultiplier: 3}},
		},
	}))

	world := donburi.NewWorld()
	cavalry := world.Entry(CreateUnit(world, "cavalry", "player-1", domain.Position{X: 2, Y: 3}))
	if !cavalry.HasComponent(ChargeAbilityC) {
		t.Fatalf("cavalry missing charge ability")
	}
	if cavalry.HasComponent(RangedAbilityC) {
		t.Fatalf("cavalry should not be ranged")
	}

	archer := world.Entry(CreateUnit(world, "archer", "player-1", domain.Position{X: 1, Y: 1}))
	if !archer.HasComponent(RangedAbilityC) {
		t.Fatalf("archer missing ranged ability")
	}

	siege := world.Entry(CreateUnit(world, "siege", "player-1", domain.Position{X: 4, Y: 5}))
	if !siege.HasComponent(SiegeAbilityC) {
		t.Fatalf("siege missing siege ability")
	}

	saboteur := world.Entry(CreateUnit(world, "saboteur", "player-2", domain.Position{X: 6, Y: 7}))
	if !saboteur.HasComponent(DestroyAbilityC) {
		t.Fatalf("saboteur missing destroy ability")
	}
}

func TestCreateBuildingSetsNodeOwner(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Buildings: []staticdata.BuildingDefinition{
			{ID: "wall", MaxHP: 50},
		},
	}))

	world := donburi.NewWorld()
	nodeEntity := CreateNode(world, MapNode{ID: "A1", X: 0, Y: 0, Terrain: "plain"})
	nodeEntry := world.Entry(nodeEntity)

	building := world.Entry(CreateBuilding(world, "wall", "player-1", "", nodeEntry))
	comp := donburi.Get[BuildingComp](building, BuildingC)
	if comp.Type != domain.BuildingTypeWall || comp.HP != 50 || comp.Owner != "player-1" {
		t.Fatalf("Building = %#v", comp)
	}

	node := donburi.Get[NodeComp](nodeEntry, NodeC)
	if node.Owner != "player-1" {
		t.Fatalf("node owner = %q", node.Owner)
	}
}

func TestCreateBuildingStoresOriginCityID(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Buildings: []staticdata.BuildingDefinition{
			{ID: "farm", MaxHP: 40},
		},
	}))

	world := donburi.NewWorld()
	nodeEntity := CreateNode(world, MapNode{ID: "B2", X: 1, Y: 1, Terrain: "plain"})
	nodeEntry := world.Entry(nodeEntity)

	building := world.Entry(CreateBuilding(world, "farm", "player-1", "city-a", nodeEntry))
	comp := donburi.Get[BuildingComp](building, BuildingC)
	if comp.CityID != "city-a" {
		t.Fatalf("origin city id = %q", comp.CityID)
	}
}
