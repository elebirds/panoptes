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
