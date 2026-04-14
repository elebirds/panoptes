package dispatch

import (
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	transportproblem "github.com/elebirds/panoptes/internal/transport/problem"
)

type InboundContext struct {
	PlayerID     string
	ConnectionID string
	RequestID    string
	TraceID      string
}

func AsProblem(err error) (*pb.Problem, bool) {
	return transportproblem.AsProblem(err)
}
