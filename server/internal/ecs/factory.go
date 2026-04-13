package ecs

import (
	"fmt"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/google/uuid"
	"github.com/yohamta/donburi"
)

type MapNode struct {
	ID              string `json:"id"`
	X               int    `json:"x"`
	Y               int    `json:"y"`
	Terrain         string `json:"terrain"`
	IsResourcePoint bool   `json:"is_resource_point"`
	ResourceType    string `json:"resource_type"`
}

func CreateNode(world donburi.World, mapNode MapNode) donburi.Entity {
	entity := world.Create(PositionC, NodeC)
	entry := world.Entry(entity)
	PositionC.SetValue(entry, PositionComp{X: mapNode.X, Y: mapNode.Y})
	NodeC.SetValue(entry, NodeComp{
		ID:           mapNode.ID,
		Terrain:      domain.Terrain(mapNode.Terrain),
		IsResource:   mapNode.IsResourcePoint,
		ResourceType: mapNode.ResourceType,
	})
	return entity
}

func CreateUnit(world donburi.World, unitType string, faction string, pos domain.Position) donburi.Entity {
	cfg, ok := staticdata.Default().GetUnit(unitType)
	if !ok {
		fallback, hasFallback := fallbackUnitDefinition(unitType)
		if !hasFallback {
			panic(fmt.Sprintf("unknown unit type: %s", unitType))
		}
		cfg = fallback
	}

	entity := world.Create(PositionC, UnitStatsC, UnitCapabilitiesC)
	entry := world.Entry(entity)
	PositionC.SetValue(entry, PositionComp{X: pos.X, Y: pos.Y})
	UnitStatsC.SetValue(entry, UnitStatsComp{
		ID:          uuid.NewString(),
		Faction:     faction,
		Type:        domain.UnitType(unitType),
		HP:          cfg.MaxHP,
		MaxHP:       cfg.MaxHP,
		Attack:      cfg.Attack,
		AttackRange: cfg.AttackRange,
		Speed:       cfg.MoveRange,
	})
	UnitCapabilitiesC.SetValue(entry, UnitCapabilitiesComp{
		Civilian:    cfg.Class == "civilian",
		Melee:       cfg.Class != "civilian" && cfg.AttackRange <= 1,
		Ranged:      cfg.AttackRange > 1,
		Charge:      cfg.ChargeBonus > 0,
		Siege:       cfg.Flags.CanSiege,
		DestroyRoad: cfg.Flags.CanDestroyRoad,
	})

	if cfg.Flags.CanSiege {
		entry.AddComponent(SiegeAbilityC)
		SiegeAbilityC.SetValue(entry, SiegeAbilityComp{Multiplier: cfg.Flags.SiegeMultiplier})
	}
	if cfg.Flags.CanDestroyRoad {
		entry.AddComponent(DestroyAbilityC)
		DestroyAbilityC.SetValue(entry, DestroyAbilityComp{Multiplier: cfg.Flags.DestroyMultiplier})
	}
	if cfg.AttackRange > 1 {
		entry.AddComponent(RangedAbilityC)
		RangedAbilityC.SetValue(entry, RangedAbilityComp{Range: cfg.AttackRange})
	}
	if cfg.ChargeBonus > 0 {
		entry.AddComponent(ChargeAbilityC)
		ChargeAbilityC.SetValue(entry, ChargeAbilityComp{BonusMultiplier: cfg.ChargeBonus})
	}

	return entity
}

func fallbackUnitDefinition(unitType string) (staticdata.UnitDefinition, bool) {
	switch unitType {
	case string(domain.UnitTypeSettler):
		return staticdata.UnitDefinition{
			ID:          string(domain.UnitTypeSettler),
			Class:       "support",
			MaxHP:       18,
			Attack:      0,
			AttackRange: 0,
			MoveRange:   2,
			VisionRange: 3,
			TrainCost:   staticdata.ResourceAmounts{},
			Upkeep:      staticdata.ResourceAmounts{},
			Multipliers: map[string]float64{},
			Flags: staticdata.UnitFlags{
				CanSiege:       false,
				CanDestroyRoad: false,
				CanCapture:     false,
			},
		}, true
	default:
		return staticdata.UnitDefinition{}, false
	}
}

func CreateBuilding(world donburi.World, buildingType string, owner string, castleID string, nodeEntry *donburi.Entry) donburi.Entity {
	cfg, ok := staticdata.Default().GetBuilding(buildingType)
	if !ok {
		fallback, hasFallback := fallbackBuildingDefinition(buildingType)
		if !hasFallback {
			panic(fmt.Sprintf("unknown building type: %s", buildingType))
		}
		cfg = fallback
	}

	comp := BuildingComp{
		Type:      domain.BuildingType(buildingType),
		HP:        cfg.Combat.MaxHP,
		MaxHP:     cfg.Combat.MaxHP,
		Owner:     owner,
		CastleID:  castleID,
		WallLevel: cfg.Combat.WallLevel,
		Towers:    cfg.Combat.Towers,
	}

	if nodeEntry != nil {
		if !nodeEntry.HasComponent(BuildingC) {
			nodeEntry.AddComponent(BuildingC)
		}
		BuildingC.SetValue(nodeEntry, comp)
		node := NodeC.Get(nodeEntry)
		node.Owner = owner
		return nodeEntry.Entity()
	}

	entity := world.Create(BuildingC)
	buildingEntry := world.Entry(entity)
	BuildingC.SetValue(buildingEntry, comp)
	return entity
}

func fallbackBuildingDefinition(buildingType string) (staticdata.BuildingDefinition, bool) {
	switch buildingType {
	case "castle":
		castleHP := 100
		if catalog := staticdata.Default(); catalog != nil {
			castleHP = catalog.Rules().CastleBaseHP
		}
		return staticdata.BuildingDefinition{
			ID: buildingType,
			Combat: staticdata.BuildingCombat{
				MaxHP: castleHP,
			},
		}, true
	default:
		return staticdata.BuildingDefinition{}, false
	}
}
