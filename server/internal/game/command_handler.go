package game

import (
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/transport"
	cmddispatch "github.com/elebirds/panoptes/internal/transport/dispatch"
	transportproblem "github.com/elebirds/panoptes/internal/transport/problem"
)

type RegistryCommandHandler struct {
	rooms transport.GameRoomRegistry
}

func NewRegistryCommandHandler(rooms transport.GameRoomRegistry) *RegistryCommandHandler {
	return &RegistryCommandHandler{rooms: rooms}
}

func (h *RegistryCommandHandler) HandleGameCommand(ctx cmddispatch.InboundContext, cmd *pb.GameCommand) error {
	if h == nil || h.rooms == nil {
		return transportproblem.InternalError("game room registry is not configured")
	}

	room, ok := h.rooms.GetRoomByParticipantID(ctx.PlayerID)
	if !ok {
		return transportproblem.New("game_not_found", "game not found")
	}
	return room.HandleGameCommand(ctx, cmd)
}
