package minister

import (
	"strings"
	"testing"
)

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
		ActionCandidates:   "candidate_id=domestic:research:bronze_working:4 kind=research target_id=bronze_working target_label=青铜冶炼 source=rule_only",
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
	if !strings.Contains(req.SystemPrompt, "## JSON Response Format") ||
		!strings.Contains(req.SystemPrompt, "`metrics` 是数值轨") ||
		!strings.Contains(req.SystemPrompt, `"proposals": [`) ||
		!strings.Contains(req.SystemPrompt, `"type": "select_candidate|build|set_research|set_policy|set_institution_loadout|set_building_recipe"`) {
		t.Fatalf("SystemPrompt = %q, want exact report JSON response format", req.SystemPrompt)
	}
	if !strings.Contains(req.SystemPrompt, "你负责内政、研究、国策和制度") {
		t.Fatalf("SystemPrompt = %q, want role duty text", req.SystemPrompt)
	}
	if !strings.Contains(req.SystemPrompt, "`select_candidate`") || !strings.Contains(req.SystemPrompt, "candidate_id") ||
		!strings.Contains(req.UserPrompt, "candidate_id=domestic:research:bronze_working:4") {
		t.Fatalf("Prompt = %q\n%s, want candidate selection contract", req.SystemPrompt, req.UserPrompt)
	}
	if !strings.Contains(req.SystemPrompt, "至少输出 1-3 个最匹配的提案") ||
		!strings.Contains(req.UserPrompt, "至少给出 1-3 个提案") {
		t.Fatalf("Prompt = %q\n%s, want multi-proposal requirement", req.SystemPrompt, req.UserPrompt)
	}
	if !strings.Contains(req.SystemPrompt, `"risk_note": "<可选，简体中文风险提示>"`) ||
		!strings.Contains(req.UserPrompt, "`title`、`summary`、`rationale`、`risk_note`") {
		t.Fatalf("Prompt = %q\n%s, want optional action proposal copy fields", req.SystemPrompt, req.UserPrompt)
	}
	if !strings.Contains(req.SystemPrompt, "不得把候选来源、规则规划器、内部校验或系统实现写进玩家可见提案文案") ||
		!strings.Contains(req.UserPrompt, "不得提到候选来源、规则规划器、内部校验或系统实现") {
		t.Fatalf("Prompt = %q\n%s, want no internal proposal copy rule", req.SystemPrompt, req.UserPrompt)
	}
	if !strings.Contains(req.SystemPrompt, "`build`") || !strings.Contains(req.UserPrompt, "单位/地图行动只能通过 Action Candidates") {
		t.Fatalf("Prompt = %q\n%s, want report action contract", req.SystemPrompt, req.UserPrompt)
	}
	if !strings.Contains(req.SystemPrompt, "`set_research`") || !strings.Contains(req.SystemPrompt, "`set_policy`") ||
		!strings.Contains(req.SystemPrompt, "`set_institution_loadout`") || !strings.Contains(req.SystemPrompt, "`set_building_recipe`") {
		t.Fatalf("SystemPrompt = %q, want expanded report action contract", req.SystemPrompt)
	}
	if strings.Contains(req.SystemPrompt, "`move_units`") || strings.Contains(req.SystemPrompt, "`unit_order`") ||
		strings.Contains(req.UserPrompt, "`move_units`") || strings.Contains(req.UserPrompt, "`unit_order`") {
		t.Fatalf("Prompt = %q\n%s, deprecated unit action compatibility types must not be advertised", req.SystemPrompt, req.UserPrompt)
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
	if !strings.Contains(req.UserPrompt, "## Two-Track Output Contract") ||
		!strings.Contains(req.UserPrompt, "Return exactly this JSON shape and no other fields") ||
		!strings.Contains(req.UserPrompt, `"action_id": "<简短英文或数字标识；无动作时可为空字符串>"`) ||
		!strings.Contains(req.UserPrompt, `"proposals": [`) {
		t.Fatalf("UserPrompt = %q, want exact report JSON shape and two-track contract", req.UserPrompt)
	}
}

func TestBuildReportPromptVariesSubjectivePressureForSameObservation(t *testing.T) {
	input := ReportPromptInput{
		Turn:               5,
		Phase:              "planning",
		PlayerID:           "player-1",
		ObservationSummary: "visible_nodes=3; report_confidence=low; reported_omitted=2; reported_delayed=1; reported_misread=1",
		CurrentPolicy:      "reorganization",
		CurrentResearch:    "agrarian_foundations",
	}

	cautious := BuildReportPrompt(MinisterProfile{
		Name:            "沈衡",
		Role:            "domestic",
		Personality:     "steady",
		PersonalityDesc: "稳健审慎",
		Loyalty:         8,
		Ambition:        3,
		Cautiousness:    82,
		Decisiveness:    40,
		LoyaltyTendency: 90,
		AmbitionStyle:   20,
	}, input)
	ambitious := BuildReportPrompt(MinisterProfile{
		Name:            "李猛",
		Role:            "command",
		Personality:     "aggressive",
		PersonalityDesc: "果断激进",
		Loyalty:         2,
		Ambition:        9,
		Cautiousness:    35,
		Decisiveness:    75,
		LoyaltyTendency: 10,
		AmbitionStyle:   80,
	}, input)

	if cautious.UserPrompt != ambitious.UserPrompt {
		t.Fatalf("same observation user prompt differed:\n%s\n---\n%s", cautious.UserPrompt, ambitious.UserPrompt)
	}
	if cautious.SystemPrompt == ambitious.SystemPrompt {
		t.Fatalf("system prompts should differ for distinct minister profiles")
	}
	if !strings.Contains(cautious.SystemPrompt, "高谨慎度会让你更强调风险边界") {
		t.Fatalf("cautious system prompt = %q, want cautious distortion pressure", cautious.SystemPrompt)
	}
	if !strings.Contains(ambitious.SystemPrompt, "低忠诚和高野心会让你更倾向淡化不利信息") {
		t.Fatalf("ambitious system prompt = %q, want low-loyalty/high-ambition distortion pressure", ambitious.SystemPrompt)
	}
}
