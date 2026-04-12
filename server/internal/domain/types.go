package domain

import (
	"fmt"
	"sort"
)

type Terrain string

const (
	TerrainPlain    Terrain = "plain"
	TerrainMountain Terrain = "mountain"
	TerrainForest   Terrain = "forest"
	TerrainRiver    Terrain = "river"
)

type UnitType string

const (
	UnitTypeSettler  UnitType = "settler"
	UnitTypeWarrior  UnitType = "warrior"
	UnitTypeArcher   UnitType = "archer"
	UnitTypeCavalry  UnitType = "cavalry"
	UnitTypeInfantry UnitType = "infantry"
	UnitTypeSiege    UnitType = "siege"
	UnitTypeSaboteur UnitType = "saboteur"
)

type BuildingType string

const (
	BuildingTypeFarm         BuildingType = "farm"
	BuildingTypeMine         BuildingType = "mine"
	BuildingTypeLumber       BuildingType = "lumber"
	BuildingTypeGranary      BuildingType = "granary"
	BuildingTypeSmelter      BuildingType = "smelter"
	BuildingTypeWorkshop     BuildingType = "workshop"
	BuildingTypeBarracks     BuildingType = "barracks"
	BuildingTypeStable       BuildingType = "stable"
	BuildingTypeEngineerCamp BuildingType = "engineer_camp"
	BuildingTypeWall         BuildingType = "wall"
	BuildingTypeTower        BuildingType = "tower"
	BuildingTypeWatchtower   BuildingType = "watchtower"
)

type BuildingCategory string

const (
	BuildingCategoryProduction         BuildingCategory = "production"
	BuildingCategoryMilitaryProduction BuildingCategory = "military_production"
	BuildingCategoryMilitary           BuildingCategory = "military"
)

type Policy string

const (
	PolicyReadyForWar  Policy = "ready_for_war"
	PolicyExpansion    Policy = "expansion"
	PolicyRecuperation Policy = "recuperation"
	PolicyDiplomacy    Policy = "diplomacy"
)

type RoadStatus string

const (
	RoadStatusIntact    RoadStatus = "intact"
	RoadStatusDamaged   RoadStatus = "damaged"
	RoadStatusDestroyed RoadStatus = "destroyed"
)

type Position struct {
	X int
	Y int
}

func (p Position) DistanceTo(other Position) int {
	dx := p.X - other.X
	if dx < 0 {
		dx = -dx
	}
	dy := p.Y - other.Y
	if dy < 0 {
		dy = -dy
	}
	return dx + dy
}

func (p Position) Add(other Position) Position {
	return Position{X: p.X + other.X, Y: p.Y + other.Y}
}

func (p Position) Neighbors() []Position {
	return []Position{
		{X: p.X, Y: p.Y - 1},
		{X: p.X, Y: p.Y + 1},
		{X: p.X - 1, Y: p.Y},
		{X: p.X + 1, Y: p.Y},
	}
}

type ResourceKey string

const (
	ResourceOre         ResourceKey = "ore"
	ResourceWood        ResourceKey = "wood"
	ResourceFood        ResourceKey = "food"
	ResourceRefinedOre  ResourceKey = "refined_ore"
	ResourceEngineerMat ResourceKey = "engineer_material"
	ResourceBuildPoints ResourceKey = "build_points"
)

var knownResourceKeys = map[ResourceKey]struct{}{
	ResourceOre:         {},
	ResourceWood:        {},
	ResourceFood:        {},
	ResourceRefinedOre:  {},
	ResourceEngineerMat: {},
	ResourceBuildPoints: {},
}

type ResourceBag map[ResourceKey]int

func NewResourceBag() ResourceBag {
	return make(ResourceBag)
}

func (r ResourceBag) Clone() ResourceBag {
	cloned := make(ResourceBag, len(r))
	for key, value := range r {
		cloned[key] = value
	}
	return cloned
}

func (r ResourceBag) Get(key ResourceKey) int {
	return r[key]
}

func (r ResourceBag) Set(key ResourceKey, amount int) {
	if amount == 0 {
		delete(r, key)
		return
	}
	r[key] = amount
}

func (r ResourceBag) AddAmount(key ResourceKey, delta int) {
	r.Set(key, r.Get(key)+delta)
}

func (r ResourceBag) Add(other ResourceBag) ResourceBag {
	sum := r.Clone()
	for key, value := range other {
		sum.AddAmount(key, value)
	}
	return sum.Normalize()
}

func (r ResourceBag) Sub(other ResourceBag) ResourceBag {
	diff := r.Clone()
	for key, value := range other {
		diff.AddAmount(key, -value)
	}
	return diff.Normalize()
}

func (r ResourceBag) CanAfford(cost ResourceBag) bool {
	for key, value := range cost {
		if r.Get(key) < value {
			return false
		}
	}
	return true
}

func (r ResourceBag) IsZero() bool {
	for _, value := range r {
		if value != 0 {
			return false
		}
	}
	return true
}

func (r ResourceBag) Normalize() ResourceBag {
	normalized := make(ResourceBag, len(r))
	for key, value := range r {
		if value != 0 {
			normalized[key] = value
		}
	}
	return normalized
}

func (r ResourceBag) Keys() []ResourceKey {
	keys := make([]ResourceKey, 0, len(r))
	for key, value := range r {
		if value != 0 {
			keys = append(keys, key)
		}
	}
	sort.Slice(keys, func(i, j int) bool { return keys[i] < keys[j] })
	return keys
}

func (r ResourceBag) KnownOnly() ResourceBag {
	filtered := make(ResourceBag)
	for key, value := range r {
		if _, ok := knownResourceKeys[key]; ok && value != 0 {
			filtered[key] = value
		}
	}
	return filtered
}

func (r ResourceBag) ValidateNonNegative() error {
	for key, value := range r {
		if value < 0 {
			return fmt.Errorf("resource %q is negative: %d", key, value)
		}
	}
	return nil
}

func ResourceBagFromAmounts(amount map[string]int) (ResourceBag, error) {
	bag := NewResourceBag()
	for key, value := range amount {
		resourceKey := ResourceKey(key)
		if _, ok := knownResourceKeys[resourceKey]; !ok {
			return nil, fmt.Errorf("unknown resource key %q", key)
		}
		bag.Set(resourceKey, value)
	}
	return bag, nil
}
