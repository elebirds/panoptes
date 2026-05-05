package session

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func TestPlanningStartRunnerPromotesPendingTechAndInstitutionsWithoutRefreshingTokens(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TokensPerTurn:             3,
			BaseResearchOutputPerTurn: 1,
		},
		Technologies: []staticdata.TechnologyDefinition{
			{
				ID:           "agrarian_foundations",
				ResearchCost: 2,
				ExplicitEffects: []staticdata.ExplicitEffect{
					{Type: "unlock_building", TargetID: "farm"},
					{Type: "unlock_recipe", TargetID: "farm_food"},
					{Type: "unlock_policy", TargetID: "academy_charter"},
					{Type: "add_institution_slots", InstitutionSlots: 1},
					{Type: "grant", GrantResources: staticdata.ResourceAmounts{"wood": 2}, GrantUnits: []string{"scout"}},
				},
			},
		},
		Policies: []staticdata.PolicyDefinition{
			{ID: "academy_charter", Layer: "institutional", ActivationTiming: "next_turn"},
		},
		Units: []staticdata.UnitDefinition{
			{
				ID:          "scout",
				Class:       "military",
				MaxHP:       8,
				Attack:      2,
				AttackRange: 1,
				MoveRange:   2,
				VisionRange: 2,
			},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
	}))

	world := donburi.NewWorld()
	nodeIndex := map[string]donburi.Entity{
		"S0": ecs.CreateNode(world, ecs.MapNode{ID: "S0", Q: 2, R: 1, Terrain: "plain"}),
		"S1": ecs.CreateNode(world, ecs.MapNode{ID: "S1", Q: 3, R: 1, Terrain: "plain"}),
		"S2": ecs.CreateNode(world, ecs.MapNode{ID: "S2", Q: 4, R: 1, Terrain: "plain"}),
		"S3": ecs.CreateNode(world, ecs.MapNode{ID: "S3", Q: 5, R: 1, Terrain: "plain"}),
		"S4": ecs.CreateNode(world, ecs.MapNode{ID: "S4", Q: 6, R: 1, Terrain: "plain"}),
	}
	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{
		ID:           "default",
		PlayerSpawns: map[string]domain.Position{"player-1": {Q: 2, R: 1}},
		NodeIndex:    nodeIndex,
	})
	state.World = world
	state.NodeIndex = nodeIndex
	state.Turn = 3
	state.Players["player-1"].TokensLeft = 1
	state.Players["player-1"].Research.MarkTechnologyCompleted("agrarian_foundations", 2)
	state.Players["player-1"].Institutions.PendingPolicyIDs = []string{"academy_charter"}
	state.Players["player-1"].Institutions.PendingActivationTurn = 3
	state.Players["player-1"].Institutions.SlotCount = 1
	state.Players["player-1"].Institutions.UnlockCandidate("academy_charter")

	result := NewPlanningStartRunner().Run(state)

	if got := state.Players["player-1"].Research.ActiveTechnologyIDs(); len(got) != 1 || got[0] != "agrarian_foundations" {
		t.Fatalf("technology should activate on planning start")
	}
	if !state.IsBuildingUnlocked("player-1", "farm") {
		t.Fatalf("farm should unlock on planning start")
	}
	if !state.IsRecipeUnlocked("player-1", "farm_food") {
		t.Fatalf("farm_food should unlock on planning start")
	}
	if got := state.Players["player-1"].Institutions.ActivePolicyIDs; len(got) != 1 || got[0] != "academy_charter" {
		t.Fatalf("active institution policies = %#v, want academy_charter", got)
	}
	if got := state.Players["player-1"].TokensLeft; got != 1 {
		t.Fatalf("tokens left = %d, want planning start to preserve current tokens", got)
	}
	if got := state.Players["player-1"].Resources.Get(domain.ResourceWood); got != 2 {
		t.Fatalf("wood after activation = %d, want 2", got)
	}
	if result == nil {
		t.Fatalf("planning start result is nil")
	}
	var activated, granted, institutions bool
	for _, evt := range result.Events {
		if evt == nil {
			continue
		}
		switch evt.Kind() {
		case "technology_activated":
			activated = true
		case "technology_grant_applied":
			granted = true
		case "institution_loadout_activated":
			institutions = true
		}
	}
	if !activated {
		t.Fatalf("planning start result missing technology_activated")
	}
	if !granted {
		t.Fatalf("planning start result missing technology_grant_applied")
	}
	if !institutions {
		t.Fatalf("planning start result missing institution_loadout_activated")
	}

	unitsAtSpawn := 0
	ecs.AllUnits(state.World).Each(state.World, func(entry *donburi.Entry) {
		if entry == nil {
			return
		}
		stats := ecs.UnitStatsC.Get(entry)
		pos := ecs.PositionC.Get(entry)
		if stats.Faction == "player-1" && string(stats.Type) == "scout" && pos.Q == 3 && pos.R == 1 {
			unitsAtSpawn++
		}
	})
	if unitsAtSpawn != 1 {
		t.Fatalf("granted scout count = %d, want 1", unitsAtSpawn)
	}
}
