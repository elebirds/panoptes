package domain

import (
	"reflect"
	"testing"

	"github.com/elebirds/panoptes/internal/staticdata"
)

func TestPositionMethods(t *testing.T) {
	pos := Position{X: 3, Y: 4}
	other := Position{X: 1, Y: 9}

	if got := pos.DistanceTo(other); got != 7 {
		t.Fatalf("DistanceTo() = %d", got)
	}

	if got := pos.Add(Position{X: -2, Y: 5}); got != (Position{X: 1, Y: 9}) {
		t.Fatalf("Add() = %#v", got)
	}

	neighbors := pos.Neighbors()
	want := []Position{
		{X: 3, Y: 3},
		{X: 3, Y: 5},
		{X: 2, Y: 4},
		{X: 4, Y: 4},
	}
	if len(neighbors) != len(want) {
		t.Fatalf("Neighbors len = %d", len(neighbors))
	}
	for idx := range want {
		if neighbors[idx] != want[idx] {
			t.Fatalf("Neighbors[%d] = %#v, want %#v", idx, neighbors[idx], want[idx])
		}
	}
}

func TestResourceBagMethods(t *testing.T) {
	base := ResourceBag{
		ResourceOre:         3,
		ResourceWood:        4,
		ResourceFood:        5,
		ResourceRefinedOre:  2,
		ResourceEngineerMat: 1,
		ResourceBuildPoints: 6,
		ResourceKey("mana"): 9,
	}
	other := ResourceBag{
		ResourceOre:         1,
		ResourceWood:        2,
		ResourceFood:        3,
		ResourceRefinedOre:  1,
		ResourceEngineerMat: 1,
		ResourceBuildPoints: 2,
		ResourceKey("mana"): 4,
	}

	if got := base.Add(other); !reflect.DeepEqual(got, ResourceBag{
		ResourceOre:         4,
		ResourceWood:        6,
		ResourceFood:        8,
		ResourceRefinedOre:  3,
		ResourceEngineerMat: 2,
		ResourceBuildPoints: 8,
		ResourceKey("mana"): 13,
	}) {
		t.Fatalf("Add() = %#v", got)
	}

	if got := base.Sub(other); !reflect.DeepEqual(got, ResourceBag{
		ResourceOre:         2,
		ResourceWood:        2,
		ResourceFood:        2,
		ResourceRefinedOre:  1,
		ResourceBuildPoints: 4,
		ResourceKey("mana"): 5,
	}) {
		t.Fatalf("Sub() = %#v", got)
	}

	if !base.CanAfford(other) {
		t.Fatalf("CanAfford() = false")
	}
	if base.CanAfford(ResourceBag{ResourceFood: 99}) {
		t.Fatalf("CanAfford() = true for impossible cost")
	}
	if !(ResourceBag{}).IsZero() {
		t.Fatalf("IsZero() = false")
	}
	if base.IsZero() {
		t.Fatalf("IsZero() = true")
	}
}

func TestResourceBagUtilityMethods(t *testing.T) {
	bag := NewResourceBag()
	bag.Set(ResourceOre, 3)
	bag.AddAmount(ResourceWood, 2)
	bag.AddAmount(ResourceWood, -2)
	bag.Set(ResourceKey("mystery"), 4)

	if bag.Get(ResourceOre) != 3 {
		t.Fatalf("ResourceOre = %d", bag.Get(ResourceOre))
	}
	if _, ok := bag[ResourceWood]; ok {
		t.Fatalf("zero resource should be normalized away")
	}

	keys := bag.Keys()
	if len(keys) != 2 {
		t.Fatalf("Keys len = %d", len(keys))
	}

	knownOnly := bag.KnownOnly()
	if len(knownOnly) != 1 || knownOnly.Get(ResourceOre) != 3 {
		t.Fatalf("KnownOnly = %#v", knownOnly)
	}

	withNegative := ResourceBag{
		ResourceOre: -1,
	}
	if err := withNegative.ValidateNonNegative(); err == nil {
		t.Fatalf("ValidateNonNegative() expected error")
	}
}

func TestResourceBagFromAmounts(t *testing.T) {
	bag, err := ResourceBagFromAmounts(map[string]int{
		"ore":          2,
		"build_points": 5,
	})
	if err != nil {
		t.Fatalf("ResourceBagFromAmounts() error = %v", err)
	}
	if bag.Get(ResourceOre) != 2 || bag.Get(ResourceBuildPoints) != 5 {
		t.Fatalf("bag = %#v", bag)
	}
}

func TestNewGameStateInitializesPlayersAndWorld(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TokensPerTurn:      3,
			CastleBaseHP:       100,
			BuildPointsPerTurn: 10,
		},
	}))

	mapData := &MapData{
		ID:     "default",
		Width:  20,
		Height: 20,
		SpawnPoints: map[int]Position{
			0: {X: 2, Y: 10},
			1: {X: 17, Y: 10},
		},
		NamedNodes: map[string]string{"K10": "龙脊"},
	}

	state := NewGameState(
		"game-1",
		[]string{"player-1", "player-2"},
		[]string{"alice", "bob"},
		mapData,
	)

	if state.GameID != "game-1" {
		t.Fatalf("GameID = %q", state.GameID)
	}
	if state.Turn != 1 {
		t.Fatalf("Turn = %d", state.Turn)
	}
	if state.Phase != "domestic" {
		t.Fatalf("Phase = %q", state.Phase)
	}
	if state.World == nil {
		t.Fatalf("World is nil")
	}
	if state.World.Len() != 0 {
		t.Fatalf("World should start empty")
	}
	if len(state.Players) != 2 {
		t.Fatalf("Players len = %d", len(state.Players))
	}

	player := state.Players["player-1"]
	if player == nil {
		t.Fatalf("player-1 missing")
	}
	if player.Username != "alice" {
		t.Fatalf("Username = %q", player.Username)
	}
	if player.Resources.Get(ResourceBuildPoints) != 10 {
		t.Fatalf("BuildPoints = %d", player.Resources.Get(ResourceBuildPoints))
	}
	if player.TokensLeft != 3 {
		t.Fatalf("TokensLeft = %d", player.TokensLeft)
	}
	if player.MainCastleHP != 100 {
		t.Fatalf("MainCastleHP = %d", player.MainCastleHP)
	}
	if state.NodeIndex == nil {
		t.Fatalf("NodeIndex is nil")
	}
	if _, ok := state.GetNode("missing"); ok {
		t.Fatalf("GetNode() should miss")
	}
}
