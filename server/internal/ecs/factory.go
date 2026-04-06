package ecs

import (
	"fmt"

	"github.com/elebirds/panoptes/internal/config"
	"github.com/elebirds/panoptes/internal/domain"
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
	cfg, ok := config.Data.Units[unitType]
	if !ok {
		panic(fmt.Sprintf("unknown unit type: %s", unitType))
	}

	entity := world.Create(PositionC, UnitStatsC)
	entry := world.Entry(entity)
	PositionC.SetValue(entry, PositionComp{X: pos.X, Y: pos.Y})
	UnitStatsC.SetValue(entry, UnitStatsComp{
		ID:      uuid.NewString(),
		Faction: faction,
		Type:    domain.UnitType(unitType),
		HP:      cfg.HP,
		MaxHP:   cfg.HP,
		Attack:  cfg.Attack,
		Speed:   cfg.Speed,
	})

	if cfg.CanSiege {
		entry.AddComponent(SiegeAbilityC)
		SiegeAbilityC.SetValue(entry, SiegeAbilityComp{Multiplier: cfg.SiegeMultiplier})
	}
	if cfg.CanDestroy {
		entry.AddComponent(DestroyAbilityC)
		DestroyAbilityC.SetValue(entry, DestroyAbilityComp{Multiplier: cfg.DestroyMultiplier})
	}
	if cfg.Range > 1 {
		entry.AddComponent(RangedAbilityC)
		RangedAbilityC.SetValue(entry, RangedAbilityComp{Range: cfg.Range})
	}
	if unitType == string(domain.UnitTypeCavalry) {
		entry.AddComponent(ChargeAbilityC)
		ChargeAbilityC.SetValue(entry, ChargeAbilityComp{BonusMultiplier: cfg.ChargeBonus})
	}

	return entity
}

func CreateBuilding(world donburi.World, buildingType string, owner string, nodeEntry *donburi.Entry) donburi.Entity {
	cfg, ok := config.Data.Buildings[buildingType]
	if !ok {
		panic(fmt.Sprintf("unknown building type: %s", buildingType))
	}

	entity := world.Create(BuildingC)
	buildingEntry := world.Entry(entity)
	comp := BuildingComp{
		Type:  domain.BuildingType(buildingType),
		HP:    cfg.HP,
		MaxHP: cfg.HP,
		Owner: owner,
	}
	BuildingC.SetValue(buildingEntry, comp)

	if nodeEntry != nil {
		if !nodeEntry.HasComponent(BuildingC) {
			nodeEntry.AddComponent(BuildingC)
		}
		BuildingC.SetValue(nodeEntry, comp)
		node := NodeC.Get(nodeEntry)
		node.Owner = owner
	}

	return entity
}
