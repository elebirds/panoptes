package minister

import "testing"

func TestParseMinisterResponseSanitizesObviouslyEnglishPlayerText(t *testing.T) {
	out, err := ParseMinisterResponse(`{
		"report":"Enemy pressure is rising near the border.",
		"metrics":[{"label":"Food Reserve","value":"Low","trend":"down","confidence":"medium","is_delayed":false}],
		"actions":[],
		"action_id":"keep_supply"
	}`)
	if err != nil {
		t.Fatalf("ParseMinisterResponse error = %v", err)
	}

	if out.Report != chineseReportFallback {
		t.Fatalf("Report = %q, want %q", out.Report, chineseReportFallback)
	}
	if len(out.Metrics) != 1 {
		t.Fatalf("metrics len = %d, want 1", len(out.Metrics))
	}
	if out.Metrics[0].GetLabel() != chineseMetricLabelFallback {
		t.Fatalf("Metric label = %q, want %q", out.Metrics[0].GetLabel(), chineseMetricLabelFallback)
	}
	if out.Metrics[0].GetValue() != chineseMetricValueFallback {
		t.Fatalf("Metric value = %q, want %q", out.Metrics[0].GetValue(), chineseMetricValueFallback)
	}
	if out.Metrics[0].GetTrend() != "down" || out.Metrics[0].GetConfidence() != "medium" {
		t.Fatalf("Metric enum fields changed unexpectedly: %+v", out.Metrics[0])
	}
	if out.ActionID != "keep_supply" {
		t.Fatalf("ActionID = %q, want unchanged", out.ActionID)
	}
}

func TestParseMinisterResponseAcceptsFencedJSON(t *testing.T) {
	out, err := ParseMinisterResponse("```json\n{\n  \"report\":\"边境局势暂稳。\",\n  \"metrics\":[{\"label\":\"粮食储备\",\"value\":\"充足\",\"trend\":\"stable\",\"confidence\":\"high\",\"is_delayed\":false}],\n  \"actions\":[],\n  \"action_id\":\"observe\"\n}\n```")
	if err != nil {
		t.Fatalf("ParseMinisterResponse error = %v", err)
	}

	if out.Report != "边境局势暂稳。" {
		t.Fatalf("Report = %q, want parsed fenced JSON content", out.Report)
	}
	if len(out.Metrics) != 1 {
		t.Fatalf("metrics len = %d, want 1", len(out.Metrics))
	}
	if out.Metrics[0].GetLabel() != "粮食储备" || out.Metrics[0].GetValue() != "充足" {
		t.Fatalf("Metric content = %+v, want preserved Chinese strings", out.Metrics[0])
	}
	if out.ActionID != "observe" {
		t.Fatalf("ActionID = %q, want observe", out.ActionID)
	}
}

func TestParseMinisterResponseSkipsBraceNoiseBeforeJSONObject(t *testing.T) {
	out, err := ParseMinisterResponse("提示：沿用 {report} 字段，不要改键名。\n{\"report\":\"边境驻军已完成轮换。\",\"metrics\":[{\"label\":\"前线兵力\",\"value\":\"稳定\",\"trend\":\"stable\",\"confidence\":\"high\",\"is_delayed\":false}],\"actions\":[],\"action_id\":\"hold_line\"}")
	if err != nil {
		t.Fatalf("ParseMinisterResponse error = %v", err)
	}

	if out.Report != "边境驻军已完成轮换。" {
		t.Fatalf("Report = %q, want extracted JSON report", out.Report)
	}
	if len(out.Metrics) != 1 {
		t.Fatalf("metrics len = %d, want 1", len(out.Metrics))
	}
	if out.Metrics[0].GetLabel() != "前线兵力" || out.Metrics[0].GetValue() != "稳定" {
		t.Fatalf("Metric content = %+v, want preserved Chinese strings", out.Metrics[0])
	}
	if out.ActionID != "hold_line" {
		t.Fatalf("ActionID = %q, want hold_line", out.ActionID)
	}
}

func TestParseMinisterResponsePreservesActionObjects(t *testing.T) {
	out, err := ParseMinisterResponse(`{
		"report":"建议尽快批准青铜冶炼。",
		"metrics":[],
		"actions":[{"type":"select_candidate","params":{"draft_id":"domestic:research:bronze_working:4"},"title":"青铜研究","summary":"建议先研究青铜冶炼。","rationale":"此举能补强后续军备。","risk_note":"若边境告急，可暂缓。"}],
		"action_id":"select_research"
	}`)
	if err != nil {
		t.Fatalf("ParseMinisterResponse error = %v", err)
	}

	if len(out.Actions) != 1 {
		t.Fatalf("actions len = %d, want 1", len(out.Actions))
	}
	if out.Actions[0].Type != "select_candidate" {
		t.Fatalf("Action type = %q, want select_candidate", out.Actions[0].Type)
	}
	if got, _ := out.Actions[0].Params["draft_id"].(string); got != "domestic:research:bronze_working:4" {
		t.Fatalf("Action draft_id = %q, want candidate id", got)
	}
	if out.Actions[0].Title != "青铜研究" || out.Actions[0].Summary != "建议先研究青铜冶炼。" ||
		out.Actions[0].Rationale != "此举能补强后续军备。" || out.Actions[0].RiskNote != "若边境告急，可暂缓。" {
		t.Fatalf("Action proposal copy = %#v, want preserved Chinese optional fields", out.Actions[0])
	}
	if out.ActionID != "select_research" {
		t.Fatalf("ActionID = %q, want select_research", out.ActionID)
	}
}

func TestParseMinisterResponseDropsEnglishActionProposalText(t *testing.T) {
	out, err := ParseMinisterResponse(`{
		"report":"建议维持当前节奏。",
		"metrics":[],
		"actions":[{"type":"select_candidate","params":{"draft_id":"domestic:policy:expansion:4"},"title":"Expansion Plan","summary":"Take expansion now."}],
		"action_id":"select_policy"
	}`)
	if err != nil {
		t.Fatalf("ParseMinisterResponse error = %v", err)
	}
	if len(out.Actions) != 1 {
		t.Fatalf("actions len = %d, want 1", len(out.Actions))
	}
	if out.Actions[0].Title != "" || out.Actions[0].Summary != "" {
		t.Fatalf("English action copy should be dropped, got %#v", out.Actions[0])
	}
}

func TestParseMinisterResponseFlattensProposalsIntoActions(t *testing.T) {
	out, err := ParseMinisterResponse(`{
		"report":"建议分两步推进。",
		"metrics":[],
		"proposals":[
			{
				"title":"先修道路",
				"summary":"先处理补给线。",
				"rationale":"道路优先。",
				"risk_note":"会占用一回合。",
				"actions":[
					{"type":"build","params":{"node_id":"A2","building_type":"road"}}
				]
			},
			{
				"title":"再整备军令",
				"summary":"随后移动部队。",
				"actions":[
					{"type":"select_candidate","params":{"draft_id":"command:operation:secure_a2:4"}}
				]
			}
		],
		"actions":[],
		"action_id":"multi_step"
	}`)
	if err != nil {
		t.Fatalf("ParseMinisterResponse error = %v", err)
	}
	if len(out.Actions) != 2 {
		t.Fatalf("actions len = %d, want 2", len(out.Actions))
	}
	if out.Actions[0].Title != "先修道路" || out.Actions[0].Summary != "先处理补给线。" || out.Actions[0].Rationale != "道路优先。" || out.Actions[0].RiskNote != "会占用一回合。" {
		t.Fatalf("first flattened action = %#v, want proposal copy", out.Actions[0])
	}
	if out.Actions[1].Title != "再整备军令" || out.Actions[1].Summary != "随后移动部队。" {
		t.Fatalf("second flattened action = %#v, want proposal copy", out.Actions[1])
	}
}
