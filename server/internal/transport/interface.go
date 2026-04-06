package transport

import "google.golang.org/protobuf/proto"

// GameTransport 是游戏消息推送抽象。
// WebSocket 和 gRPC 都应实现该接口。
type GameTransport interface {
	Send(playerID string, msg proto.Message) error
	Broadcast(roomID string, msg proto.Message) error
	Stream(playerID string, msgs <-chan proto.Message) error
}
