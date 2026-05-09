package session

import (
	"strings"
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	ministerengine "github.com/elebirds/panoptes/internal/engine/minister"
	gameorders "github.com/elebirds/panoptes/internal/game/orders"
	"github.com/elebirds/panoptes/internal/game/query"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func TestRuntimeApplyMinisterActionsStagesValidatedBuildProposal(t *testing.T) {
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
			Title:     "农田营建",
			Summary:   "建议在A2修建农田，先补稳粮食来源。",
			Rationale: "粮食节点已经纳入控制，尽早开发能支撑后续扩张。",
			RiskNote:  "若本回合需要保留劳力机动，可暂缓批准。",
		},
	})
	if err != nil {
		t.Fatalf("ApplyMinisterActions error = %v", err)
	}

	if got := len(state.TurnRuntime.Planning.BuildOrders); got != 0 {
		t.Fatalf("build orders = %d, want 0 before approval", got)
	}
	builds := state.TurnRuntime.Planning.MinisterDraftsForPlayer("player-1")
	if len(builds) != 1 {
		t.Fatalf("minister drafts = %#v, want 1 proposal", builds)
	}
	for _, draft := range builds {
		if draft.Source != domain.MinisterDraftSourceLLMAction {
			t.Fatalf("draft = %#v, want llm action source", draft)
		}
		if draft.Status != domain.MinisterDraftStatusPending || !draft.Available {
			t.Fatalf("draft = %#v, want pending and available", draft)
		}
		if draft.Title != "农田营建" || draft.Summary != "建议在A2修建农田，先补稳粮食来源。" {
			t.Fatalf("draft visible copy = %#v, want LLM action proposal copy", draft)
		}
		if containsInternalProposalCopy(draft) {
			t.Fatalf("draft visible copy leaks internal rule-planner text: %#v", draft)
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
	if got := len(syncMsg.GetMinisterProposals()); got != 1 {
		t.Fatalf("minister proposals = %d, want 1", got)
	}
	proposals := query.BuildMinisterProposalViews(state, "player-1")
	if len(proposals) != 1 {
		t.Fatalf("proposal views = %d, want 1", len(proposals))
	}
	if got := len(state.TurnRuntime.Resolving.ActiveMarches); got != 0 {
		t.Fatalf("active marches = %d, want 0 before approval", got)
	}
	if got := len(state.TurnRuntime.Planning.UnitOrders); got != 0 {
		t.Fatalf("unit orders = %d, want 0 before approval", got)
	}
}

func TestRuntimeApplyMinisterActionsStagesLLMProposalForNextTurn(t *testing.T) {
	previous := staticdata.Default()
	t.Cleanup(func() {
		staticdata.SetDefault(previous)
	})
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{CityCoreMaxHP: 100, BaseResearchOutputPerTurn: 1},
		Technologies: []staticdata.TechnologyDefinition{
			{ID: "bronze_working", Name: "Bronze Working", ResearchCost: 2},
		},
	}))

	state := domain.NewGameState("game-llm-turn", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	state.Turn = 5
	state.Phase = domain.PhaseResolving.String()

	player := &capturePlayer{playerID: "player-1", username: "alice"}
	runtime := newTestRuntime("game-llm-turn", []*capturePlayer{player}, nil)
	runtime.SetState(state)

	err := runtime.ApplyMinisterActions("player-1", "domestic", []ministerengine.MinisterActionItem{
		{Type: "set_research", Params: map[string]any{"technology_id": "bronze_working"}},
	})
	if err != nil {
		t.Fatalf("ApplyMinisterActions error = %v", err)
	}

	drafts := state.TurnRuntime.Planning.MinisterDraftsForPlayer("player-1")
	if len(drafts) != 1 {
		t.Fatalf("minister drafts = %#v, want 1", drafts)
	}
	if drafts[0].Turn != 6 {
		t.Fatalf("draft turn = %d, want 6 for next planning phase", drafts[0].Turn)
	}
	if !strings.HasSuffix(drafts[0].DraftID, ":6") {
		t.Fatalf("draft id = %q, want next-turn suffix", drafts[0].DraftID)
	}
	if got := state.TurnRuntime.Planning.PendingResearchTarget("player-1"); got != "" {
		t.Fatalf("pending research target = %q, want empty before approval", got)
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
		},
		InstitutionCategories: []staticdata.InstitutionCategoryDefinition{
			{ID: "administration", Name: "Administration"},
		},
		Institutions: []staticdata.InstitutionDefinition{
			{ID: "academy_charter", Name: "Academy Charter", Category: "administration", ActivationTiming: "next_turn"},
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
		{Type: "set_institution_loadout", Params: map[string]any{"institution_ids": []any{"academy_charter"}}},
		{Type: "set_building_recipe", Params: map[string]any{"node_id": "A1", "recipe_id": "train_settler"}},
	})
	if err != nil {
		t.Fatalf("ApplyMinisterActions error = %v", err)
	}

	drafts := state.TurnRuntime.Planning.MinisterDraftsForPlayer("player-1")
	if len(drafts) != 4 {
		t.Fatalf("minister drafts = %#v, want 4 expanded proposals", drafts)
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
		if containsInternalProposalCopy(draft) {
			t.Fatalf("draft visible copy leaks internal rule-planner text: %#v", draft)
		}
	}
	for _, want := range []domain.MinisterDraftKind{
		domain.MinisterDraftKindResearch,
		domain.MinisterDraftKindPolicy,
		domain.MinisterDraftKindInstitution,
		domain.MinisterDraftKindRecipe,
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
	if got := len(query.BuildMinisterProposalViews(state, "player-1")); got != 4 {
		t.Fatalf("proposal views = %d, want 4", got)
	}
}

func TestBuildMinisterDraftsFromLegalCandidatesReservesUnitsAcrossCommandOperations(t *testing.T) {
	previous := staticdata.Default()
	t.Cleanup(func() {
		staticdata.SetDefault(previous)
	})
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Units: []staticdata.UnitDefinition{
			{ID: "infantry", Class: "melee", MaxHP: 30, Attack: 10, AttackRange: 1, MoveRange: 2, VisionRange: 3, Multipliers: map[string]float64{}},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
	}))

	world := donburi.NewWorld()
	nodeIndex := map[string]donburi.Entity{
		"A1": ecs.CreateNode(world, ecs.MapNode{ID: "A1", Q: 0, R: 0, Terrain: "plain"}),
		"B1": ecs.CreateNode(world, ecs.MapNode{ID: "B1", Q: 1, R: 0, Terrain: "plain"}),
		"B2": ecs.CreateNode(world, ecs.MapNode{ID: "B2", Q: 0, R: 1, Terrain: "plain"}),
	}
	state := domain.NewGameState("game-command-reserve", []string{"player-1"}, []string{"alice"}, &domain.MapData{
		ID:        "default",
		NodeIndex: nodeIndex,
	})
	state.World = world
	state.NodeIndex = nodeIndex
	unitEntry := state.World.Entry(ecs.CreateUnit(world, "infantry", "player-1", domain.Position{Q: 0, R: 0}))
	ecs.UnitStatsC.Get(unitEntry).ID = "u1"

	observation := &query.ObservationSnapshot{
		ViewerID: "player-1",
		VisibleNodes: []*pb.NodeView{
			{Id: "B1", ControllerPlayerId: "enemy-1", TerritoryOwnerPlayerId: "enemy-1", EnemyUnitCount: 1},
			{Id: "B2", ControllerPlayerId: "enemy-1", TerritoryOwnerPlayerId: "enemy-1", EnemyUnitCount: 1},
		},
		Units: []*pb.UnitView{
			{Id: "u1", Faction: "player-1", UnitType: "infantry"},
		},
	}

	drafts := buildMinisterDraftsFromLegalCandidates(7, "player-1", state, observation)
	commandOps := make([]domain.MinisterDraft, 0)
	for _, draft := range drafts {
		if draft.MinisterRole == commandMinisterRole && draft.Kind == domain.MinisterDraftKindOperation {
			commandOps = append(commandOps, draft)
		}
	}
	if len(commandOps) != 1 {
		t.Fatalf("command operations = %#v, want 1 reserved proposal", commandOps)
	}
	if len(commandOps[0].OperationSteps) != 1 || commandOps[0].OperationSteps[0].Action != string(gameorders.ActionMove) {
		t.Fatalf("command operation = %#v, want one move step", commandOps[0])
	}
}

func TestRuntimeApplyMinisterActionsIgnoresUnitActionCompatibilityTypes(t *testing.T) {
	state := domain.NewGameState("game-unsupported-actions", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	player := &capturePlayer{playerID: "player-1", username: "alice"}
	runtime := newTestRuntime("game-unsupported-actions", []*capturePlayer{player}, nil)
	runtime.SetState(state)

	err := runtime.ApplyMinisterActions("player-1", "military", []ministerengine.MinisterActionItem{
		{Type: "move_units", Params: map[string]any{"unit_id": "u1", "target_node": "A2"}},
		{Type: "unit_order", Params: map[string]any{"unit_id": "u1", "action": "hold"}},
		{Type: "issue_unit_order", Params: map[string]any{"unit_id": "u1", "action": "hold"}},
	})
	if err != nil {
		t.Fatalf("ApplyMinisterActions error = %v", err)
	}
	if got := len(state.TurnRuntime.Planning.MinisterDraftsForPlayer("player-1")); got != 0 {
		t.Fatalf("minister drafts = %d, want 0 for unsupported unit action compatibility types", got)
	}
	if len(player.sent) != 0 {
		t.Fatalf("sent messages = %d, want none", len(player.sent))
	}
}

func TestRuntimeApplyMinisterActionsSelectsRuleCandidateWithoutExposingOtherHiddenCandidates(t *testing.T) {
	state := domain.NewGameState("game-candidate-selection", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	state.Turn = 4
	player := &capturePlayer{playerID: "player-1", username: "alice"}
	runtime := newTestRuntime("game-candidate-selection", []*capturePlayer{player}, nil)
	runtime.SetState(state)
	runtime.preparedMinisterDraftsMu.Lock()
	runtime.preparedMinisterDrafts[4] = map[string][]domain.MinisterDraft{
		"player-1": {
			{
				DraftID:      "domestic:research:bronze_working:4",
				PlayerID:     "player-1",
				MinisterRole: "domestic",
				Kind:         domain.MinisterDraftKindResearch,
				TargetID:     "bronze_working",
				TargetLabel:  "Bronze Working",
				Status:       domain.MinisterDraftStatusPending,
				Available:    true,
				Turn:         4,
				Source:       domain.MinisterDraftSourceRuleOnly,
			},
			{
				DraftID:      "domestic:policy:expansion:4",
				PlayerID:     "player-1",
				MinisterRole: "domestic",
				Kind:         domain.MinisterDraftKindPolicy,
				TargetID:     "expansion",
				TargetLabel:  "Expansion",
				Status:       domain.MinisterDraftStatusPending,
				Available:    true,
				Turn:         4,
				Source:       domain.MinisterDraftSourceRuleOnly,
			},
			{
				DraftID:      "military:operation:secure_a2:4",
				PlayerID:     "player-1",
				MinisterRole: "military",
				Kind:         domain.MinisterDraftKindOperation,
				TargetID:     "secure_a2",
				TargetLabel:  "控制或侦察 A2",
				Status:       domain.MinisterDraftStatusPending,
				Available:    true,
				Turn:         4,
				Source:       domain.MinisterDraftSourceRuleOnly,
			},
		},
	}
	runtime.preparedMinisterDraftsMu.Unlock()

	err := runtime.ApplyMinisterActions("player-1", "domestic", []ministerengine.MinisterActionItem{
		{
			Type:      "select_candidate",
			Params:    map[string]any{"draft_id": "domestic:research:bronze_working:4"},
			Title:     "青铜研究",
			Summary:   "建议先推进青铜冶炼，为后续军备和营建打基础。",
			Rationale: "当前没有更紧迫的已知威胁，先补技术根基更稳妥。",
			RiskNote:  "若边境突然吃紧，可改选更偏军事的安排。",
		},
	})
	if err != nil {
		t.Fatalf("ApplyMinisterActions error = %v", err)
	}

	drafts := state.TurnRuntime.Planning.MinisterDraftsForPlayer("player-1")
	byID := make(map[string]domain.MinisterDraft, len(drafts))
	for _, draft := range drafts {
		byID[draft.DraftID] = draft
	}
	selected := byID["domestic:research:bronze_working:4"]
	if selected.Source != domain.MinisterDraftSourceLLMAction || selected.Status != domain.MinisterDraftStatusPending || !selected.Available {
		t.Fatalf("selected draft = %#v, want pending llm_action", selected)
	}
	if selected.Title != "青铜研究" || selected.Summary != "建议先推进青铜冶炼，为后续军备和营建打基础。" {
		t.Fatalf("selected draft visible copy = %#v, want LLM-selected proposal copy", selected)
	}
	if containsInternalProposalCopy(selected) {
		t.Fatalf("selected draft visible copy leaks internal rule-planner text: %#v", selected)
	}
	if _, ok := byID["domestic:policy:expansion:4"]; ok {
		t.Fatalf("unselected domestic candidate should stay hidden, got visible draft %#v", byID["domestic:policy:expansion:4"])
	}
	if _, ok := byID["military:operation:secure_a2:4"]; ok {
		t.Fatalf("other role candidate should stay hidden, got visible draft %#v", byID["military:operation:secure_a2:4"])
	}
	proposals := query.BuildMinisterProposalViews(state, "player-1")
	if len(proposals) != 1 {
		t.Fatalf("proposal views = %d, want only selected candidate proposal", len(proposals))
	}
}

func containsInternalProposalCopy(draft domain.MinisterDraft) bool {
	text := draft.Title + draft.Summary + draft.Rationale + draft.RiskNote
	return strings.Contains(text, "规则规划器") || strings.Contains(text, "规则层") || strings.Contains(text, "rule planner")
}
