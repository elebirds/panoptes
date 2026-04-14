// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现调试支持模块的中间件逻辑。

package debug

import (
	"log/slog"
	"sync"

	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"google.golang.org/protobuf/encoding/protojson"
)

type playerSnapshot struct {
	turn  int32
	phase string
}

// MessageLogger logs websocket ingress/egress messages in DEV_MODE.
type MessageLogger struct {
	enabled bool

	mu       sync.RWMutex
	players  map[string]playerSnapshot
	jsonOpts protojson.UnmarshalOptions
}

func NewMessageLogger(enabled bool) *MessageLogger {
	return &MessageLogger{
		enabled:  enabled,
		players:  make(map[string]playerSnapshot),
		jsonOpts: protojson.UnmarshalOptions{DiscardUnknown: true},
	}
}

func (l *MessageLogger) LogOutgoing(playerID string, raw []byte) {
	if l == nil || !l.enabled {
		return
	}

	envelope := &pb.Envelope{}
	if err := l.jsonOpts.Unmarshal(raw, envelope); err != nil {
		slog.Debug("→ 推送消息",
			"player_id", playerID,
			"type", "unknown",
			"turn", int32(0),
			"phase", "",
			"payload_size", len(raw),
		)
		return
	}

	turn, phase := l.resolveOutgoingSnapshot(playerID, envelope.GetType(), envelope.GetPayload())
	slog.Debug("→ 推送消息",
		"player_id", playerID,
		"type", envelope.GetType(),
		"turn", turn,
		"phase", phase,
		"payload_size", len(envelope.GetPayload()),
	)
}

func (l *MessageLogger) LogIncoming(playerID string, msgType string, payloadJSON string) {
	if l == nil || !l.enabled {
		return
	}

	slog.Debug("← 收到消息",
		"player_id", playerID,
		"type", msgType,
		"raw", payloadJSON,
	)
}

func (l *MessageLogger) resolveOutgoingSnapshot(playerID string, msgType string, payload string) (int32, string) {
	prev := l.snapshot(playerID)
	turn := prev.turn
	phase := prev.phase

	switch msgType {
	case "MsgGameInit":
		msg := &pb.MsgGameInit{}
		if err := l.jsonOpts.Unmarshal([]byte(payload), msg); err == nil {
			turn = msg.GetTurn()
			phase = msg.GetPhase()
		}
	case "MsgPlanningStart":
		msg := &pb.MsgPlanningStart{}
		if err := l.jsonOpts.Unmarshal([]byte(payload), msg); err == nil {
			turn = msg.GetTurn()
			phase = msg.GetPhase()
		}
	case "MsgTurnSettlement":
		msg := &pb.MsgTurnSettlement{}
		if err := l.jsonOpts.Unmarshal([]byte(payload), msg); err == nil {
			turn = msg.GetTurn()
			phase = msg.GetPhase()
		}
	}

	l.storeSnapshot(playerID, playerSnapshot{turn: turn, phase: phase})
	return turn, phase
}

func (l *MessageLogger) snapshot(playerID string) playerSnapshot {
	l.mu.RLock()
	defer l.mu.RUnlock()
	return l.players[playerID]
}

func (l *MessageLogger) storeSnapshot(playerID string, snap playerSnapshot) {
	l.mu.Lock()
	defer l.mu.Unlock()
	l.players[playerID] = snap
}
