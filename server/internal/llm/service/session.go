// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现LLM 服务层的会话状态与上下文管理。

package service

import (
	"context"
	"crypto/rand"
	"encoding/hex"
	"fmt"
	"log/slog"
	"sync"
	"sync/atomic"
	"time"
)

var chatSessions = sync.Map{}

var fallbackNonce atomic.Uint64

// GenerateSessionID 生成会话 ID。优先使用 crypto/rand；若失败则使用时间戳 + 进程内单调计数器
// 作为非空 fallback，并返回包装后的错误供调用方记录（sessionID 仍可用于继续流程）。
func GenerateSessionID() (string, error) {
	b := make([]byte, 16)
	if _, err := rand.Read(b); err != nil {
		id := fmt.Sprintf("fb-%d-%d", time.Now().UnixNano(), fallbackNonce.Add(1))
		slog.Warn("GenerateSessionID: crypto/rand 失败，已使用 fallback", "err", err, "sessionID", id)
		return id, fmt.Errorf("crypto/rand: %w", err)
	}
	return hex.EncodeToString(b), nil
}

func SetChatSession(sessionID string, session context.CancelFunc) {
	if sessionID == "" {
		return
	}
	if _, loaded := chatSessions.LoadOrStore(sessionID, session); loaded {
		return
	}
}

func GetChatSession(sessionID string) context.CancelFunc {
	if sessionID == "" {
		return nil
	}
	cancelFunc, ok := chatSessions.Load(sessionID)
	if ok {
		return cancelFunc.(context.CancelFunc)
	}

	return nil
}

func RemoveChatSession(sessionID string) {
	if sessionID == "" {
		return
	}
	chatSessions.Delete(sessionID)
}
