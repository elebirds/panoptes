package ai

import (
	"context"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/game/participant"
	"github.com/elebirds/panoptes/internal/game/planning"
)

type Request struct {
	Participant participant.Participant
	State       *domain.GameState
}

type Provider interface {
	BuildPlanningIntents(ctx context.Context, req Request) ([]planning.Intent, error)
}

type RuleBotProvider struct{}

func (RuleBotProvider) BuildPlanningIntents(context.Context, Request) ([]planning.Intent, error) {
	return []planning.Intent{
		planning.SubmitTurnIntent{},
	}, nil
}
