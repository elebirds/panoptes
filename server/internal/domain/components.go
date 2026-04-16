// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 定义领域模型的组件定义与装配映射。

package domain

import "github.com/yohamta/donburi"

type PositionComp struct {
	X, Y int
}

type NodeComp struct {
	ID             string
	Terrain        Terrain
	Owner          string
	TerritoryOwner string
	HasRoad        bool
	IsResource     bool
	ResourceType   string
	NodeName       string
}

type BuildingComp struct {
	Type      BuildingType
	HP        int
	MaxHP     int
	WallLevel int
	Owner     string
	Towers    int
}

type BuildingBindingComp struct {
	Scope         string
	CityID        string
	ServiceCityID string
}

type BuildingOperationComp struct {
	SelectedRecipeID string
	ProgressTurns    int
	RequiredTurns    int
	DelayTurns       int
	BlockedReason    string
	ProgressRemainder int
	ConsumedResources ResourceBag
	ConsumedPoints    PointBag
}

type BuildingStateComp struct {
	Disabled       bool
	DisabledReason string
	Status         string
	Reason         string
	OnlineOnTurn   int
}

type FacilityTakeoverComp struct {
	Mode               string
	Progress           int
	Required           int
	Completed          bool
	ControllerPlayerID string
}

type UnitCategoryComp struct {
	Category string
}

type VisibilityHookComp struct {
	Key string
}

type UnitStatsComp struct {
	ID          string
	Faction     string
	Type        UnitType
	HP          int
	MaxHP       int
	Attack      int
	AttackRange int
	Speed       int
}

type UnitCapabilitiesComp = UnitCapabilities

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
	PositionC          = donburi.NewComponentType[PositionComp]()
	NodeC              = donburi.NewComponentType[NodeComp]()
	BuildingC          = donburi.NewComponentType[BuildingComp]()
	BuildingBindingC   = donburi.NewComponentType[BuildingBindingComp]()
	BuildingOperationC = donburi.NewComponentType[BuildingOperationComp]()
	BuildingStateC     = donburi.NewComponentType[BuildingStateComp]()
	FacilityTakeoverC  = donburi.NewComponentType[FacilityTakeoverComp]()
	UnitStatsC         = donburi.NewComponentType[UnitStatsComp]()
	UnitCategoryC      = donburi.NewComponentType[UnitCategoryComp]()
	VisibilityHookC    = donburi.NewComponentType[VisibilityHookComp]()
	UnitCapabilitiesC  = donburi.NewComponentType[UnitCapabilitiesComp]()
	MoveIntentC        = donburi.NewComponentType[MoveIntentComp]()
	SiegeAbilityC      = donburi.NewComponentType[SiegeAbilityComp]()
	DestroyAbilityC    = donburi.NewComponentType[DestroyAbilityComp]()
	RangedAbilityC     = donburi.NewComponentType[RangedAbilityComp]()
	ChargeAbilityC     = donburi.NewComponentType[ChargeAbilityComp]()
	PoisonEffectC      = donburi.NewComponentType[PoisonEffectComp]()
	StarvingC          = donburi.NewComponentType[StarvingComp]()
)
