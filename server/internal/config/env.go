package config

import (
	"fmt"
	"os"
	"path/filepath"

	"github.com/caarlos0/env/v11"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/joho/godotenv"
)

func Load() (Config, error) {
	if err := godotenv.Load(); err != nil && !os.IsNotExist(err) {
		return Config{}, fmt.Errorf("load .env: %w", err)
	}

	cfg := Config{}
	if err := env.Parse(&cfg); err != nil {
		return Config{}, fmt.Errorf("parse env config: %w", err)
	}

	catalog, err := staticdata.LoadDir(filepath.Join(cfg.DataRoot, "generated/server"))
	if err != nil {
		return Config{}, fmt.Errorf("load static data: %w", err)
	}
	staticdata.SetDefault(catalog)

	return cfg, nil
}
