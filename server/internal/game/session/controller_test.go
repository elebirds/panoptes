package session

import (
	"bytes"
	"context"
	"encoding/json"
	"errors"
	"log/slog"
	"strings"
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/game/ai"
	"github.com/elebirds/panoptes/internal/game/participant"
	"github.com/elebirds/panoptes/internal/game/planning"
	gamequery "github.com/elebirds/panoptes/internal/game/query"
)

func TestAutonomousControllerLogsPlanningSummary(t *testing.T) {
	controller := NewAutonomousController(stubProvider{
		intents: []planning.Intent{
			planning.BuildStructureIntent{NodeID: "A2", BuildingTypeID: "farm"},
			planning.SubmitTurnIntent{},
		},
	})
	state := domain.NewGameState("game-1", []string{"bot-1"}, []string{"bot"}, &domain.MapData{ID: "default"})
	state.Turn = 5
	state.Phase = domain.PhasePlanning.String()

	logs := captureControllerLogs(t)

	err := controller.BeginPlanning(context.Background(), participant.Participant{ID: "bot-1", Kind: participant.KindBot}, state, &gamequery.ObservationSnapshot{}, stubIntentSubmitter{})
	if err != nil {
		t.Fatalf("BeginPlanning() error = %v", err)
	}

	records := logs.records()
	if len(records) != 1 {
		t.Fatalf("log count = %d, want 1", len(records))
	}
	if records[0]["msg"] != "AI 规划摘要" {
		t.Fatalf("msg = %#v, want AI 规划摘要", records[0]["msg"])
	}
	if records[0]["component"] != "ai_planning_summary" {
		t.Fatalf("component = %#v, want ai_planning_summary", records[0]["component"])
	}
	if records[0]["generated_intent_count"] != float64(2) {
		t.Fatalf("generated_intent_count = %#v, want 2", records[0]["generated_intent_count"])
	}
	if records[0]["generated_intent_types"] == nil {
		t.Fatalf("generated_intent_types missing in %#v", records[0])
	}
	if records[0]["summary"] != "本回合计划：在 A2 建造 farm -> 结束回合" {
		t.Fatalf("summary = %#v, want readable summary", records[0]["summary"])
	}
	if records[0]["outcome"] != "generated" {
		t.Fatalf("outcome = %#v, want generated", records[0]["outcome"])
	}
	if _, ok := records[0]["generated_intents"]; ok {
		t.Fatalf("generated_intents should be omitted for readability")
	}
}

func TestAutonomousControllerLogsPlanningBuildFailure(t *testing.T) {
	controller := NewAutonomousController(stubProvider{
		err: errors.New("provider boom"),
	})
	state := domain.NewGameState("game-1", []string{"bot-1"}, []string{"bot"}, &domain.MapData{ID: "default"})
	state.Turn = 3
	state.Phase = domain.PhasePlanning.String()

	logs := captureControllerLogs(t)

	err := controller.BeginPlanning(context.Background(), participant.Participant{ID: "bot-1", Kind: participant.KindAI}, state, &gamequery.ObservationSnapshot{}, stubIntentSubmitter{})
	if err == nil {
		t.Fatalf("BeginPlanning() error = nil, want error")
	}

	records := logs.records()
	if len(records) != 1 {
		t.Fatalf("log count = %d, want 1", len(records))
	}
	if records[0]["msg"] != "AI 规划摘要" {
		t.Fatalf("msg = %#v, want AI 规划摘要", records[0]["msg"])
	}
	if records[0]["component"] != "ai_planning_summary" {
		t.Fatalf("component = %#v, want ai_planning_summary", records[0]["component"])
	}
	if records[0]["outcome"] != "build_failed" {
		t.Fatalf("outcome = %#v, want build_failed", records[0]["outcome"])
	}
	if records[0]["summary"] != "本回合计划生成失败" {
		t.Fatalf("summary = %#v, want readable failure summary", records[0]["summary"])
	}
}

type stubProvider struct {
	intents []planning.Intent
	err     error
}

func (p stubProvider) BuildPlanningIntents(context.Context, ai.Request) ([]planning.Intent, error) {
	if p.err != nil {
		return nil, p.err
	}
	return append([]planning.Intent(nil), p.intents...), nil
}

type stubIntentSubmitter struct{}

func (stubIntentSubmitter) SubmitIntent(context.Context, planning.IntentEnvelope) error { return nil }

type controllerLogCapture struct {
	buffer *bytes.Buffer
}

func captureControllerLogs(t *testing.T) controllerLogCapture {
	t.Helper()

	buffer := &bytes.Buffer{}
	previous := slog.Default()
	logger := slog.New(slog.NewJSONHandler(buffer, &slog.HandlerOptions{Level: slog.LevelDebug}))
	slog.SetDefault(logger)
	t.Cleanup(func() {
		slog.SetDefault(previous)
	})
	return controllerLogCapture{buffer: buffer}
}

func (c controllerLogCapture) records() []map[string]any {
	lines := strings.Split(strings.TrimSpace(c.buffer.String()), "\n")
	if len(lines) == 1 && lines[0] == "" {
		return nil
	}
	records := make([]map[string]any, 0, len(lines))
	for _, line := range lines {
		if strings.TrimSpace(line) == "" {
			continue
		}
		record := make(map[string]any)
		if err := json.Unmarshal([]byte(line), &record); err != nil {
			continue
		}
		records = append(records, record)
	}
	return records
}
