// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: Tests procedural map generation behavior.

package maploader

import (
	"reflect"
	"testing"

	"github.com/elebirds/panoptes/internal/algo/geometry"
	"github.com/elebirds/panoptes/internal/staticdata"
)

func TestGenerateProceduralMapDeterministicForSeed(t *testing.T) {
	previousCatalog := staticdata.Default()
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{InitialCityTerritoryRadius: 2},
	}))
	t.Cleanup(func() {
		staticdata.SetDefault(previousCatalog)
	})

	base := &staticdata.MapRuntimeBundle{
		ID:     "deterministic",
		Name:   "Deterministic",
		Width:  12,
		Height: 10,
		Nodes: []staticdata.MapRuntimeNode{
			{ID: "A1", X: 0, Y: 0, Terrain: "plain", NodeName: "origin"},
			{ID: "L10", X: 11, Y: 9, Terrain: "plain", NodeName: "corner"},
		},
		NamedNodes:    map[string]string{"A1": "origin", "L10": "corner"},
		CentralPoints: []string{"A1"},
		Tags:          []string{"test"},
	}

	first := GenerateProceduralMap(base, 3, 20260430)
	second := GenerateProceduralMap(base, 3, 20260430)

	if !reflect.DeepEqual(first, second) {
		t.Fatalf("GenerateProceduralMap() should be deterministic for the same seed")
	}
	if len(first.Nodes) != base.Width*base.Height {
		t.Fatalf("nodes len = %d, want %d", len(first.Nodes), base.Width*base.Height)
	}
	if len(first.SpawnPoints) != 3 {
		t.Fatalf("spawn points len = %d, want 3", len(first.SpawnPoints))
	}
}

func TestEnforcePassableTerrainAroundSpawnsClearsSixHexRadius(t *testing.T) {
	previousCatalog := staticdata.Default()
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true},
			{ID: "forest", Passable: true},
			{ID: "river", Passable: false},
			{ID: "mountain", Passable: false},
		},
	}))
	t.Cleanup(func() {
		staticdata.SetDefault(previousCatalog)
	})

	width := 15
	height := 15
	grid := make([][]string, height)
	for y := 0; y < height; y++ {
		grid[y] = make([]string, width)
		for x := 0; x < width; x++ {
			grid[y][x] = "river"
		}
	}
	grid[7][7] = "mountain"

	spawn := staticdata.SpawnPoint{Slot: 0, X: 7, Y: 7}
	enforcePassableTerrainAroundSpawns(grid, width, height, []staticdata.SpawnPoint{spawn}, spawnPassableRadius)

	center := geometry.OffsetToAxial(spawn.X, spawn.Y)
	for y := 0; y < height; y++ {
		for x := 0; x < width; x++ {
			distance := geometry.OffsetToAxial(x, y).DistanceTo(center)
			if distance <= spawnPassableRadius && grid[y][x] != "plain" {
				t.Fatalf("terrain at (%d,%d), distance %d = %q, want plain", x, y, distance, grid[y][x])
			}
			if distance > spawnPassableRadius && grid[y][x] != "river" {
				t.Fatalf("terrain outside spawn radius at (%d,%d), distance %d = %q, want river", x, y, distance, grid[y][x])
			}
		}
	}
}

func TestGenerateProceduralMapKeepsSpawnRadiusPassable(t *testing.T) {
	previousCatalog := staticdata.Default()
	catalog := staticdata.NewCatalog(staticdata.CatalogBundle{
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true},
			{ID: "forest", Passable: true},
			{ID: "river", Passable: false},
		},
		Rules: staticdata.Rules{InitialCityTerritoryRadius: 2},
	})
	staticdata.SetDefault(catalog)
	t.Cleanup(func() {
		staticdata.SetDefault(previousCatalog)
	})

	base := &staticdata.MapRuntimeBundle{
		ID:     "spawn-safe",
		Name:   "Spawn Safe",
		Width:  20,
		Height: 20,
	}

	for seed := int64(1); seed <= 30; seed++ {
		runtime := GenerateProceduralMap(base, 2, seed)
		if runtime == nil {
			t.Fatalf("GenerateProceduralMap(seed=%d) returned nil", seed)
		}

		for _, spawn := range runtime.SpawnPoints {
			center := geometry.OffsetToAxial(spawn.X, spawn.Y)
			for _, node := range runtime.Nodes {
				distance := geometry.OffsetToAxial(node.X, node.Y).DistanceTo(center)
				if distance > spawnPassableRadius {
					continue
				}
				terrain, ok := catalog.GetTerrain(node.Terrain)
				if !ok || !terrain.Passable {
					t.Fatalf("seed %d spawn slot %d has impassable terrain %q at (%d,%d), distance %d", seed, spawn.Slot, node.Terrain, node.X, node.Y, distance)
				}
			}
		}
	}
}
