// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载 planning 命令处理、端口拆分与响应投递相关逻辑。

package planning

import (
	"context"
	"testing"

	pb "github.com/elebirds/panoptes/internal/gen/proto"
	coretransport "github.com/elebirds/panoptes/internal/transport"
	"google.golang.org/protobuf/proto"
)

type deliveryRecorder struct {
	ops      []string
	playerID string
	meta     *pb.EventMeta
}

func (r *deliveryRecorder) SendToPlayer(ctx context.Context, playerID string, msg proto.Message) error {
	r.playerID = playerID
	r.meta = coretransport.EventMetaFromContext(ctx)
	switch msg.(type) {
	case *pb.MsgBuildStructureResult:
		r.ops = append(r.ops, "build_result")
	case *pb.MsgTokenResult:
		r.ops = append(r.ops, "token_result")
	default:
		r.ops = append(r.ops, "message")
	}
	return nil
}

func (r *deliveryRecorder) SendPlanningSnapshot(ctx context.Context, playerID string) error {
	r.playerID = playerID
	r.meta = coretransport.EventMetaFromContext(ctx)
	r.ops = append(r.ops, "snapshot")
	return nil
}

func TestCommandDeliverySendsResultsBeforeSnapshot(t *testing.T) {
	recorder := &deliveryRecorder{}
	ctx := coretransport.ContextWithEventMeta(context.Background(), &pb.EventMeta{
		RequestId: "req-1",
		TraceId:   "trace-1",
	})
	delivery := newCommandDelivery(ctx, "player-1", recorder)

	delivery.sendAllWithSnapshot(
		&pb.MsgBuildStructureResult{Success: true},
		&pb.MsgTokenResult{Success: true, Action: "build"},
	)

	wantOps := []string{"build_result", "token_result", "snapshot"}
	if len(recorder.ops) != len(wantOps) {
		t.Fatalf("ops = %#v, want %#v", recorder.ops, wantOps)
	}
	for i := range wantOps {
		if recorder.ops[i] != wantOps[i] {
			t.Fatalf("ops = %#v, want %#v", recorder.ops, wantOps)
		}
	}
	if recorder.playerID != "player-1" {
		t.Fatalf("playerID = %q, want player-1", recorder.playerID)
	}
	if recorder.meta == nil || recorder.meta.GetRequestId() != "req-1" || recorder.meta.GetTraceId() != "trace-1" {
		t.Fatalf("meta = %#v, want request/trace metadata", recorder.meta)
	}
}
