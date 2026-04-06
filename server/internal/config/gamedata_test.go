package config

import (
	"os"
	"path/filepath"
	"testing"
)

func TestLoadGameDataParsesCoreFields(t *testing.T) {
	dir := t.TempDir()
	path := filepath.Join(dir, "gamedata.json")
	if err := os.WriteFile(path, []byte(`{
  "units": {
    "cavalry": {
      "name": "骑兵",
      "hp": 25,
      "attack": 12,
      "speed": 3,
      "cost": { "food": 2, "refined_ore": 1 },
      "multipliers": { "infantry": 1.5 },
      "can_siege": false,
      "siege_multiplier": 1.0,
      "can_destroy": false,
      "destroy_multiplier": 1.0,
      "range": 1,
      "road_speed_bonus": 1,
      "charge_bonus": 1.5
    }
  },
  "buildings": {
    "wall": {
      "name": "城墙",
      "category": "military",
      "build_cost": { "ore": 2, "build_points": 3 },
      "hp": 50,
      "defense_bonus_per_level": 0.1,
      "max_level": 3
    }
  },
  "terrain": {
    "plain": {
      "name": "平原",
      "move_cost_no_road": 2,
      "defense_bonus": 0.0,
      "attack_penalty": 0.0,
      "blocks_cavalry": false
    }
  },
  "combat": {
    "wall_reduction_per_level": 0.1,
    "max_wall_reduction": 0.5,
    "tower_damage_per_tower": 8,
    "guard_bonus_per_unit": 3,
    "road_move_cost": 1
  },
  "rules": {
    "turn_time_limit_domestic": 60,
    "turn_time_limit_combat": 60,
    "tokens_per_turn": 3,
    "tokens_recuperation_bonus": 1,
    "max_turns": 30,
    "castle_base_hp": 100,
    "safe_zone_radius": 4,
    "occupy_turns": 1,
    "build_points_per_turn": 10,
    "build_points_max": 30
  }
}`), 0o600); err != nil {
		t.Fatalf("WriteFile() error = %v", err)
	}

	data, err := LoadGameData(path)
	if err != nil {
		t.Fatalf("LoadGameData() error = %v", err)
	}

	if data.Units["cavalry"].ChargeBonus != 1.5 {
		t.Fatalf("ChargeBonus = %f", data.Units["cavalry"].ChargeBonus)
	}
	if data.Buildings["wall"].Category != "military" {
		t.Fatalf("Building category = %q", data.Buildings["wall"].Category)
	}
	if data.Rules.CastleBaseHP != 100 {
		t.Fatalf("CastleBaseHP = %d", data.Rules.CastleBaseHP)
	}
}
