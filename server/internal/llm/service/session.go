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

// GenerateSessionId 生成会话 ID。优先使用 crypto/rand；若失败则使用时间戳 + 进程内单调计数器
// 作为非空 fallback，并返回包装后的错误供调用方记录（sessionId 仍可用于继续流程）。
func GenerateSessionId() (string, error) {
	b := make([]byte, 16)
	if _, err := rand.Read(b); err != nil {
		id := fmt.Sprintf("fb-%d-%d", time.Now().UnixNano(), fallbackNonce.Add(1))
		slog.Warn("GenerateSessionId: crypto/rand 失败，已使用 fallback", "err", err, "sessionId", id)
		return id, fmt.Errorf("crypto/rand: %w", err)
	}
	return hex.EncodeToString(b), nil
}

func SetChatSession(sessionId string, session context.CancelFunc) {
	if sessionId == "" {
		return
	}
	if _, loaded := chatSessions.LoadOrStore(sessionId, session); loaded {
		return
	}
}

func GetChatSession(sessionId string) context.CancelFunc {
	if sessionId == "" {
		return nil
	}
	cancelFunc, ok := chatSessions.Load(sessionId)
	if ok {
		return cancelFunc.(context.CancelFunc)
	}

	return nil
}

func RemoveChatSession(sessionId string) {
	if sessionId == "" {
		return
	}
	chatSessions.Delete(sessionId)
}
