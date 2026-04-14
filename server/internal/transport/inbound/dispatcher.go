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

func (d Dispatcher) Dispatch(ctx cmddispatch.InboundContext, frame *pb.ClientFrame) error {
	if frame == nil {
		return transportproblem.InvalidRequest("client frame is nil")
	}
	ctx.RequestID = frame.GetMeta().GetRequestId()
	ctx.TraceID = frame.GetMeta().GetTraceId()

	switch target := frame.Target.(type) {
	case *pb.ClientFrame_Auth:
		if d.Auth == nil {
			return transportproblem.InternalError("auth handler is not configured")
		}
		return d.Auth.HandleAuthCommand(ctx, target.Auth)
	case *pb.ClientFrame_Lobby:
		if d.Lobby == nil {
			return transportproblem.InternalError("lobby handler is not configured")
		}
		return d.Lobby.HandleLobbyCommand(ctx, target.Lobby)
	case *pb.ClientFrame_Game:
		if d.Game == nil {
			return transportproblem.InternalError("game handler is not configured")
		}
		return d.Game.HandleGameCommand(ctx, target.Game)
	default:
		return transportproblem.InvalidRequest("client frame target is required")
	}
}
