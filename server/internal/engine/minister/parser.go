// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现部长引擎的输出解析逻辑。

package minister

import (
	"encoding/json"
	"strings"
	"unicode"

	pb "github.com/elebirds/panoptes/internal/gen/proto"
)

type MinisterOutput struct {
	Report   string
	Metrics  []*pb.MetricItem
	Actions  []MinisterActionItem
	ActionID string
}

// MinisterReportResponse is the exact JSON object expected from report prompts.
type MinisterReportResponse struct {
	Report   string               `json:"report"`
	Metrics  []MinisterMetricItem `json:"metrics"`
	Actions  []MinisterActionItem `json:"actions"`
	ActionID string               `json:"action_id"`
}

// MinisterMetricItem is the JSON shape for one report metric item.
type MinisterMetricItem struct {
	Label      string `json:"label"`
	Value      string `json:"value"`
	Trend      string `json:"trend"`
	Confidence string `json:"confidence"`
	IsDelayed  bool   `json:"is_delayed"`
}

type MinisterActionItem struct {
	Type      string         `json:"type"`
	Params    map[string]any `json:"params"`
	Title     string         `json:"title,omitempty"`
	Summary   string         `json:"summary,omitempty"`
	Rationale string         `json:"rationale,omitempty"`
	RiskNote  string         `json:"risk_note,omitempty"`
}

const (
	chineseReportFallback      = "大臣暂未生成中文汇报，请以当前观察和既定计划为准。"
	chineseMetricLabelFallback = "战局指标"
	chineseMetricValueFallback = "待补充中文说明"
)

func ParseMinisterResponse(response string) (*MinisterOutput, error) {
	response = normalizeJSONObjectPayload(response)
	var raw MinisterReportResponse
	if err := json.Unmarshal([]byte(response), &raw); err != nil {
		return nil, err
	}

	out := &MinisterOutput{Report: raw.Report, ActionID: raw.ActionID}
	out.Metrics = make([]*pb.MetricItem, 0, len(raw.Metrics))
	for _, m := range raw.Metrics {
		out.Metrics = append(out.Metrics, &pb.MetricItem{
			Label:      sanitizePlayerVisibleChinese(m.Label, chineseMetricLabelFallback),
			Value:      sanitizePlayerVisibleChinese(m.Value, chineseMetricValueFallback),
			Trend:      m.Trend,
			Confidence: m.Confidence,
			IsDelayed:  m.IsDelayed,
		})
	}
	out.Actions = make([]MinisterActionItem, 0, len(raw.Actions))
	for _, a := range raw.Actions {
		out.Actions = append(out.Actions, MinisterActionItem{
			Type:      a.Type,
			Params:    a.Params,
			Title:     sanitizeOptionalPlayerVisibleChinese(a.Title),
			Summary:   sanitizeOptionalPlayerVisibleChinese(a.Summary),
			Rationale: sanitizeOptionalPlayerVisibleChinese(a.Rationale),
			RiskNote:  sanitizeOptionalPlayerVisibleChinese(a.RiskNote),
		})
	}
	out.Report = sanitizePlayerVisibleChinese(out.Report, chineseReportFallback)
	return out, nil
}

func sanitizeOptionalPlayerVisibleChinese(text string) string {
	text = strings.TrimSpace(text)
	if text == "" || isObviouslyEnglishText(text) {
		return ""
	}
	return text
}

func normalizeJSONObjectPayload(response string) string {
	response = strings.TrimSpace(response)
	if response == "" {
		return response
	}

	response = stripMarkdownJSONFence(response)
	if object, ok := extractFirstJSONObject(response); ok {
		return object
	}
	return response
}

func stripMarkdownJSONFence(response string) string {
	lines := strings.Split(response, "\n")
	if len(lines) < 3 {
		return response
	}

	first := strings.TrimSpace(lines[0])
	last := strings.TrimSpace(lines[len(lines)-1])
	if !strings.HasPrefix(first, "```") || last != "```" {
		return response
	}

	return strings.TrimSpace(strings.Join(lines[1:len(lines)-1], "\n"))
}

func extractFirstJSONObject(response string) (string, bool) {
	for start := strings.IndexByte(response, '{'); start >= 0; start = nextJSONObjectStart(response, start+1) {
		object, ok := scanJSONObjectAt(response, start)
		if !ok {
			continue
		}
		if json.Valid([]byte(object)) {
			return object, true
		}
	}

	return "", false
}

func nextJSONObjectStart(response string, offset int) int {
	if offset >= len(response) {
		return -1
	}
	next := strings.IndexByte(response[offset:], '{')
	if next < 0 {
		return -1
	}
	return offset + next
}

func scanJSONObjectAt(response string, start int) (string, bool) {
	depth := 0
	inString := false
	escaped := false
	for i := start; i < len(response); i++ {
		switch response[i] {
		case '\\':
			if inString {
				escaped = !escaped
			}
		case '"':
			if !escaped {
				inString = !inString
			}
			escaped = false
		case '{':
			if !inString {
				depth++
			}
			escaped = false
		case '}':
			if !inString {
				depth--
				if depth == 0 {
					return response[start : i+1], true
				}
			}
			escaped = false
		default:
			escaped = false
		}
	}

	return "", false
}

func sanitizePlayerVisibleChinese(text string, fallback string) string {
	text = strings.TrimSpace(text)
	if text == "" {
		return fallback
	}
	if isObviouslyEnglishText(text) {
		return fallback
	}
	return text
}

func isObviouslyEnglishText(text string) bool {
	text = strings.TrimSpace(text)
	if text == "" {
		return false
	}

	latinLetters := 0
	latinRun := 0
	hasLatinWord := false
	for _, r := range text {
		if unicode.Is(unicode.Han, r) {
			return false
		}
		if isLatinLetter(r) {
			latinLetters++
			latinRun++
			if latinRun >= 3 {
				hasLatinWord = true
			}
			continue
		}
		latinRun = 0
	}

	return hasLatinWord && latinLetters >= 3
}

func isLatinLetter(r rune) bool {
	return (r >= 'a' && r <= 'z') || (r >= 'A' && r <= 'Z')
}
