package config

type Config struct {
	Port      string `env:"PORT" envDefault:"8080"`
	LogLevel  string `env:"LOG_LEVEL" envDefault:"info"`
	LogFormat string `env:"LOG_FORMAT" envDefault:"auto"`

	// PostgreSQL 配置
	PostgresDSN string `env:"POSTGRES_DSN" envDefault:"postgres://panoptes:panoptes_dev@localhost:5432/panoptes?sslmode=disable"`

	// Redis 配置
	RedisAddr     string `env:"REDIS_ADDR" envDefault:"localhost:6379"`
	RedisPassword string `env:"REDIS_PASSWORD" envDefault:""`
	RedisDB       int    `env:"REDIS_DB" envDefault:"0"`

	// JWT 配置
	JWTSecret     string `env:"JWT_SECRET" envDefault:"your-secret-key"`
	JWTExpiration int    `env:"JWT_EXPIRATION" envDefault:"86400"` // 秒，默认24小时

	DefaultMaxPlayers int `env:"DEFAULT_MAX_PLAYERS" envDefault:"2"`
}
