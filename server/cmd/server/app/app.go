package app

import (
	"context"
	"errors"
	"log/slog"
	"net/http"
	"os/signal"
	"syscall"
	"time"

	"github.com/elebirds/panoptes/internal/config"
	"github.com/elebirds/panoptes/internal/observe"
	"github.com/elebirds/panoptes/internal/transport"
)

type App struct {
	cfg           *config.Config
	logger        *slog.Logger
	infra         *infrastructure
	server        *http.Server
	gameTransport transport.GameTransport
}

func New() *App {
	return &App{}
}

func (a *App) Run() {
	cfg, err := config.Load()
	if err != nil {
		slog.Error("加载配置失败", "错误", err)
		return
	}
	a.cfg = &cfg

	a.logger = observe.NewLogger("panoptes", observe.LoggerConfig{
		Level:  a.cfg.LogLevel,
		Format: a.cfg.LogFormat,
	})
	slog.SetDefault(a.logger)

	if err := a.initInfra(context.Background()); err != nil {
		a.logger.Error("基础设施初始化失败", "错误", err)
		return
	}
	defer a.infra.cleanup()

	a.server = a.buildServer()
	a.start()
}

func (a *App) start() {
	a.logger.Info("服务器启动", "监听地址", a.server.Addr)
	a.logger.Info("WebSocket 端点", "地址", "ws://localhost:"+a.cfg.Port+"/ws")
	a.logger.Info("HTTP API 端点", "地址", "http://localhost:"+a.cfg.Port+"/api")

	errCh := make(chan error, 1)
	go func() {
		errCh <- a.server.ListenAndServe()
	}()

	sigCtx, stop := signal.NotifyContext(context.Background(), syscall.SIGINT, syscall.SIGTERM)
	defer stop()

	select {
	case err := <-errCh:
		if err != nil && !errors.Is(err, http.ErrServerClosed) {
			a.logger.Error("服务器异常退出", "错误", err)
		}
	case <-sigCtx.Done():
		a.shutdown()
		if err := <-errCh; err != nil && !errors.Is(err, http.ErrServerClosed) {
			a.logger.Error("服务器错误", "错误", err)
		}
	}

	a.logger.Info("服务器已停止")
}

func (a *App) shutdown() {
	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()

	if err := a.server.Shutdown(ctx); err != nil {
		a.logger.Error("优雅关闭失败", "错误", err)
	}
}
