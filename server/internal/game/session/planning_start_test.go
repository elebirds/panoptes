package session

import (
	"encoding/json"
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	gamequery "github.com/elebirds/panoptes/internal/game/query"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func TestBuildPlanningStartMessageFromObservationUsesPerPlayerVisibility(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TurnTimeLimitPlanning:      30,
			TokensPerTurn:              3,
			SafeZoneRadius:             2,
			CityCoreMaxHP:              100,
			BaseResearchOutputPerTurn:  1,
			BaseIndustryOutputPerTurn:  2,
			FacilityTakeoverTurns:      2,
			InitialCityTerritoryRadius: 1,
		},
		Units: []staticdata.UnitDefinition{
			{ID: "infantry", Class: "melee", MaxHP: 30, Attack: 10, AttackRange: 1, MoveRange: 2, VisionRange: 1, TrainCost: staticdata.ResourceAmounts{}, Upkeep: staticdata.ResourceAmounts{}},
		},
	}))

	world := donburi.NewWorld()
	mapData := &domain.MapData{
		ID:           "planning-fog",
		Width:        3,
		Height:       1,
		PlayerSpawns: map[string]domain.Position{"player-1": {Q: 0, R: 0}, "player-2": {Q: 2, R: 0}},
		NodeIndex:    map[string]donburi.Entity{},
	}
	createPlanningStartNode(world, mapData, "N0", 0, 0)
	createPlanningStartNode(world, mapData, "N1", 1, 0)
	createPlanningStartNode(world, mapData, "N2", 2, 0)

	state := domain.NewGameState("game-1", []string{"player-1", "player-2"}, []string{"alice", "bob"}, mapData)
	state.World = world
	state.Turn = 2
	state.Phase = domain.PhasePlanning.String()

	allyEntry := world.Entry(ecs.CreateUnit(world, "infantry", "player-1", domain.Position{Q: 0, R: 0}))
	enemyEntry := world.Entry(ecs.CreateUnit(world, "infantry", "player-2", domain.Position{Q: 2, R: 0}))
	ecs.UnitStatsC.Get(allyEntry).ID = "ally-1"
	ecs.UnitStatsC.Get(enemyEntry).ID = "enemy-1"

	store := gamequery.NewObservationStore()
	left := BuildPlanningStartMessageFromObservation(state, store.BuildObservation(state, "player-1"), domain.PhasePlanning.String(), nil)
	right := BuildPlanningStartMessageFromObservation(state, store.BuildObservation(state, "player-2"), domain.PhasePlanning.String(), nil)

	if hasPlanningStartUnit(left, "enemy-1") {
		t.Fatalf("player-1 planning_start should hide enemy-1")
	}
	if !hasPlanningStartUnit(left, "ally-1") {
		t.Fatalf("player-1 planning_start should include ally-1")
	}
	if hasPlanningStartUnit(right, "ally-1") {
		t.Fatalf("player-2 planning_start should hide ally-1")
	}
	if !hasPlanningStartUnit(right, "enemy-1") {
		t.Fatalf("player-2 planning_start should include enemy-1")
	}
	report := left.GetInformationReport()
	if report == nil {
		t.Fatalf("player-1 planning_start information_report = nil")
	}
	if report.GetMode() != gamequery.ReportingModeStandard {
		t.Fatalf("player-1 planning_start information_report.mode = %q, want %q", report.GetMode(), gamequery.ReportingModeStandard)
	}
	if report.GetUnknownNodeCount() == 0 {
		t.Fatalf("player-1 planning_start information_report should track at least one unknown node")
	}
}

func TestBuildPlanningStartMessageFromObservationIncludesMinisterDrafts(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TurnTimeLimitPlanning:      30,
			TokensPerTurn:              3,
			CityCoreMaxHP:              100,
			BaseResearchOutputPerTurn:  1,
			BaseIndustryOutputPerTurn:  2,
			FacilityTakeoverTurns:      2,
			InitialCityTerritoryRadius: 1,
		},
	}))

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "planning-start"})
	state.Turn = 2
	state.Phase = domain.PhasePlanning.String()
	state.TurnRuntime.Planning.SetMinisterDrafts("player-1", []domain.MinisterDraft{
		{
			DraftID:      "draft-policy-1",
			PlayerID:     "player-1",
			MinisterRole: "domestic",
			Kind:         domain.MinisterDraftKindPolicy,
			TargetID:     "expansion",
			TargetLabel:  "Expansion",
			Title:        "建议转向扩张国策",
			Status:       domain.MinisterDraftStatusPending,
			Available:    true,
			Turn:         2,
			Source:       domain.MinisterDraftSourceRuleOnly,
		},
	})

	msg := BuildPlanningStartMessageFromObservation(state, nil, domain.PhasePlanning.String(), nil)
	if msg == nil {
		t.Fatalf("BuildPlanningStartMessageFromObservation() = nil")
	}
	if got := len(msg.GetMinisterDrafts()); got != 1 {
		t.Fatalf("planning_start minister draft count = %d, want 1", got)
	}
	if got := len(msg.GetSnapshot().GetMinisterDrafts()); got != 1 {
		t.Fatalf("snapshot minister draft count = %d, want 1", got)
	}
	var payload map[string]any
	if err := json.Unmarshal([]byte(msg.GetMinisterDrafts()[0].GetJsonPayload()), &payload); err != nil {
		t.Fatalf("unmarshal planning_start minister draft payload: %v", err)
	}
	if payload["draft_id"] != "draft-policy-1" {
		t.Fatalf("planning_start draft_id = %#v, want draft-policy-1", payload["draft_id"])
	}
}

func createPlanningStartNode(world donburi.World, mapData *domain.MapData, nodeID string, x int, y int) {
	entity := ecs.CreateNode(world, ecs.MapNode{ID: nodeID, Q: x, R: y, Terrain: "plain"})
	mapData.NodeIndex[nodeID] = entity
}

func hasPlanningStartUnit(msg interface{ GetUnits() []*pb.UnitView }, unitID string) bool {
	for _, unit := range msg.GetUnits() {
		if unit != nil && unit.GetId() == unitID {
			return true
		}
	}
	return false
}
