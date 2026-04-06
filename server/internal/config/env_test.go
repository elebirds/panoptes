package config

import (
	"os"
	"path/filepath"
	"testing"
)

func TestLoadReadsDevModeAndGameDefaults(t *testing.T) {
	dir := t.TempDir()
	gameDataPath := filepath.Join(dir, "gamedata.json")
	mapPath := filepath.Join(dir, "default.json")
	if err := os.WriteFile(gameDataPath, []byte(`{
  "units": {},
  "buildings": {},
  "terrain": {},
  "combat": {},
  "rules": {
    "tokens_per_turn": 3,
    "castle_base_hp": 100,
    "build_points_per_turn": 10
  }
}`), 0o600); err != nil {
		t.Fatalf("WriteFile(gamedata) error = %v", err)
	}
	if err := os.WriteFile(mapPath, []byte(`{"id":"default","width":20,"height":20,"spawn_points":[],"nodes":[],"central_points":[],"named_nodes":{}}`), 0o600); err != nil {
		t.Fatalf("WriteFile(map) error = %v", err)
	}

	t.Setenv("DEV_MODE", "true")
	t.Setenv("TOKENS_PER_TURN", "3")
	t.Setenv("TURN_TIME_LIMIT_DOMESTIC", "15")
	t.Setenv("TURN_TIME_LIMIT_COMBAT", "20")
	t.Setenv("GAMEDATA_PATH", gameDataPath)
	t.Setenv("MAP_PATH", mapPath)

	cfg, err := Load()
	if err != nil {
		t.Fatalf("Load() error = %v", err)
	}

	if !cfg.DevMode {
		t.Fatalf("DevMode = false")
	}
	if cfg.TokensPerTurn != 3 {
		t.Fatalf("TokensPerTurn = %d", cfg.TokensPerTurn)
	}
	if cfg.TurnTimeLimitDomestic != 15 {
		t.Fatalf("TurnTimeLimitDomestic = %d", cfg.TurnTimeLimitDomestic)
	}
	if cfg.TurnTimeLimitCombat != 20 {
		t.Fatalf("TurnTimeLimitCombat = %d", cfg.TurnTimeLimitCombat)
	}
	if cfg.MapPath != mapPath {
		t.Fatalf("MapPath = %q", cfg.MapPath)
	}
	if Data.Rules.CastleBaseHP != 100 {
		t.Fatalf("CastleBaseHP = %d", Data.Rules.CastleBaseHP)
	}
}
