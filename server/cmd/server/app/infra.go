// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 提供服务端应用装配的基础设施依赖装配。

package app

import (
	"context"
	"fmt"
	"log/slog"

	"github.com/elebirds/panoptes/internal/store/postgres"
	"github.com/elebirds/panoptes/internal/store/redis"
)

type infrastructure struct {
	pgDB        *postgres.DB
	redisClient *redis.Client
	cleanups    []func()
}

func (i *infrastructure) cleanup() {
	for idx := len(i.cleanups) - 1; idx >= 0; idx-- {
		i.cleanups[idx]()
	}
}

func (a *App) initInfra(ctx context.Context) error {
	a.infra = &infrastructure{}

	if a.cfg.PostgresDSN == "" {
		return fmt.Errorf("postgres DSN 不能为空")
	}

	pgDB, err := postgres.NewDB(ctx, a.cfg.PostgresDSN)
	if err != nil {
		return fmt.Errorf("连接 PostgreSQL 失败: %w", err)
	}
	a.infra.pgDB = pgDB
	a.infra.cleanups = append(a.infra.cleanups, pgDB.Close)
	slog.Info("PostgreSQL 连接成功")

	if a.cfg.RedisAddr != "" {
		rc := redis.NewClient(a.cfg.RedisAddr, a.cfg.RedisPassword, a.cfg.RedisDB)
		if err := rc.Ping(ctx); err != nil {
			slog.Warn("Redis 连接失败，已跳过", "错误", err)
		} else {
			if err := redis.ClearLobbyNamespace(ctx, rc); err != nil {
				return fmt.Errorf("清理大厅 Redis 命名空间失败: %w", err)
			}
			a.infra.redisClient = rc
			a.infra.cleanups = append(a.infra.cleanups, func() {
				if err := rc.Close(); err != nil {
					slog.Warn("Redis 关闭失败", "错误", err)
				}
			})
			slog.Info("Redis 连接成功，已清理大厅残留")
		}
	}

	return nil
}
