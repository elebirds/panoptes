package config

type Config struct {
	Port      string `env:"PORT" envDefault:"8080"`
	LogLevel  string `env:"LOG_LEVEL" envDefault:"info"`
	LogFormat string `env:"LOG_FORMAT" envDefault:"auto"`
	DevMode   bool   `env:"DEV_MODE" envDefault:"false"`

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
	DefaultMaxPlayers     int    `env:"DEFAULT_MAX_PLAYERS" envDefault:"2"`
	TokensPerTurn         int    `env:"TOKENS_PER_TURN" envDefault:"3"`
	TurnTimeLimitDomestic int    `env:"TURN_TIME_LIMIT_DOMESTIC" envDefault:"15"`
	TurnTimeLimitCombat   int    `env:"TURN_TIME_LIMIT_COMBAT" envDefault:"20"`
	GameDataPath          string `env:"GAMEDATA_PATH" envDefault:"data/gamedata.json"`
	MapPath               string `env:"MAP_PATH" envDefault:"data/maps/default.json"`

	// LLM 配置
	QwenAPIKey     string `env:"QWEN_API_KEY" envDefault:""`
	DeepSeekAPIKey string `env:"DEEPSEEK_API_KEY" envDefault:""`
}
