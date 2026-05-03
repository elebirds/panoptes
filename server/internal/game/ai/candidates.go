// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载规则 AI 规划器拆分后的候选、评分与意图生成逻辑。

package ai

import (
	"math/rand"
	"sort"

	"github.com/elebirds/panoptes/internal/game/planning"
)

type scoredIntent struct {
	key    string
	score  int
	intent planning.Intent
}

type buildCandidate struct {
	key            string
	score          int
	nodeID         string
	buildingTypeID string
	cityID         string
}

type recipeCandidate struct {
	key      string
	score    int
	nodeID   string
	recipeID string
}

type expansionCandidate struct {
	key        string
	score      int
	unitID     string
	targetNode string
}

type combatCandidate struct {
	key    string
	score  int
	intent planning.Intent
}

func pickBestScoredIntent(rng *rand.Rand, candidates []scoredIntent) (planning.Intent, bool) {
	if len(candidates) == 0 {
		return nil, false
	}
	sort.Slice(candidates, func(i, j int) bool {
		if candidates[i].score == candidates[j].score {
			return candidates[i].key < candidates[j].key
		}
		return candidates[i].score > candidates[j].score
	})
	best := candidates[0].score
	end := 1
	for end < len(candidates) && candidates[end].score == best {
		end++
	}
	return candidates[rng.Intn(end)].intent, true
}

func pickBestBuildCandidate(rng *rand.Rand, candidates []buildCandidate) (buildCandidate, bool) {
	if len(candidates) == 0 {
		return buildCandidate{}, false
	}
	sort.Slice(candidates, func(i, j int) bool {
		if candidates[i].score == candidates[j].score {
			return candidates[i].key < candidates[j].key
		}
		return candidates[i].score > candidates[j].score
	})
	best := candidates[0].score
	end := 1
	for end < len(candidates) && candidates[end].score == best {
		end++
	}
	return candidates[rng.Intn(end)], true
}

func pickBestRecipeCandidate(rng *rand.Rand, candidates []recipeCandidate) (recipeCandidate, bool) {
	if len(candidates) == 0 {
		return recipeCandidate{}, false
	}
	sort.Slice(candidates, func(i, j int) bool {
		if candidates[i].score == candidates[j].score {
			return candidates[i].key < candidates[j].key
		}
		return candidates[i].score > candidates[j].score
	})
	best := candidates[0].score
	end := 1
	for end < len(candidates) && candidates[end].score == best {
		end++
	}
	return candidates[rng.Intn(end)], true
}

func pickBestExpansionCandidate(rng *rand.Rand, candidates []expansionCandidate) (expansionCandidate, bool) {
	if len(candidates) == 0 {
		return expansionCandidate{}, false
	}
	sort.Slice(candidates, func(i, j int) bool {
		if candidates[i].score == candidates[j].score {
			return candidates[i].key < candidates[j].key
		}
		return candidates[i].score > candidates[j].score
	})
	best := candidates[0].score
	end := 1
	for end < len(candidates) && candidates[end].score == best {
		end++
	}
	return candidates[rng.Intn(end)], true
}
