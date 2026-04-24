package game

import (
	"context"
	"math/rand"
	"path/filepath"
	"runtime"
	"sort"
	"testing"

	"github.com/elebirds/panoptes/internal/config"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/engine/maploader"
	"github.com/elebirds/panoptes/internal/game/ai"
	"github.com/elebirds/panoptes/internal/game/participant"
	"github.com/elebirds/panoptes/internal/game/planning"
	gamesession "github.com/elebirds/panoptes/internal/game/session"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func TestPeacefulBotBuildsBarracksAndProducesInfantryWithRealContent(t *testing.T) {
	catalog := loadRealContentCatalogForGameTest(t)
	staticdata.SetDefault(catalog)

	room, state := newPeacefulBotRegressionRoom(t, catalog, 17)
	planner := ai.RuleBotProvider{}
	service := planning.Service{}
	service.Enter(room)

	initialInfantry := countOwnedUnitsByTypeForGameTest(state.World, "bot-1", "infantry")
	if initialInfantry == 0 {
		t.Fatalf("expected bootstrap infantry for bot-1")
	}

	const maxTurns = 14
	for turn := 0; turn < maxTurns; turn++ {
		gamesession.PreparePlanningStartState(state)
		observation := room.runtime.BuildObservation("bot-1")

		intents, err := planner.BuildPlanningIntents(context.Background(), ai.Request{
			Participant: participant.Participant{ID: "bot-1", Username: "bot", Kind: participant.KindBot},
			State:       state,
			Observation: observation,
			RNG:         rand.New(rand.NewSource(int64(500 + state.Turn))),
		})
		if err != nil {
			t.Fatalf("BuildPlanningIntents() error on turn %d = %v", state.Turn, err)
		}

		for _, intent := range intents {
			if intent == nil {
				continue
			}
			if err := service.HandleIntent(room, planning.IntentEnvelope{
				ParticipantID: "bot-1",
				Intent:        intent,
			}); err != nil {
				t.Fatalf("HandleIntent() error on turn %d for %#v = %v", state.Turn, intent, err)
			}
		}

		room.RunTurnResolution()
		if got := countOwnedUnitsByTypeForGameTest(state.World, "bot-1", "infantry"); got > initialInfantry {
			return
		}
		state.Turn++
		state.Phase = domain.PhasePlanning.String()
	}

	player := state.Players["bot-1"]
	t.Fatalf(
		"bot-1 did not produce extra infantry within %d turns; active_tech=%v completed_tech=%v buildings=%v resources={food:%d wood:%d ore:%d}",
		maxTurns,
		player.Research.ActiveTechnologyIDs(),
		player.Research.CompletedTechnologyIDs(),
		ownedBuildingTypesForGameTest(state, "bot-1"),
		player.Resources.Get(domain.ResourceFood),
		player.Resources.Get(domain.ResourceWood),
		player.Resources.Get(domain.ResourceOre),
	)
}

func loadRealContentCatalogForGameTest(t *testing.T) *staticdata.Catalog {
	t.Helper()

	_, currentFile, _, ok := runtime.Caller(0)
	if !ok {
		t.Fatalf("runtime.Caller() failed")
	}
	repoRoot := filepath.Clean(filepath.Join(filepath.Dir(currentFile), "..", "..", ".."))
	catalog, err := staticdata.LoadDir(filepath.Join(repoRoot, "data", "generated", "server"))
	if err != nil {
		t.Fatalf("LoadDir(real content) error = %v", err)
	}
	return catalog
}

func newPeacefulBotRegressionRoom(t *testing.T, catalog *staticdata.Catalog, seed int64) (*GameRoom, *domain.GameState) {
	t.Helper()

	baseMap, err := maploader.LoadMap(catalog, catalog.DefaultMapID())
	if err != nil {
		t.Fatalf("LoadMap() error = %v", err)
	}

	runtimeMap := maploader.GenerateProceduralMap(baseMap, 2, seed)
	if runtimeMap == nil {
		t.Fatalf("GenerateProceduralMap() returned nil")
	}

	playerIDs := []string{"bot-1", "player-2"}
	usernames := []string{"bot", "alice"}
	world := donburi.NewWorld()
	mapData := maploader.InitWorldFromMap(world, runtimeMap, playerIDs)
	state := domain.NewGameState("peaceful-bot-military-regression", playerIDs, usernames, mapData)
	state.World = world
	state.Phase = domain.PhasePlanning.String()

	for _, playerID := range playerIDs {
		bootstrapProceduralSpawnForGameTest(t, state, playerID)
		player := state.Players[playerID]
		player.Resources.Set(domain.ResourceFood, 200)
		player.Resources.Set(domain.ResourceWood, 200)
		player.Resources.Set(domain.ResourceOre, 200)
	}

	room := NewPreparedRoom(
		state.GameID,
		[]ParticipantSpec{
			NewBotParticipantSpec("bot-1", "bot"),
			NewHumanParticipantSpec("player-2", "alice"),
		},
		nil,
		&config.Config{DevMode: true},
		state,
	)
	if err := room.runtime.InitializePrepared(state); err != nil {
		t.Fatalf("InitializePrepared() error = %v", err)
	}
	return room, state
}

func bootstrapProceduralSpawnForGameTest(t *testing.T, state *domain.GameState, playerID string) {
	t.Helper()

	if state == nil || state.World == nil || state.Map == nil {
		t.Fatalf("state/world/map must be initialized")
	}

	spawnPos, ok := state.Map.PlayerSpawns[playerID]
	if !ok {
		t.Fatalf("spawn missing for %s", playerID)
	}
	spawnEntry, ok := domain.GetNodeAt(state.World, spawnPos)
	if !ok || spawnEntry == nil {
		t.Fatalf("spawn node missing for %s", playerID)
	}

	if !spawnEntry.HasComponent(ecs.BuildingC) {
		ecs.CreateBuilding(state.World, "city_core", playerID, ecs.NodeC.Get(spawnEntry).ID, spawnEntry)
		state.RefreshBuildingMaxHPAtEntry(spawnEntry)
	}

	footprintEntries, _, reason := ecs.TerritoryFootprint(state, spawnEntry)
	if reason == "" {
		for _, entry := range footprintEntries {
			if entry == nil {
				continue
			}
			node := ecs.NodeC.Get(entry)
			node.Owner = playerID
			node.TerritoryOwner = playerID
		}
	} else {
		node := ecs.NodeC.Get(spawnEntry)
		node.Owner = playerID
		node.TerritoryOwner = playerID
	}

	building := ecs.BuildingC.Get(spawnEntry)
	building.Owner = playerID

	cityID := ecs.NodeC.Get(spawnEntry).ID
	player := state.Players[playerID]
	player.CapitalCityID = cityID
	player.CapitalCityCoreHP = building.HP
	city := state.EnsureCityState(playerID, cityID)
	city.CoreNodeID = cityID
	city.OwnerID = playerID
	city.OnlineOnTurn = 0

	spawnStartingInfantryForGameTest(state, playerID, spawnPos)
}

func spawnStartingInfantryForGameTest(state *domain.GameState, playerID string, spawnPos domain.Position) {
	if state == nil || state.World == nil {
		return
	}

	infantryPos := spawnPos
	for _, candidate := range spawnPos.Neighbors() {
		if canPlaceStartingInfantryForGameTest(state, candidate) {
			infantryPos = candidate
			break
		}
	}
	ecs.CreateUnit(state.World, string(domain.UnitTypeInfantry), playerID, infantryPos)
}

func canPlaceStartingInfantryForGameTest(state *domain.GameState, pos domain.Position) bool {
	if state == nil || state.World == nil {
		return false
	}

	entry, ok := domain.GetNodeAt(state.World, pos)
	if !ok || entry == nil || entry.HasComponent(ecs.BuildingC) || hasAnyUnitAtPositionForGameTest(state.World, pos) {
		return false
	}
	node := ecs.NodeC.Get(entry)
	terrain, ok := staticdata.Default().GetTerrain(string(node.Terrain))
	if !ok {
		return false
	}
	if node.HasRoad && terrain.PassableWithRoad {
		return true
	}
	return terrain.Passable
}

func hasAnyUnitAtPositionForGameTest(world donburi.World, pos domain.Position) bool {
	found := false
	ecs.AllUnits(world).Each(world, func(entry *donburi.Entry) {
		if found || entry == nil {
			return
		}
		unitPos := ecs.PositionC.Get(entry)
		found = unitPos.Q == pos.Q && unitPos.R == pos.R
	})
	return found
}

func countOwnedUnitsByTypeForGameTest(world donburi.World, owner string, unitType string) int {
	count := 0
	ecs.AllUnits(world).Each(world, func(entry *donburi.Entry) {
		if entry == nil {
			return
		}
		stats := ecs.UnitStatsC.Get(entry)
		if stats.Faction == owner && string(stats.Type) == unitType {
			count++
		}
	})
	return count
}

func ownedBuildingTypesForGameTest(state *domain.GameState, owner string) []string {
	if state == nil || state.World == nil {
		return nil
	}

	buildings := make([]string, 0)
	ecs.NodesWithBuilding(state.World).Each(state.World, func(entry *donburi.Entry) {
		if entry == nil {
			return
		}
		building := ecs.BuildingC.Get(entry)
		if building.Owner == owner {
			buildings = append(buildings, string(building.Type))
		}
	})
	sort.Strings(buildings)
	return buildings
}
