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
				ID:              "city_core",
				PlacementKind:   "city_foundation_center",
				BuildingScope:   "city_core",
				DefaultRecipeID: "city_core_settler",
				MaxHP:           100,
				TakeoverMode:    "disabled",
			},
			{
				ID:              "farm",
				PlacementKind:   "city_territory",
				BuildingScope:   "out_of_city",
				DefaultRecipeID: "farm_food",
				MaxHP:           80,
				TakeoverMode:    "delayed",
			},
		},
		Recipes: []staticdata.RecipeDefinition{
			{ID: "city_core_settler", BuildingID: "city_core", WorkAmount: 2, BaseProgress: 1},
			{ID: "farm_food", BuildingID: "farm", WorkAmount: 1, BaseProgress: 1},
		},
		Technologies: []staticdata.TechnologyDefinition{
			{
				ID:           "agri_unlock_farm",
				Branch:       "agriculture",
				Tier:         1,
				ResearchCost: 1,
				ExplicitEffects: []staticdata.ExplicitEffect{
					{Type: "unlock_building", TargetID: "farm"},
					{Type: "unlock_recipe", TargetID: "farm_food"},
				},
			},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
	}, cityBuildMap("research_unlock_build"))

	state, err := newState("research_unlock_build", catalog, []string{"player-1"}, []string{"alice"}, cityBuildMap("research_unlock_build"))
	if err != nil {
		return nil, err
	}
	state.Players["player-1"].Research.CurrentProgress = 1
	return &Definition{
		Name:      "research_unlock_build",
		Catalog:   catalog,
		State:     state,
		PlayerIDs: []string{"player-1"},
		Usernames: []string{"alice"},
	}, nil
}

func IndustryBudgetExhaustion() (*Definition, error) {
	rules := baseRules()
	rules.BaseIndustryOutputPerTurn = 1
	catalog := staticdata.NewCatalog(staticdata.CatalogBundle{
		Manifest: manifest("industry_budget_exhaustion"),
		Rules:    rules,
		Buildings: []staticdata.BuildingDefinition{
			{
				ID:              "city_core",
				PlacementKind:   "city_foundation_center",
				BuildingScope:   "city_core",
				DefaultRecipeID: "city_core_settler",
				MaxHP:           100,
				TakeoverMode:    "disabled",
			},
			{
				ID:            "farm",
				PlacementKind: "city_territory",
				BuildingScope: "out_of_city",
				PointCosts:    staticdata.PointAmounts{"industry_output": 1},
				MaxHP:         80,
				TakeoverMode:  "delayed",
			},
		},
		Recipes: []staticdata.RecipeDefinition{
			{ID: "city_core_settler", BuildingID: "city_core", WorkAmount: 2, BaseProgress: 1},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
	}, cityBuildMap("industry_budget_exhaustion"))

	state, err := newState("industry_budget_exhaustion", catalog, []string{"player-1"}, []string{"alice"}, cityBuildMap("industry_budget_exhaustion"))
	if err != nil {
		return nil, err
	}
	state.Players["player-1"].Research.UnlockBuilding("farm")
	return &Definition{
		Name:      "industry_budget_exhaustion",
		Catalog:   catalog,
		State:     state,
		PlayerIDs: []string{"player-1"},
		Usernames: []string{"alice"},
	}, nil
}

func BuildingModifierPointPreview() (*Definition, error) {
	catalog := staticdata.NewCatalog(staticdata.CatalogBundle{
		Manifest: manifest("building_modifier_point_preview"),
		Rules:    baseRules(),
		Buildings: []staticdata.BuildingDefinition{
			{
				ID:              "city_core",
				PlacementKind:   "city_foundation_center",
				BuildingScope:   "city_core",
				DefaultRecipeID: "city_core_settler",
				MaxHP:           100,
				TakeoverMode:    "disabled",
			},
			{
				ID:            "workshop",
				PlacementKind: "city_territory",
				BuildingScope: "in_city",
				MaxHP:         80,
				TakeoverMode:  "city_capture",
				ModifierEffects: []staticdata.ModifierEffect{
					{Trigger: "point.output", PointKey: "industry_output", ModifierType: "flat", Value: 1},
				},
			},
		},
		Recipes: []staticdata.RecipeDefinition{
			{ID: "city_core_settler", BuildingID: "city_core", WorkAmount: 2, BaseProgress: 1},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
	}, cityBuildMap("building_modifier_point_preview"))

	state, err := newState("building_modifier_point_preview", catalog, []string{"player-1"}, []string{"alice"}, cityBuildMap("building_modifier_point_preview"))
	if err != nil {
		return nil, err
	}
	nodeEntry, ok := state.GetNode("A2")
	if !ok {
		return nil, fmt.Errorf("missing node A2")
	}
	ecs.CreateBuilding(state.World, "workshop", "player-1", "A1", nodeEntry)
	return &Definition{
		Name:      "building_modifier_point_preview",
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
		Buildings: []staticdata.BuildingDefinition{
			{
				ID:              "city_core",
				PlacementKind:   "city_foundation_center",
				BuildingScope:   "city_core",
				DefaultRecipeID: "city_core_settler",
				MaxHP:           100,
				TakeoverMode:    "disabled",
			},
			{
				ID:            "barracks",
				PlacementKind: "city_territory",
				BuildingScope: "in_city",
				MaxHP:         90,
				TakeoverMode:  "city_capture",
			},
		},
		Recipes: []staticdata.RecipeDefinition{
			{ID: "city_core_settler", BuildingID: "city_core", WorkAmount: 2, BaseProgress: 1},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
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
				ID:              "city_core",
				PlacementKind:   "city_foundation_center",
				BuildingScope:   "city_core",
				DefaultRecipeID: "city_core_settler",
				MaxHP:           100,
				TakeoverMode:    "disabled",
			},
			{
				ID:              "barracks",
				PlacementKind:   "city_territory",
				BuildingScope:   "in_city",
				DefaultRecipeID: "barracks_infantry",
				MaxHP:           90,
				TakeoverMode:    "city_capture",
			},
		},
		Recipes: []staticdata.RecipeDefinition{
			{ID: "city_core_settler", BuildingID: "city_core", WorkAmount: 2, BaseProgress: 1},
			{
				ID:             "barracks_infantry",
				BuildingID:     "barracks",
				ResourceInputs: staticdata.ResourceAmounts{"food": 999, "ore": 999},
				WorkAmount:     2,
				BaseProgress:   1,
				Outputs:        staticdata.RecipeOutputs{Units: []string{"infantry"}},
			},
		},
		Units: []staticdata.UnitDefinition{
			infantryDefinition(),
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
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
	ecs.CreateBuilding(state.World, "barracks", "player-1", "A1", nodeEntry)
	state.Players["player-1"].Research.UnlockBuilding("barracks")
	state.Players["player-1"].Research.UnlockRecipe("barracks_infantry")
	return &Definition{
		Name:      "recipe_blocked_by_input",
		Catalog:   catalog,
		State:     state,
		PlayerIDs: []string{"player-1"},
		Usernames: []string{"alice"},
	}, nil
}

func DisabledRecipeSkipped() (*Definition, error) {
	catalog := staticdata.NewCatalog(staticdata.CatalogBundle{
		Manifest: manifest("disabled_recipe_skipped"),
		Rules:    baseRules(),
		Buildings: []staticdata.BuildingDefinition{
			{
				ID:              "city_core",
				PlacementKind:   "city_foundation_center",
				BuildingScope:   "city_core",
				DefaultRecipeID: "city_core_settler",
				MaxHP:           100,
				TakeoverMode:    "disabled",
			},
			{
				ID:              "barracks",
				PlacementKind:   "city_territory",
				BuildingScope:   "in_city",
				DefaultRecipeID: "barracks_infantry",
				MaxHP:           90,
				TakeoverMode:    "city_capture",
			},
		},
		Recipes: []staticdata.RecipeDefinition{
			{ID: "city_core_settler", BuildingID: "city_core", WorkAmount: 2, BaseProgress: 1},
			{
				ID:           "barracks_infantry",
				BuildingID:   "barracks",
				WorkAmount:   2,
				BaseProgress: 1,
				Outputs:      staticdata.RecipeOutputs{Units: []string{"infantry"}},
			},
		},
		Units: []staticdata.UnitDefinition{
			infantryDefinition(),
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
	}, cityBuildMap("disabled_recipe_skipped"))

	state, err := newState("disabled_recipe_skipped", catalog, []string{"player-1"}, []string{"alice"}, cityBuildMap("disabled_recipe_skipped"))
	if err != nil {
		return nil, err
	}
	nodeEntry, ok := state.GetNode("A2")
	if !ok {
		return nil, fmt.Errorf("missing node A2")
	}
	ecs.CreateBuilding(state.World, "barracks", "player-1", "A1", nodeEntry)
	if !nodeEntry.HasComponent(ecs.BuildingStateC) {
		nodeEntry.AddComponent(ecs.BuildingStateC)
	}
	ecs.BuildingStateC.SetValue(nodeEntry, ecs.BuildingStateComp{
		Disabled:       true,
		DisabledReason: "outside_territory",
	})
	state.Players["player-1"].Research.UnlockBuilding("barracks")
	state.Players["player-1"].Research.UnlockRecipe("barracks_infantry")
	return &Definition{
		Name:      "disabled_recipe_skipped",
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
				ID:              "city_core",
				PlacementKind:   "city_foundation_center",
				BuildingScope:   "city_core",
				DefaultRecipeID: "city_core_settler",
				MaxHP:           100,
				TakeoverMode:    "disabled",
			},
			{
				ID:                   "farm",
				PlacementKind:        "resource_node",
				BuildingScope:        "out_of_city",
				RequiredResourceType: "food",
				DefaultRecipeID:      "farm_food",
				MaxHP:                80,
				TakeoverMode:         "delayed",
			},
		},
		Recipes: []staticdata.RecipeDefinition{
			{ID: "city_core_settler", BuildingID: "city_core", WorkAmount: 2, BaseProgress: 1},
			{ID: "farm_food", BuildingID: "farm", WorkAmount: 1, BaseProgress: 1},
		},
		Units: []staticdata.UnitDefinition{
			infantryDefinition(),
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
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
	ecs.CreateBuilding(state.World, "farm", "player-1", "A1", nodeEntry)
	unitEntry := state.World.Entry(ecs.CreateUnit(state.World, "infantry", "player-2", domain.Position{X: 1, Y: 1}))
	ecs.UnitStatsC.Get(unitEntry).ID = "infantry-1"
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
			TokensPerTurn:              3,
			BonusTokensPerTurn:         0,
			MaxTurns:                   3,
			CityCoreMaxHP:              10,
			SafeZoneRadius:             3,
			FacilityTakeoverTurns:      2,
			BaseResearchOutputPerTurn:  0,
			BaseIndustryOutputPerTurn:  0,
			MinimumCityDistance:        2,
			InitialCityTerritoryRadius: 1,
			TurnTimeLimitPlanning:      1,
		},
		Buildings: []staticdata.BuildingDefinition{
			{
				ID:              "city_core",
				PlacementKind:   "city_foundation_center",
				BuildingScope:   "city_core",
				DefaultRecipeID: "city_core_settler",
				MaxHP:           10,
				TakeoverMode:    "disabled",
			},
		},
		Recipes: []staticdata.RecipeDefinition{
			{ID: "city_core_settler", BuildingID: "city_core", WorkAmount: 2, BaseProgress: 1},
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
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
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
		TokensPerTurn:              3,
		BonusTokensPerTurn:         0,
		MaxTurns:                   4,
		CityCoreMaxHP:              100,
		SafeZoneRadius:             3,
		FacilityTakeoverTurns:      2,
		BaseResearchOutputPerTurn:  1,
		BaseIndustryOutputPerTurn:  2,
		MinimumCityDistance:        2,
		InitialCityTerritoryRadius: 1,
		TurnTimeLimitPlanning:      1,
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

func infantryDefinition() staticdata.UnitDefinition {
	return staticdata.UnitDefinition{
		ID:          "infantry",
		Class:       "melee",
		MaxHP:       20,
		Attack:      6,
		AttackRange: 1,
		MoveRange:   2,
		VisionRange: 2,
		TrainCost:   staticdata.ResourceAmounts{},
		Upkeep:      staticdata.ResourceAmounts{},
		Multipliers: map[string]float64{},
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
			{ID: "A1", X: 0, Y: 0, Terrain: "plain", OwnerSlot: &zero, TerritoryOwnerSlot: &zero, BuildingType: "city_core"},
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
	zero := 0
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
			{ID: "A1", X: 0, Y: 0, Terrain: "plain", OwnerSlot: &zero, TerritoryOwnerSlot: &zero, BuildingType: "city_core"},
			{ID: "B2", X: 1, Y: 1, Terrain: "plain", IsResourcePoint: true, ResourceType: "food", TerritoryOwnerSlot: &one},
			{ID: "C3", X: 2, Y: 2, Terrain: "plain", OwnerSlot: &one, TerritoryOwnerSlot: &one, BuildingType: "city_core"},
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
			{ID: "A1", X: 0, Y: 0, Terrain: "plain", OwnerSlot: &zero, TerritoryOwnerSlot: &zero, BuildingType: "city_core"},
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
	initializeScenarioCities(state)
	return state, nil
}

func initializeScenarioCities(state *domain.GameState) {
	if state == nil || state.World == nil {
		return
	}
	ecs.NodesWithBuilding(state.World).Each(state.World, func(entry *donburi.Entry) {
		if entry == nil || !entry.HasComponent(ecs.BuildingC) {
			return
		}
		building := ecs.BuildingC.Get(entry)
		if string(building.Type) != "city_core" || building.Owner == "" {
			return
		}
		node := ecs.NodeC.Get(entry)
		cityState := state.EnsureCityState(building.Owner, node.ID)
		if cityState != nil {
			cityState.OnlineOnTurn = 0
		}
		if state.Map == nil {
			return
		}
		corePos := ecs.PositionC.Get(entry)
		if spawnPos, ok := state.Map.PlayerSpawns[building.Owner]; ok && spawnPos.X == corePos.X && spawnPos.Y == corePos.Y {
			playerState := state.Players[building.Owner]
			if playerState != nil {
				playerState.CapitalCityID = node.ID
				playerState.CapitalCityCoreHP = building.HP
			}
		}
	})
}
