package debug

import (
	"testing"
	"time"

	"github.com/elebirds/panoptes/internal/game/scenario"
)

func TestHarnessH0MixedPVESoakPreservesStateInvariants(t *testing.T) {
	def, err := scenario.PVESoak()
	if err != nil {
		t.Fatalf("scenario build error = %v", err)
	}

	h, err := NewHarness(def)
	if err != nil {
		t.Fatalf("NewHarness() error = %v", err)
	}
	if err := h.Start(); err != nil {
		t.Fatalf("Start() error = %v", err)
	}
	if !h.room.HasParticipant("bot-1") {
		t.Fatalf("room should include bot-1 participant")
	}
	if h.room.IsHumanParticipant("bot-1") {
		t.Fatalf("bot-1 should be autonomous, not human")
	}

	AssertStateInvariants(t, h.room.State())
	for turn := 1; turn <= 40; turn++ {
		start, err := h.WaitPlanningStart("player-1", turn, 2*time.Second)
		if err != nil {
			t.Fatalf("WaitPlanningStart(turn=%d) error = %v", turn, err)
		}
		if start.GetInformationReport() == nil {
			t.Fatalf("turn %d planning start missing information report", turn)
		}
		AssertStateInvariants(t, h.room.State())

		if err := h.SubmitTurn("player-1"); err != nil {
			t.Fatalf("SubmitTurn(turn=%d) error = %v", turn, err)
		}
		record, err := h.WaitGameSync("player-1", turn, 3*time.Second)
		if err != nil {
			t.Fatalf("WaitGameSync(turn=%d) error = %v", turn, err)
		}
		if record.GameSync.GetInformationReport() == nil {
			t.Fatalf("turn %d game sync missing information report", turn)
		}
		AssertStateInvariants(t, h.room.State())
		if record.Summary.IsOver {
			t.Fatalf("game ended during H0 soak at turn %d: winner=%q reason=%q", turn, record.Summary.WinnerID, record.Summary.OverReason)
		}
	}
}
