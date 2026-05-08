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

func TestParseDraftResponseSanitizesObviouslyEnglishPlayerText(t *testing.T) {
	out, err := ParseDraftResponse(`{
		"title":"Hold the Line",
		"summary":"Keep the border secure this turn.",
		"rationale":"Enemy scouts were seen nearby.",
		"risk_note":"Supply lines may be exposed."
	}`)
	if err != nil {
		t.Fatalf("ParseDraftResponse error = %v", err)
	}

	if out.Title != chineseDraftTitleFallback {
		t.Fatalf("Title = %q, want %q", out.Title, chineseDraftTitleFallback)
	}
	if out.Summary != chineseDraftSummaryFallback {
		t.Fatalf("Summary = %q, want %q", out.Summary, chineseDraftSummaryFallback)
	}
	if out.Rationale != chineseDraftReasonFallback {
		t.Fatalf("Rationale = %q, want %q", out.Rationale, chineseDraftReasonFallback)
	}
	if out.RiskNote != chineseDraftRiskNoteFallback {
		t.Fatalf("RiskNote = %q, want %q", out.RiskNote, chineseDraftRiskNoteFallback)
	}
}

func TestParseDraftResponseExtractsFirstJSONObjectFromNoisyInput(t *testing.T) {
	out, err := ParseDraftResponse("下面是整理后的建议，请直接采用。\n{\"title\":\"整备边防\",\"summary\":\"本轮优先巩固边境驻防。\",\"rationale\":\"侦察回报显示边境压力上升。\",\"risk_note\":\"若同时扩张，后勤会更紧张。\"}\n补充说明：其余内容可忽略。")
	if err != nil {
		t.Fatalf("ParseDraftResponse error = %v", err)
	}

	if out.Title != "整备边防" {
		t.Fatalf("Title = %q, want extracted JSON title", out.Title)
	}
	if out.Summary != "本轮优先巩固边境驻防。" {
		t.Fatalf("Summary = %q, want extracted JSON summary", out.Summary)
	}
	if out.Rationale != "侦察回报显示边境压力上升。" {
		t.Fatalf("Rationale = %q, want extracted JSON rationale", out.Rationale)
	}
	if out.RiskNote != "若同时扩张，后勤会更紧张。" {
		t.Fatalf("RiskNote = %q, want extracted JSON risk note", out.RiskNote)
	}
}
