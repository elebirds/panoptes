// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: Procedural spawn assignment helpers.

package maploader

import (
	"math/rand"

	"github.com/elebirds/panoptes/internal/staticdata"
)

func pickSpawnPoints(rng *rand.Rand, grid [][]string, width, height int, resources map[mapPoint]string, playerCount int) []staticdata.SpawnPoint {
	candidates := make([]mapPoint, 0, width*height)
	margin := 1
	if catalog := staticdata.Default(); catalog != nil {
		if radius := catalog.Rules().InitialCityTerritoryRadius; radius > 0 {
			margin = radius
		}
	}
	for y := 0; y < height; y++ {
		for x := 0; x < width; x++ {
			p := mapPoint{X: x, Y: y}
			if _, hasResource := resources[p]; hasResource {
				continue
			}
			if x < margin || y < margin || x >= width-margin || y >= height-margin {
				continue
			}
			if !isResourceCandidateTerrain(grid[y][x]) {
				continue
			}
			candidates = append(candidates, p)
		}
	}
	if len(candidates) == 0 {
		for y := 0; y < height; y++ {
			for x := 0; x < width; x++ {
				candidates = append(candidates, mapPoint{X: x, Y: y})
			}
		}
	}

	minSpawnDistance := clamp(min(width, height)/3, 4, 10)
	selected := make([]mapPoint, 0, playerCount)
	remaining := append([]mapPoint(nil), candidates...)

	for i := 0; i < playerCount && len(remaining) > 0; i++ {
		bestIdx := 0
		bestScore := -1
		for idx, p := range remaining {
			score := minDistanceToSet(p, selected)
			if score > bestScore {
				bestScore = score
				bestIdx = idx
			} else if score == bestScore && rng.Intn(2) == 0 {
				bestIdx = idx
			}
		}

		chosen := remaining[bestIdx]
		if len(selected) > 0 && bestScore < minSpawnDistance {
			// still pick the farthest; map too crowded to satisfy strict spacing
			chosen = remaining[bestIdx]
		}
		selected = append(selected, chosen)
		remaining[bestIdx] = remaining[len(remaining)-1]
		remaining = remaining[:len(remaining)-1]
	}

	spawns := make([]staticdata.SpawnPoint, 0, len(selected))
	for slot, p := range selected {
		spawns = append(spawns, staticdata.SpawnPoint{
			Slot: slot,
			X:    p.X,
			Y:    p.Y,
		})
	}
	return spawns
}
