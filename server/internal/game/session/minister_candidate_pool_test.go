package session

import (
	"strings"
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/game/planning"
	gamequery "github.com/elebirds/panoptes/internal/game/query"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/ministerroles"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func TestBuildMinisterDraftsFromLegalCandidatesEnumeratesVisibleLegalActionSpace(t *testing.T) {
	previous := staticdata.Default()
	t.Cleanup(func() {
		staticdata.SetDefault(previous)
	})
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			BaseResearchOutputPerTurn:  1,
			BaseIndustryOutputPerTurn:  2,
			InitialCityTerritoryRadius: 2,
			CityCoreMaxHP:              100,
		},
		Technologies: []staticdata.TechnologyDefinition{
			{ID: "bronze_working", Name: "Bronze Working", ResearchCost: 2},
			{ID: "irrigation", Name: "Irrigation", ResearchCost: 2},
		},
		Policies: []staticdata.PolicyDefinition{
			{ID: "expansion", Name: "Expansion", Layer: "national"},
			{ID: "reorganization", Name: "Reorganization", Layer: "national"},
		},
		InstitutionCategories: []staticdata.InstitutionCategoryDefinition{
			{ID: "administration", Name: "Administration"},
		},
		Institutions: []staticdata.InstitutionDefinition{
			{ID: "academy_charter", Name: "Academy Charter", Category: "administration", ActivationTiming: "next_turn"},
			{ID: "logistics_board", Name: "Logistics Board", Category: "administration", ActivationTiming: "next_turn"},
		},
		Units: []staticdata.UnitDefinition{
			{ID: "infantry", Class: "melee", MaxHP: 30, Attack: 10, AttackRange: 1, MoveRange: 2, VisionRange: 3, Multipliers: map[string]float64{}},
		},
		Buildings: []staticdata.BuildingDefinition{
			{ID: "city_core", Name: "City Core", PlacementKind: "city_foundation_center", BuildingScope: "city_core", RecipeIDs: []string{"train_settler", "train_infantry"}, MaxHP: 100, TakeoverMode: "disabled"},
			{ID: "granary", Name: "Granary", PlacementKind: "city_territory", BuildingScope: "in_city", MaxHP: 40, TakeoverMode: "city_capture"},
			{ID: "barracks", Name: "Barracks", PlacementKind: "city_territory", BuildingScope: "in_city", MaxHP: 60, TakeoverMode: "city_capture"},
		},
		Recipes: []staticdata.RecipeDefinition{
			{ID: "train_settler", Name: "Train Settler", BuildingID: "city_core"},
			{ID: "train_infantry", Name: "Train Infantry", BuildingID: "city_core"},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
	}))

	state := newMinisterCandidatePoolState(t)
	observation := &gamequery.ObservationSnapshot{
		ViewerID: "player-1",
		VisibleNodes: []*pb.NodeView{
			{Id: "C1", ControllerPlayerId: "player-1", TerritoryOwnerPlayerId: "player-1", BuildingTypeId: "city_core", Operation: &pb.BuildingOperationView{}},
			{Id: "B1", ControllerPlayerId: "player-1", TerritoryOwnerPlayerId: "player-1", EnemyUnitCount: 1},
		},
		Units: []*pb.UnitView{
			{Id: "u1", Faction: "player-1", UnitType: "infantry"},
		},
	}

	drafts := buildMinisterDraftsFromLegalCandidates(7, "player-1", state, observation)
	countByKind := make(map[domain.MinisterDraftKind]int)
	seenDraftIDs := make(map[string]struct{}, len(drafts))
	for _, draft := range drafts {
		countByKind[draft.Kind]++
		if _, ok := seenDraftIDs[draft.DraftID]; ok {
			t.Fatalf("duplicate draft id %q in %#v", draft.DraftID, drafts)
		}
		seenDraftIDs[draft.DraftID] = struct{}{}
		if draft.Source != domain.MinisterDraftSourceRuleOnly || draft.Status != domain.MinisterDraftStatusPending || !draft.Available {
			t.Fatalf("draft = %#v, want pending rule-only candidate", draft)
		}
		if draft.NodeID == "B2" || draft.TargetNodeID == "B2" || strings.Contains(draft.TargetID, "B2") {
			t.Fatalf("draft = %#v, hidden node B2 must not produce a candidate", draft)
		}
		for _, step := range draft.OperationSteps {
			if step.NodeID == "B2" || step.TargetNodeID == "B2" || strings.Contains(step.TargetID, "B2") {
				t.Fatalf("operation draft = %#v, hidden node B2 must not produce a step", draft)
			}
		}
		if draft.Kind == domain.MinisterDraftKindBuild && draft.BuildingTypeID == "city_core" {
			t.Fatalf("city core must not be offered as a normal build candidate: %#v", draft)
		}
		if draft.Kind == domain.MinisterDraftKindOperation && len(draft.OperationSteps) == 0 {
			t.Fatalf("operation draft must contain command steps: %#v", draft)
		}
		if draft.MinisterRole == defenseMinisterRole && draft.Kind == domain.MinisterDraftKindOperation {
			t.Fatalf("defense minister must not fall back to hold/readiness operation candidates: %#v", draft)
		}
		switch draft.Kind {
		case domain.MinisterDraftKindBuild:
			switch draft.BuildingTypeID {
			case "granary":
				if draft.MinisterRole != worksMinisterRole {
					t.Fatalf("granary build draft = %#v, want works minister", draft)
				}
			case "barracks":
				if draft.MinisterRole != defenseMinisterRole {
					t.Fatalf("barracks build draft = %#v, want defense minister", draft)
				}
			case "city_core":
				t.Fatalf("city core must not be offered as a normal build candidate: %#v", draft)
			}
		case domain.MinisterDraftKindRecipe:
			switch draft.RecipeID {
			case "train_settler":
				if draft.MinisterRole != frontierMinisterRole {
					t.Fatalf("settler recipe draft = %#v, want frontier minister", draft)
				}
			case "train_infantry":
				if draft.MinisterRole != defenseMinisterRole {
					t.Fatalf("infantry recipe draft = %#v, want defense minister", draft)
				}
			}
		}
	}

	for kind, wantAtLeast := range map[domain.MinisterDraftKind]int{
		domain.MinisterDraftKindResearch:    2,
		domain.MinisterDraftKindPolicy:      2,
		domain.MinisterDraftKindInstitution: 2,
		domain.MinisterDraftKindBuild:       2,
		domain.MinisterDraftKindRecipe:      2,
		domain.MinisterDraftKindOperation:   1,
	} {
		if got := countByKind[kind]; got < wantAtLeast {
			t.Fatalf("draft kind %q count = %d, want at least %d; all drafts = %#v", kind, got, wantAtLeast, drafts)
		}
	}
	if got := countByKind[domain.MinisterDraftKindBuild]; got > 2 {
		t.Fatalf("build candidates = %d, want at most 2 after budget filtering", got)
	}
	if got := len(state.TurnRuntime.Planning.BuildOrders); got != 0 {
		t.Fatalf("build orders = %d, want 0 before approval", got)
	}
	if got := len(state.TurnRuntime.Planning.RecipeSelections); got != 0 {
		t.Fatalf("recipe selections = %d, want 0 before approval", got)
	}
	if got := len(state.TurnRuntime.Planning.UnitOrders); got != 0 {
		t.Fatalf("unit orders = %d, want 0 before approval", got)
	}
	if got := countByKind[domain.MinisterDraftKindUnitOrder]; got != 0 {
		t.Fatalf("top-level unit order candidates = %d, want 0; unit commands should be inside operations", got)
	}
	if got := countByKind[domain.MinisterDraftKindOperation]; got > ministerOperationCandidateLimit {
		t.Fatalf("operation candidates = %d, limit = %d", got, ministerOperationCandidateLimit)
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
}

func TestBuildMinisterDraftsFromLegalCandidatesOffersOpeningMilitaryRecon(t *testing.T) {
	previous := staticdata.Default()
	t.Cleanup(func() {
		staticdata.SetDefault(previous)
	})
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{CityCoreMaxHP: 100},
		Units: []staticdata.UnitDefinition{
			{ID: "infantry", Class: "melee", MaxHP: 30, Attack: 10, AttackRange: 1, MoveRange: 2, VisionRange: 3, Multipliers: map[string]float64{}},
		},
		Buildings: []staticdata.BuildingDefinition{
			{ID: "city_core", Name: "City Core", PlacementKind: "city_foundation_center", BuildingScope: "city_core", MaxHP: 100, TakeoverMode: "disabled"},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
	}))

	state := newMinisterCandidatePoolState(t)
	observation := &gamequery.ObservationSnapshot{
		ViewerID: "player-1",
		VisibleNodes: []*pb.NodeView{
			{Id: "C1", ControllerPlayerId: "player-1", TerritoryOwnerPlayerId: "player-1", BuildingTypeId: "city_core"},
			{Id: "B1", ControllerPlayerId: "player-1", TerritoryOwnerPlayerId: "player-1"},
		},
		Units: []*pb.UnitView{
			{Id: "u1", Faction: "player-1", UnitType: "infantry"},
		},
	}

	drafts := buildMinisterDraftsFromLegalCandidates(7, "player-1", state, observation)
	for _, draft := range drafts {
		if draft.MinisterRole != commandMinisterRole || draft.Kind != domain.MinisterDraftKindOperation {
			continue
		}
		if len(draft.OperationSteps) != 1 || draft.OperationSteps[0].TargetNodeID != "B1" {
			t.Fatalf("military recon draft = %#v, want one move step to B1", draft)
		}
		return
	}
	t.Fatalf("drafts = %#v, want opening military recon operation", drafts)
}

func TestBuildMinisterDraftsFromLegalCandidatesIncludesSafeZoneBuildsWithoutTerritoryOwner(t *testing.T) {
	previous := staticdata.Default()
	t.Cleanup(func() {
		staticdata.SetDefault(previous)
	})
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			SafeZoneRadius:             2,
			CityCoreMaxHP:              100,
			InitialCityTerritoryRadius: 1,
		},
		Buildings: []staticdata.BuildingDefinition{
			{ID: "city_core", Name: "City Core", PlacementKind: "city_foundation_center", BuildingScope: "city_core", MaxHP: 100, TakeoverMode: "disabled"},
			{ID: "farm", Name: "Farm", PlacementKind: "resource_node", BuildingScope: "out_of_city", RequiredResourceType: "food", MaxHP: 40, TakeoverMode: "delayed"},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
	}))

	world := donburi.NewWorld()
	nodeIndex := map[string]donburi.Entity{
		"C1": ecs.CreateNode(world, ecs.MapNode{ID: "C1", Q: 0, R: 0, Terrain: "plain"}),
		"B1": ecs.CreateNode(world, ecs.MapNode{ID: "B1", Q: 1, R: 0, Terrain: "plain", IsResourcePoint: true, ResourceType: "food"}),
	}
	state := domain.NewGameState("game-safezone-build", []string{"player-1"}, []string{"alice"}, &domain.MapData{
		ID:        "default",
		NodeIndex: nodeIndex,
	})
	state.World = world
	state.NodeIndex = nodeIndex
	state.Map.PlayerSpawns = make(map[string]domain.Position)
	state.Map.PlayerSpawns["player-1"] = domain.Position{Q: 0, R: 0}
	state.Players["player-1"].CapitalCityID = "C1"
	state.EnsureCityState("player-1", "C1")
	state.Players["player-1"].Research.UnlockBuilding("farm")
	ecs.CreateBuilding(world, "city_core", "player-1", "C1", world.Entry(nodeIndex["C1"]))
	ecs.NodeC.Get(world.Entry(nodeIndex["B1"])).TerritoryOwner = ""

	drafts := buildMinisterDraftsFromLegalCandidates(7, "player-1", state, &gamequery.ObservationSnapshot{
		ViewerID: "player-1",
		VisibleNodes: []*pb.NodeView{
			{Id: "B1", ControllerPlayerId: "player-1", TerritoryOwnerPlayerId: "", IsResourcePoint: true, ResourceType: "food"},
		},
	})
	for _, draft := range drafts {
		if draft.Kind == domain.MinisterDraftKindBuild && draft.BuildingTypeID == "farm" && draft.NodeID == "B1" {
			if draft.MinisterRole != worksMinisterRole {
				t.Fatalf("safe-zone farm build draft = %#v, want works minister", draft)
			}
			return
		}
	}
	t.Fatalf("drafts = %#v, want safe-zone farm build candidate", drafts)
}

func TestMinisterDraftFromIntentKeepsDraftIDsUniqueForCommandDimensions(t *testing.T) {
	leftBuild, ok := ministerDraftFromIntent(3, "player-1", planning.BuildStructureIntent{NodeID: "B1", BuildingTypeID: "farm", CityID: "C1"})
	if !ok {
		t.Fatalf("left build draft missing")
	}
	rightBuild, ok := ministerDraftFromIntent(3, "player-1", planning.BuildStructureIntent{NodeID: "B1", BuildingTypeID: "farm", CityID: "C2"})
	if !ok {
		t.Fatalf("right build draft missing")
	}
	if leftBuild.DraftID == rightBuild.DraftID {
		t.Fatalf("build draft ids should include city context, both were %q", leftBuild.DraftID)
	}

	leftUnit, ok := ministerDraftFromIntent(3, "player-1", planning.IssueUnitOrderIntent{
		UnitID:          "u1",
		Action:          "build_road",
		TargetNodeID:    "A1",
		SecondaryNodeID: "A2",
	})
	if !ok {
		t.Fatalf("left unit draft missing")
	}
	rightUnit, ok := ministerDraftFromIntent(3, "player-1", planning.IssueUnitOrderIntent{
		UnitID:          "u1",
		Action:          "build_road",
		TargetNodeID:    "A1",
		SecondaryNodeID: "A3",
	})
	if !ok {
		t.Fatalf("right unit draft missing")
	}
	if leftUnit.DraftID == rightUnit.DraftID {
		t.Fatalf("unit draft ids should include secondary target, both were %q", leftUnit.DraftID)
	}
}

func TestMinisterRosterViewsTreatFiredRolesAsVacant(t *testing.T) {
	previous := staticdata.Default()
	t.Cleanup(func() {
		staticdata.SetDefault(previous)
	})
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TokensPerTurn:             3,
			BaseResearchOutputPerTurn: 1,
		},
	}))

	state := domain.NewGameState("game-minister-roster", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	state.Players["player-1"].ClearMinisterForRole(ministerroles.Domestic)

	views := gamequery.BuildMinisterRosterViewsForPlayer(state, "player-1")
	for _, view := range views {
		if view == nil || view.Role != ministerroles.Domestic {
			continue
		}
		if !view.Vacant || view.MinisterId != "" || view.Name != "空缺" {
			t.Fatalf("domestic roster view = %#v, want vacant minister slot", view)
		}
		return
	}

	t.Fatalf("domestic roster view missing: %#v", views)
}

func TestRefreshMinisterCandidatesForPlayerOnlyAffectsTargetPlayer(t *testing.T) {
	previous := staticdata.Default()
	t.Cleanup(func() {
		staticdata.SetDefault(previous)
	})
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TokensPerTurn:             3,
			BaseResearchOutputPerTurn: 1,
		},
	}))

	state := domain.NewGameState("game-minister-candidates", []string{"player-1", "player-2"}, []string{"alice", "bob"}, &domain.MapData{ID: "default"})
	before1 := state.Players["player-1"].MinisterCandidates[ministerroles.Domestic].ID
	before2 := state.Players["player-2"].MinisterCandidates[ministerroles.Domestic].ID
	beforeCycle1 := state.Players["player-1"].MinisterCandidateCycle
	beforeCycle2 := state.Players["player-2"].MinisterCandidateCycle

	state.RefreshMinisterCandidatesForPlayer("player-1")

	after1 := state.Players["player-1"].MinisterCandidates[ministerroles.Domestic].ID
	after2 := state.Players["player-2"].MinisterCandidates[ministerroles.Domestic].ID

	if state.Players["player-1"].MinisterCandidateCycle != beforeCycle1+1 {
		t.Fatalf("player-1 cycle = %d, want %d", state.Players["player-1"].MinisterCandidateCycle, beforeCycle1+1)
	}
	if state.Players["player-2"].MinisterCandidateCycle != beforeCycle2 {
		t.Fatalf("player-2 cycle = %d, want %d", state.Players["player-2"].MinisterCandidateCycle, beforeCycle2)
	}
	if after1 == before1 {
		t.Fatalf("player-1 domestic candidate id did not change after refresh: %q", after1)
	}
	if after2 != before2 {
		t.Fatalf("player-2 domestic candidate id changed unexpectedly: before=%q after=%q", before2, after2)
	}
}

func newMinisterCandidatePoolState(t *testing.T) *domain.GameState {
	t.Helper()

	world := donburi.NewWorld()
	nodeIndex := map[string]donburi.Entity{
		"C1": ecs.CreateNode(world, ecs.MapNode{ID: "C1", Q: 0, R: 0, Terrain: "plain"}),
		"B1": ecs.CreateNode(world, ecs.MapNode{ID: "B1", Q: 1, R: 0, Terrain: "plain"}),
		"B2": ecs.CreateNode(world, ecs.MapNode{ID: "B2", Q: 0, R: 1, Terrain: "plain"}),
	}
	state := domain.NewGameState("game-candidate-pool", []string{"player-1"}, []string{"alice"}, &domain.MapData{
		ID:        "default",
		NodeIndex: nodeIndex,
	})
	state.World = world
	state.NodeIndex = nodeIndex
	state.Turn = 7

	player := state.Players["player-1"]
	player.CapitalCityID = "C1"
	player.Institutions.SlotCount = 2
	player.Institutions.UnlockCandidate("academy_charter")
	player.Institutions.UnlockCandidate("logistics_board")
	player.Research.UnlockBuilding("granary")
	player.Research.UnlockBuilding("barracks")
	player.Research.UnlockRecipe("train_settler")
	player.Research.UnlockRecipe("train_infantry")
	state.EnsureCityState("player-1", "C1")

	coreEntry := state.World.Entry(nodeIndex["C1"])
	ecs.CreateBuilding(world, "city_core", "player-1", "C1", coreEntry)
	for _, nodeID := range []string{"B1", "B2"} {
		entry := state.World.Entry(nodeIndex[nodeID])
		node := ecs.NodeC.Get(entry)
		node.TerritoryOwner = "player-1"
		node.Owner = "player-1"
	}
	unitEntry := state.World.Entry(ecs.CreateUnit(world, "infantry", "player-1", domain.Position{Q: 0, R: 0}))
	ecs.UnitStatsC.Get(unitEntry).ID = "u1"

	return state
}
