package dispatch

import (
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	transportproblem "github.com/elebirds/panoptes/internal/transport/problem"
)

type Dispatcher struct {
	Auth  AuthHandler
	Lobby LobbyHandler
	Game  GameHandler
}

func (d Dispatcher) Dispatch(ctx InboundContext, frame *pb.ClientFrame) error {
	if frame == nil {
		return transportproblem.InvalidRequest("client frame is nil")
	}

	ctx.RequestID = frame.GetMeta().GetRequestId()
	ctx.TraceID = frame.GetMeta().GetTraceId()

	switch target := frame.Target.(type) {
	case *pb.ClientFrame_Auth:
		return DispatchAuthCommand(ctx, target.Auth, d.Auth)
	case *pb.ClientFrame_Lobby:
		return DispatchLobbyCommand(ctx, target.Lobby, d.Lobby)
	case *pb.ClientFrame_Game:
		return DispatchGameCommand(ctx, target.Game, d.Game)
	default:
		return transportproblem.InvalidRequest("client frame target is required")
	}
}
