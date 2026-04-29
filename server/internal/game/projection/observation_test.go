package projection

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	gamequery "github.com/elebirds/panoptes/internal/game/query"
	gameresolution "github.com/elebirds/panoptes/internal/game/resolution"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func TestProjectGameSyncFromObservationUsesPerPlayerVisibility(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
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
		ID:           "settlement-fog",
		Width:        3,
		Height:       1,
		PlayerSpawns: map[string]domain.Position{"player-1": {Q: 0, R: 0}, "player-2": {Q: 2, R: 0}},
		NodeIndex:    map[string]donburi.Entity{},
	}
	createSettlementNode(world, mapData, "N0", 0, 0)
	createSettlementNode(world, mapData, "N1", 1, 0)
	createSettlementNode(world, mapData, "N2", 2, 0)

	state := domain.NewGameState("game-1", []string{"player-1", "player-2"}, []string{"alice", "bob"}, mapData)
	state.World = world
	state.Turn = 4

	allyEntry := world.Entry(ecs.CreateUnit(world, "infantry", "player-1", domain.Position{Q: 0, R: 0}))
	enemyEntry := world.Entry(ecs.CreateUnit(world, "infantry", "player-2", domain.Position{Q: 2, R: 0}))
	ecs.UnitStatsC.Get(allyEntry).ID = "ally-1"
	ecs.UnitStatsC.Get(enemyEntry).ID = "enemy-1"

	store := gamequery.NewObservationStore()
	left := ProjectGameSyncFromObservation(state, store.BuildObservation(state, "player-1"), int32(state.Turn), domain.PhaseResolving.String(), domain.PhasePlanning.String(), gameresolution.NewCollector())
	right := ProjectGameSyncFromObservation(state, store.BuildObservation(state, "player-2"), int32(state.Turn), domain.PhaseResolving.String(), domain.PhasePlanning.String(), gameresolution.NewCollector())

	if settlementHasUnit(left, "enemy-1") {
		t.Fatalf("player-1 game sync should hide enemy-1")
	}
	if !settlementHasUnit(left, "ally-1") {
		t.Fatalf("player-1 game sync should include ally-1")
	}
	if settlementHasUnit(right, "ally-1") {
		t.Fatalf("player-2 game sync should hide ally-1")
	}
	if !settlementHasUnit(right, "enemy-1") {
		t.Fatalf("player-2 game sync should include enemy-1")
	}
}

func createSettlementNode(world donburi.World, mapData *domain.MapData, nodeID string, x int, y int) {
	entity := ecs.CreateNode(world, ecs.MapNode{ID: nodeID, Q: x, R: y, Terrain: "plain"})
	mapData.NodeIndex[nodeID] = entity
}

func settlementHasUnit(msg interface{ GetUnits() []*pb.UnitView }, unitID string) bool {
	for _, unit := range msg.GetUnits() {
		if unit != nil && unit.GetId() == unitID {
			return true
		}
	}
	return false
}
