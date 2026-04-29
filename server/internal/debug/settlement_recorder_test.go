package debug

import (
	"testing"

	pb "github.com/elebirds/panoptes/internal/gen/proto"
)

func TestSettlementRecorderClonesRecordedMessages(t *testing.T) {
	recorder := NewSettlementRecorder()

	syncMsg := &pb.MsgGameSync{
		Turn:  2,
		Phase: "resolving",
	}
	gameOver := &pb.MsgGameOver{
		WinnerId: "player-1",
		Reason:   "city_core_destroyed",
	}

	recorder.RecordGameSync("room-1", "player-1", syncMsg)
	recorder.RecordGameOver("room-1", gameOver)

	syncMsg.Turn = 9
	gameOver.Reason = "mutated"

	gotSync := recorder.LatestGameSync("room-1", "player-1")
	if gotSync == nil {
		t.Fatalf("LatestGameSync() = nil")
	}
	if gotSync.GetTurn() != 2 {
		t.Fatalf("game sync turn = %d, want 2", gotSync.GetTurn())
	}

	gotGameOver := recorder.LatestGameOver("room-1")
	if gotGameOver == nil {
		t.Fatalf("LatestGameOver() = nil")
	}
	if gotGameOver.GetReason() != "city_core_destroyed" {
		t.Fatalf("game over reason = %q, want city_core_destroyed", gotGameOver.GetReason())
	}

	gotSync.Turn = 11
	gotGameOver.Reason = "changed_again"

	if recorder.LatestGameSync("room-1", "player-1").GetTurn() != 2 {
		t.Fatalf("LatestGameSync() should return cloned message")
	}
	if recorder.LatestGameOver("room-1").GetReason() != "city_core_destroyed" {
		t.Fatalf("LatestGameOver() should return cloned message")
	}
}
