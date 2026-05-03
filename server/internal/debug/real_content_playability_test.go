package debug

import (
	"testing"
	"time"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/engine/economy"
	"github.com/elebirds/panoptes/internal/engine/maploader"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func TestRealContentD2PlayabilityChainCoversAuthoredExpansion(t *testing.T) {
	state := newRealContentD2PlayabilityState(t)
	playerID := "player-1"
	cityID := "A2"

	baseRoadCapacity := state.EffectiveRoadBaseCapacity(playerID)
	placeRealContentBuilding(t, state, "warehouse", playerID, cityID, "B2")
	if got := state.EffectiveRoadBaseCapacity(playerID); got <= baseRoadCapacity {
		t.Fatalf("warehouse road capacity = %d, want > base %d", got, baseRoadCapacity)
	}

	baseResearch := state.EffectiveResearchOutput(playerID)
	placeRealContentBuilding(t, state, "academy", playerID, cityID, "C2")
	if got := state.EffectiveResearchOutput(playerID); got < baseResearch+2 {
		t.Fatalf("academy research output = %d, want at least %d", got, baseResearch+2)
	}

	placeRealContentBuilding(t, state, "market", playerID, cityID, "D2")
	placeRealContentBuilding(t, state, "watchtower", playerID, cityID, "E2")
	placeRealContentBuilding(t, state, "training_ground", playerID, cityID, "F2")
	state.AddResourcesToCity(playerID, cityID, domain.ResourceBag{
		domain.ResourceFood: 20,
		domain.ResourceWood: 20,
		domain.ResourceOre:  20,
	})
	unlockRealContent(t, state, playerID, []string{
		"warehouse_reserve_rations",
		"market_ore_contracts",
		"watchtower_scout",
		"training_ground_spearman",
	})
	clearSelectedRecipes(t, state, []string{"A2", "B2", "C2", "D2", "E2", "F2"})

	foodBefore := state.CityStorage(playerID, cityID).Get(domain.ResourceFood)
	selectRealContentRecipe(t, state, "B2", "warehouse_reserve_rations")
	runSelectedRecipeToCompletion(t, state, "B2")
	clearSelectedRecipe(t, state, "B2")
	if got := state.CityStorage(playerID, cityID).Get(domain.ResourceFood); got <= foodBefore {
		t.Fatalf("warehouse reserve rations food = %d, want > %d", got, foodBefore)
	}

	oreBefore := state.CityStorage(playerID, cityID).Get(domain.ResourceOre)
	selectRealContentRecipe(t, state, "D2", "market_ore_contracts")
	runSelectedRecipeToCompletion(t, state, "D2")
	clearSelectedRecipe(t, state, "D2")
	if got := state.CityStorage(playerID, cityID).Get(domain.ResourceOre); got <= oreBefore {
		t.Fatalf("market ore contracts ore = %d, want > %d", got, oreBefore)
	}

	selectRealContentRecipe(t, state, "E2", "watchtower_scout")
	runSelectedRecipeToCompletion(t, state, "E2")
	clearSelectedRecipe(t, state, "E2")
	scoutID := findOwnedUnitIDByType(t, state, playerID, "scout")
	assertUnitAtNode(t, state, scoutID, "E2")

	selectRealContentRecipe(t, state, "F2", "training_ground_spearman")
	runSelectedRecipeToCompletion(t, state, "F2")
	clearSelectedRecipe(t, state, "F2")
	spearmanID := findOwnedUnitIDByType(t, state, playerID, "spearman")
	assertUnitAtNode(t, state, spearmanID, "F2")
	AssertStateInvariants(t, state)
}

func TestHarnessD2RealContentFrontierBasinPVESoakPreservesStateInvariants(t *testing.T) {
	def := newRealContentFrontierBasinPVESoakDefinition(t)
	h, err := NewHarness(def)
	if err != nil {
		t.Fatalf("NewHarness() error = %v", err)
	}
	if err := h.Start(); err != nil {
		t.Fatalf("Start() error = %v", err)
	}
	if !h.room.HasParticipant("bot-1") {
		t.Fatalf("room should include bot-1 participant")
	}
	if h.room.IsHumanParticipant("bot-1") {
		t.Fatalf("bot-1 should be autonomous, not human")
	}

	AssertStateInvariants(t, h.room.State())
	completedTurns := 0
	for turn := 1; turn <= 50; turn++ {
		start, err := h.WaitPlanningStart("player-1", turn, 2*time.Second)
		if err != nil {
			t.Fatalf("WaitPlanningStart(turn=%d) error = %v", turn, err)
		}
		if start.GetInformationReport() == nil {
			t.Fatalf("turn %d planning start missing information report", turn)
		}
		AssertStateInvariants(t, h.room.State())

		if err := h.SubmitTurn("player-1"); err != nil {
			t.Fatalf("SubmitTurn(turn=%d) error = %v", turn, err)
		}
		record, err := h.WaitGameSync("player-1", turn, 3*time.Second)
		if err != nil {
			t.Fatalf("WaitGameSync(turn=%d) error = %v", turn, err)
		}
		if record.GameSync.GetInformationReport() == nil {
			t.Fatalf("turn %d game sync missing information report", turn)
		}
		AssertStateInvariants(t, h.room.State())
		completedTurns = turn
		if record.Summary.IsOver {
			if record.Summary.OverReason == "" {
				t.Fatalf("real content soak ended at turn %d without over reason", turn)
			}
			break
		}
	}
	if completedTurns < staticdata.Default().Rules().MaxTurns && completedTurns < 50 {
		t.Fatalf("real content soak completed %d turns, want 50 or configured game over at max turn %d", completedTurns, staticdata.Default().Rules().MaxTurns)
	}
}

func newRealContentD2PlayabilityState(t *testing.T) *domain.GameState {
	t.Helper()

	catalog := loadRealContentCatalog(t)
	staticdata.SetDefault(catalog)

	playerIDs := []string{"player-1"}
	usernames := []string{"alice"}
	world := donburi.NewWorld()
	mapData := maploader.InitWorldFromMap(world, realContentD2PlayabilityMap(), playerIDs)
	state := domain.NewGameState("real-content-d2-playability", playerIDs, usernames, mapData)
	state.World = world
	player := state.Players["player-1"]
	player.CapitalCityID = "A2"
	state.EnsureCityState("player-1", "A2")
	return state
}

func realContentD2PlayabilityMap() *staticdata.MapRuntimeBundle {
	zero := 0
	nodes := realContentAllPlainNodes(7, 3)
	setNodeNodeConfig(nodes, "A2", func(node *staticdata.MapRuntimeNode) {
		node.OwnerSlot = &zero
		node.TerritoryOwnerSlot = &zero
		node.BuildingType = "city_core"
	})
	return &staticdata.MapRuntimeBundle{
		ID:     "real-content-d2-playability",
		Name:   "Real Content D2 Playability",
		Width:  7,
		Height: 3,
		SpawnPoints: []staticdata.SpawnPoint{
			{Slot: 0, X: 0, Y: 1},
		},
		Nodes: nodes,
	}
}

func placeRealContentBuilding(t *testing.T, state *domain.GameState, buildingType string, owner string, cityID string, nodeID string) {
	t.Helper()

	entry, ok := state.GetNode(nodeID)
	if !ok {
		t.Fatalf("missing node %s", nodeID)
	}
	ecs.CreateBuilding(state.World, buildingType, owner, cityID, entry)
	node := ecs.NodeC.Get(entry)
	node.Owner = owner
	node.TerritoryOwner = owner
}

func unlockRealContent(t *testing.T, state *domain.GameState, playerID string, recipeIDs []string) {
	t.Helper()

	player := state.Players[playerID]
	if player == nil {
		t.Fatalf("missing player %s", playerID)
	}
	for _, recipeID := range recipeIDs {
		recipe, ok := staticdata.Default().GetRecipe(recipeID)
		if !ok {
			t.Fatalf("missing recipe %s", recipeID)
		}
		player.Research.UnlockRecipe(recipeID)
		player.Research.UnlockBuilding(recipe.BuildingID)
	}
}

func clearSelectedRecipes(t *testing.T, state *domain.GameState, nodeIDs []string) {
	t.Helper()

	for _, nodeID := range nodeIDs {
		clearSelectedRecipe(t, state, nodeID)
	}
}

func selectRealContentRecipe(t *testing.T, state *domain.GameState, nodeID string, recipeID string) {
	t.Helper()

	recipe, ok := staticdata.Default().GetRecipe(recipeID)
	if !ok {
		t.Fatalf("missing recipe %s", recipeID)
	}
	entry, ok := state.GetNode(nodeID)
	if !ok {
		t.Fatalf("missing node %s", nodeID)
	}
	if !entry.HasComponent(ecs.BuildingOperationC) {
		entry.AddComponent(ecs.BuildingOperationC)
	}
	ecs.BuildingOperationC.SetValue(entry, ecs.BuildingOperationComp{
		SelectedRecipeID:  recipeID,
		RequiredTurns:     recipe.WorkAmount,
		ConsumedResources: domain.NewResourceBag(),
		ConsumedPoints:    domain.NewPointBag(),
	})
}

func runSelectedRecipeToCompletion(t *testing.T, state *domain.GameState, nodeID string) {
	t.Helper()

	for attempt := 1; attempt <= 8; attempt++ {
		events := economy.NewRunner().Run(state.World, state)
		AssertStateInvariants(t, state)
		for _, evt := range events {
			completed, ok := evt.(event.RecipeCompletedEvent)
			if ok && completed.NodeID == nodeID {
				return
			}
		}
		state.Turn++
	}
	t.Fatalf("recipe at node %s did not complete", nodeID)
}
