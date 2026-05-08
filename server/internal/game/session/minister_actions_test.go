package session

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	ministerengine "github.com/elebirds/panoptes/internal/engine/minister"
	"github.com/elebirds/panoptes/internal/game/query"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func TestRuntimeApplyMinisterActionsStagesValidatedBuildAndMoveProposals(t *testing.T) {
	previous := staticdata.Default()
	t.Cleanup(func() {
		staticdata.SetDefault(previous)
	})
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{CityCoreMaxHP: 100},
		Units: []staticdata.UnitDefinition{
			{ID: "infantry", Class: "melee", MaxHP: 30, Attack: 10, AttackRange: 1, MoveRange: 2, VisionRange: 3, Multipliers: map[string]float64{}, Flags: staticdata.UnitFlags{CanAttackStructures: true}},
		},
		Buildings: []staticdata.BuildingDefinition{
			{ID: "city_core", PlacementKind: "city_foundation_center", BuildingScope: "city_core", MaxHP: 100, TakeoverMode: "disabled"},
			{ID: "farm", PlacementKind: "resource_node", BuildingScope: "out_of_city", RequiredResourceType: "food", MaxHP: 15, TakeoverMode: "delayed"},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
	}))

	world := donburi.NewWorld()
	nodeIndex := map[string]donburi.Entity{
		"A1": ecs.CreateNode(world, ecs.MapNode{ID: "A1", Q: 0, R: 0, Terrain: "plain"}),
		"A2": ecs.CreateNode(world, ecs.MapNode{ID: "A2", Q: 1, R: 0, Terrain: "plain", IsResourcePoint: true, ResourceType: "food"}),
	}
	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{
		ID:        "default",
		NodeIndex: nodeIndex,
	})
	state.World = world
	state.NodeIndex = nodeIndex
	state.Players["player-1"].Research.UnlockBuilding("farm")

	coreEntry := state.World.Entry(nodeIndex["A1"])
	ecs.CreateBuilding(world, "city_core", "player-1", "A1", coreEntry)
	resourceEntry := state.World.Entry(nodeIndex["A2"])
	ecs.NodeC.Get(resourceEntry).TerritoryOwner = "player-1"

	unitEntry := state.World.Entry(ecs.CreateUnit(world, "infantry", "player-1", domain.Position{Q: 0, R: 0}))
	ecs.UnitStatsC.Get(unitEntry).ID = "infantry-1"

	player := &capturePlayer{playerID: "player-1", username: "alice"}
	runtime := newTestRuntime("game-1", []*capturePlayer{player}, nil)
	runtime.SetState(state)

	err := runtime.ApplyMinisterActions("player-1", "domestic", []ministerengine.MinisterActionItem{
		{
			Type: "build",
			Params: map[string]any{
				"node_id":       "A2",
				"building_type": "farm",
				"city_id":       "A1",
			},
		},
		{
			Type: "move_units",
			Params: map[string]any{
				"unit_id":     "infantry-1",
				"target_node": "A2",
			},
		},
	})
	if err != nil {
		t.Fatalf("ApplyMinisterActions error = %v", err)
	}

	if got := len(state.TurnRuntime.Planning.BuildOrders); got != 0 {
		t.Fatalf("build orders = %d, want 0 before approval", got)
	}
	builds := state.TurnRuntime.Planning.MinisterDraftsForPlayer("player-1")
	if len(builds) != 2 {
		t.Fatalf("minister drafts = %#v, want 2 proposals", builds)
	}
	for _, draft := range builds {
		if draft.Source != domain.MinisterDraftSourceLLMAction {
			t.Fatalf("draft = %#v, want llm action source", draft)
		}
		if draft.Status != domain.MinisterDraftStatusPending || !draft.Available {
			t.Fatalf("draft = %#v, want pending and available", draft)
		}
	}
	var syncMsg *pb.MsgGameSync
	for i := len(player.sent) - 1; i >= 0; i-- {
		if msg, ok := player.sent[i].(*pb.MsgGameSync); ok {
			syncMsg = msg
			break
		}
	}
	if syncMsg == nil {
		t.Fatalf("game sync message missing")
	}
	if got := len(syncMsg.GetMinisterProposals()); got != 2 {
		t.Fatalf("minister proposals = %d, want 2", got)
	}
	proposals := query.BuildMinisterProposalViews(state, "player-1")
	if len(proposals) != 2 {
		t.Fatalf("proposal views = %d, want 2", len(proposals))
	}
	if got := len(state.TurnRuntime.Resolving.ActiveMarches); got != 0 {
		t.Fatalf("active marches = %d, want 0 before approval", got)
	}
	if got := len(state.TurnRuntime.Planning.UnitOrders); got != 0 {
		t.Fatalf("unit orders = %d, want 0 before approval", got)
	}
}

func TestRuntimeApplyMinisterActionsStagesExpandedPlanningProposals(t *testing.T) {
	previous := staticdata.Default()
	t.Cleanup(func() {
		staticdata.SetDefault(previous)
	})
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{CityCoreMaxHP: 100},
		Technologies: []staticdata.TechnologyDefinition{
			{ID: "bronze_working", Name: "Bronze Working", ResearchCost: 2},
		},
		Policies: []staticdata.PolicyDefinition{
			{ID: "expansion", Name: "Expansion", Layer: "national"},
			{ID: "academy_charter", Name: "Academy Charter", Layer: "institutional"},
		},
		Units: []staticdata.UnitDefinition{
			{ID: "infantry", Class: "melee", MaxHP: 30, Attack: 10, AttackRange: 1, MoveRange: 2, VisionRange: 3, Multipliers: map[string]float64{}},
		},
		Buildings: []staticdata.BuildingDefinition{
			{ID: "city_core", Name: "City Core", PlacementKind: "city_foundation_center", BuildingScope: "city_core", RecipeIDs: []string{"train_settler"}, MaxHP: 100, TakeoverMode: "disabled"},
		},
		Recipes: []staticdata.RecipeDefinition{
			{ID: "train_settler", Name: "Train Settler", BuildingID: "city_core"},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
	}))

	world := donburi.NewWorld()
	nodeIndex := map[string]donburi.Entity{
		"A1": ecs.CreateNode(world, ecs.MapNode{ID: "A1", Q: 0, R: 0, Terrain: "plain"}),
	}
	state := domain.NewGameState("game-expanded-actions", []string{"player-1"}, []string{"alice"}, &domain.MapData{
		ID:        "default",
		NodeIndex: nodeIndex,
	})
	state.World = world
	state.NodeIndex = nodeIndex
	state.Players["player-1"].Research.UnlockRecipe("train_settler")
	state.Players["player-1"].Institutions.SlotCount = 1
	state.Players["player-1"].Institutions.UnlockCandidate("academy_charter")

	coreEntry := state.World.Entry(nodeIndex["A1"])
	ecs.CreateBuilding(world, "city_core", "player-1", "A1", coreEntry)
	unitEntry := state.World.Entry(ecs.CreateUnit(world, "infantry", "player-1", domain.Position{Q: 0, R: 0}))
	ecs.UnitStatsC.Get(unitEntry).ID = "infantry-1"

	player := &capturePlayer{playerID: "player-1", username: "alice"}
	runtime := newTestRuntime("game-expanded-actions", []*capturePlayer{player}, nil)
	runtime.SetState(state)

	err := runtime.ApplyMinisterActions("player-1", "domestic", []ministerengine.MinisterActionItem{
		{Type: "set_research", Params: map[string]any{"technology_id": "bronze_working"}},
		{Type: "set_policy", Params: map[string]any{"policy_id": "expansion"}},
		{Type: "set_institution_loadout", Params: map[string]any{"policy_ids": []any{"academy_charter"}}},
		{Type: "set_building_recipe", Params: map[string]any{"node_id": "A1", "recipe_id": "train_settler"}},
		{Type: "unit_order", Params: map[string]any{"unit_id": "infantry-1", "action": "hold"}},
	})
	if err != nil {
		t.Fatalf("ApplyMinisterActions error = %v", err)
	}

	drafts := state.TurnRuntime.Planning.MinisterDraftsForPlayer("player-1")
	if len(drafts) != 5 {
		t.Fatalf("minister drafts = %#v, want 5 expanded proposals", drafts)
	}
	seen := make(map[domain.MinisterDraftKind]bool, len(drafts))
	for _, draft := range drafts {
		seen[draft.Kind] = true
		if draft.Source != domain.MinisterDraftSourceLLMAction {
			t.Fatalf("draft = %#v, want llm action source", draft)
		}
		if draft.Status != domain.MinisterDraftStatusPending || !draft.Available {
			t.Fatalf("draft = %#v, want pending available proposal", draft)
		}
	}
	for _, want := range []domain.MinisterDraftKind{
		domain.MinisterDraftKindResearch,
		domain.MinisterDraftKindPolicy,
		domain.MinisterDraftKindInstitution,
		domain.MinisterDraftKindRecipe,
		domain.MinisterDraftKindUnitOrder,
	} {
		if !seen[want] {
			t.Fatalf("draft kinds = %#v, missing %q", seen, want)
		}
	}
	if got := state.TurnRuntime.Planning.PendingResearchTarget("player-1"); got != "" {
		t.Fatalf("pending research = %q, want empty before approval", got)
	}
	if got := state.TurnRuntime.Planning.PendingPolicy("player-1"); got != "" {
		t.Fatalf("pending policy = %q, want empty before approval", got)
	}
	if got := len(state.TurnRuntime.Planning.PendingInstitutionLoadout("player-1")); got != 0 {
		t.Fatalf("pending institution loadout count = %d, want 0 before approval", got)
	}
	if got := len(state.TurnRuntime.Planning.RecipeSelections); got != 0 {
		t.Fatalf("recipe selections = %d, want 0 before approval", got)
	}
	if got := len(state.TurnRuntime.Planning.UnitOrders); got != 0 {
		t.Fatalf("unit orders = %d, want 0 before approval", got)
	}
	if got := len(query.BuildMinisterProposalViews(state, "player-1")); got != 5 {
		t.Fatalf("proposal views = %d, want 5", got)
	}
}
