// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现观测模块的日志记录配置。

package observe

import (
	"io"
	"log/slog"
	"os"
)

// LoggerConfig 定义日志配置。
type LoggerConfig struct {
	// Level 日志级别：debug, info, warn, error
	Level string
	// Format 输出格式：auto, json, text, console
	Format string
}

// Format 表示日志输出格式。
type Format string

const (
	FormatAuto    Format = "auto"
	FormatJSON    Format = "json"
	FormatText    Format = "text"
	FormatConsole Format = "console"
)

// NewLogger 创建一个配置好的 slog.Logger。
//
// service 参数会作为日志的 "service" 字段，用于标识服务名称。
func NewLogger(service string, config LoggerConfig) *slog.Logger {
	return newLogger(os.Stdout, service, config)
}

func newLogger(w io.Writer, service string, config LoggerConfig) *slog.Logger {
	level := parseLevel(config.Level)
	format := parseFormat(config.Format)
	tty := isTerminal(w)

	handler := createHandler(w, level, format, tty)

	logger := slog.New(handler).With("service", service)
	return logger
}

// parseLevel 将字符串级别转换为 slog.Level。
func parseLevel(level string) slog.Level {
	switch level {
	case "debug":
		return slog.LevelDebug
	case "info":
		return slog.LevelInfo
	case "warn":
		return slog.LevelWarn
	case "error":
		return slog.LevelError
	default:
		return slog.LevelInfo
	}
}

// parseFormat 将字符串格式转换为 Format。
func parseFormat(format string) Format {
	switch format {
	case "json":
		return FormatJSON
	case "text":
		return FormatText
	case "console":
		return FormatConsole
	case "", "auto":
		return FormatAuto
	default:
		return FormatAuto
	}
}

// createHandler 根据配置创建对应的 Handler。
func createHandler(w io.Writer, level slog.Level, format Format, tty bool) slog.Handler {
	options := &slog.HandlerOptions{Level: level}

	switch resolveFormat(format, tty) {
	case FormatConsole:
		return newConsoleHandler(w, level)
	case FormatText:
		return slog.NewTextHandler(w, options)
	default:
		return slog.NewJSONHandler(w, options)
	}
}

// resolveFormat 根据格式和终端状态解析最终格式。
func resolveFormat(format Format, tty bool) Format {
	switch format {
	case FormatJSON, FormatText, FormatConsole:
		return format
	case FormatAuto:
		if tty {
			return FormatConsole
		}
		return FormatJSON
	default:
		if tty {
			return FormatConsole
		}
		return FormatJSON
	}
}
