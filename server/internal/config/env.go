// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现配置模块的环境变量加载与默认配置装配。

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
