// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: Tests procedural map generation behavior.

package maploader

import (
	"reflect"
	"testing"

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
