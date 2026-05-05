package minister

import (
	"context"
	"strings"
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/llm"
	"google.golang.org/protobuf/proto"
)

type scriptedMinisterLLMClient struct {
	chunks []string
	err    error
}

func (c *scriptedMinisterLLMClient) Stream(ctx context.Context, _ llm.CompletionRequest) (<-chan string, error) {
	if c.err != nil {
		return nil, c.err
	}
	out := make(chan string, len(c.chunks))
	go func() {
		defer close(out)
		for _, chunk := range c.chunks {
			select {
			case <-ctx.Done():
				return
			case out <- chunk:
			}
		}
	}()
	return out, nil
}

type ministerEngineTestRoom struct {
	state    *domain.GameState
	input    ReportPromptInput
	messages []proto.Message
}

func (r *ministerEngineTestRoom) State() *domain.GameState {
	return r.state
}

func (r *ministerEngineTestRoom) HumanPlayerIDs() []string {
	return []string{"player-1"}
}

func (r *ministerEngineTestRoom) SendToPlayer(_ context.Context, _ string, msg proto.Message) error {
	r.messages = append(r.messages, msg)
	return nil
}

func (r *ministerEngineTestRoom) BuildMinisterReportInput(_ string, _ string) ReportPromptInput {
	return r.input
}

func TestGenerateOneReportDoesNotLeakEnglishChunksToPlayers(t *testing.T) {
	engine := NewMinisterEngine(&scriptedMinisterLLMClient{
		chunks: []string{
			`{"report":"Enemy pressure is rising near the border.","metrics":[{"label":"Food Reserve","value":"Low","trend":"down","confidence":"medium","is_delayed":false}],"actions":[],"action_id":"hold"}`,
		},
	})
	room := &ministerEngineTestRoom{
		state: domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"}),
		input: ReportPromptInput{
			Turn:               3,
			Phase:              "planning",
			PlayerID:           "player-1",
			ObservationSummary: "边境可见敌军活动。",
		},
	}

	engine.generateOneReport(context.Background(), "player-1", MinisterProfile{
		ID:              "m001",
		Name:            "沈衍",
		Role:            "domestic",
		Ability:         7,
		Personality:     "steady",
		PersonalityDesc: "稳健审慎",
		Loyalty:         8,
		Ambition:        4,
	}, room)

	var reportChunks []*pb.MsgMinisterReportChunk
	var metrics *pb.MsgMinisterMetrics
	for _, msg := range room.messages {
		switch typed := msg.(type) {
		case *pb.MsgMinisterReportChunk:
			reportChunks = append(reportChunks, typed)
			if strings.Contains(typed.GetChunk(), "Enemy pressure") || strings.Contains(typed.GetChunk(), "Food Reserve") {
				t.Fatalf("player-visible report chunk leaked English text: %q", typed.GetChunk())
			}
		case *pb.MsgMinisterMetrics:
			metrics = typed
		}
	}

	if len(reportChunks) != 2 {
		t.Fatalf("report chunk count = %d, want 2", len(reportChunks))
	}
	if reportChunks[0].GetChunk() != chineseReportFallback || reportChunks[0].GetIsFinal() {
		t.Fatalf("first report chunk = %+v, want sanitized non-final report", reportChunks[0])
	}
	if reportChunks[1].GetChunk() != "" || !reportChunks[1].GetIsFinal() {
		t.Fatalf("final report chunk = %+v, want empty final marker", reportChunks[1])
	}
	if metrics == nil || len(metrics.GetMetrics()) != 1 {
		t.Fatalf("metrics message missing or unexpected: %+v", metrics)
	}
	if metrics.GetMetrics()[0].GetLabel() != chineseMetricLabelFallback || metrics.GetMetrics()[0].GetValue() != chineseMetricValueFallback {
		t.Fatalf("metrics were not sanitized: %+v", metrics.GetMetrics()[0])
	}
}
