package websocket

import (
	"context"
	"errors"
	"log/slog"
	"sync/atomic"

	"github.com/elebirds/panoptes/internal/auth"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/lobby"
	coretransport "github.com/elebirds/panoptes/internal/transport"
	"google.golang.org/protobuf/encoding/protojson"
)

// Sender is a minimal outbound contract for router handlers.
type Sender interface {
	Send(data []byte) error
}

type Router struct {
	lobbySvc  *lobby.LobbyService
	gameRooms coretransport.GameRoomRegistry
}

var activeRouter atomic.Pointer[Router]

func NewRouter(lobbySvc *lobby.LobbyService, gameRooms coretransport.GameRoomRegistry) *Router {
	return &Router{lobbySvc: lobbySvc, gameRooms: gameRooms}
}

func Route(sender Sender, playerID string, envelope *pb.Envelope) {
	router := activeRouter.Load()
	if router == nil {
		slog.Warn("WebSocket Router 未设置", "玩家ID", playerID)
		return
	}
	router.Route(sender, playerID, envelope)
}

func (r *Router) Route(sender Sender, playerID string, envelope *pb.Envelope) {
	if r == nil || r.lobbySvc == nil {
		slog.Warn("LobbyService 未注入 Router", "玩家ID", playerID)
		return
	}

	if envelope == nil {
		slog.Warn("收到空的 WebSocket 封包", "玩家ID", playerID)
		return
	}

	slog.Info("收到 WebSocket 消息", "玩家ID", playerID, "类型", envelope.GetType())

	switch envelope.GetType() {
	case "MsgCreateRoom":
		msg := &pb.MsgCreateRoom{}
		if err := protojson.Unmarshal([]byte(envelope.GetPayload()), msg); err != nil {
			r.sendLobbyError(sender, err)
			return
		}
		if err := r.lobbySvc.CreateRoom(context.Background(), playerID, msg.GetName(), int(msg.GetMaxPlayers())); err != nil {
			r.sendLobbyError(sender, err)
		}
	case "MsgJoinRoom":
		msg := &pb.MsgJoinRoom{}
		if err := protojson.Unmarshal([]byte(envelope.GetPayload()), msg); err != nil {
			r.sendLobbyError(sender, err)
			return
		}
		if err := r.lobbySvc.JoinRoom(context.Background(), playerID, msg.GetRoomCode()); err != nil {
			r.sendLobbyError(sender, err)
		}
	case "MsgLeaveRoom":
		msg := &pb.MsgLeaveRoom{}
		if err := protojson.Unmarshal([]byte(envelope.GetPayload()), msg); err != nil {
			r.sendLobbyError(sender, err)
			return
		}
		if err := r.lobbySvc.LeaveRoom(context.Background(), playerID); err != nil {
			r.sendLobbyError(sender, err)
		}
	case "MsgReadyUp":
		msg := &pb.MsgReadyUp{}
		if err := protojson.Unmarshal([]byte(envelope.GetPayload()), msg); err != nil {
			r.sendLobbyError(sender, err)
			return
		}
		if err := r.lobbySvc.ReadyUp(context.Background(), playerID); err != nil {
			r.sendLobbyError(sender, err)
		}
	case "MsgAddBot":
		msg := &pb.MsgAddBot{}
		if err := protojson.Unmarshal([]byte(envelope.GetPayload()), msg); err != nil {
			r.sendLobbyError(sender, err)
			return
		}
		if err := r.lobbySvc.AddBot(context.Background(), playerID); err != nil {
			r.sendLobbyError(sender, err)
		}
	case "MsgStartGame":
		msg := &pb.MsgStartGame{}
		if err := protojson.Unmarshal([]byte(envelope.GetPayload()), msg); err != nil {
			r.sendLobbyError(sender, err)
			return
		}
		if err := r.lobbySvc.StartGame(context.Background(), playerID); err != nil {
			r.sendLobbyError(sender, err)
		}
	case "MsgKickPlayer":
		msg := &pb.MsgKickPlayer{}
		if err := protojson.Unmarshal([]byte(envelope.GetPayload()), msg); err != nil {
			r.sendLobbyError(sender, err)
			return
		}
		if err := r.lobbySvc.KickPlayer(context.Background(), playerID, msg.GetPlayerId()); err != nil {
			r.sendLobbyError(sender, err)
		}
	case "MsgSubmitDomestic":
		msg := &pb.MsgSubmitDomestic{}
		if err := protojson.Unmarshal([]byte(envelope.GetPayload()), msg); err != nil {
			r.sendLobbyError(sender, err)
			return
		}
		if r.gameRooms == nil {
			r.sendLobbyError(sender, lobby.ErrRoomNotFound)
			return
		}
		room, ok := r.gameRooms.GetRoomByPlayerID(playerID)
		if !ok {
			r.sendLobbyError(sender, lobby.ErrRoomNotFound)
			return
		}
		room.OnHumanSubmitDomestic(playerID)
	case "MsgSubmitCombat":
		msg := &pb.MsgSubmitCombat{}
		if err := protojson.Unmarshal([]byte(envelope.GetPayload()), msg); err != nil {
			r.sendLobbyError(sender, err)
			return
		}
		if r.gameRooms == nil {
			r.sendLobbyError(sender, lobby.ErrRoomNotFound)
			return
		}
		room, ok := r.gameRooms.GetRoomByPlayerID(playerID)
		if !ok {
			r.sendLobbyError(sender, lobby.ErrRoomNotFound)
			return
		}
		room.OnHumanSubmitCombat(playerID)
	case "MsgSetPolicy",
		"MsgTokenBuild",
		"MsgTokenReveal",
		"MsgMinisterDirective",
		"MsgSetWarZone",
		"MsgWarZoneDirective",
		"MsgTokenVetoCombat",
		"MsgTokenMicro",
		"MsgCombatOrder":
		if r.gameRooms == nil {
			r.sendGameNotFound(sender)
			return
		}
		room, ok := r.gameRooms.GetRoomByPlayerID(playerID)
		if !ok {
			r.sendGameNotFound(sender)
			return
		}
		handler, ok := room.(interface {
			OnHumanMessage(playerID, msgType string, payload []byte) error
		})
		if !ok {
			r.sendLobbyError(sender, errors.New("room message handler unavailable"))
			return
		}
		if err := handler.OnHumanMessage(playerID, envelope.GetType(), []byte(envelope.GetPayload())); err != nil {
			r.sendLobbyError(sender, err)
			return
		}
	default:
		slog.Warn("未知的 WebSocket 消息类型", "玩家ID", playerID, "类型", envelope.GetType())
	}
}

func (r *Router) sendGameNotFound(sender Sender) {
	if sender == nil {
		return
	}
	msg := &pb.MsgLobbyError{Code: "game_not_found", Message: "game room not found"}
	data, err := marshalEnvelope(msg)
	if err != nil {
		slog.Warn("序列化 game_not_found 失败", "错误", err)
		return
	}
	if err := sender.Send(data); err != nil {
		slog.Warn("发送 game_not_found 失败", "错误", err)
	}
}

func (r *Router) sendLobbyError(sender Sender, err error) {
	if sender == nil || err == nil {
		return
	}

	msg := &pb.MsgLobbyError{
		Code:    mapLobbyErrorCode(err),
		Message: err.Error(),
	}

	data, marshalErr := marshalEnvelope(msg)
	if marshalErr != nil {
		slog.Warn("序列化 Lobby 错误消息失败", "错误", marshalErr)
		return
	}
	if sendErr := sender.Send(data); sendErr != nil {
		slog.Warn("发送 Lobby 错误消息失败", "错误", sendErr)
	}
}

func mapLobbyErrorCode(err error) string {
	switch {
	case errors.Is(err, lobby.ErrRoomFull):
		return "room_full"
	case errors.Is(err, lobby.ErrAlreadyInRoom):
		return "already_in_room"
	case errors.Is(err, lobby.ErrRoomNotFound):
		return "room_not_found"
	case errors.Is(err, lobby.ErrPlayerNotFound), errors.Is(err, auth.ErrUserNotFound):
		return "player_not_found"
	case errors.Is(err, lobby.ErrNotHost):
		return "unauthorized"
	case errors.Is(err, lobby.ErrInvalidStatus):
		return "invalid_status"
	case errors.Is(err, lobby.ErrInvalidPlayers):
		return "invalid_player_count"
	default:
		return "internal_error"
	}
}
