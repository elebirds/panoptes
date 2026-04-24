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
		Q:               10,
		R:               9,
		Terrain:         "mountain",
		IsResourcePoint: true,
		ResourceType:    "ore",
	})

	entry := world.Entry(entity)
	pos := donburi.Get[PositionComp](entry, PositionC)
	if pos.Q != 10 || pos.R != 9 {
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
	cavalry := world.Entry(CreateUnit(world, "cavalry", "player-1", domain.Position{Q: 2, R: 3}))
	if !cavalry.HasComponent(ChargeAbilityC) {
		t.Fatalf("cavalry missing charge ability")
	}
	if cavalry.HasComponent(RangedAbilityC) {
		t.Fatalf("cavalry should not be ranged")
	}

	archer := world.Entry(CreateUnit(world, "archer", "player-1", domain.Position{Q: 1, R: 1}))
	if !archer.HasComponent(RangedAbilityC) {
		t.Fatalf("archer missing ranged ability")
	}

	siege := world.Entry(CreateUnit(world, "siege", "player-1", domain.Position{Q: 4, R: 5}))
	if !siege.HasComponent(SiegeAbilityC) {
		t.Fatalf("siege missing siege ability")
	}

	saboteur := world.Entry(CreateUnit(world, "saboteur", "player-2", domain.Position{Q: 6, R: 7}))
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
	nodeEntity := CreateNode(world, MapNode{ID: "A1", Q: 0, R: 0, Terrain: "plain"})
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

func TestCreateUnitUsesAuthorSourcedStructureAttackCapability(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Units: []staticdata.UnitDefinition{
			{ID: "settler", Class: "civilian", MaxHP: 12, Attack: 0, MoveRange: 2, AttackRange: 0, Flags: staticdata.UnitFlags{CanAttackStructures: false}},
			{ID: "infantry", Class: "melee", MaxHP: 30, Attack: 10, MoveRange: 2, AttackRange: 1, Flags: staticdata.UnitFlags{CanAttackStructures: true}},
		},
	}))

	world := donburi.NewWorld()
	settler := world.Entry(CreateUnit(world, "settler", "player-1", domain.Position{Q: 0, R: 0}))
	infantry := world.Entry(CreateUnit(world, "infantry", "player-1", domain.Position{Q: 1, R: 0}))

	if ecsCaps := donburi.Get[UnitCapabilitiesComp](settler, UnitCapabilitiesC); ecsCaps.CanAttackStructures {
		t.Fatalf("settler should not inherit structure attack capability")
	}
	if ecsCaps := donburi.Get[UnitCapabilitiesComp](infantry, UnitCapabilitiesC); !ecsCaps.CanAttackStructures {
		t.Fatalf("infantry should inherit structure attack capability from static data")
	}
}

func TestCreateBuildingStoresBinding(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Buildings: []staticdata.BuildingDefinition{
			{ID: "farm", BuildingScope: "out_of_city", MaxHP: 40, TakeoverMode: "delayed"},
		},
	}))

	world := donburi.NewWorld()
	nodeEntity := CreateNode(world, MapNode{ID: "B2", Q: 1, R: 1, Terrain: "plain"})
	nodeEntry := world.Entry(nodeEntity)

	building := world.Entry(CreateBuilding(world, "farm", "player-1", "city-a", nodeEntry))
	comp := donburi.Get[BuildingComp](building, BuildingC)
	if comp.Owner != "player-1" {
		t.Fatalf("owner = %q, want player-1", comp.Owner)
	}
	if !building.HasComponent(BuildingBindingC) {
		t.Fatalf("building missing BuildingBindingC")
	}
	binding := donburi.Get[BuildingBindingComp](building, BuildingBindingC)
	if binding.Scope != BuildingScopeOutOfCity {
		t.Fatalf("binding scope = %q, want out_of_city", binding.Scope)
	}
	if binding.CityID != "city-a" || binding.ServiceCityID != "city-a" {
		t.Fatalf("binding = %#v, want city/service city-a", binding)
	}
}

func TestCreateBuildingAttachesCityScopeComponents(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			FacilityTakeoverTurns: 2,
		},
		Buildings: []staticdata.BuildingDefinition{
			{ID: "city_core", BuildingScope: "city_core", MaxHP: 100, TakeoverMode: "disabled"},
			{ID: "barracks", BuildingScope: "in_city", MaxHP: 80, TakeoverMode: "city_capture"},
			{ID: "farm", BuildingScope: "out_of_city", MaxHP: 60, TakeoverMode: "delayed"},
		},
	}))

	world := donburi.NewWorld()
	cityEntry := world.Entry(CreateNode(world, MapNode{ID: "C1", Q: 0, R: 0, Terrain: "plain"}))
	barracksEntry := world.Entry(CreateNode(world, MapNode{ID: "C2", Q: 1, R: 0, Terrain: "plain"}))
	farmEntry := world.Entry(CreateNode(world, MapNode{ID: "C3", Q: 2, R: 0, Terrain: "plain"}))

	CreateBuilding(world, "city_core", "player-1", "C1", cityEntry)
	CreateBuilding(world, "barracks", "player-1", "C1", barracksEntry)
	CreateBuilding(world, "farm", "player-1", "C1", farmEntry)

	for nodeID, entry := range map[string]*donburi.Entry{
		"C1": cityEntry,
		"C2": barracksEntry,
		"C3": farmEntry,
	} {
		if !entry.HasComponent(BuildingBindingC) {
			t.Fatalf("%s missing BuildingBindingC", nodeID)
		}
	}
	if got := donburi.Get[BuildingBindingComp](cityEntry, BuildingBindingC).Scope; got != BuildingScopeCityCore {
		t.Fatalf("city core scope = %q, want city_core", got)
	}
	if got := donburi.Get[BuildingBindingComp](barracksEntry, BuildingBindingC).Scope; got != BuildingScopeInCity {
		t.Fatalf("barracks scope = %q, want in_city", got)
	}
	farmBinding := donburi.Get[BuildingBindingComp](farmEntry, BuildingBindingC)
	if farmBinding.Scope != BuildingScopeOutOfCity {
		t.Fatalf("farm scope = %q, want out_of_city", farmBinding.Scope)
	}
	if farmBinding.CityID != "C1" || farmBinding.ServiceCityID != "C1" {
		t.Fatalf("farm binding = %#v, want C1/C1", farmBinding)
	}
	if !farmEntry.HasComponent(FacilityTakeoverC) {
		t.Fatalf("farm missing FacilityTakeoverC")
	}
}
