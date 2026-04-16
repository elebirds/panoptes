package transport

import (
	"context"

	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/transport/dispatch"
	"google.golang.org/protobuf/proto"
)

type eventMetaContextKey struct{}

func ContextWithEventMeta(ctx context.Context, meta *pb.EventMeta) context.Context {
	if ctx == nil {
		ctx = context.Background()
	}
	if meta == nil {
		return ctx
	}
	return context.WithValue(ctx, eventMetaContextKey{}, proto.Clone(meta))
}

func EventMetaFromContext(ctx context.Context) *pb.EventMeta {
	if ctx == nil {
		return nil
	}
	meta, _ := ctx.Value(eventMetaContextKey{}).(*pb.EventMeta)
	if meta == nil {
		return nil
	}
	return proto.Clone(meta).(*pb.EventMeta)
}

func EventMetaFromInbound(ctx dispatch.InboundContext) *pb.EventMeta {
	if ctx.RequestID == "" && ctx.TraceID == "" {
		return nil
	}
	return &pb.EventMeta{
		RequestId: ctx.RequestID,
		TraceId:   ctx.TraceID,
	}
}

func ContextWithGameSessionID(ctx context.Context, gameSessionID string) context.Context {
	if ctx == nil {
		ctx = context.Background()
	}
	if gameSessionID == "" {
		return ctx
	}
	meta := EventMetaFromContext(ctx)
	if meta == nil {
		meta = &pb.EventMeta{}
	}
	meta.GameSessionId = gameSessionID
	return ContextWithEventMeta(ctx, meta)
}
