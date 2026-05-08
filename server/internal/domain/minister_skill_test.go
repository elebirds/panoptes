package domain

import (
	"testing"

	"github.com/elebirds/panoptes/internal/staticdata"
)

func TestDefaultMinisterSkillLoadoutsAssignsCardsByRoleTags(t *testing.T) {
	loadouts := DefaultMinisterSkillLoadouts([]staticdata.MinisterSkillCard{
		{ID: "stargazing", RoleTags: []string{"domestic", "military"}},
		{ID: "empty"},
	})

	if got := loadouts["domestic"]; len(got) != 1 || got[0] != "stargazing" {
		t.Fatalf("domestic loadout = %#v, want stargazing", got)
	}
	if got := loadouts["military"]; len(got) != 1 || got[0] != "stargazing" {
		t.Fatalf("military loadout = %#v, want stargazing", got)
	}
}

func TestQueueFullMapVisionFromMinisterSkillIsActiveOnlyForTargetTurn(t *testing.T) {
	state := NewGameState("skill-test", []string{"player-1"}, []string{"alice"}, &MapData{})
	state.Turn = 3

	if !QueueFullMapVisionFromMinisterSkill(state, "player-1", "domestic", "stargazing", 1, 1) {
		t.Fatalf("QueueFullMapVisionFromMinisterSkill() = false")
	}
	if PlayerHasFullMapVision(state, "player-1") {
		t.Fatalf("full map vision active on queue turn, want next turn only")
	}

	state.Turn = 4
	if !PlayerHasFullMapVision(state, "player-1") {
		t.Fatalf("full map vision inactive on active turn")
	}

	state.Turn = 5
	if PlayerHasFullMapVision(state, "player-1") {
		t.Fatalf("full map vision still active after expiry")
	}
	ExpireMinisterSkillEffects(state)
	if len(state.Players["player-1"].MinisterSkillEffects) != 0 {
		t.Fatalf("expired effects = %#v, want empty", state.Players["player-1"].MinisterSkillEffects)
	}
}
