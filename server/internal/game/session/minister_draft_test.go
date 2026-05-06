package session

import (
	"strings"
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	ministerengine "github.com/elebirds/panoptes/internal/engine/minister"
)

func TestMinisterDraftTextUsesChineseRuleCopyForResearch(t *testing.T) {
	title, summary, rationale, riskNote := ministerDraftText(domesticMinisterRole, string(domain.MinisterDraftKindResearch), "组织化劳动")

	if title != "研究建议" {
		t.Fatalf("title = %q, want 研究建议", title)
	}
	if !strings.Contains(summary, "组织化劳动") || !strings.Contains(summary, "优先研究目标") {
		t.Fatalf("summary = %q, want Chinese research guidance", summary)
	}
	if !strings.Contains(rationale, "规则规划器") {
		t.Fatalf("rationale = %q, want Chinese rule planner rationale", rationale)
	}
	if !strings.Contains(riskNote, "不采纳") {
		t.Fatalf("riskNote = %q, want Chinese rejection hint", riskNote)
	}
}

func TestMinisterDraftTextUsesChineseRuleCopyForMilitary(t *testing.T) {
	title, summary, rationale, riskNote := ministerDraftText(militaryMinisterRole, string(domain.MinisterDraftKindUnitOrder), "军团一号进驻 A3")

	if title != "军事建议" {
		t.Fatalf("title = %q, want 军事建议", title)
	}
	if !strings.Contains(summary, "军团一号进驻 A3") {
		t.Fatalf("summary = %q, want target label in Chinese copy", summary)
	}
	if strings.Contains(summary, "Execute") || strings.Contains(rationale, "rule planner") || strings.Contains(riskNote, "Reject") {
		t.Fatalf("rule-only draft copy should not leak English: %q / %q / %q", summary, rationale, riskNote)
	}
}

func TestApplyPreparedMinisterDraftPolishOnlyUpdatesMilitaryDisplayFields(t *testing.T) {
	runtime := &Runtime{
		preparedMinisterDrafts: map[int]map[string][]domain.MinisterDraft{
			3: {
				"player-1": {
					{
						DraftID:      "military:unit_order:u1_move_a2:3",
						PlayerID:     "player-1",
						MinisterRole: militaryMinisterRole,
						Kind:         domain.MinisterDraftKindUnitOrder,
						TargetID:     "u1:move:A2:",
						TargetLabel:  "u1 move -> A2",
						UnitID:       "u1",
						Action:       "move",
						TargetNodeID: "A2",
						Source:       domain.MinisterDraftSourceRuleOnly,
					},
				},
			},
		},
	}

	runtime.applyPreparedMinisterDraftPolish(preparedMinisterDraftPolishJob{
		Turn:     3,
		PlayerID: "player-1",
		Draft: domain.MinisterDraft{
			DraftID:      "military:unit_order:u1_move_a2:3",
			MinisterRole: militaryMinisterRole,
		},
	}, &ministerengine.DraftOutput{
		Title:     "整备边防",
		Summary:   "本回合按既定军令推进。",
		Rationale: "规则层已经选定单位和目标。",
		RiskNote:  "保持后备调整空间。",
	})

	draft := runtime.preparedMinisterDrafts[3]["player-1"][0]
	if draft.Source != domain.MinisterDraftSourceRuleLLM {
		t.Fatalf("Source = %q, want rule+llm", draft.Source)
	}
	if draft.Title != "整备边防" || draft.Summary != "本回合按既定军令推进。" {
		t.Fatalf("display text not polished: %#v", draft)
	}
	if draft.DraftID != "military:unit_order:u1_move_a2:3" || draft.TargetID != "u1:move:A2:" || draft.UnitID != "u1" || draft.Action != "move" || draft.TargetNodeID != "A2" {
		t.Fatalf("executable draft fields changed: %#v", draft)
	}
}
