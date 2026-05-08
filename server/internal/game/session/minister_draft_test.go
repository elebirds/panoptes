package session

import (
	"strings"
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
)

func TestMinisterDraftTextUsesChineseProposalCopyForResearch(t *testing.T) {
	title, summary, rationale, riskNote := ministerDraftText(domesticMinisterRole, string(domain.MinisterDraftKindResearch), "组织化劳动")

	if title != "研究提案" {
		t.Fatalf("title = %q, want 研究提案", title)
	}
	if !strings.Contains(summary, "组织化劳动") || !strings.Contains(summary, "研究目标") {
		t.Fatalf("summary = %q, want Chinese research guidance", summary)
	}
	if strings.Contains(rationale, "规则规划器") || strings.Contains(rationale, "规则层") || strings.Contains(rationale, "rule") {
		t.Fatalf("rationale = %q, must not expose internal rule-planner copy", rationale)
	}
	if !strings.Contains(riskNote, "暂不采纳") {
		t.Fatalf("riskNote = %q, want Chinese rejection hint", riskNote)
	}
}

func TestMinisterDraftTextUsesChineseRuleCopyForCommand(t *testing.T) {
	title, summary, rationale, riskNote := ministerDraftText(militaryMinisterRole, string(domain.MinisterDraftKindUnitOrder), "军团一号进驻 A3")

	if title != "军令提案" {
		t.Fatalf("title = %q, want 军令提案", title)
	}
	if !strings.Contains(summary, "军团一号进驻 A3") {
		t.Fatalf("summary = %q, want target label in Chinese copy", summary)
	}
	if strings.Contains(summary, "Execute") || strings.Contains(rationale, "规则规划器") || strings.Contains(rationale, "rule planner") || strings.Contains(riskNote, "Reject") {
		t.Fatalf("rule-only draft copy should not leak English: %q / %q / %q", summary, rationale, riskNote)
	}
}
