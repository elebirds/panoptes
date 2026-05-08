package minister

import (
	"context"
	"strings"
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/llm"
	"github.com/elebirds/panoptes/internal/staticdata"
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
	state          *domain.GameState
	input          ReportPromptInput
	messages       []proto.Message
	appliedPlayer  string
	appliedRole    string
	appliedActions []MinisterActionItem
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

func (r *ministerEngineTestRoom) ApplyMinisterActions(playerID string, role string, actions []MinisterActionItem) error {
	r.appliedPlayer = playerID
	r.appliedRole = role
	r.appliedActions = append(r.appliedActions, actions...)
	return nil
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

func TestGenerateOneReportForwardsActionsToRoom(t *testing.T) {
	engine := NewMinisterEngine(&scriptedMinisterLLMClient{
		chunks: []string{
			`{"report":"局势稳定。","metrics":[],"actions":[{"type":"build","params":{"node_id":"A2","building_type":"farm","city_id":"A1"}},{"type":"move_units","params":{"unit_id":"u1","target_node":"A2"}}],"action_id":"build_and_move"}`,
		},
	})
	room := &ministerEngineTestRoom{
		state: domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"}),
		input: ReportPromptInput{
			Turn:               3,
			Phase:              "planning",
			PlayerID:           "player-1",
			ObservationSummary: "局势稳定。",
		},
	}

	engine.generateOneReport(context.Background(), "player-1", MinisterProfile{
		ID:              "m001",
		Name:            "沈衡",
		Role:            "domestic",
		Ability:         7,
		Personality:     "steady",
		PersonalityDesc: "稳健审慎",
		Loyalty:         8,
		Ambition:        4,
	}, room)

	if room.appliedPlayer != "player-1" {
		t.Fatalf("applied player = %q, want player-1", room.appliedPlayer)
	}
	if room.appliedRole != "domestic" {
		t.Fatalf("applied role = %q, want domestic", room.appliedRole)
	}
	if len(room.appliedActions) != 2 {
		t.Fatalf("applied actions = %#v, want 2 parsed actions", room.appliedActions)
	}
	if room.appliedActions[0].Type != "build" || room.appliedActions[1].Type != "move_units" {
		t.Fatalf("applied actions = %#v, want build then move_units", room.appliedActions)
	}
}

func TestPolishDraftHonorsEnabledRolesForMilitary(t *testing.T) {
	setMinisterProfilesForTest(t, []staticdata.Minister{
		{ID: "m001", Name: "李猛", Role: "military", Ability: 8, Personality: "aggressive", PersonalityDesc: "果断激进", Loyalty: 7, Ambition: 6},
		{ID: "m002", Name: "沈衡", Role: "domestic", Ability: 7, Personality: "steady", PersonalityDesc: "稳健审慎", Loyalty: 8, Ambition: 4},
	})
	draft := domain.MinisterDraft{
		DraftID:      "military:unit_order:u1_move_a2:3",
		PlayerID:     "player-1",
		MinisterRole: "military",
		Kind:         domain.MinisterDraftKindUnitOrder,
		TargetID:     "u1:move:A2:",
		TargetLabel:  "u1 move -> A2",
		UnitID:       "u1",
		Action:       "move",
		TargetNodeID: "A2",
	}

	engine := NewMinisterEngine(&scriptedMinisterLLMClient{
		chunks: []string{`{"title":"整备边防","summary":"本回合按既定军令推进至目标节点。","rationale":"该建议只润色规则层已选定的单位命令。","risk_note":"若前线情报不足，请保留手动调整空间。"}`},
	})
	engine.SetEnabledRoles([]string{"domestic"})
	if output, ok := engine.PolishDraft(context.Background(), "player-1", draft, DraftPromptInput{Turn: 3}); ok || output != nil {
		t.Fatalf("PolishDraft enabled with domestic-only roles, got ok=%v output=%#v", ok, output)
	}

	engine.SetEnabledRoles([]string{"domestic", "military"})
	output, ok := engine.PolishDraft(context.Background(), "player-1", draft, DraftPromptInput{Turn: 3})
	if !ok || output == nil {
		t.Fatalf("PolishDraft disabled for enabled military role, ok=%v output=%#v", ok, output)
	}
	if output.Title != "整备边防" || output.Summary == "" {
		t.Fatalf("PolishDraft output = %#v, want military LLM polish text", output)
	}
}

func TestPolishDraftSanitizesEnglishMilitaryText(t *testing.T) {
	setMinisterProfilesForTest(t, []staticdata.Minister{
		{ID: "m001", Name: "李猛", Role: "military", Ability: 8, Personality: "aggressive", PersonalityDesc: "果断激进", Loyalty: 7, Ambition: 6},
	})
	engine := NewMinisterEngine(&scriptedMinisterLLMClient{
		chunks: []string{`{"title":"Advance border troops","summary":"Move the unit to A2 now.","rationale":"This is a rule-selected order.","risk_note":"Watch for enemies."}`},
	})
	engine.SetEnabledRoles([]string{"military"})

	output, ok := engine.PolishDraft(context.Background(), "player-1", domain.MinisterDraft{
		DraftID:      "military:unit_order:u1_move_a2:3",
		PlayerID:     "player-1",
		MinisterRole: "military",
		Kind:         domain.MinisterDraftKindUnitOrder,
		TargetID:     "u1:move:A2:",
		TargetLabel:  "u1 move -> A2",
		UnitID:       "u1",
		Action:       "move",
		TargetNodeID: "A2",
	}, DraftPromptInput{Turn: 3})
	if !ok || output == nil {
		t.Fatalf("PolishDraft rejected parseable English output, ok=%v output=%#v", ok, output)
	}
	if output.Title != chineseDraftTitleFallback || output.Summary != chineseDraftSummaryFallback || output.Rationale != chineseDraftReasonFallback || output.RiskNote != chineseDraftRiskNoteFallback {
		t.Fatalf("English draft text was not sanitized: %#v", output)
	}
}

func TestPolishDraftRejectsInvalidMilitaryJSON(t *testing.T) {
	setMinisterProfilesForTest(t, []staticdata.Minister{
		{ID: "m001", Name: "李猛", Role: "military", Ability: 8, Personality: "aggressive", PersonalityDesc: "果断激进", Loyalty: 7, Ambition: 6},
	})
	engine := NewMinisterEngine(&scriptedMinisterLLMClient{chunks: []string{`{"title":"整备边防"`}})
	engine.SetEnabledRoles([]string{"military"})

	output, ok := engine.PolishDraft(context.Background(), "player-1", domain.MinisterDraft{
		DraftID:      "military:unit_order:u1_move_a2:3",
		PlayerID:     "player-1",
		MinisterRole: "military",
		Kind:         domain.MinisterDraftKindUnitOrder,
		TargetID:     "u1:move:A2:",
		TargetLabel:  "u1 move -> A2",
		UnitID:       "u1",
		Action:       "move",
		TargetNodeID: "A2",
	}, DraftPromptInput{Turn: 3})
	if ok || output != nil {
		t.Fatalf("PolishDraft accepted invalid JSON, ok=%v output=%#v", ok, output)
	}
}

func TestGenerateReportsSendsMilitaryWhenRoleEnabled(t *testing.T) {
	setMinisterProfilesForTest(t, []staticdata.Minister{
		{ID: "m001", Name: "李猛", Role: "military", Ability: 8, Personality: "aggressive", PersonalityDesc: "果断激进", Loyalty: 7, Ambition: 6},
		{ID: "m002", Name: "沈衡", Role: "domestic", Ability: 7, Personality: "steady", PersonalityDesc: "稳健审慎", Loyalty: 8, Ambition: 4},
	})
	engine := NewMinisterEngine(nil)
	engine.SetEnabledRoles([]string{"domestic", "military"})
	room := &ministerEngineTestRoom{
		state: domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"}),
		input: ReportPromptInput{
			Turn:     3,
			Phase:    "planning",
			PlayerID: "player-1",
		},
	}

	engine.GenerateReports(context.Background(), room)

	roles := make(map[string]int)
	for _, msg := range room.messages {
		if chunk, ok := msg.(*pb.MsgMinisterReportChunk); ok && chunk.GetIsFinal() {
			roles[chunk.GetMinisterRole()]++
		}
	}
	if roles["domestic"] != 1 || roles["military"] != 1 {
		t.Fatalf("final report roles = %#v, want one domestic and one military final chunk", roles)
	}
}

func setMinisterProfilesForTest(t *testing.T, ministers []staticdata.Minister) {
	t.Helper()
	previous := staticdata.Default()
	t.Cleanup(func() {
		staticdata.SetDefault(previous)
	})
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Ministers: ministers,
	}))
}
