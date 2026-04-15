package debug

import (
	"testing"

	pb "github.com/elebirds/panoptes/internal/gen/proto"
)

func TestSettlementRecorderClonesRecordedMessages(t *testing.T) {
	recorder := NewSettlementRecorder()

	settlement := &pb.MsgTurnSettlement{
		Turn:  2,
		Phase: "resolving",
	}
	gameOver := &pb.MsgGameOver{
		WinnerId: "player-1",
		Reason:   "castle_destroyed",
	}

	recorder.RecordSettlement("room-1", "player-1", settlement)
	recorder.RecordGameOver("room-1", gameOver)

	settlement.Turn = 9
	gameOver.Reason = "mutated"

	gotSettlement := recorder.LatestSettlement("room-1", "player-1")
	if gotSettlement == nil {
		t.Fatalf("LatestSettlement() = nil")
	}
	if gotSettlement.GetTurn() != 2 {
		t.Fatalf("settlement turn = %d, want 2", gotSettlement.GetTurn())
	}

	gotGameOver := recorder.LatestGameOver("room-1")
	if gotGameOver == nil {
		t.Fatalf("LatestGameOver() = nil")
	}
	if gotGameOver.GetReason() != "castle_destroyed" {
		t.Fatalf("game over reason = %q, want castle_destroyed", gotGameOver.GetReason())
	}

	gotSettlement.Turn = 11
	gotGameOver.Reason = "changed_again"

	if recorder.LatestSettlement("room-1", "player-1").GetTurn() != 2 {
		t.Fatalf("LatestSettlement() should return cloned message")
	}
	if recorder.LatestGameOver("room-1").GetReason() != "castle_destroyed" {
		t.Fatalf("LatestGameOver() should return cloned message")
	}
}
