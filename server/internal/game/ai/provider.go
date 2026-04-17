package ai

import (
	"context"
	"math/rand"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/game/participant"
	"github.com/elebirds/panoptes/internal/game/planning"
	gamequery "github.com/elebirds/panoptes/internal/game/query"
)

type Request struct {
	Participant participant.Participant
	State       *domain.GameState
	Observation *gamequery.ObservationSnapshot
	RNG         *rand.Rand
}

type Provider interface {
	BuildPlanningIntents(ctx context.Context, req Request) ([]planning.Intent, error)
}

type RuleBotProvider struct{}
