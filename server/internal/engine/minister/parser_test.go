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
		"actions":[{"type":"select_candidate","params":{"draft_id":"domestic:research:bronze_working:4"}}],
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
	if out.ActionID != "select_research" {
		t.Fatalf("ActionID = %q, want select_research", out.ActionID)
	}
}
