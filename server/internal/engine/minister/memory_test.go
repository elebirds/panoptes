package minister

import (
	"strings"
	"testing"
)

func TestMinisterMemoryFeedbackAdjustsFavorAndPromptHint(t *testing.T) {
	memory := &MinisterMemory{PlayerID: "player-1", Role: "domestic", Favor: 50}

	memory.Add(MemoryEntry{Turn: 1, Type: "draft_feedback", Content: "policy:扩张", Outcome: "rejected", PlayerResp: "rejected"})
	memory.Add(MemoryEntry{Turn: 2, Type: "draft_feedback", Content: "policy:扩张", Outcome: "rejected", PlayerResp: "rejected"})
	memory.Add(MemoryEntry{Turn: 3, Type: "draft_feedback", Content: "policy:扩张", Outcome: "rejected", PlayerResp: "rejected"})

	if got := memory.GetFavor(); got != 26 {
		t.Fatalf("favor = %d, want 26", got)
	}
	prompt := memory.ToPromptString()
	if !strings.Contains(prompt, "favor=26/100") || !strings.Contains(prompt, "玩家近期多次否决") {
		t.Fatalf("prompt = %q, want low-favor behavior hint", prompt)
	}

	memory.ChangeFavor(100)
	if got := memory.GetFavor(); got != 100 {
		t.Fatalf("favor after clamp = %d, want 100", got)
	}
}
