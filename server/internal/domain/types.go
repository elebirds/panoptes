package domain

type Terrain string

const (
	TerrainPlain    Terrain = "plain"
	TerrainMountain Terrain = "mountain"
	TerrainForest   Terrain = "forest"
	TerrainRiver    Terrain = "river"
)

type UnitType string

const (
	UnitTypeInfantry UnitType = "infantry"
	UnitTypeArcher   UnitType = "archer"
	UnitTypeCavalry  UnitType = "cavalry"
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

type Resources struct {
	Ore              int
	Wood             int
	Food             int
	RefinedOre       int
	EngineerMaterial int
	BuildPoints      int
}

func (r Resources) Add(other Resources) Resources {
	return Resources{
		Ore:              r.Ore + other.Ore,
		Wood:             r.Wood + other.Wood,
		Food:             r.Food + other.Food,
		RefinedOre:       r.RefinedOre + other.RefinedOre,
		EngineerMaterial: r.EngineerMaterial + other.EngineerMaterial,
		BuildPoints:      r.BuildPoints + other.BuildPoints,
	}
}

func (r Resources) Sub(other Resources) Resources {
	return Resources{
		Ore:              r.Ore - other.Ore,
		Wood:             r.Wood - other.Wood,
		Food:             r.Food - other.Food,
		RefinedOre:       r.RefinedOre - other.RefinedOre,
		EngineerMaterial: r.EngineerMaterial - other.EngineerMaterial,
		BuildPoints:      r.BuildPoints - other.BuildPoints,
	}
}

func (r Resources) CanAfford(cost Resources) bool {
	return r.Ore >= cost.Ore &&
		r.Wood >= cost.Wood &&
		r.Food >= cost.Food &&
		r.RefinedOre >= cost.RefinedOre &&
		r.EngineerMaterial >= cost.EngineerMaterial &&
		r.BuildPoints >= cost.BuildPoints
}

func (r Resources) IsZero() bool {
	return r == (Resources{})
}
