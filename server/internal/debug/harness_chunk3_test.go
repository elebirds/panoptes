package debug

import (
	"testing"
	"time"

	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/game/scenario"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
)

func TestHarnessIndustryBudgetExhaustion_RecordsBudgetAndSkip(t *testing.T) {
	def, err := scenario.IndustryBudgetExhaustion()
	if err != nil {
		t.Fatalf("scenario build error = %v", err)
	}

	h, err := NewHarness(def)
	if err != nil {
		t.Fatalf("NewHarness() error = %v", err)
	}
	if err := h.Start(); err != nil {
		t.Fatalf("Start() error = %v", err)
	}
	if _, err := h.WaitPlanningStart("player-1", 1, 2*time.Second); err != nil {
		t.Fatalf("WaitPlanningStart(turn=1) error = %v", err)
	}
	for _, nodeID := range []string{"A2", "B1"} {
		if err := h.InjectPlanningCommand("player-1", "req-build-"+nodeID, &pb.PlanningCommand{
			Body: &pb.PlanningCommand_BuildStructure{
				BuildStructure: &pb.MsgBuildStructure{NodeId: nodeID, BuildingTypeId: "farm"},
			},
		}); err != nil {
			t.Fatalf("InjectPlanningCommand(build %s) error = %v", nodeID, err)
		}
	}
	if err := h.SubmitTurn("player-1"); err != nil {
		t.Fatalf("SubmitTurn() error = %v", err)
	}

	record, err := h.WaitSettlement("player-1", 1, 3*time.Second)
	if err != nil {
		t.Fatalf("WaitSettlement() error = %v", err)
	}
	if !hasTurnEvent(record.Settlement, "economy", "point_budget_refreshed") {
		t.Fatalf("missing point_budget_refreshed event")
	}
	if !hasTurnEvent(record.Settlement, "economy", "point_spent") {
		t.Fatalf("missing point_spent event")
	}
	if !hasTurnEvent(record.Settlement, "economy", "building_skipped") {
		t.Fatalf("missing building_skipped event")
	}
	if _, ok := record.Summary.Buildings["A2"]; !ok {
		t.Fatalf("expected first build at A2 to succeed")
	}
	if _, ok := record.Summary.Buildings["B1"]; ok {
		t.Fatalf("second build at B1 should be skipped after budget exhaustion")
	}
}

func TestHarnessSettlementBuildRevalidation_ReportsSkippedBuild(t *testing.T) {
	def, err := scenario.IndustryBudgetExhaustion()
	if err != nil {
		t.Fatalf("scenario build error = %v", err)
	}

	h, err := NewHarness(def)
	if err != nil {
		t.Fatalf("NewHarness() error = %v", err)
	}
	if err := h.Start(); err != nil {
		t.Fatalf("Start() error = %v", err)
	}
	if _, err := h.WaitPlanningStart("player-1", 1, 2*time.Second); err != nil {
		t.Fatalf("WaitPlanningStart(turn=1) error = %v", err)
	}
	if err := h.InjectPlanningCommand("player-1", "req-build-A2", &pb.PlanningCommand{
		Body: &pb.PlanningCommand_BuildStructure{
			BuildStructure: &pb.MsgBuildStructure{NodeId: "A2", BuildingTypeId: "farm"},
		},
	}); err != nil {
		t.Fatalf("InjectPlanningCommand(build) error = %v", err)
	}
	nodeEntry, ok := h.room.State().GetNode("A2")
	if !ok {
		t.Fatalf("missing node A2")
	}
	node := ecs.NodeC.Get(nodeEntry)
	node.Owner = "player-2"
	node.TerritoryOwner = "player-2"
	if err := h.SubmitTurn("player-1"); err != nil {
		t.Fatalf("SubmitTurn() error = %v", err)
	}

	record, err := h.WaitSettlement("player-1", 1, 3*time.Second)
	if err != nil {
		t.Fatalf("WaitSettlement() error = %v", err)
	}
	if !hasTurnEvent(record.Settlement, "economy", "building_skipped") {
		t.Fatalf("missing building_skipped event")
	}
	if _, ok := record.Summary.Buildings["A2"]; ok {
		t.Fatalf("build should not survive settlement revalidation")
	}
}

func TestHarnessPointPreviewRemainsEffectiveOutputAfterSettlement(t *testing.T) {
	def, err := scenario.BuildingModifierPointPreview()
	if err != nil {
		t.Fatalf("scenario build error = %v", err)
	}

	h, err := NewHarness(def)
	if err != nil {
		t.Fatalf("NewHarness() error = %v", err)
	}
	if err := h.Start(); err != nil {
		t.Fatalf("Start() error = %v", err)
	}
	if _, err := h.WaitPlanningStart("player-1", 1, 2*time.Second); err != nil {
		t.Fatalf("WaitPlanningStart(turn=1) error = %v", err)
	}
	if err := h.SubmitTurn("player-1"); err != nil {
		t.Fatalf("SubmitTurn() error = %v", err)
	}

	record, err := h.WaitSettlement("player-1", 1, 3*time.Second)
	if err != nil {
		t.Fatalf("WaitSettlement() error = %v", err)
	}
	if got := pointAmount(record.Settlement.GetMyPlayerAfter(), "industry_output"); got != 3 {
		t.Fatalf("industry_output preview after settlement = %d, want 3", got)
	}
}

func TestHarnessDisabledRecipeReportsRecipeSkipped(t *testing.T) {
	def, err := scenario.DisabledRecipeSkipped()
	if err != nil {
		t.Fatalf("scenario build error = %v", err)
	}

	h, err := NewHarness(def)
	if err != nil {
		t.Fatalf("NewHarness() error = %v", err)
	}
	if err := h.Start(); err != nil {
		t.Fatalf("Start() error = %v", err)
	}
	if _, err := h.WaitPlanningStart("player-1", 1, 2*time.Second); err != nil {
		t.Fatalf("WaitPlanningStart(turn=1) error = %v", err)
	}
	if err := h.SubmitTurn("player-1"); err != nil {
		t.Fatalf("SubmitTurn() error = %v", err)
	}

	record, err := h.WaitSettlement("player-1", 1, 3*time.Second)
	if err != nil {
		t.Fatalf("WaitSettlement() error = %v", err)
	}
	if !hasTurnEvent(record.Settlement, "economy", "recipe_skipped") {
		t.Fatalf("missing recipe_skipped event")
	}
}

func pointAmount(player *pb.PlayerView, key string) int32 {
	if player == nil || player.GetPoints() == nil {
		return 0
	}
	for _, item := range player.GetPoints().GetItems() {
		if item.GetKey() == key {
			return item.GetAmount()
		}
	}
	return 0
}
