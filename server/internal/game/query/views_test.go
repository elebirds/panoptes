package query

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func TestBuildPlayerViewUsesCurrentResearchTargetAndCost(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			SafeZoneRadius:             3,
			CityCoreMaxHP:              100,
			BaseResearchOutputPerTurn:  1,
			BaseIndustryOutputPerTurn:  2,
			FacilityTakeoverTurns:      2,
			InitialCityTerritoryRadius: 1,
		},
		Technologies: []staticdata.TechnologyDefinition{
			{ID: "agrarian_foundations", Branch: "agriculture", Tier: 1, ResearchCost: 4},
			{ID: "civic_institutions", Branch: "governance", Tier: 1, ResearchCost: 1},
		},
		InstitutionCategories: []staticdata.InstitutionCategoryDefinition{
			{ID: "administration", Name: "Administration"},
		},
		Institutions: []staticdata.InstitutionDefinition{
			{ID: "academy_charter", Category: "administration", ActivationTiming: "next_turn"},
		},
	}))

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{
		ID:           "default",
		PlayerSpawns: map[string]domain.Position{"player-1": {Q: 0, R: 0}},
	})
	player := state.Players["player-1"]
	player.Research.SetCurrentTarget("agrarian_foundations")
	player.Research.SetProgress("agrarian_foundations", 2)
	player.Research.ProgressCap = 99
	player.Research.UnlockTechnology("mining")
	player.Research.SetProgress("civic_institutions", 1)
	player.Research.MarkTechnologyCompleted("civic_institutions", 1)
	player.Institutions.SlotCount = 1
	player.Institutions.UnlockCandidate("academy_charter")
	player.Institutions.ActiveInstitutionIDs = []string{"academy_charter"}

	view := BuildPlayerView(state, "player-1")

	if got := view.GetResearch().GetCurrentTargetTechnologyId(); got != "agrarian_foundations" {
		t.Fatalf("current_target_technology_id = %q, want agrarian_foundations", got)
	}
	if got := view.GetResearch().GetRequiredProgress(); got != 4 {
		t.Fatalf("required_progress = %d, want 4", got)
	}
	if got := view.GetResearch().GetCurrentProgress(); got != 2 {
		t.Fatalf("current_progress = %d, want 2", got)
	}
	if got := view.GetResearch().GetCompletedTechnologyIds(); len(got) != 2 || got[0] != "civic_institutions" || got[1] != "mining" {
		t.Fatalf("completed_technology_ids = %#v, want [civic_institutions mining]", got)
	}
	if got := view.GetResearch().GetActiveTechnologyIds(); len(got) != 1 || got[0] != "mining" {
		t.Fatalf("active_technology_ids = %#v, want [mining]", got)
	}
	if got := view.GetResearch().GetPendingActivationTechnologyIds(); len(got) != 1 || got[0] != "civic_institutions" {
		t.Fatalf("pending_activation_technology_ids = %#v, want [civic_institutions]", got)
	}
	if got := view.GetResearch().GetSavedProgress(); len(got) != 1 || got[0].GetTechnologyId() != "civic_institutions" || got[0].GetCurrentProgress() != 1 {
		t.Fatalf("saved_progress = %#v, want civic_institutions progress 1", got)
	}
	if got := view.GetInstitutions().GetSlotCount(); got != 1 {
		t.Fatalf("institution slot_count = %d, want 1", got)
	}
	if got := view.GetInstitutions().GetCandidateInstitutionIds(); len(got) != 1 || got[0] != "academy_charter" {
		t.Fatalf("candidate_institution_ids = %#v, want [academy_charter]", got)
	}
	if got := view.GetInstitutions().GetActiveInstitutionIds(); len(got) != 1 || got[0] != "academy_charter" {
		t.Fatalf("active_institution_ids = %#v, want [academy_charter]", got)
	}
}

func TestBuildNodeViewPopulatesCityServiceStatusAndTakeoverFields(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			SafeZoneRadius:             3,
			CityCoreMaxHP:              100,
			BaseResearchOutputPerTurn:  1,
			BaseIndustryOutputPerTurn:  2,
			FacilityTakeoverTurns:      3,
			InitialCityTerritoryRadius: 1,
		},
		Buildings: []staticdata.BuildingDefinition{
			{
				ID:              "city_core",
				BuildingScope:   "city_core",
				DefaultRecipeID: "train_settler",
				MaxHP:           100,
				TakeoverMode:    "disabled",
			},
			{
				ID:              "barracks",
				BuildingScope:   "in_city",
				DefaultRecipeID: "train_infantry",
				MaxHP:           80,
				TakeoverMode:    "delayed",
			},
			{
				ID:              "farm",
				BuildingScope:   "out_of_city",
				DefaultRecipeID: "grow_food",
				MaxHP:           60,
				TakeoverMode:    "delayed",
			},
		},
		Recipes: []staticdata.RecipeDefinition{
			{ID: "train_settler", BuildingID: "city_core", WorkAmount: 2, BaseProgress: 2},
			{ID: "train_infantry", BuildingID: "barracks", WorkAmount: 3, BaseProgress: 3},
			{ID: "grow_food", BuildingID: "farm", WorkAmount: 4, BaseProgress: 4},
		},
	}))

	world := donburi.NewWorld()
	mapData := &domain.MapData{
		ID:           "default",
		PlayerSpawns: map[string]domain.Position{"player-1": {Q: 0, R: 0}},
		NodeIndex:    map[string]donburi.Entity{},
	}

	cityEntry := createNodeForViewTest(world, mapData, "C1", 0, 0)
	barracksEntry := createNodeForViewTest(world, mapData, "C2", 1, 0)
	farmEntry := createNodeForViewTest(world, mapData, "C3", 2, 0)
	emptyEntry := createNodeForViewTest(world, mapData, "C4", 3, 0)

	for _, entry := range []*donburi.Entry{cityEntry, barracksEntry, farmEntry, emptyEntry} {
		node := ecs.NodeC.Get(entry)
		node.Owner = "player-1"
		node.TerritoryOwner = "player-1"
		node.HasRoad = true
	}
	ecs.NodeC.Get(emptyEntry).HasRoad = false

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, mapData)
	state.World = world
	state.EnsureCityState("player-1", "C1")

	ecs.CreateBuilding(world, "city_core", "player-1", "C1", cityEntry)
	ecs.CreateBuilding(world, "barracks", "player-1", "C1", barracksEntry)
	ecs.CreateBuilding(world, "farm", "player-1", "C1", farmEntry)
	cityEntry.AddComponent(ecs.BuildingOperationC)
	ecs.BuildingOperationC.SetValue(cityEntry, ecs.BuildingOperationComp{
		SelectedRecipeID: "train_settler",
		RequiredTurns:    2,
	})
	barracksEntry.AddComponent(ecs.BuildingOperationC)
	ecs.BuildingOperationC.SetValue(barracksEntry, ecs.BuildingOperationComp{
		SelectedRecipeID: "train_infantry",
		RequiredTurns:    3,
	})
	farmEntry.AddComponent(ecs.BuildingOperationC)
	ecs.BuildingOperationC.SetValue(farmEntry, ecs.BuildingOperationComp{
		SelectedRecipeID: "grow_food",
		ProgressTurns:    1,
		RequiredTurns:    4,
		BlockedReason:    "insufficient_resources",
	})

	cityView := BuildNodeView(state, cityEntry, "player-1")
	if got := cityView.GetCityId(); got != "C1" {
		t.Fatalf("city core city_id = %q, want C1", got)
	}
	if got := cityView.GetServiceCityId(); got != "C1" {
		t.Fatalf("city core service_city_id = %q, want C1", got)
	}
	if !cityView.GetIsCityCore() {
		t.Fatalf("city core is_city_core = false, want true")
	}
	if got := cityView.GetTakeoverRequired(); got != 0 {
		t.Fatalf("city core takeover_required = %d, want 0", got)
	}
	if got := cityView.GetBuildingStatus(); got != "active" {
		t.Fatalf("city core building_status = %q, want active", got)
	}
	if got := cityView.GetOperation().GetBaseProgress(); got != 2 {
		t.Fatalf("city core base_progress = %d, want 2", got)
	}
	if !cityView.GetIsNetworkConnected() || cityView.GetNetworkStatus() != "connected" || cityView.GetNetworkCityId() != "C1" {
		t.Fatalf("city network fields = connected:%v status:%q city:%q, want connected/C1", cityView.GetIsNetworkConnected(), cityView.GetNetworkStatus(), cityView.GetNetworkCityId())
	}
	if got := cityView.GetRoadStatus(); got != "intact" {
		t.Fatalf("city road_status = %q, want intact", got)
	}

	barracksView := BuildNodeView(state, barracksEntry, "player-1")
	if got := barracksView.GetCityId(); got != "C1" {
		t.Fatalf("barracks city_id = %q, want C1", got)
	}
	if got := barracksView.GetServiceCityId(); got != "C1" {
		t.Fatalf("barracks service_city_id = %q, want C1", got)
	}
	if got := barracksView.GetBuildingStatus(); got != "active" {
		t.Fatalf("barracks building_status = %q, want active", got)
	}
	if got := barracksView.GetTakeoverRequired(); got != 3 {
		t.Fatalf("barracks takeover_required = %d, want 3", got)
	}
	if got := barracksView.GetOperation().GetBaseProgress(); got != 3 {
		t.Fatalf("barracks base_progress = %d, want 3", got)
	}
	if !barracksView.GetIsNetworkConnected() || barracksView.GetNetworkCityId() != "C1" {
		t.Fatalf("barracks network fields = connected:%v city:%q, want connected C1", barracksView.GetIsNetworkConnected(), barracksView.GetNetworkCityId())
	}

	farmView := BuildNodeView(state, farmEntry, "player-1")
	if got := farmView.GetCityId(); got != "C1" {
		t.Fatalf("farm city_id = %q, want C1", got)
	}
	if got := farmView.GetServiceCityId(); got != "C1" {
		t.Fatalf("farm service_city_id = %q, want C1", got)
	}
	if got := farmView.GetBuildingStatus(); got != "blocked" {
		t.Fatalf("farm building_status = %q, want blocked", got)
	}
	if got := farmView.GetTakeoverProgress(); got != 0 {
		t.Fatalf("farm takeover_progress = %d, want 0", got)
	}
	if got := farmView.GetTakeoverRequired(); got != 3 {
		t.Fatalf("farm takeover_required = %d, want 3", got)
	}
	if got := farmView.GetOperation().GetBaseProgress(); got != 4 {
		t.Fatalf("farm base_progress = %d, want 4", got)
	}
	if got := farmView.GetOperation().GetBlockedMessage(); got != "生产所需资源不足，本回合无法推进。" {
		t.Fatalf("farm blocked_message = %q, want localized blocked message", got)
	}
	if !farmView.GetIsNetworkConnected() || farmView.GetNetworkStatus() != "connected" || farmView.GetNetworkCityId() != "C1" {
		t.Fatalf("farm network fields = connected:%v status:%q city:%q, want connected/C1", farmView.GetIsNetworkConnected(), farmView.GetNetworkStatus(), farmView.GetNetworkCityId())
	}

	emptyView := BuildNodeView(state, emptyEntry, "player-1")
	if got := emptyView.GetBuildingStatus(); got != "empty" {
		t.Fatalf("empty building_status = %q, want empty", got)
	}
	if got := emptyView.GetCityId(); got != "" {
		t.Fatalf("empty city_id = %q, want empty", got)
	}
	if got := emptyView.GetTakeoverRequired(); got != 0 {
		t.Fatalf("empty takeover_required = %d, want 0", got)
	}
	if emptyView.GetIsNetworkConnected() || emptyView.GetNetworkStatus() != "disconnected" || emptyView.GetRoadStatus() != "destroyed" {
		t.Fatalf("empty network fields = connected:%v status:%q road:%q, want disconnected/destroyed", emptyView.GetIsNetworkConnected(), emptyView.GetNetworkStatus(), emptyView.GetRoadStatus())
	}
}

func TestBuildNodeViewUsesDisabledBuildingStateAndTakeoverRuntime(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			SafeZoneRadius:             3,
			CityCoreMaxHP:              100,
			BaseResearchOutputPerTurn:  1,
			BaseIndustryOutputPerTurn:  2,
			FacilityTakeoverTurns:      5,
			InitialCityTerritoryRadius: 1,
		},
		Buildings: []staticdata.BuildingDefinition{
			{
				ID:            "farm",
				BuildingScope: "out_of_city",
				MaxHP:         60,
				TakeoverMode:  "delayed",
			},
		},
	}))

	world := donburi.NewWorld()
	mapData := &domain.MapData{
		ID:           "default",
		PlayerSpawns: map[string]domain.Position{"player-1": {Q: 0, R: 0}},
		NodeIndex:    map[string]donburi.Entity{},
	}
	nodeEntry := createNodeForViewTest(world, mapData, "F1", 0, 0)
	node := ecs.NodeC.Get(nodeEntry)
	node.Owner = "player-1"
	node.TerritoryOwner = "player-1"
	node.IsResource = true
	node.ResourceType = "food"

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, mapData)
	state.World = world
	state.EnsureCityState("player-1", "C1")

	ecs.CreateBuilding(world, "farm", "player-1", "C1", nodeEntry)
	nodeEntry.AddComponent(ecs.BuildingStateC)
	ecs.BuildingStateC.SetValue(nodeEntry, ecs.BuildingStateComp{
		Disabled:       true,
		DisabledReason: "outside_territory",
	})
	ecs.FacilityTakeoverC.SetValue(nodeEntry, ecs.FacilityTakeoverComp{
		Mode:      "delayed",
		Progress:  2,
		Required:  5,
		Completed: false,
	})

	view := BuildNodeView(state, nodeEntry, "player-1")
	if got := view.GetBuildingStatus(); got != "disabled" {
		t.Fatalf("building_status = %q, want disabled", got)
	}
	if got := view.GetTakeoverProgress(); got != 2 {
		t.Fatalf("takeover_progress = %d, want 2", got)
	}
	if got := view.GetTakeoverRequired(); got != 5 {
		t.Fatalf("takeover_required = %d, want 5", got)
	}
}

func createNodeForViewTest(world donburi.World, mapData *domain.MapData, nodeID string, x int, y int) *donburi.Entry {
	entity := ecs.CreateNode(world, ecs.MapNode{ID: nodeID, Q: x, R: y, Terrain: "plain"})
	mapData.NodeIndex[nodeID] = entity
	return world.Entry(entity)
}
