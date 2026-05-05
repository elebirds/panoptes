package domain

import (
	"strconv"
	"testing"

	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func TestResolveUnitSpawnPositionSkipsReservedAndBlockedCandidates(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
			{ID: "mountain", Passable: false, Buildable: false},
		},
	}))

	state := newSpawnResolverState(t, 9, 9, Position{Q: 4, R: 4})
	origin := Position{Q: 4, R: 4}

	blocked := []Position{
		{Q: 5, R: 4},
		{Q: 5, R: 3},
		{Q: 4, R: 3},
		{Q: 3, R: 4},
		{Q: 3, R: 5},
	}
	for _, pos := range blocked {
		entry := mustSpawnResolverNode(t, state, pos)
		addSpawnResolverBuilding(entry, "farm", "player-2")
	}
	state.TurnRuntime.Planning.BuildOrders = []BuildOrder{{PlayerID: "player-1", NodeID: nodeIDAtPosition(t, state, Position{Q: 4, R: 5}), BuildingType: "farm"}}

	got, ok := ResolveUnitSpawnPosition(state, origin)
	if !ok {
		t.Fatalf("ResolveUnitSpawnPosition() = not found, want valid ring-2 tile")
	}
	if got != (Position{Q: 6, R: 4}) {
		t.Fatalf("spawn pos = %#v, want first valid ring-2 tile", got)
	}
}

func TestResolveUnitSpawnPositionRejectsPocketWithoutEscape(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
			{ID: "mountain", Passable: false, Buildable: false},
		},
	}))

	state := newSparseSpawnResolverState(t, []Position{{Q: 4, R: 4}, {Q: 5, R: 4}}, Position{Q: 4, R: 4})
	origin := Position{Q: 4, R: 4}

	if got, ok := ResolveUnitSpawnPosition(state, origin); ok {
		t.Fatalf("ResolveUnitSpawnPosition() = %#v, want no spawn when only candidate cannot escape beyond ring 3", got)
	}
}

func TestResolveUnitSpawnPositionAllowsRingThreeCandidateWhenEscapePathExists(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
			{ID: "mountain", Passable: false, Buildable: false},
		},
	}))

	state := newSparseSpawnResolverState(t, []Position{
		{Q: 0, R: 0},
		{Q: 1, R: 0},
		{Q: 2, R: 0},
		{Q: 3, R: 0},
		{Q: 4, R: 0},
	}, Position{Q: 0, R: 0})
	origin := Position{Q: 0, R: 0}

	addSpawnResolverBuilding(mustSpawnResolverNode(t, state, Position{Q: 1, R: 0}), "farm", "player-2")
	addSpawnResolverBuilding(mustSpawnResolverNode(t, state, Position{Q: 2, R: 0}), "farm", "player-2")

	got, ok := ResolveUnitSpawnPosition(state, origin)
	if !ok {
		t.Fatalf("ResolveUnitSpawnPosition() = not found, want ring-3 fallback")
	}
	if got != (Position{Q: 3, R: 0}) {
		t.Fatalf("spawn pos = %#v, want ring-3 tile with escape path", got)
	}
}

func TestResolveUnitSpawnPositionTreatsPendingBuildOrdersAsEscapeBlockers(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
			{ID: "mountain", Passable: false, Buildable: false},
		},
	}))

	state := newSparseSpawnResolverState(t, []Position{
		{Q: 0, R: 0},
		{Q: 1, R: 0},
		{Q: 2, R: 0},
		{Q: 3, R: 0},
		{Q: 4, R: 0},
	}, Position{Q: 0, R: 0})
	origin := Position{Q: 0, R: 0}
	state.TurnRuntime.Planning.BuildOrders = []BuildOrder{
		{PlayerID: "player-1", NodeID: nodeIDAtPosition(t, state, Position{Q: 4, R: 0}), BuildingType: "farm"},
	}

	if got, ok := ResolveUnitSpawnPosition(state, origin); ok {
		t.Fatalf("ResolveUnitSpawnPosition() = %#v, want no spawn when pending build blocks the only escape tile beyond ring 3", got)
	}
}

func newSpawnResolverState(t *testing.T, width int, height int, origin Position) *GameState {
	t.Helper()

	world := donburi.NewWorld()
	nodeIndex := make(map[string]donburi.Entity, width*height)
	for q := 0; q < width; q++ {
		for r := 0; r < height; r++ {
			id := spawnResolverNodeID(q, r)
			entity := world.Create(PositionC, NodeC)
			entry := world.Entry(entity)
			PositionC.SetValue(entry, PositionComp{Q: q, R: r})
			NodeC.SetValue(entry, NodeComp{ID: id, Terrain: TerrainPlain})
			nodeIndex[id] = entity
		}
	}

	state := NewGameState("spawn-resolver", []string{"player-1"}, []string{"alice"}, &MapData{
		ID:           "spawn-resolver",
		NodeIndex:    nodeIndex,
		PlayerSpawns: map[string]Position{"player-1": origin},
	})
	state.World = world
	return state
}

func newSparseSpawnResolverState(t *testing.T, nodes []Position, origin Position) *GameState {
	t.Helper()

	world := donburi.NewWorld()
	nodeIndex := make(map[string]donburi.Entity, len(nodes))
	for _, pos := range nodes {
		id := spawnResolverNodeID(pos.Q, pos.R)
		entity := world.Create(PositionC, NodeC)
		entry := world.Entry(entity)
		PositionC.SetValue(entry, PositionComp{Q: pos.Q, R: pos.R})
		NodeC.SetValue(entry, NodeComp{ID: id, Terrain: TerrainPlain})
		nodeIndex[id] = entity
	}

	state := NewGameState("spawn-resolver", []string{"player-1"}, []string{"alice"}, &MapData{
		ID:           "spawn-resolver",
		NodeIndex:    nodeIndex,
		PlayerSpawns: map[string]Position{"player-1": origin},
	})
	state.World = world
	return state
}

func mustSpawnResolverNode(t *testing.T, state *GameState, pos Position) *donburi.Entry {
	t.Helper()
	entry, ok := GetNodeAt(state.World, pos)
	if !ok || entry == nil {
		t.Fatalf("node at %#v missing", pos)
	}
	return entry
}

func nodeIDAtPosition(t *testing.T, state *GameState, pos Position) string {
	t.Helper()
	return NodeC.Get(mustSpawnResolverNode(t, state, pos)).ID
}

func spawnResolverNodeID(q int, r int) string {
	return "N" + strconv.Itoa(q) + "_" + strconv.Itoa(r)
}

func addSpawnResolverBuilding(entry *donburi.Entry, buildingType string, owner string) {
	if entry == nil {
		return
	}
	if !entry.HasComponent(BuildingC) {
		entry.AddComponent(BuildingC)
	}
	BuildingC.SetValue(entry, BuildingComp{
		Type:  BuildingType(buildingType),
		HP:    1,
		MaxHP: 1,
		Owner: owner,
	})
}
