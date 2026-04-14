// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 声明观测模块包的职责与边界。

// Package observe 提供结构化日志和可观测性功能。
//
// 该包基于 Go 1.21+ 标准库的 log/slog 实现，支持多种输出格式：
//   - auto: 根据终端自动选择 console 或 json 格式
//   - console: 彩色控制台输出，适合本地开发
//   - json: JSON 结构化格式，适合日志聚合系统
//   - text: 纯文本格式，适合 CI/CD 环境
//
// 使用示例：
//
//	logger := observe.NewLogger("my-service", observe.LoggerConfig{
//	    Level:  "info",
//	    Format: "auto",
//	})
//	logger.Info("service started", "port", 8080)
package observe
