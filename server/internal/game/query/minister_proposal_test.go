package query

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
)

func TestBuildMinisterProposalViewsCarriesTypedCommandAndRawJSON(t *testing.T) {
	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, nil)
	state.TurnRuntime.Planning.SetMinisterDrafts("player-1", []domain.MinisterDraft{{
		DraftID:      "draft-1",
		MinisterRole: "domestic",
		Kind:         domain.MinisterDraftKindResearch,
		TargetID:     "agrarian_foundations",
		Title:        "研究农政基础",
		Summary:      "提升早期经济稳定性",
		Status:       domain.MinisterDraftStatusPending,
		Available:    true,
		Turn:         3,
	}})

	views := BuildMinisterProposalViews(state, "player-1")
	if len(views) != 1 {
		t.Fatalf("proposal count = %d, want 1", len(views))
	}
	proposal := views[0]
	if proposal.GetProposalId() != "draft-1" || proposal.GetMinisterRole() != "domestic" {
		t.Fatalf("proposal header = %#v", proposal)
	}
	if proposal.GetProposedCommand().GetSetResearchTarget().GetTechnologyId() != "agrarian_foundations" {
		t.Fatalf("typed command = %#v", proposal.GetProposedCommand())
	}
	if proposal.GetRawJson() == "" {
		t.Fatalf("raw_json is empty")
	}
}
