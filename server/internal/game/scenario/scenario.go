package scenario

import (
	"fmt"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/engine/maploader"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

type Definition struct {
	Name      string
	Catalog   *staticdata.Catalog
	State     *domain.GameState
	PlayerIDs []string
	Usernames []string
}

func ResearchUnlockBuild() (*Definition, error) {
	catalog := staticdata.NewCatalog(staticdata.CatalogBundle{
		Manifest: manifest("research_unlock_build"),
		Rules:    baseRules(),
		Buildings: []staticdata.BuildingDefinition{
			{
				ID:              "farm",
				Category:        "production",
				PlacementRule:   "city_only",
				BuildCost:       staticdata.ResourceAmounts{},
				RecipeIDs:       []string{"farm_food"},
				DefaultRecipeID: "farm_food",
				Combat:          staticdata.BuildingCombat{MaxHP: 80},
			},
		},
		Recipes: []staticdata.RecipeDefinition{
			{
				ID:            "farm_food",
				BuildingID:    "farm",
				Cost:          staticdata.ResourceAmounts{},
				DurationTurns: 1,
				DelayPenalty:  staticdata.RecipeDelayPenalty{Mode: "add_turns", Value: 1},
				Outputs:       staticdata.RecipeOutputs{Resources: staticdata.ResourceAmounts{"food": 2}},
			},
		},
		Technologies: []staticdata.TechnologyDefinition{
			{
				ID:            "agri_unlock_farm",
				Branch:        "agriculture",
				Tier:          1,
				TechPointCost: 1,
				Effects: []staticdata.TechnologyEffect{
					{Type: "unlock_building", TargetID: "farm"},
					{Type: "unlock_recipe", TargetID: "farm_food"},
				},
			},
		},
	}, cityBuildMap("research_unlock_build"))

	state, err := newState("research_unlock_build", catalog, []string{"player-1"}, []string{"alice"}, cityBuildMap("research_unlock_build"))
	if err != nil {
		return nil, err
	}
	state.Players["player-1"].Research.TechPoints = 1
	return &Definition{
		Name:      "research_unlock_build",
		Catalog:   catalog,
		State:     state,
		PlayerIDs: []string{"player-1"},
		Usernames: []string{"alice"},
	}, nil
}

func SettlerFoundCity() (*Definition, error) {
	catalog := staticdata.NewCatalog(staticdata.CatalogBundle{
		Manifest: manifest("settler_found_city"),
		Rules:    baseRules(),
		Units: []staticdata.UnitDefinition{
			settlerDefinition(),
		},
	}, expansionMap("settler_found_city"))

	state, err := newState("settler_found_city", catalog, []string{"player-1"}, []string{"alice"}, expansionMap("settler_found_city"))
	if err != nil {
		return nil, err
	}
	entry := state.World.Entry(ecs.CreateUnit(state.World, string(domain.UnitTypeSettler), "player-1", domain.Position{X: 2, Y: 2}))
	stats := ecs.UnitStatsC.Get(entry)
	stats.ID = "settler-1"
	return &Definition{
		Name:      "settler_found_city",
		Catalog:   catalog,
		State:     state,
		PlayerIDs: []string{"player-1"},
		Usernames: []string{"alice"},
	}, nil
}

func RecipeBlockedByInput() (*Definition, error) {
	catalog := staticdata.NewCatalog(staticdata.CatalogBundle{
		Manifest: manifest("recipe_blocked_by_input"),
		Rules:    baseRules(),
		Buildings: []staticdata.BuildingDefinition{
			{
				ID:              "smelter",
				Category:        "production",
				PlacementRule:   "city_only",
				BuildCost:       staticdata.ResourceAmounts{},
				RecipeIDs:       []string{"smelter_refined_ore"},
				DefaultRecipeID: "smelter_refined_ore",
				Combat:          staticdata.BuildingCombat{MaxHP: 80},
			},
		},
		Recipes: []staticdata.RecipeDefinition{
			{
				ID:            "smelter_refined_ore",
				BuildingID:    "smelter",
				Cost:          staticdata.ResourceAmounts{"ore": 2},
				DurationTurns: 1,
				DelayPenalty:  staticdata.RecipeDelayPenalty{Mode: "add_turns", Value: 2},
				Outputs:       staticdata.RecipeOutputs{Resources: staticdata.ResourceAmounts{"refined_ore": 1}},
			},
		},
	}, cityBuildMap("recipe_blocked_by_input"))

	state, err := newState("recipe_blocked_by_input", catalog, []string{"player-1"}, []string{"alice"}, cityBuildMap("recipe_blocked_by_input"))
	if err != nil {
		return nil, err
	}
	nodeEntry, ok := state.GetNode("A2")
	if !ok {
		return nil, fmt.Errorf("missing node A2")
	}
	ecs.CreateBuilding(state.World, "smelter", "player-1", "", nodeEntry)
	if !nodeEntry.HasComponent(ecs.BuildingOperationC) {
		nodeEntry.AddComponent(ecs.BuildingOperationC)
	}
	ecs.BuildingOperationC.SetValue(nodeEntry, domain.BuildingOperationComp{
		SelectedRecipeID: "smelter_refined_ore",
		RequiredTurns:    1,
	})
	state.Players["player-1"].Research.UnlockBuilding("smelter")
	state.Players["player-1"].Research.UnlockRecipe("smelter_refined_ore")
	return &Definition{
		Name:      "recipe_blocked_by_input",
		Catalog:   catalog,
		State:     state,
		PlayerIDs: []string{"player-1"},
		Usernames: []string{"alice"},
	}, nil
}

func OuterFacilityCapture() (*Definition, error) {
	catalog := staticdata.NewCatalog(staticdata.CatalogBundle{
		Manifest: manifest("outer_facility_capture"),
		Rules:    baseRules(),
		Buildings: []staticdata.BuildingDefinition{
			{
				ID:                   "farm",
				Category:             "production",
				PlacementRule:        "resource_only",
				RequiredResourceType: "food",
				BuildCost:            staticdata.ResourceAmounts{},
				RecipeIDs:            []string{"farm_food"},
				DefaultRecipeID:      "farm_food",
				Combat:               staticdata.BuildingCombat{MaxHP: 80},
			},
		},
		Recipes: []staticdata.RecipeDefinition{
			{
				ID:            "farm_food",
				BuildingID:    "farm",
				Cost:          staticdata.ResourceAmounts{},
				DurationTurns: 1,
				DelayPenalty:  staticdata.RecipeDelayPenalty{Mode: "add_turns", Value: 1},
				Outputs:       staticdata.RecipeOutputs{Resources: staticdata.ResourceAmounts{"food": 2}},
			},
		},
	}, contestedFacilityMap("outer_facility_capture"))

	state, err := newState("outer_facility_capture", catalog, []string{"player-1", "player-2"}, []string{"alice", "bob"}, contestedFacilityMap("outer_facility_capture"))
	if err != nil {
		return nil, err
	}
	nodeEntry, ok := state.GetNode("B2")
	if !ok {
		return nil, fmt.Errorf("missing node B2")
	}
	ecs.CreateBuilding(state.World, "farm", "player-1", "", nodeEntry)
	if !nodeEntry.HasComponent(ecs.BuildingOperationC) {
		nodeEntry.AddComponent(ecs.BuildingOperationC)
	}
	ecs.BuildingOperationC.SetValue(nodeEntry, domain.BuildingOperationComp{
		SelectedRecipeID: "farm_food",
		RequiredTurns:    1,
	})
	state.Players["player-1"].Research.UnlockBuilding("farm")
	state.Players["player-1"].Research.UnlockRecipe("farm_food")
	return &Definition{
		Name:      "outer_facility_capture",
		Catalog:   catalog,
		State:     state,
		PlayerIDs: []string{"player-1"},
		Usernames: []string{"alice"},
	}, nil
}

func CapitalDestroyGameOver() (*Definition, error) {
	catalog := staticdata.NewCatalog(staticdata.CatalogBundle{
		Manifest: manifest("capital_destroy_gameover"),
		Rules: staticdata.Rules{
			TokensPerTurn:         3,
			StartingTechPoints:    0,
			TechPointsPerTurn:     0,
			TechPointsMax:         10,
			BuildPointsPerTurn:    10,
			BuildPointsMax:        20,
			CastleBaseHP:          10,
			TurnTimeLimitPlanning: 1,
			MaxTurns:              3,
		},
		Units: []staticdata.UnitDefinition{
			{
				ID:          "siege_engine",
				Class:       "siege",
				MaxHP:       20,
				Attack:      10,
				AttackRange: 1,
				MoveRange:   1,
				VisionRange: 2,
				TrainCost:   staticdata.ResourceAmounts{},
				Upkeep:      staticdata.ResourceAmounts{},
				Multipliers: map[string]float64{},
				Flags: staticdata.UnitFlags{
					CanSiege:        true,
					SiegeMultiplier: 1,
					CanCapture:      true,
				},
			},
		},
	}, capitalSiegeMap("capital_destroy_gameover"))

	state, err := newState("capital_destroy_gameover", catalog, []string{"player-1", "player-2"}, []string{"alice", "bob"}, capitalSiegeMap("capital_destroy_gameover"))
	if err != nil {
		return nil, err
	}
	entry := state.World.Entry(ecs.CreateUnit(state.World, "siege_engine", "player-2", domain.Position{X: 0, Y: 0}))
	stats := ecs.UnitStatsC.Get(entry)
	stats.ID = "siege-1"
	return &Definition{
		Name:      "capital_destroy_gameover",
		Catalog:   catalog,
		State:     state,
		PlayerIDs: []string{"player-1"},
		Usernames: []string{"alice"},
	}, nil
}

func baseRules() staticdata.Rules {
	return staticdata.Rules{
		TokensPerTurn:         3,
		StartingTechPoints:    0,
		TechPointsPerTurn:     0,
		TechPointsMax:         10,
		BuildPointsPerTurn:    10,
		BuildPointsMax:        20,
		CastleBaseHP:          100,
		TurnTimeLimitPlanning: 1,
		MaxTurns:              4,
	}
}

func manifest(defaultMapID string) staticdata.Manifest {
	return staticdata.Manifest{
		SchemaVersion:  "2026-04-15",
		ContentVersion: defaultMapID,
		BundleHash:     defaultMapID,
		DefaultLocale:  "zh-CN",
		DefaultMapID:   defaultMapID,
	}
}

func settlerDefinition() staticdata.UnitDefinition {
	return staticdata.UnitDefinition{
		ID:          "settler",
		Class:       "civilian",
		MaxHP:       12,
		Attack:      0,
		AttackRange: 0,
		MoveRange:   2,
		VisionRange: 2,
		TrainCost:   staticdata.ResourceAmounts{},
		Upkeep:      staticdata.ResourceAmounts{},
		Multipliers: map[string]float64{},
		Flags: staticdata.UnitFlags{
			CanCapture: true,
		},
	}
}

func cityBuildMap(id string) *staticdata.MapRuntimeBundle {
	zero := 0
	return &staticdata.MapRuntimeBundle{
		ID:     id,
		Name:   id,
		Width:  3,
		Height: 3,
		SpawnPoints: []staticdata.SpawnPoint{
			{Slot: 0, X: 0, Y: 0},
		},
		Nodes: []staticdata.MapRuntimeNode{
			{ID: "A1", X: 0, Y: 0, Terrain: "plain", OwnerSlot: &zero, TerritoryOwnerSlot: &zero, BuildingType: "castle"},
			{ID: "A2", X: 0, Y: 1, Terrain: "plain", OwnerSlot: &zero, TerritoryOwnerSlot: &zero},
			{ID: "B1", X: 1, Y: 0, Terrain: "plain", OwnerSlot: &zero, TerritoryOwnerSlot: &zero},
			{ID: "B2", X: 1, Y: 1, Terrain: "plain", OwnerSlot: &zero, TerritoryOwnerSlot: &zero},
		},
		NamedNodes: map[string]string{
			"A1": "主城",
			"A2": "空地",
		},
	}
}

func expansionMap(id string) *staticdata.MapRuntimeBundle {
	return &staticdata.MapRuntimeBundle{
		ID:     id,
		Name:   id,
		Width:  5,
		Height: 5,
		SpawnPoints: []staticdata.SpawnPoint{
			{Slot: 0, X: 2, Y: 2},
		},
		Nodes: allPlainNodes(5, 5),
		NamedNodes: map[string]string{
			"C3": "建城点",
		},
	}
}

func contestedFacilityMap(id string) *staticdata.MapRuntimeBundle {
	one := 1
	return &staticdata.MapRuntimeBundle{
		ID:     id,
		Name:   id,
		Width:  3,
		Height: 3,
		SpawnPoints: []staticdata.SpawnPoint{
			{Slot: 0, X: 0, Y: 0},
			{Slot: 1, X: 2, Y: 2},
		},
		Nodes: []staticdata.MapRuntimeNode{
			{ID: "A1", X: 0, Y: 0, Terrain: "plain"},
			{ID: "B2", X: 1, Y: 1, Terrain: "plain", IsResourcePoint: true, ResourceType: "food", TerritoryOwnerSlot: &one},
			{ID: "C3", X: 2, Y: 2, Terrain: "plain"},
		},
		NamedNodes: map[string]string{
			"B2": "争议农田",
		},
	}
}

func capitalSiegeMap(id string) *staticdata.MapRuntimeBundle {
	zero := 0
	one := 1
	return &staticdata.MapRuntimeBundle{
		ID:     id,
		Name:   id,
		Width:  2,
		Height: 2,
		SpawnPoints: []staticdata.SpawnPoint{
			{Slot: 0, X: 0, Y: 0},
			{Slot: 1, X: 1, Y: 1},
		},
		Nodes: []staticdata.MapRuntimeNode{
			{ID: "A1", X: 0, Y: 0, Terrain: "plain", OwnerSlot: &zero, TerritoryOwnerSlot: &zero, BuildingType: "castle"},
			{ID: "B2", X: 1, Y: 1, Terrain: "plain", OwnerSlot: &one, TerritoryOwnerSlot: &one},
		},
		NamedNodes: map[string]string{
			"A1": "主城",
		},
	}
}

func allPlainNodes(width int, height int) []staticdata.MapRuntimeNode {
	nodes := make([]staticdata.MapRuntimeNode, 0, width*height)
	for y := 0; y < height; y++ {
		for x := 0; x < width; x++ {
			nodes = append(nodes, staticdata.MapRuntimeNode{
				ID:      nodeID(x, y),
				X:       x,
				Y:       y,
				Terrain: "plain",
			})
		}
	}
	return nodes
}

func nodeID(x int, y int) string {
	return string(rune('A'+x)) + fmt.Sprintf("%d", y+1)
}

func newState(gameID string, catalog *staticdata.Catalog, playerIDs []string, usernames []string, mapBundle *staticdata.MapRuntimeBundle) (*domain.GameState, error) {
	if catalog == nil {
		return nil, fmt.Errorf("catalog is nil")
	}
	if mapBundle == nil {
		return nil, fmt.Errorf("map bundle is nil")
	}

	staticdata.SetDefault(catalog)
	world := donburi.NewWorld()
	mapData := maploader.InitWorldFromMap(world, mapBundle, playerIDs)
	state := domain.NewGameState(gameID, playerIDs, usernames, mapData)
	state.World = world
	state.GameID = gameID
	return state, nil
}
