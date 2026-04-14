// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现WebSocket 传输层的消息传输逻辑。

package websocket

import (
	"errors"

	coretransport "github.com/elebirds/panoptes/internal/transport"
	"github.com/elebirds/panoptes/internal/transport/codec"
	"google.golang.org/protobuf/proto"
)

var _ coretransport.GameTransport = (*WSTransport)(nil)

// WSTransport adapts GameTransport to websocket clients managed by Hub.
type WSTransport struct {
	hub *Hub
}

func NewTransport(hub *Hub) *WSTransport {
	return &WSTransport{hub: hub}
}

func (t *WSTransport) Send(playerID string, msg proto.Message) error {
	if playerID == "" {
		return errors.New("playerID is required")
	}

	data, err := codec.EncodeServerMessage(msg, nil)
	if err != nil {
		return err
	}

	return t.hub.SendToPlayer(playerID, data)
}

func (t *WSTransport) Broadcast(roomID string, msg proto.Message) error {
	if roomID == "" {
		return errors.New("roomID is required")
	}

	data, err := codec.EncodeServerMessage(msg, nil)
	if err != nil {
		return err
	}

	t.hub.BroadcastToRoom(roomID, data)
	return nil
}

func (t *WSTransport) Stream(playerID string, msgs <-chan proto.Message) error {
	for msg := range msgs {
		if err := t.Send(playerID, msg); err != nil {
			return err
		}
	}
	return nil
}
