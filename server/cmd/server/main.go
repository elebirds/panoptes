package main

import (
	"encoding/json"
	"log/slog"
	"net/http"

	"panoptes-server/internal/config"
	"panoptes-server/internal/observe"
)

func main() {
	cfg, err := config.Load()
	if err != nil {
		slog.Error("加载配置失败", "error", err)
		return
	}

	// 初始化日志系统
	logConfig := observe.LoggerConfig{
		Level:  cfg.LogLevel,
		Format: cfg.LogFormat,
	}
	logger := observe.NewLogger("panoptes", logConfig)
	slog.SetDefault(logger)

	port := cfg.Port

	mux := http.NewServeMux()
	mux.HandleFunc("/health", healthHandler)

	logger.Info("panoptes 服务器正在监听", "port", port)
	if err := http.ListenAndServe(":"+port, mux); err != nil {
		logger.Error("服务器错误", "error", err)
	}
}

func healthHandler(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("Content-Type", "application/json")
	_ = json.NewEncoder(w).Encode(map[string]string{"status": "ok"})
}
