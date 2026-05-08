package domain

import (
	"strings"

	"github.com/elebirds/panoptes/internal/staticdata"
)

const MinisterSkillEffectFullMapVision = "full_map_vision"

type MinisterSkillEffectState struct {
	SkillCardID        string
	EffectKey          string
	SourceMinisterRole string
	ActiveTurn         int
	ExpiresAfterTurn   int
}

func DefaultMinisterSkillLoadouts(cards []staticdata.MinisterSkillCard) map[string][]string {
	loadouts := make(map[string][]string)
	for _, card := range cards {
		cardID := strings.TrimSpace(card.ID)
		if cardID == "" {
			continue
		}
		for _, role := range card.RoleTags {
			role = strings.TrimSpace(role)
			if role == "" {
				continue
			}
			loadouts[role] = append(loadouts[role], cardID)
		}
	}
	return loadouts
}

func (p *PlayerState) EnsureMinisterSkillState() {
	if p == nil {
		return
	}
	if p.MinisterSkillLoadouts == nil {
		p.MinisterSkillLoadouts = make(map[string][]string)
	}
	if p.MinisterSkillEffects == nil {
		p.MinisterSkillEffects = make(map[string]MinisterSkillEffectState)
	}
}

func (p *PlayerState) MinisterSkillCardIDs(role string) []string {
	if p == nil {
		return nil
	}
	role = strings.TrimSpace(role)
	if role == "" || p.MinisterSkillLoadouts == nil {
		return nil
	}
	ids := p.MinisterSkillLoadouts[role]
	out := make([]string, len(ids))
	copy(out, ids)
	return out
}

func (p *PlayerState) HasMinisterSkillCard(role string, skillCardID string) bool {
	role = strings.TrimSpace(role)
	skillCardID = strings.TrimSpace(skillCardID)
	if p == nil || role == "" || skillCardID == "" {
		return false
	}
	for _, id := range p.MinisterSkillLoadouts[role] {
		if strings.TrimSpace(id) == skillCardID {
			return true
		}
	}
	return false
}

func QueueMinisterSkillEffect(state *GameState, playerID string, effect MinisterSkillEffectState) bool {
	if state == nil {
		return false
	}
	playerID = strings.TrimSpace(playerID)
	player := state.Players[playerID]
	if player == nil {
		return false
	}
	effect.SkillCardID = strings.TrimSpace(effect.SkillCardID)
	effect.EffectKey = strings.TrimSpace(effect.EffectKey)
	if effect.SkillCardID == "" || effect.EffectKey == "" || effect.ActiveTurn <= 0 || effect.ExpiresAfterTurn < effect.ActiveTurn {
		return false
	}
	player.EnsureMinisterSkillState()
	player.MinisterSkillEffects[effect.SkillCardID] = effect
	return true
}

func QueueFullMapVisionFromMinisterSkill(state *GameState, playerID string, ministerRole string, skillCardID string, delayTurns int, durationTurns int) bool {
	if state == nil {
		return false
	}
	if delayTurns < 0 {
		delayTurns = 0
	}
	if durationTurns <= 0 {
		durationTurns = 1
	}
	activeTurn := state.Turn + delayTurns
	return QueueMinisterSkillEffect(state, playerID, MinisterSkillEffectState{
		SkillCardID:        strings.TrimSpace(skillCardID),
		EffectKey:          MinisterSkillEffectFullMapVision,
		SourceMinisterRole: strings.TrimSpace(ministerRole),
		ActiveTurn:         activeTurn,
		ExpiresAfterTurn:   activeTurn + durationTurns - 1,
	})
}

func PlayerHasFullMapVision(state *GameState, playerID string) bool {
	if state == nil {
		return false
	}
	player := state.Players[strings.TrimSpace(playerID)]
	if player == nil || player.MinisterSkillEffects == nil {
		return false
	}
	for _, effect := range player.MinisterSkillEffects {
		if strings.TrimSpace(effect.EffectKey) != MinisterSkillEffectFullMapVision {
			continue
		}
		if state.Turn >= effect.ActiveTurn && state.Turn <= effect.ExpiresAfterTurn {
			return true
		}
	}
	return false
}

func ExpireMinisterSkillEffects(state *GameState) {
	if state == nil {
		return
	}
	for _, player := range state.Players {
		if player == nil || player.MinisterSkillEffects == nil {
			continue
		}
		for skillCardID, effect := range player.MinisterSkillEffects {
			if effect.ExpiresAfterTurn < state.Turn {
				delete(player.MinisterSkillEffects, skillCardID)
			}
		}
	}
}
