package debug

import (
	"context"
	"sync"

	"google.golang.org/protobuf/proto"
)

type CaptureTransport struct {
	mu   sync.RWMutex
	sent map[string][]proto.Message
}

func NewCaptureTransport() *CaptureTransport {
	return &CaptureTransport{
		sent: make(map[string][]proto.Message),
	}
}

func (t *CaptureTransport) Send(_ context.Context, playerID string, msg proto.Message) error {
	t.mu.Lock()
	defer t.mu.Unlock()
	t.sent[playerID] = append(t.sent[playerID], proto.Clone(msg))
	return nil
}

func (t *CaptureTransport) Broadcast(context.Context, string, proto.Message) error { return nil }

func (t *CaptureTransport) Stream(ctx context.Context, playerID string, msgs <-chan proto.Message) error {
	for msg := range msgs {
		if err := t.Send(ctx, playerID, msg); err != nil {
			return err
		}
	}
	return nil
}

func (t *CaptureTransport) Snapshot(playerID string) []proto.Message {
	t.mu.RLock()
	defer t.mu.RUnlock()
	out := make([]proto.Message, len(t.sent[playerID]))
	for i, msg := range t.sent[playerID] {
		out[i] = proto.Clone(msg)
	}
	return out
}
