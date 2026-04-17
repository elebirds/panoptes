package minister

import (
	"strings"
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
)

func TestBuildDraftPromptInjectsProfileAndDraftConstraints(t *testing.T) {
	req := BuildDraftPrompt(MinisterProfile{
		ID:              "m002",
		Name:            "沈衡",
		Role:            "domestic",
		Ability:         7,
		Personality:     "steady",
		PersonalityDesc: "稳健审慎",
		Loyalty:         8,
		Ambition:        4,
	}, DraftPromptInput{
		Turn:               6,
		PlayerID:           "player-1",
		ObservationSummary: "可见 3 个节点，当前食物储备偏紧。",
		CurrentPolicy:      "reorganization",
		CurrentResearch:    "agrarian_foundations",
		Draft: domain.MinisterDraft{
			DraftID:      "domestic:research:bronze_working:6",
			MinisterRole: "domestic",
			Kind:         domain.MinisterDraftKindResearch,
			TargetID:     "bronze_working",
			TargetLabel:  "Bronze Working",
		},
		Memory: &MinisterMemory{
			PlayerID: "player-1",
			Role:     "domestic",
			Entries: []MemoryEntry{
				{Turn: 5, Type: "draft", Content: "建议优先农业基础", Outcome: "generated", PlayerResp: "accepted"},
			},
		},
	})

	if !strings.Contains(req.SystemPrompt, "沈衡") || !strings.Contains(req.SystemPrompt, "稳健审慎") {
		t.Fatalf("SystemPrompt = %q, want injected minister profile", req.SystemPrompt)
	}
	if !strings.Contains(req.SystemPrompt, "不得改写目标") || !strings.Contains(req.SystemPrompt, "严格输出JSON") {
		t.Fatalf("SystemPrompt = %q, want draft-only JSON constraints", req.SystemPrompt)
	}
	if !strings.Contains(req.UserPrompt, "bronze_working") || !strings.Contains(req.UserPrompt, "可见 3 个节点") {
		t.Fatalf("UserPrompt = %q, want target id and observation summary", req.UserPrompt)
	}
}

func TestBuildReportPromptInjectsObservationBoundaryAndJsonContract(t *testing.T) {
	req := BuildReportPrompt(MinisterProfile{
		ID:              "m002",
		Name:            "沈衡",
		Role:            "domestic",
		Ability:         7,
		Personality:     "steady",
		PersonalityDesc: "稳健审慎",
		Loyalty:         8,
		Ambition:        4,
	}, ReportPromptInput{
		Turn:               4,
		Phase:              "planning",
		PlayerID:           "player-1",
		ObservationSummary: "当前只看到本土腹地，边境敌军位置未知。",
		CurrentPolicy:      "reorganization",
		CurrentResearch:    "agrarian_foundations",
		Memory:             &MinisterMemory{PlayerID: "player-1", Role: "domestic"},
	})

	if !strings.Contains(req.SystemPrompt, "只能基于玩家视角信息") || !strings.Contains(req.SystemPrompt, "不得编造隐藏信息") {
		t.Fatalf("SystemPrompt = %q, want visibility boundary", req.SystemPrompt)
	}
	if !strings.Contains(req.UserPrompt, "边境敌军位置未知") || !strings.Contains(req.UserPrompt, `"metrics"`) {
		t.Fatalf("UserPrompt = %q, want observation summary and JSON contract", req.UserPrompt)
	}
}
