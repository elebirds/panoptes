// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 验证调试支持模块的中间件逻辑。

package debug

import (
	"testing"

	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"google.golang.org/protobuf/encoding/protojson"
)

func TestMessageLoggerResolveOutgoingSnapshotTracksPlanningStart(t *testing.T) {
	logger := NewMessageLogger(true)
	payload, err := protojson.Marshal(&pb.MsgPlanningStart{
		Turn:  3,
		Phase: "planning",
	})
	if err != nil {
		t.Fatalf("Marshal() error = %v", err)
	}

	turn, phase := logger.resolveOutgoingSnapshot("player-1", "MsgPlanningStart", string(payload))
	if turn != 3 || phase != "planning" {
		t.Fatalf("snapshot = (%d, %q)", turn, phase)
	}
}

func TestMessageLoggerResolveOutgoingSnapshotTracksTurnSettlement(t *testing.T) {
	logger := NewMessageLogger(true)
	logger.storeSnapshot("player-1", playerSnapshot{turn: 2, phase: "planning"})

	payload, err := protojson.Marshal(&pb.MsgTurnSettlement{
		Turn:  2,
		Phase: "resolving",
	})
	if err != nil {
		t.Fatalf("Marshal() error = %v", err)
	}

	turn, phase := logger.resolveOutgoingSnapshot("player-1", "MsgTurnSettlement", string(payload))
	if turn != 2 || phase != "resolving" {
		t.Fatalf("snapshot = (%d, %q)", turn, phase)
	}
}
