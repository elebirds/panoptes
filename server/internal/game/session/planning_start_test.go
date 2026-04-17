package session

import (
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
		PlayerSpawns: map[string]domain.Position{"player-1": {X: 0, Y: 0}, "player-2": {X: 2, Y: 0}},
		NodeIndex:    map[string]donburi.Entity{},
	}
	createPlanningStartNode(world, mapData, "N0", 0, 0)
	createPlanningStartNode(world, mapData, "N1", 1, 0)
	createPlanningStartNode(world, mapData, "N2", 2, 0)

	state := domain.NewGameState("game-1", []string{"player-1", "player-2"}, []string{"alice", "bob"}, mapData)
	state.World = world
	state.Turn = 2
	state.Phase = domain.PhasePlanning.String()

	allyEntry := world.Entry(ecs.CreateUnit(world, "infantry", "player-1", domain.Position{X: 0, Y: 0}))
	enemyEntry := world.Entry(ecs.CreateUnit(world, "infantry", "player-2", domain.Position{X: 2, Y: 0}))
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
}

func createPlanningStartNode(world donburi.World, mapData *domain.MapData, nodeID string, x int, y int) {
	entity := ecs.CreateNode(world, ecs.MapNode{ID: nodeID, X: x, Y: y, Terrain: "plain"})
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
