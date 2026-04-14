package lobby

import (
	"context"

	pb "github.com/elebirds/panoptes/internal/gen/proto"
	cmddispatch "github.com/elebirds/panoptes/internal/transport/dispatch"
	transportproblem "github.com/elebirds/panoptes/internal/transport/problem"
)

type CommandHandler struct {
	service *LobbyService
}

func NewCommandHandler(service *LobbyService) *CommandHandler {
	return &CommandHandler{service: service}
}

func (h *CommandHandler) HandleLobbyCommand(ctx cmddispatch.InboundContext, cmd *pb.LobbyCommand) error {
	if h == nil || h.service == nil {
		return transportproblem.InternalError("lobby service is not configured")
	}
	return cmddispatch.DispatchLobbyCommand(ctx, cmd, lobbyAdapter{service: h.service})
}

type lobbyAdapter struct {
	service *LobbyService
}

func (a lobbyAdapter) CreateRoom(ctx cmddispatch.InboundContext, cmd *pb.MsgCreateRoom) error {
	return a.service.CreateRoom(context.Background(), ctx.PlayerID, cmd.GetName(), int(cmd.GetMaxPlayers()))
}

func (a lobbyAdapter) JoinRoom(ctx cmddispatch.InboundContext, cmd *pb.MsgJoinRoom) error {
	return a.service.JoinRoom(context.Background(), ctx.PlayerID, cmd.GetRoomCode())
}

func (a lobbyAdapter) LeaveRoom(ctx cmddispatch.InboundContext, _ *pb.MsgLeaveRoom) error {
	return a.service.LeaveRoom(context.Background(), ctx.PlayerID)
}

func (a lobbyAdapter) ReadyUp(ctx cmddispatch.InboundContext, _ *pb.MsgReadyUp) error {
	return a.service.ReadyUp(context.Background(), ctx.PlayerID)
}

func (a lobbyAdapter) AddBot(ctx cmddispatch.InboundContext, _ *pb.MsgAddBot) error {
	return a.service.AddBot(context.Background(), ctx.PlayerID)
}

func (a lobbyAdapter) StartGame(ctx cmddispatch.InboundContext, _ *pb.MsgStartGame) error {
	return a.service.StartGame(context.Background(), ctx.PlayerID)
}

func (a lobbyAdapter) KickPlayer(ctx cmddispatch.InboundContext, cmd *pb.MsgKickPlayer) error {
	return a.service.KickPlayer(context.Background(), ctx.PlayerID, cmd.GetPlayerId())
}
