package domain

import (
	"testing"

	"github.com/elebirds/panoptes/internal/config"
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

func TestResourcesMethods(t *testing.T) {
	base := Resources{
		Ore:              3,
		Wood:             4,
		Food:             5,
		RefinedOre:       2,
		EngineerMaterial: 1,
		BuildPoints:      6,
	}
	other := Resources{
		Ore:              1,
		Wood:             2,
		Food:             3,
		RefinedOre:       1,
		EngineerMaterial: 1,
		BuildPoints:      2,
	}

	if got := base.Add(other); got != (Resources{
		Ore:              4,
		Wood:             6,
		Food:             8,
		RefinedOre:       3,
		EngineerMaterial: 2,
		BuildPoints:      8,
	}) {
		t.Fatalf("Add() = %#v", got)
	}

	if got := base.Sub(other); got != (Resources{
		Ore:              2,
		Wood:             2,
		Food:             2,
		RefinedOre:       1,
		EngineerMaterial: 0,
		BuildPoints:      4,
	}) {
		t.Fatalf("Sub() = %#v", got)
	}

	if !base.CanAfford(other) {
		t.Fatalf("CanAfford() = false")
	}
	if base.CanAfford(Resources{Food: 99}) {
		t.Fatalf("CanAfford() = true for impossible cost")
	}
	if !(Resources{}).IsZero() {
		t.Fatalf("IsZero() = false")
	}
	if base.IsZero() {
		t.Fatalf("IsZero() = true")
	}
}

func TestNewGameStateInitializesPlayersAndWorld(t *testing.T) {
	config.Data = config.GameData{
		Rules: config.RulesConfig{
			TokensPerTurn:      3,
			CastleBaseHP:       100,
			BuildPointsPerTurn: 10,
		},
	}

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
	if player.Resources.BuildPoints != 10 {
		t.Fatalf("BuildPoints = %d", player.Resources.BuildPoints)
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
