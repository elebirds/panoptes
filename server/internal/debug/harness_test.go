package debug

import (
	"testing"
	"time"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/event"
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

	turn1, err := h.WaitGameSync("player-1", 1, 3*time.Second)
	if err != nil {
		t.Fatalf("WaitGameSync(turn=1) error = %v", err)
	}
	if !hasTurnEvent(turn1.GameSync, "economy", "technology_completed") {
		t.Fatalf("turn 1 missing technology_completed event")
	}
	if _, ok := turn1.Summary.Buildings["A2"]; ok {
		t.Fatalf("turn 1 should not build farm on A2")
	}

	start2, err := h.WaitPlanningStart("player-1", 2, 2*time.Second)
	if err != nil {
		t.Fatalf("WaitPlanningStart(turn=2) error = %v", err)
	}
	if !hasPlanningStartEvent(start2, "technology_activated") {
		t.Fatalf("turn 2 planning start missing technology_activated event")
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

	turn2, err := h.WaitGameSync("player-1", 2, 3*time.Second)
	if err != nil {
		t.Fatalf("WaitGameSync(turn=2) error = %v", err)
	}
	if !hasTurnEvent(turn2.GameSync, "economy", "building_built") {
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

	record, err := h.WaitGameSync("player-1", 1, 3*time.Second)
	if err != nil {
		t.Fatalf("WaitGameSync() error = %v", err)
	}
	if !hasTurnEvent(record.GameSync, "map", "city_founded") {
		t.Fatalf("missing city_founded event")
	}
	if _, ok := record.Summary.Units["settler-1"]; ok {
		t.Fatalf("settler-1 should be removed after city founding")
	}
	if center, ok := record.Summary.Buildings["C3"]; !ok || center.Type != "city_core" {
		t.Fatalf("city center summary = %#v, want city_core at C3", center)
	}
}

func TestHarnessRecipeBlockedByInput_RecordsGameSyncAndState(t *testing.T) {
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

	record, err := h.WaitGameSync("player-1", 1, 3*time.Second)
	if err != nil {
		t.Fatalf("WaitGameSync() error = %v", err)
	}
	if !hasTurnEvent(record.GameSync, "economy", "building_status_changed") {
		t.Fatalf("missing building_status_changed event")
	}
	building, ok := record.Summary.Buildings["A2"]
	if !ok {
		t.Fatalf("missing building summary for A2")
	}
	if !building.Disabled && !hasTurnEvent(record.GameSync, "economy", "recipe_progressed") {
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

	record, err := h.WaitGameSync("player-1", 1, 3*time.Second)
	if err != nil {
		t.Fatalf("WaitGameSync() error = %v", err)
	}
	if !hasTurnEvent(record.GameSync, "unit", "city_core_destroyed") {
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

	record, err := h.WaitGameSync("player-1", 1, 3*time.Second)
	if err != nil {
		t.Fatalf("WaitGameSync() error = %v", err)
	}
	if !hasTurnEvent(record.GameSync, "economy", "facility_takeover_progressed") {
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

func TestHarnessM9PreflightLongGame_CoversMinisterInfoLogisticsAndWarfare(t *testing.T) {
	def := newRealContentHappyPathDefinition(t)

	h, err := NewHarness(def)
	if err != nil {
		t.Fatalf("NewHarness() error = %v", err)
	}
	if err := h.Start(); err != nil {
		t.Fatalf("Start() error = %v", err)
	}

	start1, err := h.WaitPlanningStart("player-1", 1, 2*time.Second)
	if err != nil {
		t.Fatalf("WaitPlanningStart(turn=1) error = %v", err)
	}
	assertM9PreflightPlanningStart(t, start1)
	if err := h.InjectPlanningCommand("player-1", "req-research", &pb.PlanningCommand{
		Body: &pb.PlanningCommand_SetResearchTarget{
			SetResearchTarget: &pb.MsgSetResearchTarget{TechnologyId: "agrarian_foundations"},
		},
	}); err != nil {
		t.Fatalf("InjectPlanningCommand(research) error = %v", err)
	}
	if err := h.SubmitTurn("player-1"); err != nil {
		t.Fatalf("SubmitTurn(turn=1) error = %v", err)
	}

	turn1, err := h.WaitGameSync("player-1", 1, 3*time.Second)
	if err != nil {
		t.Fatalf("WaitGameSync(turn=1) error = %v", err)
	}
	if !hasTurnEvent(turn1.GameSync, "economy", "technology_completed") {
		t.Fatalf("turn 1 missing technology_completed event")
	}
	if _, ok := turn1.Summary.Buildings["B2"]; ok {
		t.Fatalf("turn 1 should not build farm on B2")
	}

	start2, err := h.WaitPlanningStart("player-1", 2, 2*time.Second)
	if err != nil {
		t.Fatalf("WaitPlanningStart(turn=2) error = %v", err)
	}
	if !hasPlanningStartEvent(start2, "technology_activated") {
		t.Fatalf("turn 2 planning start missing technology_activated event")
	}
	if err := h.InjectPlanningCommand("player-1", "req-build-farm", &pb.PlanningCommand{
		Body: &pb.PlanningCommand_BuildStructure{
			BuildStructure: &pb.MsgBuildStructure{NodeId: "B2", BuildingTypeId: "farm", CityId: "A2"},
		},
	}); err != nil {
		t.Fatalf("InjectPlanningCommand(build farm) error = %v", err)
	}
	if err := h.SubmitTurn("player-1"); err != nil {
		t.Fatalf("SubmitTurn(turn=2) error = %v", err)
	}

	turn2, err := h.WaitGameSync("player-1", 2, 3*time.Second)
	if err != nil {
		t.Fatalf("WaitGameSync(turn=2) error = %v", err)
	}
	if !hasTurnEvent(turn2.GameSync, "economy", "building_built") {
		t.Fatalf("turn 2 missing building_built event")
	}
	if farmNode := gameSyncNodeView(t, turn2.GameSync, "B2"); farmNode.GetBuildingTypeId() != "farm" {
		t.Fatalf("turn 2 farm node = %#v, want farm at B2", farmNode)
	}
	settlerID := findOwnedUnitIDByType(t, h.room.State(), "player-1", "settler")
	clearSelectedRecipe(t, h.room.State(), "A2")

	if _, err := h.WaitPlanningStart("player-1", 3, 2*time.Second); err != nil {
		t.Fatalf("WaitPlanningStart(turn=3) error = %v", err)
	}
	if err := h.InjectPlanningCommand("player-1", "req-settle", &pb.PlanningCommand{
		Body: &pb.PlanningCommand_IssueUnitOrder{
			IssueUnitOrder: &pb.MsgIssueUnitOrder{
				UnitId:       settlerID,
				Action:       "settle_city",
				TargetNodeId: "D2",
			},
		},
	}); err != nil {
		t.Fatalf("InjectPlanningCommand(settle city) error = %v", err)
	}
	if err := h.SubmitTurn("player-1"); err != nil {
		t.Fatalf("SubmitTurn(turn=3) error = %v", err)
	}

	turn3, err := h.WaitGameSync("player-1", 3, 3*time.Second)
	if err != nil {
		t.Fatalf("WaitGameSync(turn=3) error = %v", err)
	}
	if !hasTurnEvent(turn3.GameSync, "map", "city_founded") {
		t.Fatalf("turn 3 missing city_founded event")
	}
	if founded := gameSyncNodeView(t, turn3.GameSync, "D2"); founded.GetBuildingTypeId() != "city_core" || founded.GetBuildingStatus() != "disabled" || founded.GetCityId() != "D2" {
		t.Fatalf("turn 3 city core node = %#v, want disabled city_core at D2", founded)
	}

	if _, err := h.WaitPlanningStart("player-1", 4, 2*time.Second); err != nil {
		t.Fatalf("WaitPlanningStart(turn=4) error = %v", err)
	}
	if err := h.InjectPlanningCommand("player-1", "req-build-barracks", &pb.PlanningCommand{
		Body: &pb.PlanningCommand_BuildStructure{
			BuildStructure: &pb.MsgBuildStructure{NodeId: "E2", BuildingTypeId: "barracks", CityId: "D2"},
		},
	}); err != nil {
		t.Fatalf("InjectPlanningCommand(build barracks) error = %v", err)
	}
	if err := h.SubmitTurn("player-1"); err != nil {
		t.Fatalf("SubmitTurn(turn=4) error = %v", err)
	}

	turn4, err := h.WaitGameSync("player-1", 4, 3*time.Second)
	if err != nil {
		t.Fatalf("WaitGameSync(turn=4) error = %v", err)
	}
	if !hasTurnEvent(turn4.GameSync, "economy", "building_built") {
		t.Fatalf("turn 4 missing building_built event")
	}
	if barracks := gameSyncNodeView(t, turn4.GameSync, "E2"); barracks.GetBuildingTypeId() != "barracks" || barracks.GetBuildingStatus() != "disabled" || barracks.GetCityId() != "D2" {
		t.Fatalf("turn 4 barracks node = %#v, want disabled barracks at E2 bound to D2", barracks)
	}

	if _, err := h.WaitPlanningStart("player-1", 5, 2*time.Second); err != nil {
		t.Fatalf("WaitPlanningStart(turn=5) error = %v", err)
	}
	if err := h.SubmitTurn("player-1"); err != nil {
		t.Fatalf("SubmitTurn(turn=5) error = %v", err)
	}

	turn5, err := h.WaitGameSync("player-1", 5, 3*time.Second)
	if err != nil {
		t.Fatalf("WaitGameSync(turn=5) error = %v", err)
	}
	if !hasTurnEvent(turn5.GameSync, "economy", "recipe_progressed") {
		t.Fatalf("turn 5 missing recipe_progressed event")
	}

	if _, err := h.WaitPlanningStart("player-1", 6, 2*time.Second); err != nil {
		t.Fatalf("WaitPlanningStart(turn=6) error = %v", err)
	}
	if err := h.SubmitTurn("player-1"); err != nil {
		t.Fatalf("SubmitTurn(turn=6) error = %v", err)
	}

	turn6, err := h.WaitGameSync("player-1", 6, 3*time.Second)
	if err != nil {
		t.Fatalf("WaitGameSync(turn=6) error = %v", err)
	}
	if !hasTurnEvent(turn6.GameSync, "economy", "recipe_completed") {
		t.Fatalf("turn 6 missing recipe_completed event")
	}
	if unitID := findOwnedUnitIDByTypeIfExists(h.room.State(), "player-1", "infantry"); unitID != "" {
		t.Fatalf("disconnected new city produced infantry %q without reachable ore", unitID)
	}

	start7, err := h.WaitPlanningStart("player-1", 7, 2*time.Second)
	if err != nil {
		t.Fatalf("WaitPlanningStart(turn=7) error = %v", err)
	}
	if start7.GetInformationReport() == nil {
		t.Fatalf("turn 7 planning start missing information report")
	}
	event.RoadBuiltEvent{FromNode: "D2", ToNode: "E2", Owner: "player-1"}.Apply(h.room.State().World, h.room.State())
	raider := h.room.State().World.Entry(ecs.CreateUnit(h.room.State().World, "raider", "player-1", domain.Position{Q: 3, R: 1}))
	ecs.UnitStatsC.Get(raider).ID = "preflight-raider-1"
	if err := h.InjectPlanningCommand("player-1", "req-destroy-road", &pb.PlanningCommand{
		Body: &pb.PlanningCommand_IssueUnitOrder{
			IssueUnitOrder: &pb.MsgIssueUnitOrder{
				UnitId:          "preflight-raider-1",
				Action:          "destroy_road",
				TargetNodeId:    "D2",
				SecondaryNodeId: "E2",
			},
		},
	}); err != nil {
		t.Fatalf("InjectPlanningCommand(destroy road) error = %v", err)
	}
	if err := h.SubmitTurn("player-1"); err != nil {
		t.Fatalf("SubmitTurn(turn=7) error = %v", err)
	}

	turn7, err := h.WaitGameSync("player-1", 7, 3*time.Second)
	if err != nil {
		t.Fatalf("WaitGameSync(turn=7) error = %v", err)
	}
	if !hasTurnEvent(turn7.GameSync, "map", "road_destroyed") {
		t.Fatalf("turn 7 missing road_destroyed event")
	}
}

func TestHarnessRealContentFacilityTakeover_TransfersOwnershipAndReactivates(t *testing.T) {
	def := newRealContentFacilityTakeoverDefinition(t)

	h, err := NewHarness(def)
	if err != nil {
		t.Fatalf("NewHarness() error = %v", err)
	}
	if err := h.Start(); err != nil {
		t.Fatalf("Start() error = %v", err)
	}
	player2 := h.room.State().Players["player-2"]
	player2.Resources = domain.NewResourceBag()
	player2.Resources.Set(domain.ResourceFood, 3)

	for turn := 1; turn <= 3; turn++ {
		if _, err := h.WaitPlanningStart("player-1", turn, 2*time.Second); err != nil {
			t.Fatalf("WaitPlanningStart(turn=%d) error = %v", turn, err)
		}
		if err := h.SubmitTurn("player-1"); err != nil {
			t.Fatalf("SubmitTurn(turn=%d) error = %v", turn, err)
		}

		record, err := h.WaitGameSync("player-1", turn, 3*time.Second)
		if err != nil {
			t.Fatalf("WaitGameSync(turn=%d) error = %v", turn, err)
		}

		switch turn {
		case 1:
			if !hasTurnEvent(record.GameSync, "economy", "facility_takeover_progressed") {
				t.Fatalf("turn 1 missing facility_takeover_progressed event")
			}
			building := record.Summary.Buildings["C2"]
			if building.Owner != "player-1" || !building.Disabled {
				t.Fatalf("turn 1 building summary = %#v, want player-1 disabled", building)
			}
			node := gameSyncNodeView(t, record.GameSync, "C2")
			if node.GetControllerPlayerId() != "player-2" || node.GetBuildingStatus() != "takeover" {
				t.Fatalf("turn 1 node view = %#v, want player-2 takeover", node)
			}
		case 2:
			if !hasTurnEvent(record.GameSync, "economy", "facility_takeover_completed") {
				t.Fatalf("turn 2 missing facility_takeover_completed event")
			}
			nodeView := gameSyncNodeView(t, record.GameSync, "C2")
			if nodeView.GetControllerPlayerId() != "player-2" || !nodeView.GetIsMemory() || nodeView.GetIsCurrentlyVisible() {
				t.Fatalf("turn 2 node view = %#v, want remembered player-2 takeover state", nodeView)
			}
			nodeEntry, ok := h.room.State().GetNode("C2")
			if !ok {
				t.Fatalf("missing node C2 after takeover")
			}
			runtimeNode := ecs.NodeC.Get(nodeEntry)
			if runtimeNode.Owner != "player-2" || runtimeNode.TerritoryOwner != "player-2" {
				t.Fatalf("turn 2 node owner = %#v, want player-2", runtimeNode)
			}
		case 3:
			building := record.Summary.Buildings["C2"]
			if building.Owner != "player-2" || building.CityID != "E2" || building.Disabled {
				t.Fatalf("turn 3 building summary = %#v, want active player-2/E2 farm", building)
			}
			if got := record.Summary.Players["player-2"].Resources["food"]; got != 2 {
				t.Fatalf("turn 3 player-2 food = %d, want 2 after reactivated farm output", got)
			}
		}
	}
}

func hasTurnEvent(msg *pb.MsgGameSync, channel string, eventType string) bool {
	if msg == nil {
		return false
	}
	for _, evt := range msg.GetEvents() {
		if evt.GetChannel() != channel {
			continue
		}
		if evt.GetKind() == eventType {
			return true
		}
	}
	return false
}

func hasPlanningStartEvent(msg *pb.MsgPlanningStart, eventType string) bool {
	if msg == nil {
		return false
	}
	for _, evt := range msg.GetPlanningStartEvents() {
		if evt.GetKind() == eventType {
			return true
		}
	}
	return false
}

func assertM9PreflightPlanningStart(t *testing.T, start *pb.MsgPlanningStart) {
	t.Helper()
	if start == nil {
		t.Fatalf("planning start = nil")
	}
	report := start.GetInformationReport()
	if report == nil {
		t.Fatalf("planning start missing information report")
	}
	if report.GetMode() == "" || report.GetConfidence() == "" {
		t.Fatalf("information report = %#v, want mode and confidence", report)
	}
	if report.GetVisibleNodeCount() == 0 {
		t.Fatalf("information report visible_node_count = 0, want visible map context")
	}
	if len(start.GetMinisterDrafts()) == 0 {
		t.Fatalf("planning start should include minister default drafts")
	}
	snapshot := start.GetSnapshot()
	if snapshot == nil {
		t.Fatalf("planning start snapshot = nil")
	}
}
