package minister

import (
	"strings"
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
)

func TestBuildDraftPromptInjectsProfileAndChineseConstraints(t *testing.T) {
	req := BuildDraftPrompt(MinisterProfile{
		ID:              "m002",
		Name:            "沈衡",
		Role:            "domestic",
		Ability:         7,
		Personality:     "steady",
		PersonalityDesc: "稳健审慎",
		Loyalty:         8,
		Ambition:        4,
		Cautiousness:    76,
		Decisiveness:    58,
		LoyaltyTendency: 84,
		AmbitionStyle:   32,
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
			Favor:    55,
			Entries: []MemoryEntry{
				{Turn: 5, Type: "draft", Content: "建议优先农业基础", Outcome: "generated", PlayerResp: "accepted"},
			},
		},
	})

	if !strings.Contains(req.SystemPrompt, "沈衡") || !strings.Contains(req.SystemPrompt, "稳健审慎") {
		t.Fatalf("SystemPrompt = %q, want injected minister profile", req.SystemPrompt)
	}
	if !strings.Contains(req.SystemPrompt, "# Panoptes Minister Persona") || !strings.Contains(req.SystemPrompt, "<highlight>") {
		t.Fatalf("SystemPrompt = %q, want markdown structure and highlight control tags", req.SystemPrompt)
	}
	if !strings.Contains(req.SystemPrompt, "P 社让你当上帝，我们让你当人") || !strings.Contains(req.SystemPrompt, "truth") || !strings.Contains(req.SystemPrompt, "observed") || !strings.Contains(req.SystemPrompt, "reported") {
		t.Fatalf("SystemPrompt = %q, want Panoptes premise and information model", req.SystemPrompt)
	}
	if !strings.Contains(req.SystemPrompt, "亲政令牌") || !strings.Contains(req.SystemPrompt, "游戏规则层，而不是 LLM") {
		t.Fatalf("SystemPrompt = %q, want player authority and rule authority framing", req.SystemPrompt)
	}
	if !strings.Contains(req.SystemPrompt, "谨慎度=76/100") || !strings.Contains(req.SystemPrompt, "野心表现=32/100") {
		t.Fatalf("SystemPrompt = %q, want personality dimensions", req.SystemPrompt)
	}
	if !strings.Contains(req.SystemPrompt, "大臣奏报") || !strings.Contains(req.SystemPrompt, "淡化不利信息") {
		t.Fatalf("SystemPrompt = %q, want subjective distortion instructions", req.SystemPrompt)
	}
	if !strings.Contains(req.SystemPrompt, "不得改写目标") || !strings.Contains(req.SystemPrompt, "必须使用简体中文") {
		t.Fatalf("SystemPrompt = %q, want draft-only Chinese constraints", req.SystemPrompt)
	}
	if !strings.Contains(req.SystemPrompt, "输出必须是裸 JSON 对象") {
		t.Fatalf("SystemPrompt = %q, want bare JSON constraint", req.SystemPrompt)
	}
	if !strings.Contains(req.UserPrompt, "bronze_working") || !strings.Contains(req.UserPrompt, "可见 3 个节点") {
		t.Fatalf("UserPrompt = %q, want target id and observation summary", req.UserPrompt)
	}
	if !strings.Contains(req.UserPrompt, "## Rule-Selected Draft") || !strings.Contains(req.UserPrompt, "<highlight>") {
		t.Fatalf("UserPrompt = %q, want markdown structure and highlight control tags", req.UserPrompt)
	}
	if !strings.Contains(req.UserPrompt, "favor=55/100") {
		t.Fatalf("UserPrompt = %q, want memory favor injected", req.UserPrompt)
	}
	if !strings.Contains(req.UserPrompt, "不要 ``` 或 ```json 代码块") || !strings.Contains(req.UserPrompt, "不要任何前缀说明或后缀解释") {
		t.Fatalf("UserPrompt = %q, want no-fence/no-noise output contract", req.UserPrompt)
	}
}

func TestBuildReportPromptInjectsObservationBoundaryAndChineseContract(t *testing.T) {
	req := BuildReportPrompt(MinisterProfile{
		ID:              "m002",
		Name:            "沈衡",
		Role:            "domestic",
		Ability:         7,
		Personality:     "steady",
		PersonalityDesc: "稳健审慎",
		Loyalty:         8,
		Ambition:        4,
		Cautiousness:    76,
		Decisiveness:    58,
		LoyaltyTendency: 84,
		AmbitionStyle:   32,
	}, ReportPromptInput{
		Turn:               4,
		Phase:              "planning",
		PlayerID:           "player-1",
		ObservationSummary: "当前只看到本土腹地，边境敌军位置未知。",
		CurrentPolicy:      "reorganization",
		CurrentResearch:    "agrarian_foundations",
		Memory:             &MinisterMemory{PlayerID: "player-1", Role: "domestic", Favor: 28},
	})

	if !strings.Contains(req.SystemPrompt, "只能基于玩家视角信息") || !strings.Contains(req.SystemPrompt, "不得输出英文句子") {
		t.Fatalf("SystemPrompt = %q, want visibility boundary and Chinese-only rule", req.SystemPrompt)
	}
	if !strings.Contains(req.SystemPrompt, "metrics 里的玩家可读字符串都必须是简体中文") {
		t.Fatalf("SystemPrompt = %q, want report field Chinese constraint", req.SystemPrompt)
	}
	if !strings.Contains(req.SystemPrompt, "输出必须是裸 JSON 对象") {
		t.Fatalf("SystemPrompt = %q, want bare JSON constraint", req.SystemPrompt)
	}
	if !strings.Contains(req.UserPrompt, "边境敌军位置未知") || !strings.Contains(req.UserPrompt, `"metrics"`) {
		t.Fatalf("UserPrompt = %q, want observation summary and JSON contract", req.UserPrompt)
	}
	if !strings.Contains(req.UserPrompt, "## Observation Summary") || !strings.Contains(req.UserPrompt, "<highlight>") {
		t.Fatalf("UserPrompt = %q, want markdown structure and highlight control tags", req.UserPrompt)
	}
	if !strings.Contains(req.UserPrompt, "favor=28/100") || !strings.Contains(req.UserPrompt, "玩家近期多次否决") {
		t.Fatalf("UserPrompt = %q, want memory behavior influence", req.UserPrompt)
	}
	if !strings.Contains(req.UserPrompt, "不要 ``` 或 ```json 代码块") || !strings.Contains(req.UserPrompt, "不要任何前缀说明或后缀解释") {
		t.Fatalf("UserPrompt = %q, want no-fence/no-noise output contract", req.UserPrompt)
	}
}
