package session

import (
	"context"
	"encoding/binary"
	"hash/fnv"
	"math/rand"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/game/ai"
	"github.com/elebirds/panoptes/internal/game/participant"
	"github.com/elebirds/panoptes/internal/game/planning"
	gamequery "github.com/elebirds/panoptes/internal/game/query"
)

type IntentSubmitter interface {
	SubmitIntent(ctx context.Context, envelope planning.IntentEnvelope) error
}

type Controller interface {
	IsAutonomous() bool
	BeginPlanning(ctx context.Context, participant participant.Participant, state *domain.GameState, observation *gamequery.ObservationSnapshot, submitter IntentSubmitter) error
}

type HumanController struct{}

func (HumanController) IsAutonomous() bool { return false }

func (HumanController) BeginPlanning(context.Context, participant.Participant, *domain.GameState, *gamequery.ObservationSnapshot, IntentSubmitter) error {
	return nil
}

type AutonomousController struct {
	provider ai.Provider
}

func NewAutonomousController(provider ai.Provider) AutonomousController {
	return AutonomousController{provider: provider}
}

func (c AutonomousController) IsAutonomous() bool { return true }

func (c AutonomousController) BeginPlanning(ctx context.Context, p participant.Participant, state *domain.GameState, observation *gamequery.ObservationSnapshot, submitter IntentSubmitter) error {
	if c.provider == nil || submitter == nil {
		return nil
	}
	intents, err := c.provider.BuildPlanningIntents(ctx, ai.Request{
		Participant: p,
		State:       state,
		Observation: observation,
		RNG:         newDeterministicPlanningRNG(state, p.ID),
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

func newDeterministicPlanningRNG(state *domain.GameState, participantID string) *rand.Rand {
	hasher := fnv.New64a()
	if state != nil {
		_, _ = hasher.Write([]byte(state.GameID))
		var turnBytes [8]byte
		binary.LittleEndian.PutUint64(turnBytes[:], uint64(state.Turn))
		_, _ = hasher.Write(turnBytes[:])
	}
	_, _ = hasher.Write([]byte(participantID))
	return rand.New(rand.NewSource(int64(hasher.Sum64())))
}
