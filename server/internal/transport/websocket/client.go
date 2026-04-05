package websocket

import (
	"errors"
	"log/slog"
	"sync"

	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/gorilla/websocket"
	"google.golang.org/protobuf/encoding/protojson"
)

// Client represents a single websocket connection.
type Client struct {
	hub      *Hub
	conn     *websocket.Conn
	playerID string
	roomID   string
	send     chan []byte

	closeOnce sync.Once
	sendOnce  sync.Once
}

func (c *Client) readPump() {
	// readPump 和 writePump 都会 defer Close；通过 closeOnce 保证释放逻辑只执行一次。
	defer c.Close()

	for {
		_, message, err := c.conn.ReadMessage()
		if err != nil {
			if !websocket.IsCloseError(err, websocket.CloseNormalClosure, websocket.CloseGoingAway) {
				slog.Warn("WebSocket 读取失败", "玩家ID", c.playerID, "错误", err)
			}
			return
		}

		envelope := &pb.Envelope{}
		if err := protojson.Unmarshal(message, envelope); err != nil {
			slog.Warn("无效的 WebSocket 消息封包", "玩家ID", c.playerID, "错误", err)
			continue
		}

		Route(c, c.playerID, envelope)
	}
}

func (c *Client) writePump() {
	// writePump 退出时也会进入统一的 Close 路径，避免分散清理职责。
	defer c.Close()

	for message := range c.send {
		if err := c.conn.WriteMessage(websocket.TextMessage, message); err != nil {
			slog.Warn("WebSocket 写入失败", "玩家ID", c.playerID, "错误", err)
			return
		}
	}
}

func (c *Client) Close() {
	c.closeOnce.Do(func() {
		// 设计约束：Client 负责所有连接资源释放。
		// Hub 只处理 clients 索引，不直接关闭 conn/send，避免职责交叉。
		c.hub.unregister <- c
		c.closeSend()
		_ = c.conn.Close()
	})
}

func (c *Client) closeSend() {
	c.sendOnce.Do(func() {
		// closeSend 只负责发送通道生命周期，不负责连接状态管理。
		// 如果未来把 send 关闭职责分散到 Hub 和 Client 两处，容易出现：
		// 1) 重复关闭 channel 触发 panic；
		// 2) 并发发送命中已关闭通道导致 "send on closed channel"；
		// 3) 关闭链路分散，排查连接退出问题时难以定位责任边界。
		close(c.send)
	})
}

func (c *Client) Send(data []byte) error {
	select {
	case c.send <- data:
		return nil
	default:
		return errors.New("client send buffer is full")
	}
}
