package debug

import (
	"testing"
	"time"

	"github.com/elebirds/panoptes/internal/game/scenario"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
)

func TestHarnessResearchUnlockBuild_NextTurnOnly(t *testing.T) {
	def, err := scenario.ResearchUnlockBuild()
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
	if err := h.InjectPlanningCommand("player-1", "req-research", &pb.PlanningCommand{
		Body: &pb.PlanningCommand_SetResearchTarget{
			SetResearchTarget: &pb.MsgSetResearchTarget{TechnologyId: "agri_unlock_farm"},
		},
	}); err != nil {
		t.Fatalf("InjectPlanningCommand(research) error = %v", err)
	}
	if err := h.SubmitTurn("player-1"); err != nil {
		t.Fatalf("SubmitTurn(turn=1) error = %v", err)
	}

	turn1, err := h.WaitSettlement("player-1", 1, 3*time.Second)
	if err != nil {
		t.Fatalf("WaitSettlement(turn=1) error = %v", err)
	}
	if !hasTurnEvent(turn1.Settlement, "economy", "technology_completed") {
		t.Fatalf("turn 1 missing technology_completed event")
	}
	if _, ok := turn1.Summary.Buildings["A2"]; ok {
		t.Fatalf("turn 1 should not build farm on A2")
	}

	if _, err := h.WaitPlanningStart("player-1", 2, 2*time.Second); err != nil {
		t.Fatalf("WaitPlanningStart(turn=2) error = %v", err)
	}
	if err := h.InjectPlanningCommand("player-1", "req-build", &pb.PlanningCommand{
		Body: &pb.PlanningCommand_BuildStructure{
			BuildStructure: &pb.MsgBuildStructure{NodeId: "A2", BuildingTypeId: "farm", CityId: "A1"},
		},
	}); err != nil {
		t.Fatalf("InjectPlanningCommand(build) error = %v", err)
	}
	if err := h.SubmitTurn("player-1"); err != nil {
		t.Fatalf("SubmitTurn(turn=2) error = %v", err)
	}

	turn2, err := h.WaitSettlement("player-1", 2, 3*time.Second)
	if err != nil {
		t.Fatalf("WaitSettlement(turn=2) error = %v", err)
	}
	if !hasTurnEvent(turn2.Settlement, "economy", "building_built") {
		t.Fatalf("turn 2 missing building_built event")
	}
	if building, ok := turn2.Summary.Buildings["A2"]; !ok || building.Type != "farm" {
		t.Fatalf("turn 2 building summary = %#v, want farm at A2", building)
	}
}

func TestHarnessSettlerFoundCity_RecordsTurnArtifacts(t *testing.T) {
	def, err := scenario.SettlerFoundCity()
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
	if err := h.InjectPlanningCommand("player-1", "req-settle", &pb.PlanningCommand{
		Body: &pb.PlanningCommand_IssueUnitOrder{
			IssueUnitOrder: &pb.MsgIssueUnitOrder{
				UnitId:       "settler-1",
				Action:       "settle_city",
				TargetNodeId: "C3",
			},
		},
	}); err != nil {
		t.Fatalf("InjectPlanningCommand(settle_city) error = %v", err)
	}
	if err := h.SubmitTurn("player-1"); err != nil {
		t.Fatalf("SubmitTurn() error = %v", err)
	}

	record, err := h.WaitSettlement("player-1", 1, 3*time.Second)
	if err != nil {
		t.Fatalf("WaitSettlement() error = %v", err)
	}
	if !hasTurnEvent(record.Settlement, "map", "city_founded") {
		t.Fatalf("missing city_founded event")
	}
	if _, ok := record.Summary.Units["settler-1"]; ok {
		t.Fatalf("settler-1 should be removed after city founding")
	}
	if center, ok := record.Summary.Buildings["C3"]; !ok || center.Type != "city_core" {
		t.Fatalf("city center summary = %#v, want city_core at C3", center)
	}
}

func TestHarnessRecipeBlockedByInput_RecordsSettlementAndState(t *testing.T) {
	def, err := scenario.RecipeBlockedByInput()
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
	if !hasTurnEvent(record.Settlement, "economy", "building_status_changed") {
		t.Fatalf("missing building_status_changed event")
	}
	building, ok := record.Summary.Buildings["A2"]
	if !ok {
		t.Fatalf("missing building summary for A2")
	}
	if !building.Disabled && !hasTurnEvent(record.Settlement, "economy", "recipe_progressed") {
		t.Fatalf("blocked recipe should produce recipe_progressed event")
	}
}

func TestHarnessCapitalDestroyGameOver_StopsAtGameOver(t *testing.T) {
	def, err := scenario.CapitalDestroyGameOver()
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
	if !hasTurnEvent(record.Settlement, "unit", "city_core_destroyed") {
		t.Fatalf("missing city_core_destroyed event")
	}
	if record.GameOver == nil {
		t.Fatalf("game over payload is nil")
	}
	if record.GameOver.GetReason() != "city_core_destroyed" {
		t.Fatalf("game over reason = %q, want city_core_destroyed", record.GameOver.GetReason())
	}
	if !record.Summary.IsOver || record.Summary.WinnerID != "player-2" {
		t.Fatalf("summary game over = %#v", record.Summary)
	}
}

func TestHarnessOuterFacilityCapture_DeactivatesContestedFacility(t *testing.T) {
	def, err := scenario.OuterFacilityCapture()
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
	if !hasTurnEvent(record.Settlement, "economy", "facility_takeover_progressed") {
		t.Fatalf("missing facility_takeover_progressed event")
	}
	building, ok := record.Summary.Buildings["B2"]
	if !ok {
		t.Fatalf("missing building summary for B2")
	}
	if !building.Disabled || building.DisabledReason != "enemy_control" {
		t.Fatalf("building summary = %#v, want disabled enemy_control", building)
	}
}

func hasTurnEvent(msg *pb.MsgTurnSettlement, section string, eventType string) bool {
	if msg == nil {
		return false
	}
	for _, currentSection := range msg.GetSections() {
		if currentSection.GetSection() != section {
			continue
		}
		for _, event := range currentSection.GetEvents() {
			if event.GetType() == eventType {
				return true
			}
		}
	}
	return false
}
