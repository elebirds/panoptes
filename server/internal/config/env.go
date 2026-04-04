package config

import (
	"fmt"
	"os"

	"github.com/caarlos0/env/v11"
	"github.com/joho/godotenv"
)

type Config struct {
	Port      string `env:"PORT" envDefault:"8080"`
	LogLevel  string `env:"LOG_LEVEL" envDefault:"info"`
	LogFormat string `env:"LOG_FORMAT" envDefault:"auto"`
}

func Load() (Config, error) {
	if err := godotenv.Load(); err != nil && !os.IsNotExist(err) {
		return Config{}, fmt.Errorf("load .env: %w", err)
	}

	cfg := Config{}
	if err := env.Parse(&cfg); err != nil {
		return Config{}, fmt.Errorf("parse env config: %w", err)
	}

	return cfg, nil
}
