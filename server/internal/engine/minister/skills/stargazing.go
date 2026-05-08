package skills

import (
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/staticdata"
)

const StargazingEffectKey = "next_turn_full_map_vision"

type Stargazing struct{}

func (Stargazing) EffectKey() string {
	return StargazingEffectKey
}

func (Stargazing) Activate(state *domain.GameState, playerID string, ministerRole string, card staticdata.MinisterSkillCard) (ActivationResult, bool) {
	if state == nil {
		return ActivationResult{}, false
	}
	delayTurns := card.DelayTurns
	if delayTurns < 0 {
		delayTurns = 1
	}
	durationTurns := card.DurationTurns
	if durationTurns <= 0 {
		durationTurns = 1
	}
	activeTurn := state.Turn + delayTurns
	ok := domain.QueueFullMapVisionFromMinisterSkill(state, playerID, ministerRole, card.ID, delayTurns, durationTurns)
	if !ok {
		return ActivationResult{}, false
	}
	return ActivationResult{
		SkillCardID:      strings.TrimSpace(card.ID),
		EffectKey:        domain.MinisterSkillEffectFullMapVision,
		ActiveTurn:       activeTurn,
		ExpiresAfterTurn: activeTurn + durationTurns - 1,
	}, true
}
