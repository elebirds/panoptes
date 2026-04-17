package planning

import (
	"bytes"
	"encoding/json"
	"log/slog"
	"strings"
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/game/participant"
)

func TestDebugIntentRecordForCoversAllPlanningIntents(t *testing.T) {
	tests := []struct {
		name          string
		kind          participant.Kind
		participantID string
		intent        Intent
		wantType      string
		wantLabel     string
		wantSource    string
		wantSummary   string
		wantFields    map[string]any
	}{
		{
			name:          "set policy",
			kind:          participant.KindHuman,
			participantID: "player-1",
			intent:        SetPolicyIntent{NationalPolicyID: "policy-growth"},
			wantType:      "set_policy",
			wantLabel:     "设置国策",
			wantSource:    "player",
			wantSummary:   "玩家[player-1] 设置国策 policy-growth",
			wantFields: map[string]any{
				"policy_id": "policy-growth",
			},
		},
		{
			name:          "institution loadout",
			kind:          participant.KindBot,
			participantID: "bot-1",
			intent:        SetInstitutionLoadoutIntent{PolicyIDs: []string{"card-a", "card-b"}},
			wantType:      "set_institution_loadout",
			wantLabel:     "设置制度装配",
			wantSource:    "ai",
			wantSummary:   "AI[bot-1] 设置制度装配 [card-a, card-b]",
			wantFields: map[string]any{
				"card_ids": []string{"card-a", "card-b"},
			},
		},
		{
			name:          "build structure",
			kind:          participant.KindHuman,
			participantID: "player-1",
			intent:        BuildStructureIntent{NodeID: "A2", BuildingTypeID: "farm"},
			wantType:      "build_structure",
			wantLabel:     "建造建筑",
			wantSource:    "player",
			wantSummary:   "玩家[player-1] 在 A2 建造 farm",
			wantFields: map[string]any{
				"node_id":       "A2",
				"building_type": "farm",
			},
		},
		{
			name:          "reveal node",
			kind:          participant.KindHuman,
			participantID: "player-1",
			intent:        RevealNodeIntent{NodeID: "C3"},
			wantType:      "reveal_node",
			wantLabel:     "侦察节点",
			wantSource:    "player",
			wantSummary:   "玩家[player-1] 侦察节点 C3",
			wantFields: map[string]any{
				"node_id": "C3",
			},
		},
		{
			name:          "set research target",
			kind:          participant.KindAI,
			participantID: "bot-1",
			intent:        SetResearchTargetIntent{TechnologyID: "tech-farming"},
			wantType:      "set_research_target",
			wantLabel:     "设置科研目标",
			wantSource:    "ai",
			wantSummary:   "AI[bot-1] 设置科研目标 tech-farming",
			wantFields: map[string]any{
				"technology_id": "tech-farming",
			},
		},
		{
			name:          "set building recipe",
			kind:          participant.KindHuman,
			participantID: "player-1",
			intent:        SetBuildingRecipeIntent{NodeID: "A3", RecipeID: "bread"},
			wantType:      "set_building_recipe",
			wantLabel:     "设置建筑配方",
			wantSource:    "player",
			wantSummary:   "玩家[player-1] 将 A3 配方设为 bread",
			wantFields: map[string]any{
				"node_id":   "A3",
				"recipe_id": "bread",
			},
		},
		{
			name:          "issue unit order",
			kind:          participant.KindBot,
			participantID: "bot-1",
			intent:        IssueUnitOrderIntent{UnitID: "unit-1", Action: "move", TargetNodeID: "B2"},
			wantType:      "issue_unit_order",
			wantLabel:     "下达单位指令",
			wantSource:    "ai",
			wantSummary:   "AI[bot-1] 命令 unit-1 执行 move 到 B2",
			wantFields: map[string]any{
				"unit_id":        "unit-1",
				"directive_type": "move",
				"target_node_id": "B2",
			},
		},
		{
			name:          "cancel unit order",
			kind:          participant.KindHuman,
			participantID: "player-1",
			intent:        CancelUnitOrderIntent{UnitID: "unit-9"},
			wantType:      "cancel_unit_order",
			wantLabel:     "取消单位指令",
			wantSource:    "player",
			wantSummary:   "玩家[player-1] 取消单位 unit-9 的指令",
			wantFields: map[string]any{
				"unit_id": "unit-9",
			},
		},
		{
			name:          "submit turn",
			kind:          participant.KindAI,
			participantID: "bot-1",
			intent:        SubmitTurnIntent{},
			wantType:      "submit_turn",
			wantLabel:     "结束回合",
			wantSource:    "ai",
			wantSummary:   "AI[bot-1] 结束回合",
			wantFields:    map[string]any{},
		},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			record := DebugIntentRecordFor(tc.kind, tc.participantID, tc.intent)
			if record.IntentType != tc.wantType {
				t.Fatalf("IntentType = %q, want %q", record.IntentType, tc.wantType)
			}
			if record.IntentLabel != tc.wantLabel {
				t.Fatalf("IntentLabel = %q, want %q", record.IntentLabel, tc.wantLabel)
			}
			if record.Source != tc.wantSource {
				t.Fatalf("Source = %q, want %q", record.Source, tc.wantSource)
			}
			if record.Summary != tc.wantSummary {
				t.Fatalf("Summary = %q, want %q", record.Summary, tc.wantSummary)
			}
			if !payloadEqual(record.Fields, tc.wantFields) {
				t.Fatalf("Fields = %#v, want %#v", record.Fields, tc.wantFields)
			}
		})
	}
}

func TestHandleIntentLogsAttemptAndResult(t *testing.T) {
	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	state.Turn = 7
	state.Phase = domain.PhasePlanning.String()
	session := newPlanningSessionStub(state)
	service := &Service{}

	logs := capturePlanningLogs(t)

	err := service.HandleIntent(session, IntentEnvelope{
		ParticipantID: "player-1",
		RequestID:     "req-submit",
		Intent:        SubmitTurnIntent{},
	})
	if err != nil {
		t.Fatalf("HandleIntent() error = %v", err)
	}

	records := logs.records()
	if len(records) != 2 {
		t.Fatalf("log count = %d, want 2", len(records))
	}
	assertPlanningLogField(t, records[0], "msg", "planning 操作尝试")
	assertPlanningLogField(t, records[0], "component", "planning_intent")
	assertPlanningLogField(t, records[0], "participant_id", "player-1")
	assertPlanningLogField(t, records[0], "participant_kind", "human")
	assertPlanningLogField(t, records[0], "source", "player")
	assertPlanningLogField(t, records[0], "intent_type", "submit_turn")
	assertPlanningLogField(t, records[0], "intent_label", "结束回合")
	assertPlanningLogField(t, records[0], "summary", "玩家[player-1] 尝试结束回合")
	assertPlanningLogField(t, records[0], "outcome", "attempt")
	assertPlanningLogField(t, records[1], "msg", "planning 操作结果")
	assertPlanningLogField(t, records[1], "outcome", "accepted")
	assertPlanningLogField(t, records[1], "outcome_label", "成功")
	assertPlanningLogField(t, records[1], "summary", "玩家[player-1] 结束回合，结果：成功")
}

func TestHandleIntentLogsRejectedOutcomeAndErrorCode(t *testing.T) {
	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	state.Turn = 2
	state.Phase = domain.PhasePlanning.String()
	session := newPlanningSessionStub(state)
	service := &Service{}

	logs := capturePlanningLogs(t)

	err := service.HandleIntent(session, IntentEnvelope{
		ParticipantID: "player-1",
		RequestID:     "req-research",
		Intent:        SetResearchTargetIntent{},
	})
	if err != nil {
		t.Fatalf("HandleIntent() error = %v", err)
	}

	records := logs.records()
	if len(records) != 2 {
		t.Fatalf("log count = %d, want 2", len(records))
	}
	assertPlanningLogField(t, records[0], "msg", "planning 操作尝试")
	assertPlanningLogField(t, records[0], "intent_label", "设置科研目标")
	assertPlanningLogField(t, records[0], "summary", "玩家[player-1] 尝试设置科研目标")
	assertPlanningLogField(t, records[0], "outcome", "attempt")
	assertPlanningLogField(t, records[1], "msg", "planning 操作结果")
	assertPlanningLogField(t, records[1], "outcome", "rejected")
	assertPlanningLogField(t, records[1], "outcome_label", "失败")
	assertPlanningLogField(t, records[1], "error_code", "invalid_request")
	assertPlanningLogField(t, records[1], "summary", "玩家[player-1] 设置科研目标，结果：失败")
}

type planningLogCapture struct {
	buffer *bytes.Buffer
}

func capturePlanningLogs(t *testing.T) planningLogCapture {
	t.Helper()

	buffer := &bytes.Buffer{}
	previous := slog.Default()
	logger := slog.New(slog.NewJSONHandler(buffer, &slog.HandlerOptions{Level: slog.LevelDebug}))
	slog.SetDefault(logger)
	t.Cleanup(func() {
		slog.SetDefault(previous)
	})
	return planningLogCapture{buffer: buffer}
}

func (c planningLogCapture) records() []map[string]any {
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

func assertPlanningLogField(t *testing.T, record map[string]any, key string, want any) {
	t.Helper()
	got, ok := record[key]
	if !ok {
		t.Fatalf("missing key %q in record %#v", key, record)
	}
	if got != want {
		t.Fatalf("record[%q] = %#v, want %#v", key, got, want)
	}
}

func payloadEqual(got map[string]any, want map[string]any) bool {
	gotBytes, _ := json.Marshal(got)
	wantBytes, _ := json.Marshal(want)
	return bytes.Equal(gotBytes, wantBytes)
}

func (s *planningSessionStub) Participant(participantID string) (participant.Participant, bool) {
	if s == nil || s.state == nil {
		return participant.Participant{}, false
	}
	playerState, ok := s.state.Players[participantID]
	if !ok || playerState == nil {
		return participant.Participant{}, false
	}
	return participant.Participant{ID: participantID, Username: playerState.Username, Kind: participant.KindHuman}, true
}
