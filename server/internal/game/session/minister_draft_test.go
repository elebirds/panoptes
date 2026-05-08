package session

import (
	"strings"
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
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
