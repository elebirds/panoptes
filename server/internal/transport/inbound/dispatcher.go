// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载入站协议帧分发适配逻辑。

package inbound

import (
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	cmddispatch "github.com/elebirds/panoptes/internal/transport/dispatch"
	transportproblem "github.com/elebirds/panoptes/internal/transport/problem"
)

type AuthHandler interface {
	HandleAuthCommand(ctx cmddispatch.InboundContext, cmd *pb.AuthCommand) error
}

type LobbyHandler interface {
	HandleLobbyCommand(ctx cmddispatch.InboundContext, cmd *pb.LobbyCommand) error
}

type GameHandler interface {
	HandleGameCommand(ctx cmddispatch.InboundContext, cmd *pb.GameCommand) error
}

type Dispatcher struct {
	Auth  AuthHandler
	Lobby LobbyHandler
	Game  GameHandler
}

// Dispatch 复用 transport/dispatch 的 oneof 分发，再适配到 auth/lobby/game 的粗粒度 handler。
// 这样生产入口只维护一套 ClientFrame oneof 路由，同时保留现有 app wiring。
func (d Dispatcher) Dispatch(ctx cmddispatch.InboundContext, frame *pb.ClientFrame) error {
	return cmddispatch.Dispatcher{
		Auth:  authAdapter{handler: d.Auth},
		Lobby: lobbyAdapter{handler: d.Lobby},
		Game:  gameAdapter{handler: d.Game},
	}.Dispatch(ctx, frame)
}

type authAdapter struct {
	handler AuthHandler
}

func (a authAdapter) Register(ctx cmddispatch.InboundContext, cmd *pb.MsgRegister) error {
	return a.handle(ctx, &pb.AuthCommand{Body: &pb.AuthCommand_Register{Register: cmd}})
}

func (a authAdapter) Login(ctx cmddispatch.InboundContext, cmd *pb.MsgLogin) error {
	return a.handle(ctx, &pb.AuthCommand{Body: &pb.AuthCommand_Login{Login: cmd}})
}

func (a authAdapter) handle(ctx cmddispatch.InboundContext, cmd *pb.AuthCommand) error {
	if a.handler == nil {
		return transportproblem.InternalError("auth handler is not configured")
	}
	return a.handler.HandleAuthCommand(ctx, cmd)
}

type lobbyAdapter struct {
	handler LobbyHandler
}

func (a lobbyAdapter) CreateRoom(ctx cmddispatch.InboundContext, cmd *pb.MsgCreateRoom) error {
	return a.handle(ctx, &pb.LobbyCommand{Body: &pb.LobbyCommand_CreateRoom{CreateRoom: cmd}})
}

func (a lobbyAdapter) JoinRoom(ctx cmddispatch.InboundContext, cmd *pb.MsgJoinRoom) error {
	return a.handle(ctx, &pb.LobbyCommand{Body: &pb.LobbyCommand_JoinRoom{JoinRoom: cmd}})
}

func (a lobbyAdapter) LeaveRoom(ctx cmddispatch.InboundContext, cmd *pb.MsgLeaveRoom) error {
	return a.handle(ctx, &pb.LobbyCommand{Body: &pb.LobbyCommand_LeaveRoom{LeaveRoom: cmd}})
}

func (a lobbyAdapter) ReadyUp(ctx cmddispatch.InboundContext, cmd *pb.MsgReadyUp) error {
	return a.handle(ctx, &pb.LobbyCommand{Body: &pb.LobbyCommand_ReadyUp{ReadyUp: cmd}})
}

func (a lobbyAdapter) AddBot(ctx cmddispatch.InboundContext, cmd *pb.MsgAddBot) error {
	return a.handle(ctx, &pb.LobbyCommand{Body: &pb.LobbyCommand_AddBot{AddBot: cmd}})
}

func (a lobbyAdapter) StartGame(ctx cmddispatch.InboundContext, cmd *pb.MsgStartGame) error {
	return a.handle(ctx, &pb.LobbyCommand{Body: &pb.LobbyCommand_StartGame{StartGame: cmd}})
}

func (a lobbyAdapter) KickPlayer(ctx cmddispatch.InboundContext, cmd *pb.MsgKickPlayer) error {
	return a.handle(ctx, &pb.LobbyCommand{Body: &pb.LobbyCommand_KickPlayer{KickPlayer: cmd}})
}

func (a lobbyAdapter) handle(ctx cmddispatch.InboundContext, cmd *pb.LobbyCommand) error {
	if a.handler == nil {
		return transportproblem.InternalError("lobby handler is not configured")
	}
	return a.handler.HandleLobbyCommand(ctx, cmd)
}

type gameAdapter struct {
	handler GameHandler
}

func (a gameAdapter) Planning(ctx cmddispatch.InboundContext, cmd *pb.PlanningCommand) error {
	return a.handle(ctx, &pb.GameCommand{Body: &pb.GameCommand_Planning{Planning: cmd}})
}

func (a gameAdapter) StaticCatalogSyncRequest(ctx cmddispatch.InboundContext, cmd *pb.MsgStaticCatalogSyncRequest) error {
	return a.handle(ctx, &pb.GameCommand{Body: &pb.GameCommand_StaticCatalogSyncRequest{StaticCatalogSyncRequest: cmd}})
}

func (a gameAdapter) Chat(ctx cmddispatch.InboundContext, cmd *pb.ChatCommand) error {
	return a.handle(ctx, &pb.GameCommand{Body: &pb.GameCommand_Chat{Chat: cmd}})
}

func (a gameAdapter) CommandBatch(ctx cmddispatch.InboundContext, cmd *pb.MsgGameCommandBatch) error {
	return a.handle(ctx, &pb.GameCommand{Body: &pb.GameCommand_CommandBatch{CommandBatch: cmd}})
}

func (a gameAdapter) AcknowledgeTurnReport(ctx cmddispatch.InboundContext, cmd *pb.MsgAcknowledgeTurnReport) error {
	return a.handle(ctx, &pb.GameCommand{Body: &pb.GameCommand_AcknowledgeTurnReport{AcknowledgeTurnReport: cmd}})
}

func (a gameAdapter) handle(ctx cmddispatch.InboundContext, cmd *pb.GameCommand) error {
	if a.handler == nil {
		return transportproblem.InternalError("game handler is not configured")
	}
	return a.handler.HandleGameCommand(ctx, cmd)
}
