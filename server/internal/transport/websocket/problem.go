package websocket

import (
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	transportproblem "github.com/elebirds/panoptes/internal/transport/problem"
)

func problemFromError(err error) *pb.Problem {
	return transportproblem.FromError(err)
}
