// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载规则 AI 规划器拆分后的候选、评分与意图生成逻辑。

package ai

import (
	"context"
	"math/rand"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/game/planning"
	gamequery "github.com/elebirds/panoptes/internal/game/query"
)

const (
	intMax = int(^uint(0) >> 1)
	intMin = -intMax - 1
)

func (RuleBotProvider) BuildPlanningIntents(_ context.Context, req Request) ([]planning.Intent, error) {
	if req.State == nil || req.Participant.ID == "" {
		return []planning.Intent{planning.SubmitTurnIntent{}}, nil
	}

	planner := newRuleBotPlanner(req)
	intents := make([]planning.Intent, 0, 16)

	if intent, ok := planner.chooseResearchIntent(); ok {
		intents = append(intents, intent)
	}
	if intent, ok := planner.chooseNationalPolicyIntent(); ok {
		intents = append(intents, intent)
	}
	if intent, ok := planner.chooseInstitutionIntent(); ok {
		intents = append(intents, intent)
	}
	if intent, ok := planner.chooseBuildIntent(); ok {
		intents = append(intents, intent)
	}
	intents = append(intents, planner.chooseRecipeIntents()...)
	intents = append(intents, planner.chooseExpansionIntents()...)
	intents = append(intents, planner.chooseCombatIntents()...)
	intents = append(intents, planning.SubmitTurnIntent{})
	return intents, nil
}

type ruleBotPlanner struct {
	req          Request
	rng          *rand.Rand
	player       *domain.PlayerState
	observation  *gamequery.ObservationSnapshot
	playerID     string
	threatLevel  int
	reservedUnit map[string]struct{}
}

func newRuleBotPlanner(req Request) *ruleBotPlanner {
	rng := req.RNG
	if rng == nil {
		rng = rand.New(rand.NewSource(1))
	}
	observation := req.Observation
	if observation == nil {
		observation = gamequery.NewObservationStore().BuildObservation(req.State, req.Participant.ID)
	}
	planner := &ruleBotPlanner{
		req:          req,
		rng:          rng,
		player:       req.State.Players[req.Participant.ID],
		observation:  observation,
		playerID:     req.Participant.ID,
		reservedUnit: make(map[string]struct{}),
	}
	planner.threatLevel = planner.computeThreatLevel()
	return planner
}
