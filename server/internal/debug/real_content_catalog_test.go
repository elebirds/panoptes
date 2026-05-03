package debug

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/engine/economy"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func TestRealContentCatalogSupportsExpandedMVPContent(t *testing.T) {
	catalog := loadRealContentCatalog(t)

	defaultMap, ok := catalog.GetMap("default")
	if !ok {
		t.Fatalf("default map missing")
	}
	if defaultMap.Width != 24 || defaultMap.Height != 24 {
		t.Fatalf("default map size = %dx%d, want 24x24", defaultMap.Width, defaultMap.Height)
	}
	resourcePoints := 0
	for _, node := range defaultMap.Nodes {
		if node.IsResourcePoint {
			resourcePoints++
		}
	}
	if resourcePoints != 12 {
		t.Fatalf("default map resource points = %d, want 12", resourcePoints)
	}

	duelLarge, ok := catalog.GetMap("duel_large")
	if !ok {
		t.Fatalf("duel_large map missing")
	}
	if duelLarge.Width != 36 || duelLarge.Height != 36 {
		t.Fatalf("duel_large size = %dx%d, want 36x36", duelLarge.Width, duelLarge.Height)
	}
	frontierBasin, ok := catalog.GetMap("frontier_basin")
	if !ok {
		t.Fatalf("frontier_basin map missing")
	}
	if frontierBasin.Width != 30 || frontierBasin.Height != 30 {
		t.Fatalf("frontier_basin size = %dx%d, want 30x30", frontierBasin.Width, frontierBasin.Height)
	}
	frontierResources := 0
	frontierRoadNodes := 0
	for _, node := range frontierBasin.Nodes {
		if node.IsResourcePoint {
			frontierResources++
		}
		if node.HasRoad {
			frontierRoadNodes++
		}
	}
	if frontierResources != 12 || frontierRoadNodes != 6 {
		t.Fatalf("frontier_basin resources=%d roads=%d, want 12 resources and 6 road nodes", frontierResources, frontierRoadNodes)
	}

	workshop, ok := catalog.GetBuilding("workshop")
	if !ok {
		t.Fatalf("workshop missing")
	}
	if workshop.DefaultRecipeID != "" || len(workshop.ModifierEffects) != 1 || workshop.ModifierEffects[0].PointKey != "industry_output" {
		t.Fatalf("workshop = %#v, want industry modifier and no default recipe", workshop)
	}

	cityCore, ok := catalog.GetBuilding("city_core")
	if !ok {
		t.Fatalf("city_core missing")
	}
	if cityCore.DefaultRecipeID != "city_core_provisions" || len(cityCore.RecipeIDs) != 1 || cityCore.RecipeIDs[0] != "city_core_provisions" {
		t.Fatalf("city_core recipes = %#v, want city_core_provisions as default startup recipe", cityCore)
	}
	if len(cityCore.ModifierEffects) != 1 || cityCore.ModifierEffects[0].Trigger != "point.output" || cityCore.ModifierEffects[0].PointKey != "research_output" || cityCore.ModifierEffects[0].ModifierType != "flat" || cityCore.ModifierEffects[0].Value != 1 {
		t.Fatalf("city_core modifier_effects = %#v, want flat +1 research_output", cityCore.ModifierEffects)
	}

	provisionsRecipe, ok := catalog.GetRecipe("city_core_provisions")
	if !ok {
		t.Fatalf("city_core_provisions missing")
	}
	if provisionsRecipe.BuildingID != "city_core" {
		t.Fatalf("city_core_provisions building_id = %q, want city_core", provisionsRecipe.BuildingID)
	}
	if provisionsRecipe.Outputs.Resources["food"] != 1 || len(provisionsRecipe.PointInputs) != 0 {
		t.Fatalf("city_core_provisions = %#v, want free +1 food startup output", provisionsRecipe)
	}

	frontierOffice, ok := catalog.GetBuilding("frontier_office")
	if !ok {
		t.Fatalf("frontier_office missing")
	}
	if frontierOffice.DefaultRecipeID != "city_core_settler" {
		t.Fatalf("frontier_office default recipe = %q, want city_core_settler", frontierOffice.DefaultRecipeID)
	}

	settlerRecipe, ok := catalog.GetRecipe("city_core_settler")
	if !ok {
		t.Fatalf("city_core_settler missing")
	}
	if settlerRecipe.BuildingID != "frontier_office" {
		t.Fatalf("city_core_settler building_id = %q, want frontier_office", settlerRecipe.BuildingID)
	}

	archery, ok := catalog.GetBuilding("archery")
	if !ok {
		t.Fatalf("archery missing")
	}
	if archery.DefaultRecipeID != "archery_archer" {
		t.Fatalf("archery default recipe = %q, want archery_archer", archery.DefaultRecipeID)
	}

	archer, ok := catalog.GetUnit("archer")
	if !ok {
		t.Fatalf("archer missing")
	}
	if archer.Class != "ranged" || archer.AttackRange != 2 || archer.Flags.CanAttackStructures {
		t.Fatalf("archer = %#v, want ranged non-structure attacker", archer)
	}

	fortifications, ok := catalog.GetTechnology("fortifications")
	if !ok {
		t.Fatalf("fortifications missing")
	}
	if len(fortifications.Prerequisites) != 1 || fortifications.Prerequisites[0].TargetID != "militia_mobilization" {
		t.Fatalf("fortifications prerequisites = %#v, want militia_mobilization", fortifications.Prerequisites)
	}

	reorganization, ok := catalog.GetPolicy("reorganization")
	if !ok {
		t.Fatalf("reorganization missing")
	}
	if len(reorganization.ModifierEffects) != 1 || reorganization.ModifierEffects[0].PointKey != "industry_output" {
		t.Fatalf("reorganization = %#v, want industry output modifier", reorganization)
	}

	for _, tc := range []struct {
		buildingID string
		recipeID   string
	}{
		{buildingID: "granary", recipeID: "granary_rations"},
		{buildingID: "smelter", recipeID: "smelter_refined_ore"},
		{buildingID: "stable", recipeID: "stable_cavalry"},
		{buildingID: "engineer_camp", recipeID: "engineer_camp_raider"},
	} {
		building, ok := catalog.GetBuilding(tc.buildingID)
		if !ok {
			t.Fatalf("%s missing", tc.buildingID)
		}
		if building.DefaultRecipeID != tc.recipeID {
			t.Fatalf("%s default recipe = %q, want %q", tc.buildingID, building.DefaultRecipeID, tc.recipeID)
		}
		if _, ok := catalog.GetRecipe(tc.recipeID); !ok {
			t.Fatalf("%s missing", tc.recipeID)
		}
	}

	cavalry, ok := catalog.GetUnit("cavalry")
	if !ok {
		t.Fatalf("cavalry missing")
	}
	if cavalry.Class != "mobile" || cavalry.ChargeBonus <= 0 || cavalry.MoveRange < 5 {
		t.Fatalf("cavalry = %#v, want mobile charge unit", cavalry)
	}
	staticdata.SetDefault(catalog)
	cavalryWorld := donburi.NewWorld()
	cavalryEntry := cavalryWorld.Entry(ecs.CreateUnit(cavalryWorld, "cavalry", "player-1", domain.Position{}))
	if !cavalryEntry.HasComponent(ecs.ChargeAbilityC) {
		t.Fatalf("cavalry should receive ChargeAbilityC from authored content")
	}

	for _, techID := range []string{"supply_depots", "metallurgy", "mounted_logistics", "siegecraft"} {
		if _, ok := catalog.GetTechnology(techID); !ok {
			t.Fatalf("%s technology missing", techID)
		}
	}
	mountedLogistics, _ := catalog.GetTechnology("mounted_logistics")
	if len(mountedLogistics.ExplicitEffects) != 2 || mountedLogistics.ExplicitEffects[0].TargetID != "stable" || mountedLogistics.ExplicitEffects[1].TargetID != "stable_cavalry" {
		t.Fatalf("mounted_logistics explicit effects = %#v, want stable unlocks", mountedLogistics.ExplicitEffects)
	}
	foundryDirectives, ok := catalog.GetPolicy("foundry_directives")
	if !ok {
		t.Fatalf("foundry_directives missing")
	}
	if foundryDirectives.Layer != "institutional" || len(foundryDirectives.LogisticsPriority) == 0 {
		t.Fatalf("foundry_directives = %#v, want institutional logistics policy", foundryDirectives)
	}

	for _, tc := range []struct {
		buildingID string
		recipeID   string
	}{
		{buildingID: "warehouse", recipeID: "warehouse_reserve_rations"},
		{buildingID: "market", recipeID: "market_grain_contracts"},
		{buildingID: "watchtower", recipeID: "watchtower_scout"},
		{buildingID: "training_ground", recipeID: "training_ground_spearman"},
	} {
		building, ok := catalog.GetBuilding(tc.buildingID)
		if !ok {
			t.Fatalf("%s missing", tc.buildingID)
		}
		if building.DefaultRecipeID != tc.recipeID {
			t.Fatalf("%s default recipe = %q, want %q", tc.buildingID, building.DefaultRecipeID, tc.recipeID)
		}
		if _, ok := catalog.GetRecipe(tc.recipeID); !ok {
			t.Fatalf("%s missing", tc.recipeID)
		}
	}
	academy, ok := catalog.GetBuilding("academy")
	if !ok {
		t.Fatalf("academy missing")
	}
	if academy.DefaultRecipeID != "" || len(academy.ModifierEffects) != 1 || academy.ModifierEffects[0].PointKey != "research_output" || academy.ModifierEffects[0].Value != 2 {
		t.Fatalf("academy = %#v, want research output modifier and no default recipe", academy)
	}

	scout, ok := catalog.GetUnit("scout")
	if !ok {
		t.Fatalf("scout missing")
	}
	if scout.Class != "civilian" || scout.VisionRange < 6 || scout.MoveRange < 6 {
		t.Fatalf("scout = %#v, want fast high-vision civilian", scout)
	}
	scoutEntry := cavalryWorld.Entry(ecs.CreateUnit(cavalryWorld, "scout", "player-1", domain.Position{}))
	scoutCaps := ecs.UnitCapabilitiesC.Get(scoutEntry)
	if !scoutCaps.Civilian || scoutCaps.Melee || scoutCaps.Ranged {
		t.Fatalf("scout capabilities = %#v, want pure civilian", scoutCaps)
	}
	spearman, ok := catalog.GetUnit("spearman")
	if !ok {
		t.Fatalf("spearman missing")
	}
	if spearman.Class != "melee" || !spearman.Flags.CanAttackStructures || !spearman.Flags.CanCapture {
		t.Fatalf("spearman = %#v, want capturable melee structure attacker", spearman)
	}

	for _, techID := range []string{"centralized_storage", "trade_levies", "scholastic_bureaucracy", "sentry_networks", "professional_drill"} {
		if _, ok := catalog.GetTechnology(techID); !ok {
			t.Fatalf("%s technology missing", techID)
		}
	}
	tradeLevies, _ := catalog.GetTechnology("trade_levies")
	if len(tradeLevies.ExplicitEffects) != 5 || tradeLevies.ExplicitEffects[0].TargetID != "market" || tradeLevies.ExplicitEffects[4].TargetID != "mercantile_charter" {
		t.Fatalf("trade_levies explicit effects = %#v, want market recipes and mercantile charter", tradeLevies.ExplicitEffects)
	}
	for _, policyID := range []string{"mercantile_charter", "research_mandate"} {
		policy, ok := catalog.GetPolicy(policyID)
		if !ok {
			t.Fatalf("%s missing", policyID)
		}
		if policy.Layer != "institutional" || len(policy.ModifierEffects) == 0 {
			t.Fatalf("%s = %#v, want institutional modifier policy", policyID, policy)
		}
	}
}

func TestRealContentCityCoreProvidesStartupFoodAndGovernance(t *testing.T) {
	catalog := loadRealContentCatalog(t)
	staticdata.SetDefault(catalog)

	world := donburi.NewWorld()
	cityEntity := ecs.CreateNode(world, ecs.MapNode{ID: "C1", Q: 0, R: 0, Terrain: "plain"})
	cityEntry := world.Entry(cityEntity)
	node := ecs.NodeC.Get(cityEntry)
	node.Owner = "player-1"
	node.TerritoryOwner = "player-1"

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{
		ID:        "default",
		NodeIndex: map[string]donburi.Entity{"C1": cityEntity},
	})
	state.World = world
	state.EnsureCityState("player-1", "C1")
	state.Players["player-1"].CapitalCityID = "C1"

	ecs.CreateBuilding(world, "city_core", "player-1", "C1", cityEntry)

	if got := state.EffectiveResearchOutput("player-1"); got != 2 {
		t.Fatalf("research output with one city_core = %d, want 2", got)
	}

	economy.NewRunner().Run(world, state)
	if got := state.Players["player-1"].Resources.Get(domain.ResourceFood); got != 1 {
		t.Fatalf("food after one city_core cycle = %d, want 1", got)
	}
}
