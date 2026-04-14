package ecs

import "github.com/elebirds/panoptes/internal/domain"

type PositionComp = domain.PositionComp
type NodeComp = domain.NodeComp
type BuildingComp = domain.BuildingComp
type BuildingOperationComp = domain.BuildingOperationComp
type UnitStatsComp = domain.UnitStatsComp
type UnitCapabilitiesComp = domain.UnitCapabilitiesComp
type MoveIntentComp = domain.MoveIntentComp
type SiegeAbilityComp = domain.SiegeAbilityComp
type DestroyAbilityComp = domain.DestroyAbilityComp
type RangedAbilityComp = domain.RangedAbilityComp
type ChargeAbilityComp = domain.ChargeAbilityComp
type PoisonEffectComp = domain.PoisonEffectComp
type StarvingComp = domain.StarvingComp

var (
	PositionC          = domain.PositionC
	NodeC              = domain.NodeC
	BuildingC          = domain.BuildingC
	BuildingOperationC = domain.BuildingOperationC
	UnitStatsC         = domain.UnitStatsC
	UnitCapabilitiesC  = domain.UnitCapabilitiesC
	MoveIntentC        = domain.MoveIntentC
	SiegeAbilityC      = domain.SiegeAbilityC
	DestroyAbilityC    = domain.DestroyAbilityC
	RangedAbilityC     = domain.RangedAbilityC
	ChargeAbilityC     = domain.ChargeAbilityC
	PoisonEffectC      = domain.PoisonEffectC
	StarvingC          = domain.StarvingC
)
