package websocket

import (
	"errors"

	"github.com/elebirds/panoptes/internal/auth"
	"github.com/elebirds/panoptes/internal/game"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/lobby"
	transportproblem "github.com/elebirds/panoptes/internal/transport/problem"
)

func problemFromError(err error) *pb.Problem {
	if problem, ok := transportproblem.AsProblem(err); ok {
		return problem
	}

	switch {
	case err == nil:
		return &pb.Problem{Code: "internal_error", Message: "internal error"}
	case errors.Is(err, lobby.ErrRoomFull):
		return &pb.Problem{Code: "room_full", Message: err.Error()}
	case errors.Is(err, lobby.ErrAlreadyInRoom):
		return &pb.Problem{Code: "already_in_room", Message: err.Error()}
	case errors.Is(err, lobby.ErrRoomNotFound):
		return &pb.Problem{Code: "room_not_found", Message: err.Error()}
	case errors.Is(err, lobby.ErrPlayerNotFound), errors.Is(err, auth.ErrUserNotFound):
		return &pb.Problem{Code: "player_not_found", Message: err.Error()}
	case errors.Is(err, lobby.ErrNotHost):
		return &pb.Problem{Code: "unauthorized", Message: err.Error()}
	case errors.Is(err, lobby.ErrInvalidStatus):
		return &pb.Problem{Code: "invalid_status", Message: err.Error()}
	case errors.Is(err, lobby.ErrInvalidPlayers):
		return &pb.Problem{Code: "invalid_player_count", Message: err.Error()}
	case errors.Is(err, game.ErrPhaseMismatch):
		return &pb.Problem{Code: "phase_mismatch", Message: err.Error()}
	default:
		return &pb.Problem{Code: "internal_error", Message: err.Error()}
	}
}
