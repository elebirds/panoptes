package session

import (
	"context"
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	"google.golang.org/protobuf/proto"
)

type capturePlayer struct {
	playerID string
	username string
	sent     []proto.Message
}

func (p *capturePlayer) PlayerID() string { return p.playerID }

func (p *capturePlayer) Username() string { return p.username }

func (p *capturePlayer) IsBot() bool { return false }

func (p *capturePlayer) Send(_ context.Context, msg proto.Message) error {
	p.sent = append(p.sent, msg)
	return nil
}

func TestRuntimeBootstrapDuringPlanningSendsPlanningStartWithSnapshotAndCurrentTokens(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{TurnTimeLimitPlanning: 30, TokensPerTurn: 3},
	}))

	player := &capturePlayer{playerID: "player-1", username: "alice"}
	runtime := NewRuntime("game-1", []Player{player}, nil, nil)
	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	state.Turn = 4
	state.Phase = domain.PhasePlanning.String()
	state.Players["player-1"].TokensLeft = 1

	if err := runtime.InitializePrepared(state); err != nil {
		t.Fatalf("InitializePrepared() error = %v", err)
	}

	if len(player.sent) != 3 {
		t.Fatalf("send count = %d, want 3", len(player.sent))
	}
	start, ok := player.sent[2].(*pb.MsgPlanningStart)
	if !ok {
		t.Fatalf("message type = %T, want MsgPlanningStart", player.sent[2])
	}
	if got := start.GetTokens(); got != 1 {
		t.Fatalf("tokens = %d, want 1", got)
	}
	if got := start.GetTurn(); got != 4 {
		t.Fatalf("turn = %d, want 4", got)
	}
	if start.GetMyPlayer() == nil {
		t.Fatalf("my_player is nil")
	}
	if start.GetSnapshot() == nil {
		t.Fatalf("snapshot is nil")
	}
	if len(start.GetPlanningStartEvents()) != 0 {
		t.Fatalf("planning_start_events len = %d, want 0 without pending activations", len(start.GetPlanningStartEvents()))
	}
}

func TestRuntimeBootstrapOutsidePlanningDoesNotSendPlanningStart(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{TurnTimeLimitPlanning: 30, TokensPerTurn: 3},
	}))

	player := &capturePlayer{playerID: "player-1", username: "alice"}
	runtime := NewRuntime("game-1", []Player{player}, nil, nil)
	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	state.Turn = 4
	state.Phase = domain.PhaseResolving.String()

	if err := runtime.InitializePrepared(state); err != nil {
		t.Fatalf("InitializePrepared() error = %v", err)
	}

	if len(player.sent) != 2 {
		t.Fatalf("send count = %d, want 2", len(player.sent))
	}
	if _, ok := player.sent[len(player.sent)-1].(*pb.MsgPlanningStart); ok {
		t.Fatalf("unexpected planning start in resolving bootstrap")
	}
}

func TestPreparePlanningStartStateIfNeededRunsOnlyOncePerTurn(t *testing.T) {
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
				},
			},
			{
				ID:           "masonry",
				ResearchCost: 2,
				ExplicitEffects: []staticdata.ExplicitEffect{
					{Type: "unlock_building", TargetID: "wall"},
				},
			},
		},
	}))

	runtime := NewRuntime("game-1", nil, nil, nil)
	runtime.state = domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	runtime.state.Phase = domain.PhasePlanning.String()
	runtime.state.Turn = 3
	runtime.state.Players["player-1"].Research.MarkTechnologyCompleted("agrarian_foundations", 2)

	runtime.PreparePlanningStartStateIfNeeded()
	if got := runtime.planningStartPreparedTurn; got != 3 {
		t.Fatalf("planningStartPreparedTurn = %d, want 3", got)
	}
	if !runtime.state.IsBuildingUnlocked("player-1", "farm") {
		t.Fatalf("farm should unlock on first planning-start prepare")
	}
	firstResult := runtime.PlanningStartResult()
	if firstResult == nil {
		t.Fatalf("planning start result should be cached after first prepare")
	}
	if len(firstResult.Events) == 0 {
		t.Fatalf("planning start result should include activation events")
	}

	runtime.state.Players["player-1"].Research.MarkTechnologyCompleted("masonry", 2)
	runtime.PreparePlanningStartStateIfNeeded()
	if runtime.state.IsBuildingUnlocked("player-1", "wall") {
		t.Fatalf("second prepare in same turn should be skipped")
	}
	if runtime.PlanningStartResult() != firstResult {
		t.Fatalf("planning start result should be reused within the same turn")
	}

	runtime.state.Turn = 4
	runtime.PreparePlanningStartStateIfNeeded()
	if !runtime.state.IsBuildingUnlocked("player-1", "wall") {
		t.Fatalf("prepare after turn advance should run again")
	}
	if runtime.PlanningStartResult() == firstResult {
		t.Fatalf("planning start result should refresh after turn advance")
	}
}

func TestRuntimeBootstrapPlanningStartIncludesProjectedActivationEvents(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{TurnTimeLimitPlanning: 30, TokensPerTurn: 3},
		Technologies: []staticdata.TechnologyDefinition{
			{
				ID:           "agrarian_foundations",
				ResearchCost: 2,
				ExplicitEffects: []staticdata.ExplicitEffect{
					{Type: "unlock_building", TargetID: "farm"},
				},
			},
		},
	}))

	player := &capturePlayer{playerID: "player-1", username: "alice"}
	runtime := NewRuntime("game-1", []Player{player}, nil, nil)
	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	state.Turn = 4
	state.Phase = domain.PhasePlanning.String()
	state.Players["player-1"].Research.MarkTechnologyCompleted("agrarian_foundations", 3)

	if err := runtime.InitializePrepared(state); err != nil {
		t.Fatalf("InitializePrepared() error = %v", err)
	}

	start, ok := player.sent[len(player.sent)-1].(*pb.MsgPlanningStart)
	if !ok {
		t.Fatalf("message type = %T, want MsgPlanningStart", player.sent[len(player.sent)-1])
	}
	if len(start.GetPlanningStartEvents()) != 1 {
		t.Fatalf("planning_start_events len = %d, want 1", len(start.GetPlanningStartEvents()))
	}
	if got := start.GetPlanningStartEvents()[0].GetType(); got != "technology_activated" {
		t.Fatalf("planning_start_events[0].type = %q, want technology_activated", got)
	}
}

func TestRuntimeInitializeBootstrapsCapitalOnProceduralSpawn(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Manifest: staticdata.Manifest{
			SchemaVersion:  "2026-04-15",
			ContentVersion: "runtime-bootstrap-test",
			BundleHash:     "runtime-bootstrap-test",
			DefaultLocale:  "zh-CN",
			DefaultMapID:   "runtime_bootstrap",
		},
		Rules: staticdata.Rules{
			TurnTimeLimitPlanning:      30,
			TokensPerTurn:              3,
			CityCoreMaxHP:              100,
			SafeZoneRadius:             3,
			FacilityTakeoverTurns:      2,
			BaseResearchOutputPerTurn:  1,
			BaseIndustryOutputPerTurn:  2,
			MinimumCityDistance:        2,
			InitialCityTerritoryRadius: 1,
		},
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
	}, &staticdata.MapRuntimeBundle{
		ID:     "runtime_bootstrap",
		Name:   "runtime_bootstrap",
		Width:  8,
		Height: 8,
		Nodes:  runtimeBootstrapNodes(8, 8),
	}))

	player := &capturePlayer{playerID: "player-1", username: "alice"}
	runtime := NewRuntime("game-1", []Player{player}, nil, nil)

	if err := runtime.Initialize(); err != nil {
		t.Fatalf("Initialize() error = %v", err)
	}

	state := runtime.State()
	if state == nil {
		t.Fatalf("state is nil")
	}
	playerState := state.Players["player-1"]
	if playerState == nil {
		t.Fatalf("player-1 missing")
	}
	if playerState.CapitalCityID == "" {
		t.Fatalf("CapitalCityID should be initialized")
	}
	cityState := playerState.Cities[playerState.CapitalCityID]
	if cityState == nil {
		t.Fatalf("capital city state missing for %q", playerState.CapitalCityID)
	}
	spawnPos, ok := state.Map.PlayerSpawns["player-1"]
	if !ok {
		t.Fatalf("player spawn missing")
	}
	entry, ok := domain.GetNodeAt(state.World, spawnPos)
	if !ok {
		t.Fatalf("spawn node missing")
	}
	if !entry.HasComponent(ecs.BuildingC) {
		t.Fatalf("spawn node should have a city_core")
	}
	building := ecs.BuildingC.Get(entry)
	if got := string(building.Type); got != "city_core" {
		t.Fatalf("spawn building type = %q, want city_core", got)
	}
	if got := building.Owner; got != "player-1" {
		t.Fatalf("city_core owner = %q, want player-1", got)
	}
}

func runtimeBootstrapNodes(width int, height int) []staticdata.MapRuntimeNode {
	nodes := make([]staticdata.MapRuntimeNode, 0, width*height)
	for y := 0; y < height; y++ {
		for x := 0; x < width; x++ {
			nodes = append(nodes, staticdata.MapRuntimeNode{
				ID:      string(rune('A'+x)) + string(rune('1'+y)),
				X:       x,
				Y:       y,
				Terrain: "plain",
			})
		}
	}
	return nodes
}
