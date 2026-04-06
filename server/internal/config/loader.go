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
	return data, nil
}
