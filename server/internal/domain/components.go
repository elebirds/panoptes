package domain

import "github.com/yohamta/donburi"

type PositionComp struct {
	X, Y int
}

type NodeComp struct {
	ID           string
	Terrain      Terrain
	Owner        string
	HasRoad      bool
	IsResource   bool
	ResourceType string
	NodeName     string
}

type BuildingComp struct {
	Type      BuildingType
	HP        int
	MaxHP     int
	WallLevel int
	Owner     string
	Towers    int
}

type UnitStatsComp struct {
	ID      string
	Faction string
	Type    UnitType
	HP      int
	MaxHP   int
	Attack  int
	Speed   int
}

type MoveIntentComp struct {
	Target Position
	Path   []Position
}

type SiegeAbilityComp struct {
	Multiplier float64
}

type DestroyAbilityComp struct {
	Multiplier float64
}

type RangedAbilityComp struct {
	Range int
}

type ChargeAbilityComp struct {
	BonusMultiplier float64
}

type PoisonEffectComp struct {
	DamagePerTurn int
	TurnsLeft     int
}

type StarvingComp struct {
	TurnsStarving int
}

var (
	PositionC       = donburi.NewComponentType[PositionComp]()
	NodeC           = donburi.NewComponentType[NodeComp]()
	BuildingC       = donburi.NewComponentType[BuildingComp]()
	UnitStatsC      = donburi.NewComponentType[UnitStatsComp]()
	MoveIntentC     = donburi.NewComponentType[MoveIntentComp]()
	SiegeAbilityC   = donburi.NewComponentType[SiegeAbilityComp]()
	DestroyAbilityC = donburi.NewComponentType[DestroyAbilityComp]()
	RangedAbilityC  = donburi.NewComponentType[RangedAbilityComp]()
	ChargeAbilityC  = donburi.NewComponentType[ChargeAbilityComp]()
	PoisonEffectC   = donburi.NewComponentType[PoisonEffectComp]()
	StarvingC       = donburi.NewComponentType[StarvingComp]()
)
