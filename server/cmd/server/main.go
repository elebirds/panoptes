package main

import (
	"context"
	"log/slog"
	"net/http"

	"github.com/elebirds/panoptes/internal/auth"
	"github.com/elebirds/panoptes/internal/config"
	"github.com/elebirds/panoptes/internal/observe"
	"github.com/elebirds/panoptes/internal/store/postgres"
	"github.com/elebirds/panoptes/internal/store/redis"
	"github.com/elebirds/panoptes/internal/transport"
)

func main() {
	// 1. 加载配置
	cfg, err := config.Load()
	if err != nil {
		slog.Error("加载配置失败", "error", err)
		return
	}

	// 2. 初始化日志系统
	logConfig := observe.LoggerConfig{
		Level:  cfg.LogLevel,
		Format: cfg.LogFormat,
	}
	logger := observe.NewLogger("panoptes", logConfig)
	slog.SetDefault(logger)

	ctx := context.Background()

	// 3. 初始化 PostgreSQL
	var pgDB *postgres.DB
	if cfg.PostgresDSN != "" {
		pgDB, err = postgres.NewDB(ctx, cfg.PostgresDSN)
		if err != nil {
			logger.Error("连接 PostgreSQL 失败", "error", err)
		} else {
			defer pgDB.Close()
			logger.Info("PostgreSQL 连接成功")
		}
	}

	// 4. 初始化 Redis
	var redisClient *redis.Client
	if cfg.RedisAddr != "" {
		redisClient = redis.NewClient(cfg.RedisAddr, cfg.RedisPassword, cfg.RedisDB)
		if err := redisClient.Ping(ctx); err != nil {
			logger.Error("连接 Redis 失败", "error", err)
			redisClient = nil
		} else {
			defer redisClient.Close()
			logger.Info("Redis 连接成功")
		}
	}

	// 5. 初始化 Auth Service
	var authSvc *auth.Service
	if pgDB != nil {
		userStore := auth.NewUserStore(pgDB.Queries())
		authSvc = auth.NewService(userStore, cfg.JWTSecret, cfg.JWTExpiration)
		logger.Info("Auth 服务初始化成功")
	}

	// 6. 创建 HTTP 传输层
	httpTransport := transport.NewHTTPTransport(authSvc, cfg.JWTSecret, pgDB, redisClient)

	// 7. 启动服务器
	port := cfg.Port
	logger.Info("panoptes 服务器正在监听", "port", port)
	if err := http.ListenAndServe(":"+port, httpTransport.Handler()); err != nil {
		logger.Error("服务器错误", "error", err)
	}
}
