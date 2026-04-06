package config

import (
	"encoding/json"
	"fmt"
	"os"
)

func LoadGameData(path string) (GameData, error) {
	raw, err := os.ReadFile(path)
	if err != nil {
		return GameData{}, fmt.Errorf("read gamedata: %w", err)
	}

	var data GameData
	if err := json.Unmarshal(raw, &data); err != nil {
		return GameData{}, fmt.Errorf("unmarshal gamedata: %w", err)
	}
	if data.Units == nil {
		data.Units = map[string]UnitConfig{}
	}
	if data.Buildings == nil {
		data.Buildings = map[string]BuildingConfig{}
	}
	if data.Terrain == nil {
		data.Terrain = map[string]TerrainConfig{}
	}
	if err := validateGameDataResources(data); err != nil {
		return GameData{}, err
	}
	return data, nil
}

func validateGameDataResources(data GameData) error {
	for unitType, unit := range data.Units {
		if err := validateResourceAmount(unit.Cost); err != nil {
			return fmt.Errorf("unit %s cost: %w", unitType, err)
		}
	}

	for buildingType, building := range data.Buildings {
		if err := validateResourceAmount(building.BuildCost); err != nil {
			return fmt.Errorf("building %s build_cost: %w", buildingType, err)
		}
		if err := validateResourceAmount(building.ProductionIn); err != nil {
			return fmt.Errorf("building %s production_in: %w", buildingType, err)
		}
		if err := validateResourceAmount(building.ProductionOut); err != nil {
			return fmt.Errorf("building %s production_out: %w", buildingType, err)
		}
		if err := validateResourceAmount(building.Upkeep); err != nil {
			return fmt.Errorf("building %s upkeep: %w", buildingType, err)
		}
	}

	return nil
}

func validateResourceAmount(amount ResourceAmount) error {
	for key := range amount {
		if !IsKnownResourceKey(key) {
			return fmt.Errorf("unknown resource key %q", key)
		}
	}
	return nil
}
