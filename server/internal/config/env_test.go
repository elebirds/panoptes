package config

import "testing"

func TestLoadReadsDevModeAndGameDefaults(t *testing.T) {
	t.Setenv("DEV_MODE", "true")
	t.Setenv("TOKENS_PER_TURN", "3")
	t.Setenv("TURN_TIME_LIMIT_DOMESTIC", "15")
	t.Setenv("TURN_TIME_LIMIT_COMBAT", "20")

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
}
