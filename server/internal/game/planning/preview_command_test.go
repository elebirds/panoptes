// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载 planning 命令处理、端口拆分与响应投递相关逻辑。

package planning

import (
	"context"
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	coretransport "github.com/elebirds/panoptes/internal/transport"
	cmddispatch "github.com/elebirds/panoptes/internal/transport/dispatch"
	"google.golang.org/protobuf/proto"
)

type previewCommandSessionStub struct {
	*planningSessionStub
	sentMeta      map[string][]*pb.EventMeta
	snapshotCount int
}

func newPreviewCommandSessionStub(state *domain.GameState) *previewCommandSessionStub {
	return &previewCommandSessionStub{
		planningSessionStub: newPlanningSessionStub(state),
		sentMeta:            make(map[string][]*pb.EventMeta),
	}
}

func (s *previewCommandSessionStub) SendToPlayer(ctx context.Context, playerID string, msg proto.Message) error {
	s.sentMeta[playerID] = append(s.sentMeta[playerID], coretransport.EventMetaFromContext(ctx))
	return s.planningSessionStub.SendToPlayer(ctx, playerID, msg)
}

func (s *previewCommandSessionStub) SendPlanningSnapshot(ctx context.Context, playerID string) error {
	s.snapshotCount++
	return s.planningSessionStub.SendPlanningSnapshot(ctx, playerID)
}

func TestHandleCommandPreviewPreservesInboundMetaWithoutSnapshot(t *testing.T) {
	state, unitID := newPreviewState(t)
	session := newPreviewCommandSessionStub(state)
	service := &Service{}

	err := service.HandleCommand(session, cmddispatch.InboundContext{
		PlayerID:  "player-1",
		RequestID: "transport-req-1",
		TraceID:   "trace-1",
	}, &pb.PlanningCommand{
		Body: &pb.PlanningCommand_PlanningPathPreviewRequest{
			PlanningPathPreviewRequest: &pb.MsgPlanningPathPreviewRequest{
				RequestId:    "preview-req-1",
				UnitId:       unitID,
				Action:       "move",
				TargetNodeId: "N3_0",
			},
		},
	})
	if err != nil {
		t.Fatalf("HandleCommand() error = %v", err)
	}

	msgs := session.sent["player-1"]
	if len(msgs) != 1 {
		t.Fatalf("sent messages = %d, want preview response only", len(msgs))
	}
	resp, ok := msgs[0].(*pb.MsgPlanningPathPreviewResponse)
	if !ok {
		t.Fatalf("message type = %T, want MsgPlanningPathPreviewResponse", msgs[0])
	}
	if !resp.GetValid() || resp.GetRequestId() != "preview-req-1" {
		t.Fatalf("preview response = %#v, want valid response with preview request id", resp)
	}
	if session.snapshotCount != 0 {
		t.Fatalf("snapshot count = %d, want 0 for preview command", session.snapshotCount)
	}
	if got := len(state.TurnRuntime.Planning.UnitOrders); got != 0 {
		t.Fatalf("unit order draft count = %d, want 0", got)
	}

	metas := session.sentMeta["player-1"]
	if len(metas) != 1 || metas[0] == nil {
		t.Fatalf("sent meta = %#v, want request correlation", metas)
	}
	if metas[0].GetRequestId() != "transport-req-1" || metas[0].GetTraceId() != "trace-1" {
		t.Fatalf("event meta = %#v, want transport request/trace ids", metas[0])
	}
}
