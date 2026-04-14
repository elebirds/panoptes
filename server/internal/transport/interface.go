package transport

import "google.golang.org/protobuf/proto"

// GameTransport 是游戏消息推送抽象。
// WebSocket 和 gRPC 都应实现该接口。
type GameTransport interface {
	Send(playerID string, msg proto.Message) error
	Broadcast(roomID string, msg proto.Message) error
	Stream(playerID string, msgs <-chan proto.Message) error
}

// GameRoom 是运行中的对局房间最小提交接口。
type GameRoom interface {
	OnHumanSubmitTurn(playerID string)
	OnHumanMessage(playerID, msgType string, payload []byte) error
}

// GameRoomRegistry 是运行中对局房间的最小查询接口。
type GameRoomRegistry interface {
	GetRoomByPlayerID(playerID string) (GameRoom, bool)
}
