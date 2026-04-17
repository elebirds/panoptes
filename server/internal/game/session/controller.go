package session

import (
	"context"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/game/ai"
	"github.com/elebirds/panoptes/internal/game/participant"
	"github.com/elebirds/panoptes/internal/game/planning"
)

type IntentSubmitter interface {
	SubmitIntent(ctx context.Context, envelope planning.IntentEnvelope) error
}

type Controller interface {
	IsAutonomous() bool
	BeginPlanning(ctx context.Context, participant participant.Participant, state *domain.GameState, submitter IntentSubmitter) error
}

type HumanController struct{}

func (HumanController) IsAutonomous() bool { return false }

func (HumanController) BeginPlanning(context.Context, participant.Participant, *domain.GameState, IntentSubmitter) error {
	return nil
}

type AutonomousController struct {
	provider ai.Provider
}

func NewAutonomousController(provider ai.Provider) AutonomousController {
	return AutonomousController{provider: provider}
}

func (c AutonomousController) IsAutonomous() bool { return true }

func (c AutonomousController) BeginPlanning(ctx context.Context, p participant.Participant, state *domain.GameState, submitter IntentSubmitter) error {
	if c.provider == nil || submitter == nil {
		return nil
	}
	intents, err := c.provider.BuildPlanningIntents(ctx, ai.Request{
		Participant: p,
		State:       state,
	})
	if err != nil {
		return err
	}
	for _, intent := range intents {
		if intent == nil {
			continue
		}
		if err := submitter.SubmitIntent(ctx, planning.IntentEnvelope{
			ParticipantID: p.ID,
			Intent:        intent,
		}); err != nil {
			return err
		}
	}
	return nil
}
