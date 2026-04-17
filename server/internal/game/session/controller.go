package session

import (
	"context"
	"encoding/binary"
	"hash/fnv"
	"log/slog"
	"math/rand"
	"strings"

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
		slog.Debug("AI 规划摘要",
			"component", "ai_planning_summary",
			"participant_id", p.ID,
			"participant_kind", string(p.Kind),
			"turn", stateTurn(state),
			"phase", statePhase(state),
			"outcome", "build_failed",
			"summary", "本回合计划生成失败",
			"error", err.Error(),
		)
		return err
	}
	generatedTypes := make([]string, 0, len(intents))
	generatedSummaries := make([]string, 0, len(intents))
	for _, intent := range intents {
		if intent == nil {
			continue
		}
		record := planning.DebugIntentRecordFor(p.Kind, p.ID, intent)
		generatedTypes = append(generatedTypes, record.IntentType)
		generatedSummaries = append(generatedSummaries, record.ActionSummary)
	}
	summary := "本回合计划：无操作"
	if len(generatedSummaries) > 0 {
		summary = "本回合计划：" + strings.Join(generatedSummaries, " -> ")
	}
	slog.Debug("AI 规划摘要",
		"component", "ai_planning_summary",
		"participant_id", p.ID,
		"participant_kind", string(p.Kind),
		"turn", stateTurn(state),
		"phase", statePhase(state),
		"generated_intent_count", len(generatedTypes),
		"generated_intent_types", generatedTypes,
		"summary", summary,
		"outcome", "generated",
	)
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

func stateTurn(state *domain.GameState) int {
	if state == nil {
		return 0
	}
	return state.Turn
}

func statePhase(state *domain.GameState) string {
	if state == nil {
		return ""
	}
	return state.Phase
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
