package problem

import (
	"errors"
	"strings"

	pb "github.com/elebirds/panoptes/internal/gen/proto"
)

type Error struct {
	problem *pb.Problem
}

func (e *Error) Error() string {
	if e == nil || e.problem == nil {
		return "transport problem"
	}
	if e.problem.GetMessage() != "" {
		return e.problem.GetMessage()
	}
	return e.problem.GetCode()
}

func (e *Error) Problem() *pb.Problem {
	if e == nil || e.problem == nil {
		return nil
	}
	return e.problem
}

func New(code string, message string, details ...*pb.ProblemDetail) error {
	return &Error{
		problem: &pb.Problem{
			Code:    code,
			Message: message,
			Details: details,
		},
	}
}

func InvalidRequest(message string, details ...*pb.ProblemDetail) error {
	if message == "" {
		message = "invalid request"
	}
	return New("invalid_request", message, details...)
}

func InternalError(message string) error {
	if message == "" {
		message = "internal error"
	}
	return New("internal_error", message)
}

func UnsupportedCommand(message string) error {
	if message == "" {
		message = "unsupported command"
	}
	return New("invalid_request", message)
}

func AsProblem(err error) (*pb.Problem, bool) {
	if err == nil {
		return nil, false
	}

	var problemErr *Error
	if !errors.As(err, &problemErr) || problemErr == nil {
		return nil, false
	}
	return problemErr.Problem(), true
}

func FromError(err error) *pb.Problem {
	if problem, ok := AsProblem(err); ok {
		return problem
	}
	if err == nil {
		return &pb.Problem{Code: "internal_error", Message: "internal error"}
	}

	code := codeFromErrorText(err.Error())
	return &pb.Problem{Code: code, Message: err.Error()}
}

func codeFromErrorText(text string) string {
	switch strings.TrimSpace(text) {
	case "room_full", "already_in_room", "room_not_found", "phase_mismatch",
		"invalid_status", "invalid_player_count", "player_not_found":
		return strings.TrimSpace(text)
	case "not_host":
		return "unauthorized"
	case "user not found":
		return "user_not_found"
	case "invalid credentials":
		return "invalid_credentials"
	case "user already exists":
		return "user_exists"
	default:
		return "internal_error"
	}
}
