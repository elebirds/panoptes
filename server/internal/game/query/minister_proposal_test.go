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

func TestBuildMinisterProposalViewsCarriesOperationCommands(t *testing.T) {
	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, nil)
	state.TurnRuntime.Planning.SetMinisterDrafts("player-1", []domain.MinisterDraft{{
		DraftID:      "operation-1",
		PlayerID:     "player-1",
		MinisterRole: "military",
		Kind:         domain.MinisterDraftKindOperation,
		TargetID:     "secure_a2",
		TargetLabel:  "控制或侦察 A2",
		Title:        "机动方案",
		Status:       domain.MinisterDraftStatusPending,
		Available:    true,
		Turn:         3,
		OperationID:  "secure_a2",
		Objective:    "控制或侦察 A2",
		OperationSteps: []domain.MinisterDraft{{
			DraftID:      "operation-1:step_1",
			PlayerID:     "player-1",
			MinisterRole: "military",
			Kind:         domain.MinisterDraftKindUnitOrder,
			TargetLabel:  "u1 move -> A2",
			UnitID:       "u1",
			Action:       "move",
			TargetNodeID: "A2",
			Turn:         3,
		}},
	}})

	views := BuildMinisterProposalViews(state, "player-1")
	if len(views) != 1 {
		t.Fatalf("proposal count = %d, want 1", len(views))
	}
	proposal := views[0]
	if proposal.GetProposedCommand() != nil {
		t.Fatalf("operation proposed command = %#v, want nil parent command", proposal.GetProposedCommand())
	}
	if proposal.GetOperationId() != "secure_a2" || proposal.GetObjective() != "控制或侦察 A2" {
		t.Fatalf("operation fields = id:%q objective:%q", proposal.GetOperationId(), proposal.GetObjective())
	}
	commands := proposal.GetOperationCommands()
	if len(commands) != 1 {
		t.Fatalf("operation commands = %d, want 1", len(commands))
	}
	if commands[0].GetKind() != string(domain.MinisterDraftKindUnitOrder) ||
		commands[0].GetCommand().GetIssueUnitOrder().GetTargetNodeId() != "A2" ||
		commands[0].GetRawJson() == "" {
		t.Fatalf("operation command = %#v, want typed unit order and raw json", commands[0])
	}
}
