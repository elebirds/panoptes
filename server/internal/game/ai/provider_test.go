package ai

import (
	"context"
	"math/rand"
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/game/participant"
	"github.com/elebirds/panoptes/internal/game/planning"
	gamequery "github.com/elebirds/panoptes/internal/game/query"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func TestRuleBotProviderBuildsFarmOnVisibleFoodNode(t *testing.T) {
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
			{ID: "infantry", Class: "melee", MaxHP: 30, Attack: 10, AttackRange: 1, MoveRange: 2, VisionRange: 2, TrainCost: staticdata.ResourceAmounts{}, Upkeep: staticdata.ResourceAmounts{}},
		},
		Buildings: []staticdata.BuildingDefinition{
			{ID: "farm", Name: "Farm", BuildingScope: "out_of_city", PlacementKind: "resource_node", RequiredResourceType: "food", MaxHP: 60, TakeoverMode: "delayed"},
		},
	}))

	state, botObservation := buildRuleBotState(t, func(world donburi.World, mapData *domain.MapData, state *domain.GameState) {
		resourceEntry := createNode(world, mapData, "N1", 1, 0, true, "food")
		node := ecs.NodeC.Get(resourceEntry)
		node.TerritoryOwner = "bot-1"
		node.Owner = "bot-1"

		unitEntry := world.Entry(ecs.CreateUnit(world, "infantry", "bot-1", domain.Position{Q: 0, R: 0}))
		ecs.UnitStatsC.Get(unitEntry).ID = "ally-1"
		state.Players["bot-1"].Research.UnlockBuilding("farm")
		state.EnsureCityState("bot-1", "N0")
		state.Players["bot-1"].CapitalCityID = "N0"
	})

	intents, err := RuleBotProvider{}.BuildPlanningIntents(context.Background(), Request{
		Participant: participant.Participant{ID: "bot-1", Kind: participant.KindBot},
		State:       state,
		Observation: botObservation,
		RNG:         rand.New(rand.NewSource(7)),
	})
	if err != nil {
		t.Fatalf("BuildPlanningIntents() error = %v", err)
	}

	buildIntent := findIntent[planning.BuildStructureIntent](intents)
	if buildIntent == nil {
		t.Fatalf("expected build intent, got %#v", intents)
	}
	if buildIntent.NodeID != "N1" || buildIntent.BuildingTypeID != "farm" {
		t.Fatalf("build intent = %#v, want farm at N1", *buildIntent)
	}
	if submit := findLastSubmitIntent(intents); submit == nil {
		t.Fatalf("expected submit intent at end, got %#v", intents)
	}
}

func TestRuleBotProviderAttacksVisibleEnemyUnit(t *testing.T) {
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
			{ID: "infantry", Class: "melee", MaxHP: 30, Attack: 10, AttackRange: 1, MoveRange: 2, VisionRange: 2, TrainCost: staticdata.ResourceAmounts{}, Upkeep: staticdata.ResourceAmounts{}, Flags: staticdata.UnitFlags{CanAttackStructures: true}},
			{ID: "settler", Class: "civilian", MaxHP: 12, Attack: 0, AttackRange: 0, MoveRange: 2, VisionRange: 2, TrainCost: staticdata.ResourceAmounts{}, Upkeep: staticdata.ResourceAmounts{}},
		},
	}))

	state, botObservation := buildRuleBotState(t, func(world donburi.World, mapData *domain.MapData, state *domain.GameState) {
		allyEntry := world.Entry(ecs.CreateUnit(world, "infantry", "bot-1", domain.Position{Q: 0, R: 0}))
		enemyEntry := world.Entry(ecs.CreateUnit(world, "settler", "player-2", domain.Position{Q: 1, R: 0}))
		ecs.UnitStatsC.Get(allyEntry).ID = "ally-1"
		ecs.UnitStatsC.Get(enemyEntry).ID = "enemy-1"
	})

	intents, err := RuleBotProvider{}.BuildPlanningIntents(context.Background(), Request{
		Participant: participant.Participant{ID: "bot-1", Kind: participant.KindBot},
		State:       state,
		Observation: botObservation,
		RNG:         rand.New(rand.NewSource(11)),
	})
	if err != nil {
		t.Fatalf("BuildPlanningIntents() error = %v", err)
	}

	attack := findIntent[planning.IssueUnitOrderIntent](intents)
	if attack == nil {
		t.Fatalf("expected unit order intent, got %#v", intents)
	}
	if attack.Action != "attack" || attack.TargetUnitID != "enemy-1" {
		t.Fatalf("unit order = %#v, want attack enemy-1", *attack)
	}
	if submit := findLastSubmitIntent(intents); submit == nil {
		t.Fatalf("expected submit intent at end, got %#v", intents)
	}
}

func TestRuleBotProviderMovesInfantryToExploreHiddenFrontier(t *testing.T) {
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
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
	}))

	state, botObservation := buildRuleBotState(t, func(world donburi.World, mapData *domain.MapData, state *domain.GameState) {
		unitEntry := world.Entry(ecs.CreateUnit(world, "infantry", "bot-1", domain.Position{Q: 0, R: 0}))
		ecs.UnitStatsC.Get(unitEntry).ID = "ally-1"
	})

	intents, err := RuleBotProvider{}.BuildPlanningIntents(context.Background(), Request{
		Participant: participant.Participant{ID: "bot-1", Kind: participant.KindBot},
		State:       state,
		Observation: botObservation,
		RNG:         rand.New(rand.NewSource(13)),
	})
	if err != nil {
		t.Fatalf("BuildPlanningIntents() error = %v", err)
	}

	move := findIntent[planning.IssueUnitOrderIntent](intents)
	if move == nil {
		t.Fatalf("expected unit order intent, got %#v", intents)
	}
	if move.Action != "move" || move.TargetNodeID != "N2" {
		t.Fatalf("unit order = %#v, want move to hidden frontier N2", *move)
	}
}

func TestRuleBotProviderMovesInfantryUnderOmniscientVision(t *testing.T) {
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
			{ID: "infantry", Class: "melee", MaxHP: 30, Attack: 10, AttackRange: 1, MoveRange: 2, VisionRange: 1, TrainCost: staticdata.ResourceAmounts{}, Upkeep: staticdata.ResourceAmounts{}, Flags: staticdata.UnitFlags{CanAttackStructures: true}},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
	}))

	state, _ := buildRuleBotState(t, func(world donburi.World, mapData *domain.MapData, state *domain.GameState) {
		unitEntry := world.Entry(ecs.CreateUnit(world, "infantry", "bot-1", domain.Position{Q: 0, R: 0}))
		ecs.UnitStatsC.Get(unitEntry).ID = "ally-1"
	})
	store := gamequery.NewObservationStore()
	store.SetOmniscient("bot-1", true)
	botObservation := store.BuildObservation(state, "bot-1")

	intents, err := RuleBotProvider{}.BuildPlanningIntents(context.Background(), Request{
		Participant: participant.Participant{ID: "bot-1", Kind: participant.KindBot},
		State:       state,
		Observation: botObservation,
		RNG:         rand.New(rand.NewSource(17)),
	})
	if err != nil {
		t.Fatalf("BuildPlanningIntents() error = %v", err)
	}

	move := findIntent[planning.IssueUnitOrderIntent](intents)
	if move == nil {
		t.Fatalf("expected unit order intent, got %#v", intents)
	}
	if move.Action != "move" || move.TargetNodeID != "N2" {
		t.Fatalf("unit order = %#v, want move to enemy frontier N2 under omniscient vision", *move)
	}
}

func TestRuleBotProviderAttacksEnemyCityCore(t *testing.T) {
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
			{ID: "infantry", Class: "melee", MaxHP: 30, Attack: 10, AttackRange: 1, MoveRange: 2, VisionRange: 2, TrainCost: staticdata.ResourceAmounts{}, Upkeep: staticdata.ResourceAmounts{}, Flags: staticdata.UnitFlags{CanAttackStructures: true}},
		},
		Buildings: []staticdata.BuildingDefinition{
			{ID: "city_core", Name: "City Core", BuildingScope: "in_city", PlacementKind: "city_center", MaxHP: 100, TakeoverMode: "city_capture"},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
	}))

	state, botObservation := buildRuleBotState(t, func(world donburi.World, mapData *domain.MapData, state *domain.GameState) {
		allyEntry := world.Entry(ecs.CreateUnit(world, "infantry", "bot-1", domain.Position{Q: 1, R: 0}))
		ecs.UnitStatsC.Get(allyEntry).ID = "ally-1"
		ecs.CreateBuilding(world, "city_core", "player-2", "N2", world.Entry(mapData.NodeIndex["N2"]))
	})

	intents, err := RuleBotProvider{}.BuildPlanningIntents(context.Background(), Request{
		Participant: participant.Participant{ID: "bot-1", Kind: participant.KindBot},
		State:       state,
		Observation: botObservation,
		RNG:         rand.New(rand.NewSource(19)),
	})
	if err != nil {
		t.Fatalf("BuildPlanningIntents() error = %v", err)
	}

	attack := findIntent[planning.IssueUnitOrderIntent](intents)
	if attack == nil {
		t.Fatalf("expected unit order intent, got %#v", intents)
	}
	if attack.Action != "attack" || attack.TargetNodeID != "N2" {
		t.Fatalf("unit order = %#v, want attack enemy city core at N2", *attack)
	}
}

func TestBuildDomesticDraftCandidates_MatchesRuleBotResearchAndPolicyChoices(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			SafeZoneRadius:             2,
			CityCoreMaxHP:              100,
			BaseResearchOutputPerTurn:  1,
			BaseIndustryOutputPerTurn:  2,
			FacilityTakeoverTurns:      2,
			InitialCityTerritoryRadius: 1,
		},
		Technologies: []staticdata.TechnologyDefinition{
			{ID: "agrarian_foundations", Name: "Agrarian Foundations", ResearchCost: 3},
			{ID: "bronze_working", Name: "Bronze Working", ResearchCost: 4},
		},
		Policies: []staticdata.PolicyDefinition{
			{ID: "reorganization", Name: "Reorganization", Layer: "national"},
			{ID: "expansion", Name: "Expansion", Layer: "national"},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
	}))

	state, botObservation := buildRuleBotState(t, func(world donburi.World, mapData *domain.MapData, state *domain.GameState) {
		state.Players["bot-1"].Policy = domain.Policy("reorganization")
	})

	req := Request{
		Participant: participant.Participant{ID: "bot-1", Kind: participant.KindBot},
		State:       state,
		Observation: botObservation,
		RNG:         rand.New(rand.NewSource(23)),
	}

	intents, err := RuleBotProvider{}.BuildPlanningIntents(context.Background(), req)
	if err != nil {
		t.Fatalf("BuildPlanningIntents() error = %v", err)
	}
	candidates := BuildDomesticDraftCandidates(context.Background(), req)

	researchIntent := findIntent[planning.SetResearchTargetIntent](intents)
	if researchIntent == nil {
		t.Fatalf("expected research intent, got %#v", intents)
	}
	policyIntent := findIntent[planning.SetPolicyIntent](intents)
	if policyIntent == nil {
		t.Fatalf("expected policy intent, got %#v", intents)
	}
	if got := len(candidates); got != 2 {
		t.Fatalf("candidate count = %d, want 2", got)
	}

	researchCandidate := findDomesticDraftCandidate(candidates, "research")
	if researchCandidate == nil {
		t.Fatalf("expected research candidate, got %#v", candidates)
	}
	if researchCandidate.TargetID != researchIntent.TechnologyID {
		t.Fatalf("research candidate target = %q, want %q", researchCandidate.TargetID, researchIntent.TechnologyID)
	}

	policyCandidate := findDomesticDraftCandidate(candidates, "policy")
	if policyCandidate == nil {
		t.Fatalf("expected policy candidate, got %#v", candidates)
	}
	if policyCandidate.TargetID != policyIntent.NationalPolicyID {
		t.Fatalf("policy candidate target = %q, want %q", policyCandidate.TargetID, policyIntent.NationalPolicyID)
	}
}

func TestRuleBotProviderBuildsBarracksWithoutImmediateThreat(t *testing.T) {
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
			{ID: "infantry", Class: "melee", MaxHP: 30, Attack: 10, AttackRange: 1, MoveRange: 2, VisionRange: 2},
			{ID: "settler", Class: "civilian", MaxHP: 12, Attack: 0, AttackRange: 0, MoveRange: 2, VisionRange: 2},
		},
		Buildings: []staticdata.BuildingDefinition{
			{ID: "workshop", Name: "Workshop", Description: "Industrial workshop", PlacementKind: "city_territory", BuildingScope: "in_city", MaxHP: 80, TakeoverMode: "city_capture"},
			{ID: "barracks", Name: "Barracks", Description: "Military barracks", PlacementKind: "city_territory", BuildingScope: "in_city", MaxHP: 90, TakeoverMode: "city_capture"},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
	}))

	state, botObservation := buildRuleBotState(t, func(world donburi.World, mapData *domain.MapData, state *domain.GameState) {
		ecs.CreateBuilding(world, "city_core", "bot-1", "N0", world.Entry(mapData.NodeIndex["N0"]))
		state.Players["bot-1"].Research.UnlockBuilding("workshop")
		state.Players["bot-1"].Research.UnlockBuilding("barracks")
	})

	intents, err := RuleBotProvider{}.BuildPlanningIntents(context.Background(), Request{
		Participant: participant.Participant{ID: "bot-1", Kind: participant.KindBot},
		State:       state,
		Observation: botObservation,
		RNG:         rand.New(rand.NewSource(31)),
	})
	if err != nil {
		t.Fatalf("BuildPlanningIntents() error = %v", err)
	}

	buildIntent := findIntent[planning.BuildStructureIntent](intents)
	if buildIntent == nil {
		t.Fatalf("expected build intent, got %#v", intents)
	}
	if buildIntent.NodeID != "N1" || buildIntent.BuildingTypeID != "barracks" {
		t.Fatalf("build intent = %#v, want barracks at N1", *buildIntent)
	}
}

func TestRuleBotProviderKeepsMilitaryBuildOptionsAfterFirstBarracks(t *testing.T) {
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
			{ID: "infantry", Class: "melee", MaxHP: 30, Attack: 10, AttackRange: 1, MoveRange: 2, VisionRange: 2},
			{ID: "settler", Class: "civilian", MaxHP: 12, Attack: 0, AttackRange: 0, MoveRange: 2, VisionRange: 2},
		},
		Buildings: []staticdata.BuildingDefinition{
			{ID: "workshop", Name: "Workshop", Description: "Industrial workshop", PlacementKind: "city_territory", BuildingScope: "in_city", MaxHP: 80, TakeoverMode: "city_capture"},
			{ID: "barracks", Name: "Barracks", Description: "Military barracks", PlacementKind: "city_territory", BuildingScope: "in_city", MaxHP: 90, TakeoverMode: "city_capture"},
			{ID: "archery", Name: "Archery", Description: "Military archery range", PlacementKind: "city_territory", BuildingScope: "in_city", MaxHP: 90, TakeoverMode: "city_capture"},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
	}))

	state, botObservation := buildRuleBotState(t, func(world donburi.World, mapData *domain.MapData, state *domain.GameState) {
		ecs.CreateBuilding(world, "city_core", "bot-1", "N0", world.Entry(mapData.NodeIndex["N0"]))
		state.Players["bot-1"].Research.UnlockBuilding("workshop")
		state.Players["bot-1"].Research.UnlockBuilding("barracks")
		state.Players["bot-1"].Research.UnlockBuilding("archery")

		node := ecs.NodeC.Get(world.Entry(mapData.NodeIndex["N2"]))
		node.Owner = "bot-1"
		node.TerritoryOwner = "bot-1"

		ecs.CreateBuilding(world, "barracks", "bot-1", "N0", world.Entry(mapData.NodeIndex["N1"]))
	})

	intents, err := RuleBotProvider{}.BuildPlanningIntents(context.Background(), Request{
		Participant: participant.Participant{ID: "bot-1", Kind: participant.KindBot},
		State:       state,
		Observation: botObservation,
		RNG:         rand.New(rand.NewSource(37)),
	})
	if err != nil {
		t.Fatalf("BuildPlanningIntents() error = %v", err)
	}

	buildIntent := findIntent[planning.BuildStructureIntent](intents)
	if buildIntent == nil {
		t.Fatalf("expected build intent, got %#v", intents)
	}
	if buildIntent.NodeID != "N2" {
		t.Fatalf("build intent node = %q, want N2", buildIntent.NodeID)
	}
	if buildIntent.BuildingTypeID != "barracks" && buildIntent.BuildingTypeID != "archery" {
		t.Fatalf("build intent = %#v, want military building at N2", *buildIntent)
	}
}

func TestRuleBotProviderBuildsFrontierOfficeWhenExpansionNeedsSettlerSource(t *testing.T) {
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
			{ID: "infantry", Class: "melee", MaxHP: 30, Attack: 10, AttackRange: 1, MoveRange: 2, VisionRange: 2},
			{ID: "settler", Class: "civilian", MaxHP: 12, Attack: 0, AttackRange: 0, MoveRange: 2, VisionRange: 2},
		},
		Buildings: []staticdata.BuildingDefinition{
			{ID: "workshop", Name: "Workshop", Description: "Industrial workshop", PlacementKind: "city_territory", BuildingScope: "in_city", MaxHP: 80, TakeoverMode: "city_capture"},
			{ID: "barracks", Name: "Barracks", Description: "Military barracks", PlacementKind: "city_territory", BuildingScope: "in_city", RecipeIDs: []string{"barracks_infantry"}, DefaultRecipeID: "barracks_infantry", MaxHP: 90, TakeoverMode: "city_capture"},
			{ID: "frontier_office", Name: "Frontier Office", Description: "Expansion office that trains settlers", PlacementKind: "city_territory", BuildingScope: "in_city", RecipeIDs: []string{"city_core_settler"}, DefaultRecipeID: "city_core_settler", MaxHP: 90, TakeoverMode: "city_capture"},
		},
		Recipes: []staticdata.RecipeDefinition{
			{ID: "barracks_infantry", BuildingID: "barracks", WorkAmount: 2, BaseProgress: 1, Outputs: staticdata.RecipeOutputs{Units: []string{"infantry"}}},
			{ID: "city_core_settler", BuildingID: "frontier_office", WorkAmount: 2, BaseProgress: 1, Outputs: staticdata.RecipeOutputs{Units: []string{"settler"}}},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
	}))

	state, botObservation := buildRuleBotState(t, func(world donburi.World, mapData *domain.MapData, state *domain.GameState) {
		ecs.CreateBuilding(world, "city_core", "bot-1", "N0", world.Entry(mapData.NodeIndex["N0"]))
		state.Players["bot-1"].Research.UnlockBuilding("workshop")
		state.Players["bot-1"].Research.UnlockBuilding("barracks")
		state.Players["bot-1"].Research.UnlockBuilding("frontier_office")
		ecs.CreateBuilding(world, "barracks", "bot-1", "N0", world.Entry(mapData.NodeIndex["N2"]))
		ecs.CreateUnit(world, "infantry", "bot-1", domain.Position{Q: 0, R: 0})
		ecs.CreateUnit(world, "infantry", "bot-1", domain.Position{Q: 0, R: 0})
	})

	intents, err := RuleBotProvider{}.BuildPlanningIntents(context.Background(), Request{
		Participant: participant.Participant{ID: "bot-1", Kind: participant.KindBot},
		State:       state,
		Observation: botObservation,
		RNG:         rand.New(rand.NewSource(41)),
	})
	if err != nil {
		t.Fatalf("BuildPlanningIntents() error = %v", err)
	}

	buildIntent := findIntent[planning.BuildStructureIntent](intents)
	if buildIntent == nil {
		t.Fatalf("expected build intent, got %#v", intents)
	}
	if buildIntent.NodeID != "N1" || buildIntent.BuildingTypeID != "frontier_office" {
		t.Fatalf("build intent = %#v, want frontier_office at N1", *buildIntent)
	}
}

func buildRuleBotState(t *testing.T, mutate func(world donburi.World, mapData *domain.MapData, state *domain.GameState)) (*domain.GameState, *gamequery.ObservationSnapshot) {
	t.Helper()

	world := donburi.NewWorld()
	mapData := &domain.MapData{
		ID:           "bot-test",
		Width:        3,
		Height:       1,
		PlayerSpawns: map[string]domain.Position{"bot-1": {Q: 0, R: 0}, "player-2": {Q: 2, R: 0}},
		NodeIndex:    map[string]donburi.Entity{},
	}
	createNode(world, mapData, "N0", 0, 0, false, "")
	createNode(world, mapData, "N1", 1, 0, false, "")
	createNode(world, mapData, "N2", 2, 0, false, "")

	state := domain.NewGameState("game-bot", []string{"bot-1", "player-2"}, []string{"bot", "enemy"}, mapData)
	state.World = world
	state.Turn = 3
	state.EnsureCityState("bot-1", "N0")
	state.Players["bot-1"].CapitalCityID = "N0"

	if mutate != nil {
		mutate(world, mapData, state)
	}

	observation := gamequery.NewObservationStore().BuildObservation(state, "bot-1")
	return state, observation
}

func createNode(world donburi.World, mapData *domain.MapData, nodeID string, x int, y int, isResource bool, resourceType string) *donburi.Entry {
	entity := ecs.CreateNode(world, ecs.MapNode{
		ID:              nodeID,
		Q:               x,
		R:               y,
		Terrain:         "plain",
		IsResourcePoint: isResource,
		ResourceType:    resourceType,
	})
	mapData.NodeIndex[nodeID] = entity
	entry := world.Entry(entity)
	node := ecs.NodeC.Get(entry)
	if x <= 1 {
		node.Owner = "bot-1"
		node.TerritoryOwner = "bot-1"
	} else {
		node.Owner = "player-2"
		node.TerritoryOwner = "player-2"
	}
	return entry
}

func findIntent[T planning.Intent](intents []planning.Intent) *T {
	for _, intent := range intents {
		if typed, ok := intent.(T); ok {
			return &typed
		}
	}
	return nil
}

func findLastSubmitIntent(intents []planning.Intent) *planning.SubmitTurnIntent {
	if len(intents) == 0 {
		return nil
	}
	last, ok := intents[len(intents)-1].(planning.SubmitTurnIntent)
	if !ok {
		return nil
	}
	return &last
}

func findDomesticDraftCandidate(candidates []DomesticDraftCandidate, kind string) *DomesticDraftCandidate {
	for _, candidate := range candidates {
		if candidate.Kind == kind {
			return &candidate
		}
	}
	return nil
}
