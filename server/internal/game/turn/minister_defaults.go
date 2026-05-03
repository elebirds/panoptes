// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-05-01 00:00:00 +0800
// Description: Applies rule-based minister defaults at planning start.

package turn

import (
	"context"
	"log/slog"

	"github.com/elebirds/panoptes/internal/game/ai"
	"github.com/elebirds/panoptes/internal/game/planning"
)

func (c *Coordinator) applyMinisterDefaultPlans(ctx context.Context) {
	if c == nil || c.runtime == nil || c.host == nil || c.runtime.State() == nil {
		return
	}
	provider := ai.RuleBotProvider{}
	for _, currentParticipant := range c.runtime.HumanParticipants() {
		intents, err := provider.BuildPlanningIntents(ctx, ai.Request{
			Participant: currentParticipant,
			State:       c.runtime.State(),
			Observation: c.runtime.BuildObservation(currentParticipant.ID),
		})
		if err != nil {
			slog.Warn("minister default planning failed", "participant_id", currentParticipant.ID, "err", err)
			continue
		}
		applied := 0
		for _, intent := range intents {
			result := planning.ApplyMinisterDefaultIntent(c.host, currentParticipant.ID, intent)
			if result.Applied {
				applied++
			}
		}
		if applied > 0 {
			slog.Debug("minister default planning applied",
				"component", "minister_default_planning",
				"participant_id", currentParticipant.ID,
				"turn", c.runtime.State().Turn,
				"applied_intent_count", applied,
			)
		}
	}
}
