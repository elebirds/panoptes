package websocket

import (
	"log/slog"

	pb "github.com/elebirds/panoptes/internal/gen/proto"
)

// Sender is a minimal outbound contract for router handlers.
type Sender interface {
	Send(data []byte) error
}

func Route(sender Sender, playerID string, envelope *pb.Envelope) {
	// sender 用于 handler 向客户端回复消息，当前阶段 handler 尚未实现。
	_ = sender

	if envelope == nil {
		slog.Warn("收到空的 WebSocket 封包", "玩家ID", playerID)
		return
	}

	slog.Info("收到 WebSocket 消息", "玩家ID", playerID, "类型", envelope.GetType())

	switch envelope.GetType() {
	default:
		slog.Warn("未知的 WebSocket 消息类型", "玩家ID", playerID, "类型", envelope.GetType())
	}

	// TODO: 在这里挂接具体游戏消息处理器。
}
