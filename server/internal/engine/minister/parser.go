// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现部长引擎的输出解析逻辑。

package minister

import (
	"encoding/json"
	"log/slog"
	"strings"
	"unicode"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/event"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
)

type MinisterOutput struct {
	Report   string
	Metrics  []*pb.MetricItem
	Actions  []MinisterActionItem
	ActionID string
}

type DraftOutput struct {
	Title     string
	Summary   string
	Rationale string
	RiskNote  string
}

type MinisterActionItem struct {
	Type   string
	Params map[string]any
}

const (
	chineseReportFallback        = "大臣暂未生成中文汇报，请以当前观察和既定计划为准。"
	chineseMetricLabelFallback   = "战局指标"
	chineseMetricValueFallback   = "待补充中文说明"
	chineseDraftTitleFallback    = "本轮建议待补充中文标题"
	chineseDraftSummaryFallback  = "大臣暂未生成中文摘要，请结合当前局势评估。"
	chineseDraftReasonFallback   = "中文理由暂缺，请以现有规则目标和观察信息为准。"
	chineseDraftRiskNoteFallback = "风险提示暂缺，请谨慎执行。"
)

func ParseMinisterResponse(response string) (*MinisterOutput, error) {
	response = normalizeJSONObjectPayload(response)
	var raw struct {
		Report  string `json:"report"`
		Metrics []struct {
			Label      string `json:"label"`
			Value      string `json:"value"`
			Trend      string `json:"trend"`
			Confidence string `json:"confidence"`
			IsDelayed  bool   `json:"is_delayed"`
		} `json:"metrics"`
		Actions []struct {
			Type   string         `json:"type"`
			Params map[string]any `json:"params"`
		} `json:"actions"`
		ActionID string `json:"action_id"`
	}
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
		out.Actions = append(out.Actions, MinisterActionItem{Type: a.Type, Params: a.Params})
	}
	out.Report = sanitizePlayerVisibleChinese(out.Report, chineseReportFallback)
	return out, nil
}

func ParseDraftResponse(response string) (*DraftOutput, error) {
	response = normalizeJSONObjectPayload(response)
	var raw struct {
		Title     string `json:"title"`
		Summary   string `json:"summary"`
		Rationale string `json:"rationale"`
		RiskNote  string `json:"risk_note"`
	}
	if err := json.Unmarshal([]byte(response), &raw); err != nil {
		return nil, err
	}
	return &DraftOutput{
		Title:     sanitizePlayerVisibleChinese(raw.Title, chineseDraftTitleFallback),
		Summary:   sanitizePlayerVisibleChinese(raw.Summary, chineseDraftSummaryFallback),
		Rationale: sanitizePlayerVisibleChinese(raw.Rationale, chineseDraftReasonFallback),
		RiskNote:  sanitizePlayerVisibleChinese(raw.RiskNote, chineseDraftRiskNoteFallback),
	}, nil
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

type ActionRoom interface {
	State() *domain.GameState
}

func ExecuteActions(actions []MinisterActionItem, room ActionRoom, playerID string) []event.Event {
	state := room.State()
	events := make([]event.Event, 0)
	for _, action := range actions {
		switch action.Type {
		case "build":
			nodeID, _ := asString(action.Params["node_id"])
			buildingType, _ := asString(action.Params["building_type"])
			if nodeID == "" || buildingType == "" {
				continue
			}
			state.TurnRuntime.Planning.MinisterBuilds = append(state.TurnRuntime.Planning.MinisterBuilds, domain.BuildOrder{PlayerID: playerID, NodeID: nodeID, BuildingType: buildingType})
		case "repair_road":
			// 道路当前仍未接入 Chunk 3 统一预算与 map action 结算，
			// 这里禁止部长直接落图，避免绕过点数账本。
			continue
		case "move_units":
			unitID, _ := asString(action.Params["unit_id"])
			targetNode, _ := asString(action.Params["target_node"])
			if unitID == "" || targetNode == "" {
				continue
			}
			nodeEntry, ok := state.GetNode(targetNode)
			if !ok {
				continue
			}
			p := ecs.PositionC.Get(nodeEntry)
			pos := domain.Position{Q: p.Q, R: p.R}
			state.TurnRuntime.Planning.MinisterMoves = append(state.TurnRuntime.Planning.MinisterMoves, domain.MoveOrder{PlayerID: playerID, UnitID: unitID, Target: pos})
		case "redirect_flow":
			// redirect_flow 暂时只记录，不直接修改持久配置。
		default:
			slog.Warn("unknown minister action", "type", action.Type)
		}
	}
	return events
}

func asString(v any) (string, bool) {
	s, ok := v.(string)
	return s, ok
}

func asInt(v any) (int, bool) {
	switch t := v.(type) {
	case float64:
		return int(t), true
	case int:
		return t, true
	default:
		return 0, false
	}
}
