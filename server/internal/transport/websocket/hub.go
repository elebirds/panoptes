// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现WebSocket 传输层的连接集线与广播协调。

package websocket

import (
	"context"
	"errors"
	"log/slog"
	"net/http"
	"sync"

	"github.com/elebirds/panoptes/internal/debug"
	coretransport "github.com/elebirds/panoptes/internal/transport"
	"github.com/elebirds/panoptes/internal/transport/inbound"
	"github.com/gorilla/websocket"
)

type broadcastMsg struct {
	roomID string
	data   []byte
}

type LeaveRoomFunc func(ctx context.Context, playerID string) error
type ConnectFunc func(ctx context.Context, playerID string) error

// Hub manages all active websocket connections.
type Hub struct {
	clients    map[string]*Client
	register   chan *Client
	unregister chan *Client
	broadcast  chan broadcastMsg

	jwtSecret string
	upgrader  websocket.Upgrader
	mu        sync.RWMutex
	dispatcher *inbound.Dispatcher
	leaveRoom LeaveRoomFunc
	onConnect ConnectFunc
	logger    *debug.MessageLogger
}

func NewHub(jwtSecret string) *Hub {
	return &Hub{
		clients:    make(map[string]*Client),
		register:   make(chan *Client),
		unregister: make(chan *Client),
		broadcast:  make(chan broadcastMsg, 64),
		jwtSecret:  jwtSecret,
		upgrader: websocket.Upgrader{
			ReadBufferSize:  1024,
			WriteBufferSize: 1024,
			CheckOrigin: func(_ *http.Request) bool {
				return true
			},
		},
	}
}

func (h *Hub) Run(ctx context.Context) {
	for {
		select {
		case <-ctx.Done():
			return
		case client := <-h.register:
			var stale *Client
			h.mu.Lock()
			if existing, ok := h.clients[client.playerID]; ok {
				stale = existing
			}
			h.clients[client.playerID] = client
			h.mu.Unlock()
			if stale != nil {
				// 旧连接由 Client.Close 统一释放，Hub 不直接处理底层资源。
				go stale.Close()
			}
		case client := <-h.unregister:
			var leaveRoom LeaveRoomFunc
			var removed bool
			h.mu.Lock()
			if registered, ok := h.clients[client.playerID]; ok && registered == client {
				delete(h.clients, client.playerID)
				removed = true
			}
			leaveRoom = h.leaveRoom
			h.mu.Unlock()
			if removed && leaveRoom != nil && client.playerID != "" {
				go func(playerID string) {
					if err := leaveRoom(context.Background(), playerID); err != nil {
						slog.Warn("断线退房失败", "玩家ID", playerID, "错误", err)
					}
				}(client.playerID)
			}
		case msg := <-h.broadcast:
			h.mu.RLock()
			clients := make([]*Client, 0, len(h.clients))
			for _, client := range h.clients {
				if client.roomID == msg.roomID {
					clients = append(clients, client)
				}
			}
			h.mu.RUnlock()

			for _, client := range clients {
				h.logOutgoing(client.playerID, msg.data)
				if err := client.Send(msg.data); err != nil {
					slog.Warn("WebSocket 广播失败", "玩家ID", client.playerID, "错误", err)
				}
			}
		}
	}
}

func (h *Hub) ServeWS(w http.ResponseWriter, r *http.Request) {
	token := r.URL.Query().Get("token")
	playerID, err := coretransport.ParseAndValidateJWT(h.jwtSecret, token)
	if err != nil {
		http.Error(w, "unauthorized", http.StatusUnauthorized)
		return
	}

	conn, err := h.upgrader.Upgrade(w, r, nil)
	if err != nil {
		slog.Warn("WebSocket 升级失败", "错误", err)
		return
	}

	client := &Client{
		hub:      h,
		conn:     conn,
		connectionID: conn.RemoteAddr().String(),
		playerID: playerID,
		roomID:   "",
		send:     make(chan []byte, 256),
	}

	h.register <- client

	h.mu.RLock()
	onConnect := h.onConnect
	h.mu.RUnlock()
	if onConnect != nil {
		// 使用请求的 context，允许调用方通过超时/取消控制初始化逻辑
		ctx := r.Context()
		go func() {
			if err := onConnect(ctx, playerID); err != nil {
				slog.Warn("WebSocket 连接后初始化失败", "玩家ID", playerID, "错误", err)
			}
		}()
	}

	go client.writePump()
	go client.readPump()
}

func (h *Hub) GetClient(playerID string) *Client {
	h.mu.RLock()
	defer h.mu.RUnlock()
	return h.clients[playerID]
}

func (h *Hub) SendToPlayer(playerID string, data []byte) error {
	client := h.GetClient(playerID)
	if client == nil {
		return errors.New("client not connected")
	}
	h.logOutgoing(playerID, data)
	return client.Send(data)
}

func (h *Hub) BroadcastToRoom(roomID string, data []byte) {
	h.broadcast <- broadcastMsg{roomID: roomID, data: data}
}

func (h *Hub) SetDispatcher(dispatcher *inbound.Dispatcher) {
	h.mu.Lock()
	h.dispatcher = dispatcher
	h.mu.Unlock()
}

func (h *Hub) SetLeaveRoomFunc(fn LeaveRoomFunc) {
	h.mu.Lock()
	defer h.mu.Unlock()
	h.leaveRoom = fn
}

func (h *Hub) SetConnectFunc(fn ConnectFunc) {
	h.mu.Lock()
	defer h.mu.Unlock()
	h.onConnect = fn
}

func (h *Hub) SetMessageLogger(logger *debug.MessageLogger) {
	h.mu.Lock()
	defer h.mu.Unlock()
	h.logger = logger
}

func (h *Hub) SetRoom(playerID, roomID string) {
	h.mu.Lock()
	defer h.mu.Unlock()

	if client, ok := h.clients[playerID]; ok {
		client.roomID = roomID
	}
}

func (h *Hub) logIncoming(playerID string, msgType string, payload string) {
	h.mu.RLock()
	logger := h.logger
	h.mu.RUnlock()
	if logger == nil {
		return
	}
	logger.LogIncoming(playerID, msgType, payload)
}

func (h *Hub) logOutgoing(playerID string, data []byte) {
	h.mu.RLock()
	logger := h.logger
	h.mu.RUnlock()
	if logger == nil {
		return
	}
	logger.LogOutgoing(playerID, data)
}
