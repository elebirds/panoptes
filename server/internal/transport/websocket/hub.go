package websocket

import (
	"errors"
	"log/slog"
	"net/http"
	"sync"

	coretransport "github.com/elebirds/panoptes/internal/transport"
	"github.com/gorilla/websocket"
)

type broadcastMsg struct {
	roomID string
	data   []byte
}

// Hub manages all active websocket connections.
type Hub struct {
	clients    map[string]*Client
	register   chan *Client
	unregister chan *Client
	broadcast  chan broadcastMsg

	jwtSecret string
	upgrader  websocket.Upgrader
	mu        sync.RWMutex
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

func (h *Hub) Run() {
	for {
		select {
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
			// Hub 仅维护连接索引；真正的资源清理由 Client.Close 负责。
			h.mu.Lock()
			if registered, ok := h.clients[client.playerID]; ok && registered == client {
				delete(h.clients, client.playerID)
			}
			h.mu.Unlock()
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
		playerID: playerID,
		roomID:   "",
		send:     make(chan []byte, 256),
	}

	h.register <- client
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
	return client.Send(data)
}

func (h *Hub) BroadcastToRoom(roomID string, data []byte) {
	h.broadcast <- broadcastMsg{roomID: roomID, data: data}
}

func (h *Hub) SetRoom(playerID, roomID string) {
	h.mu.Lock()
	defer h.mu.Unlock()

	if client, ok := h.clients[playerID]; ok {
		client.roomID = roomID
	}
}
