// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 定义配置模块的运行配置。

package config

type Config struct {
	Port      string `env:"PORT" envDefault:"8080"`
	LogLevel  string `env:"LOG_LEVEL" envDefault:"info"`
	LogFormat string `env:"LOG_FORMAT" envDefault:"auto"`
	DevMode   bool   `env:"DEV_MODE" envDefault:"false"`
	DataRoot  string `env:"DATA_ROOT" envDefault:"../data"`
	MapID     string `env:"MAP_ID" envDefault:""`

	// PostgreSQL 配置
	PostgresDSN string `env:"POSTGRES_DSN" envDefault:"postgres://panoptes:panoptes_dev@localhost:5432/panoptes?sslmode=disable"`

	// Redis 配置
	RedisAddr     string `env:"REDIS_ADDR" envDefault:"localhost:6379"`
	RedisPassword string `env:"REDIS_PASSWORD" envDefault:""`
	RedisDB       int    `env:"REDIS_DB" envDefault:"0"`

	// JWT 配置
	JWTSecret     string `env:"JWT_SECRET" envDefault:"your-secret-key"`
	JWTExpiration int    `env:"JWT_EXPIRATION" envDefault:"86400"` // 秒，默认24小时

	// 对局配置
	DefaultMaxPlayers int `env:"DEFAULT_MAX_PLAYERS" envDefault:"2"`

	// LLM 配置
	QwenAPIKey     string `env:"QWEN_API_KEY" envDefault:""`
	DeepSeekAPIKey string `env:"DEEPSEEK_API_KEY" envDefault:""`
}
