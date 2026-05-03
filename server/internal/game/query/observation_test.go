package query

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func TestObservationStoreTracksVisibleNodesMemoryAndHiddenUnits(t *testing.T) {
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
		Buildings: []staticdata.BuildingDefinition{
			{ID: "farm", BuildingScope: "out_of_city", MaxHP: 60, TakeoverMode: "delayed"},
		},
	}))

	world := donburi.NewWorld()
	mapData := &domain.MapData{
		ID:           "fog",
		Width:        4,
		Height:       1,
		PlayerSpawns: map[string]domain.Position{"player-1": {Q: 0, R: 0}, "player-2": {Q: 3, R: 0}},
		NodeIndex:    map[string]donburi.Entity{},
	}
	n0 := createNodeForViewTest(world, mapData, "N0", 0, 0)
	n1 := createNodeForViewTest(world, mapData, "N1", 1, 0)
	n2 := createNodeForViewTest(world, mapData, "N2", 2, 0)
	_ = createNodeForViewTest(world, mapData, "N3", 3, 0)

	ecs.NodeC.Get(n0).Owner = "player-1"
	ecs.NodeC.Get(n0).TerritoryOwner = "player-1"
	ecs.NodeC.Get(n1).Owner = ""
	ecs.NodeC.Get(n1).TerritoryOwner = ""
	ecs.NodeC.Get(n2).Owner = "player-2"
	ecs.NodeC.Get(n2).TerritoryOwner = "player-2"

	state := domain.NewGameState("game-1", []string{"player-1", "player-2"}, []string{"alice", "bob"}, mapData)
	state.World = world
	state.Turn = 1

	allyEntry := world.Entry(ecs.CreateUnit(world, "infantry", "player-1", domain.Position{Q: 1, R: 0}))
	enemyEntry := world.Entry(ecs.CreateUnit(world, "infantry", "player-2", domain.Position{Q: 2, R: 0}))
	ecs.UnitStatsC.Get(allyEntry).ID = "ally-1"
	ecs.UnitStatsC.Get(enemyEntry).ID = "enemy-1"
	ecs.CreateBuilding(world, "farm", "player-2", "N2", n2)

	store := NewObservationStore()

	first := store.BuildObservation(state, "player-1")
	if first == nil {
		t.Fatalf("first observation = nil")
	}
	seenNode := observationNodeByID(first, "N2")
	if seenNode == nil {
		t.Fatalf("first observation missing node N2")
	}
	if !seenNode.GetIsCurrentlyVisible() || seenNode.GetIsMemory() {
		t.Fatalf("first node visibility = (%v,%v), want (true,false)", seenNode.GetIsCurrentlyVisible(), seenNode.GetIsMemory())
	}
	if got := seenNode.GetLastObservedTurn(); got != 1 {
		t.Fatalf("first last_observed_turn = %d, want 1", got)
	}
	if got := seenNode.GetBuildingTypeId(); got != "farm" {
		t.Fatalf("first building_type_id = %q, want farm", got)
	}
	if !observationHasVisibleUnit(first, "enemy-1") {
		t.Fatalf("first observation should contain visible enemy unit")
	}

	state.Turn = 2
	pos := ecs.PositionC.Get(allyEntry)
	pos.Q = 0

	second := store.BuildObservation(state, "player-1")
	if second == nil {
		t.Fatalf("second observation = nil")
	}
	remembered := observationNodeByID(second, "N2")
	if remembered == nil {
		t.Fatalf("second observation missing node N2")
	}
	if remembered.GetIsCurrentlyVisible() || !remembered.GetIsMemory() {
		t.Fatalf("second node visibility = (%v,%v), want (false,true)", remembered.GetIsCurrentlyVisible(), remembered.GetIsMemory())
	}
	if got := remembered.GetLastObservedTurn(); got != 1 {
		t.Fatalf("second last_observed_turn = %d, want 1", got)
	}
	if got := remembered.GetBuildingTypeId(); got != "farm" {
		t.Fatalf("second building_type_id = %q, want remembered farm", got)
	}
	unknown := observationNodeByID(second, "N3")
	if unknown == nil {
		t.Fatalf("second observation missing node N3")
	}
	if unknown.GetIsCurrentlyVisible() || unknown.GetIsMemory() || unknown.GetLastObservedTurn() != 0 {
		t.Fatalf("unknown node state = (%v,%v,%d), want (false,false,0)", unknown.GetIsCurrentlyVisible(), unknown.GetIsMemory(), unknown.GetLastObservedTurn())
	}
	if observationHasVisibleUnit(second, "enemy-1") {
		t.Fatalf("second observation should hide enemy unit outside vision")
	}
	memoryUnit := observationMemoryUnitByID(second, "enemy-1")
	if memoryUnit == nil {
		t.Fatalf("second observation should retain hidden enemy in memory")
	}
	if got := memoryUnit.GetLastObservedTurn(); got != 1 {
		t.Fatalf("memory unit last_observed_turn = %d, want 1", got)
	}
	if got := memoryUnit.GetView().GetPos().GetQ(); got != 2 {
		t.Fatalf("memory unit x = %d, want 2", got)
	}
}

func TestObservationStoreBuildsDifferentPerPlayerViews(t *testing.T) {
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
		ID:           "fog",
		Width:        4,
		Height:       1,
		PlayerSpawns: map[string]domain.Position{"player-1": {Q: 0, R: 0}, "player-2": {Q: 3, R: 0}},
		NodeIndex:    map[string]donburi.Entity{},
	}
	_ = createNodeForViewTest(world, mapData, "N0", 0, 0)
	_ = createNodeForViewTest(world, mapData, "N1", 1, 0)
	_ = createNodeForViewTest(world, mapData, "N2", 2, 0)
	_ = createNodeForViewTest(world, mapData, "N3", 3, 0)

	state := domain.NewGameState("game-2", []string{"player-1", "player-2"}, []string{"alice", "bob"}, mapData)
	state.World = world
	state.Turn = 1

	allyEntry := world.Entry(ecs.CreateUnit(world, "infantry", "player-1", domain.Position{Q: 0, R: 0}))
	enemyEntry := world.Entry(ecs.CreateUnit(world, "infantry", "player-2", domain.Position{Q: 2, R: 0}))
	ecs.UnitStatsC.Get(allyEntry).ID = "ally-1"
	ecs.UnitStatsC.Get(enemyEntry).ID = "enemy-1"

	store := NewObservationStore()
	left := store.BuildObservation(state, "player-1")
	right := store.BuildObservation(state, "player-2")

	if observationHasVisibleUnit(left, "enemy-1") {
		t.Fatalf("player-1 should not see enemy-1 at distance 2")
	}
	if !observationHasVisibleUnit(left, "ally-1") {
		t.Fatalf("player-1 should see ally-1")
	}
	if observationHasVisibleUnit(right, "ally-1") {
		t.Fatalf("player-2 should not see ally-1 at distance 2")
	}
	if !observationHasVisibleUnit(right, "enemy-1") {
		t.Fatalf("player-2 should see enemy-1")
	}
}

func TestObservationStoreOmniscientViewerSeesWholeMap(t *testing.T) {
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
		ID:           "omniscient",
		Width:        4,
		Height:       1,
		PlayerSpawns: map[string]domain.Position{"player-1": {Q: 0, R: 0}, "player-2": {Q: 3, R: 0}},
		NodeIndex:    map[string]donburi.Entity{},
	}
	_ = createNodeForViewTest(world, mapData, "N0", 0, 0)
	_ = createNodeForViewTest(world, mapData, "N1", 1, 0)
	_ = createNodeForViewTest(world, mapData, "N2", 2, 0)
	_ = createNodeForViewTest(world, mapData, "N3", 3, 0)

	state := domain.NewGameState("game-3", []string{"player-1", "player-2"}, []string{"alice", "bob"}, mapData)
	state.World = world
	state.Turn = 1

	allyEntry := world.Entry(ecs.CreateUnit(world, "infantry", "player-1", domain.Position{Q: 0, R: 0}))
	enemyEntry := world.Entry(ecs.CreateUnit(world, "infantry", "player-2", domain.Position{Q: 3, R: 0}))
	ecs.UnitStatsC.Get(allyEntry).ID = "ally-1"
	ecs.UnitStatsC.Get(enemyEntry).ID = "enemy-1"

	store := NewObservationStore()

	normal := store.BuildObservation(state, "player-1")
	if normal == nil {
		t.Fatalf("normal observation = nil")
	}
	if observationHasVisibleUnit(normal, "enemy-1") {
		t.Fatalf("normal observation should hide distant enemy unit")
	}

	store.SetOmniscient("player-1", true)
	fullMap := store.BuildObservation(state, "player-1")
	if fullMap == nil {
		t.Fatalf("full-map observation = nil")
	}
	if len(fullMap.VisibleNodes) != 4 {
		t.Fatalf("visible nodes = %d, want 4", len(fullMap.VisibleNodes))
	}
	if len(fullMap.MemoryNodes) != 0 {
		t.Fatalf("memory nodes = %d, want 0", len(fullMap.MemoryNodes))
	}
	if !observationHasVisibleUnit(fullMap, "enemy-1") {
		t.Fatalf("full-map observation should expose enemy unit")
	}
	if node := observationNodeByID(fullMap, "N3"); node == nil || !node.GetIsCurrentlyVisible() || node.GetIsMemory() {
		t.Fatalf("node N3 should be currently visible under omniscient mode: %#v", node)
	}
}

func TestBuildInformationReportTracksStandardMemoryAndUnknowns(t *testing.T) {
	store, state, allyEntry := buildReportingScenario(t)
	first := store.BuildObservation(state, "player-1")
	if first == nil {
		t.Fatalf("first observation = nil")
	}

	state.Turn = 2
	pos := ecs.PositionC.Get(allyEntry)
	pos.Q = 0
	observation := store.BuildObservation(state, "player-1")

	report := BuildInformationReport(observation)
	if report.GetMode() != ReportingModeStandard {
		t.Fatalf("mode = %q, want %q", report.GetMode(), ReportingModeStandard)
	}
	if report.GetConfidence() != "medium" {
		t.Fatalf("confidence = %q, want medium", report.GetConfidence())
	}
	if report.GetMemoryNodeCount() != 1 {
		t.Fatalf("memory_node_count = %d, want 1", report.GetMemoryNodeCount())
	}
	if report.GetMemoryUnitCount() != 1 {
		t.Fatalf("memory_unit_count = %d, want 1", report.GetMemoryUnitCount())
	}
	if report.GetUnknownNodeCount() != 1 {
		t.Fatalf("unknown_node_count = %d, want 1", report.GetUnknownNodeCount())
	}
	if report.GetDelayedCount() != 2 {
		t.Fatalf("delayed_count = %d, want 2", report.GetDelayedCount())
	}
	if report.GetOmittedCount() != 1 {
		t.Fatalf("omitted_count = %d, want 1", report.GetOmittedCount())
	}
	if report.GetMisreadCount() != 0 {
		t.Fatalf("misread_count = %d, want 0", report.GetMisreadCount())
	}
}

func TestBuildInformationReportSupportsHighDistortionMode(t *testing.T) {
	store, state, allyEntry := buildReportingScenario(t)
	store.SetReportingMode("player-1", ReportingModeHighDistortion)
	_ = store.BuildObservation(state, "player-1")

	state.Turn = 2
	pos := ecs.PositionC.Get(allyEntry)
	pos.Q = 0
	observation := store.BuildObservation(state, "player-1")

	report := BuildInformationReport(observation)
	if report.GetMode() != ReportingModeHighDistortion {
		t.Fatalf("mode = %q, want %q", report.GetMode(), ReportingModeHighDistortion)
	}
	if report.GetConfidence() != "low" {
		t.Fatalf("confidence = %q, want low", report.GetConfidence())
	}
	if report.GetDelayedCount() != 2 {
		t.Fatalf("delayed_count = %d, want 2", report.GetDelayedCount())
	}
	if report.GetOmittedCount() < 1 {
		t.Fatalf("omitted_count = %d, want at least 1", report.GetOmittedCount())
	}
	if report.GetMisreadCount() == 0 {
		t.Fatalf("misread_count = 0, want non-zero distortion")
	}
}

func TestBuildInformationReportUsesClearModeForOmniscientDirectInspection(t *testing.T) {
	store, state, _ := buildReportingScenario(t)
	store.SetReportingMode("player-1", ReportingModeHighDistortion)
	store.SetOmniscient("player-1", true)

	observation := store.BuildObservation(state, "player-1")
	report := BuildInformationReport(observation)
	if report.GetMode() != ReportingModeClear {
		t.Fatalf("mode = %q, want %q", report.GetMode(), ReportingModeClear)
	}
	if report.GetConfidence() != "clear" {
		t.Fatalf("confidence = %q, want clear", report.GetConfidence())
	}
	if !report.GetDirectInspection() {
		t.Fatalf("direct_inspection = false, want true")
	}
	if report.GetOmittedCount() != 0 || report.GetDelayedCount() != 0 || report.GetMisreadCount() != 0 {
		t.Fatalf("clear report distortion = omitted %d delayed %d misread %d, want all zero", report.GetOmittedCount(), report.GetDelayedCount(), report.GetMisreadCount())
	}
	if report.GetVisibleNodeCount() != 4 {
		t.Fatalf("visible_node_count = %d, want 4", report.GetVisibleNodeCount())
	}
}

func buildReportingScenario(t *testing.T) (*ObservationStore, *domain.GameState, *donburi.Entry) {
	t.Helper()
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
		Buildings: []staticdata.BuildingDefinition{
			{ID: "farm", BuildingScope: "out_of_city", MaxHP: 60, TakeoverMode: "delayed"},
		},
	}))

	world := donburi.NewWorld()
	mapData := &domain.MapData{
		ID:           "reporting",
		Width:        4,
		Height:       1,
		PlayerSpawns: map[string]domain.Position{"player-1": {Q: 0, R: 0}, "player-2": {Q: 3, R: 0}},
		NodeIndex:    map[string]donburi.Entity{},
	}
	n0 := createNodeForViewTest(world, mapData, "N0", 0, 0)
	_ = createNodeForViewTest(world, mapData, "N1", 1, 0)
	n2 := createNodeForViewTest(world, mapData, "N2", 2, 0)
	_ = createNodeForViewTest(world, mapData, "N3", 3, 0)
	ecs.NodeC.Get(n0).Owner = "player-1"
	ecs.NodeC.Get(n0).TerritoryOwner = "player-1"
	ecs.NodeC.Get(n2).Owner = "player-2"
	ecs.NodeC.Get(n2).TerritoryOwner = "player-2"

	state := domain.NewGameState("game-reporting", []string{"player-1", "player-2"}, []string{"alice", "bob"}, mapData)
	state.World = world
	state.Turn = 1

	allyEntry := world.Entry(ecs.CreateUnit(world, "infantry", "player-1", domain.Position{Q: 1, R: 0}))
	enemyEntry := world.Entry(ecs.CreateUnit(world, "infantry", "player-2", domain.Position{Q: 2, R: 0}))
	ecs.UnitStatsC.Get(allyEntry).ID = "ally-1"
	ecs.UnitStatsC.Get(enemyEntry).ID = "enemy-1"
	ecs.CreateBuilding(world, "farm", "player-2", "N2", n2)

	return NewObservationStore(), state, allyEntry
}

func observationNodeByID(observation *ObservationSnapshot, nodeID string) *NodeObservationView {
	if observation == nil {
		return nil
	}
	for _, node := range observation.Nodes {
		if node == nil {
			continue
		}
		if node.GetId() == nodeID {
			return node
		}
	}
	return nil
}

func observationHasVisibleUnit(observation *ObservationSnapshot, unitID string) bool {
	if observation == nil {
		return false
	}
	for _, unit := range observation.Units {
		if unit == nil {
			continue
		}
		if unit.GetId() == unitID {
			return true
		}
	}
	return false
}

func observationMemoryUnitByID(observation *ObservationSnapshot, unitID string) *RememberedUnitView {
	if observation == nil {
		return nil
	}
	for _, unit := range observation.MemoryUnits {
		if unit == nil || unit.View == nil {
			continue
		}
		if unit.View.GetId() == unitID {
			return unit
		}
	}
	return nil
}
