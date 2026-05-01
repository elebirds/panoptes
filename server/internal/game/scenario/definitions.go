// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载后端测试/调试场景的构造与布置辅助逻辑。

package scenario

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/game/participant"
	"github.com/elebirds/panoptes/internal/staticdata"
)

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
	if _, err := placeBuildingAtNode(state, "workshop", "player-1", "A1", "A2"); err != nil {
		return nil, err
	}
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
	placeUnitWithID(state, string(domain.UnitTypeSettler), "player-1", domain.Position{Q: 1, R: 2}, "settler-1")
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
				ResourceInputs: staticdata.ResourceAmounts{"food": 999, "ore": 999, "crystal": 1},
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
	if _, err := placeBuildingAtNode(state, "barracks", "player-1", "A1", "A2"); err != nil {
		return nil, err
	}
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
	nodeEntry, err := placeBuildingAtNode(state, "barracks", "player-1", "A1", "A2")
	if err != nil {
		return nil, err
	}
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
	if _, err := placeBuildingAtNode(state, "farm", "player-1", "A1", "B2"); err != nil {
		return nil, err
	}
	placeUnitWithID(state, "infantry", "player-2", domain.Position{Q: 1, R: 1}, "infantry-1")
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
			infantryDefinition(),
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
	}, capitalSiegeMap("capital_destroy_gameover"))

	state, err := newState("capital_destroy_gameover", catalog, []string{"player-1", "player-2"}, []string{"alice", "bob"}, capitalSiegeMap("capital_destroy_gameover"))
	if err != nil {
		return nil, err
	}
	placeUnitWithID(state, "infantry", "player-2", domain.Position{Q: 1, R: 0}, "infantry-1")
	state.TurnRuntime.Planning.UnitOrders["infantry-1"] = domain.UnitDirective{
		PlayerID:     "player-2",
		UnitID:       "infantry-1",
		Action:       "attack",
		TargetNodeID: "A1",
	}
	return &Definition{
		Name:      "capital_destroy_gameover",
		Catalog:   catalog,
		State:     state,
		PlayerIDs: []string{"player-1"},
		Usernames: []string{"alice"},
	}, nil
}

func PVESkirmish() (*Definition, error) {
	rules := baseRules()
	rules.MaxTurns = 6
	catalog := staticdata.NewCatalog(staticdata.CatalogBundle{
		Manifest: manifest("pve_skirmish"),
		Rules:    rules,
		Units: []staticdata.UnitDefinition{
			infantryDefinition(),
		},
		Buildings: []staticdata.BuildingDefinition{
			{
				ID:            "city_core",
				PlacementKind: "city_foundation_center",
				BuildingScope: "city_core",
				MaxHP:         30,
				TakeoverMode:  "disabled",
			},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
	}, pveSkirmishMap("pve_skirmish"))

	playerIDs := []string{"player-1", "bot-1"}
	usernames := []string{"alice", "pve"}
	state, err := newState("pve_skirmish", catalog, playerIDs, usernames, pveSkirmishMap("pve_skirmish"))
	if err != nil {
		return nil, err
	}
	placeUnitWithID(state, "infantry", "bot-1", domain.Position{Q: 1, R: 0}, "bot-infantry-1")

	return &Definition{
		Name:      "pve_skirmish",
		Catalog:   catalog,
		State:     state,
		PlayerIDs: playerIDs,
		Usernames: usernames,
		Participants: []participant.Spec{
			{ID: "player-1", Username: "alice", Kind: participant.KindHuman},
			{ID: "bot-1", Username: "pve", Kind: participant.KindBot},
		},
	}, nil
}

func PVESoak() (*Definition, error) {
	rules := baseRules()
	rules.MaxTurns = 64
	catalog := staticdata.NewCatalog(staticdata.CatalogBundle{
		Manifest: manifest("pve_soak"),
		Rules:    rules,
		Buildings: []staticdata.BuildingDefinition{
			{
				ID:            "city_core",
				PlacementKind: "city_foundation_center",
				BuildingScope: "city_core",
				MaxHP:         100,
				TakeoverMode:  "disabled",
			},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
	}, pveSoakMap("pve_soak"))

	playerIDs := []string{"player-1", "bot-1"}
	usernames := []string{"alice", "pve"}
	state, err := newState("pve_soak", catalog, playerIDs, usernames, pveSoakMap("pve_soak"))
	if err != nil {
		return nil, err
	}

	return &Definition{
		Name:      "pve_soak",
		Catalog:   catalog,
		State:     state,
		PlayerIDs: playerIDs,
		Usernames: usernames,
		Participants: []participant.Spec{
			{ID: "player-1", Username: "alice", Kind: participant.KindHuman},
			{ID: "bot-1", Username: "pve", Kind: participant.KindBot},
		},
	}, nil
}
