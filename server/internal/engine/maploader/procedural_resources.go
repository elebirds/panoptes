// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: Procedural resource placement helpers.

package maploader

import (
	"math"
	"math/rand"
)

func assignResourceTypes(resourcePositions []mapPoint) map[mapPoint]string {
	resourceByPos := make(map[mapPoint]string, len(resourcePositions))
	resourceKinds := []string{"food", "wood", "ore"}
	for i, p := range resourcePositions {
		resourceByPos[p] = resourceKinds[i%len(resourceKinds)]
	}
	return resourceByPos
}

func placeResourcesEvenly(rng *rand.Rand, grid [][]string, width, height, count int) []mapPoint {
	if count <= 0 {
		return nil
	}

	rows := int(math.Sqrt(float64(count)))
	if rows < 1 {
		rows = 1
	}
	cols := int(math.Ceil(float64(count) / float64(rows)))
	if cols < 1 {
		cols = 1
	}

	minSpacing := clamp(min(width, height)/6, 2, 5)
	results := make([]mapPoint, 0, count)

	for idx := 0; idx < count; idx++ {
		row := idx / cols
		col := idx % cols

		x0 := col * width / cols
		x1 := (col + 1) * width / cols
		y0 := row * height / rows
		y1 := (row + 1) * height / rows
		if x1 <= x0 {
			x1 = min(width, x0+1)
		}
		if y1 <= y0 {
			y1 = min(height, y0+1)
		}

		p, ok := pickResourceInRect(rng, grid, x0, x1, y0, y1, results, minSpacing)
		if !ok {
			p, ok = pickResourceInRect(rng, grid, 0, width, 0, height, results, max(1, minSpacing-1))
		}
		if !ok {
			continue
		}
		results = append(results, p)
	}

	return results
}

func pickResourceInRect(rng *rand.Rand, grid [][]string, x0, x1, y0, y1 int, placed []mapPoint, minSpacing int) (mapPoint, bool) {
	width := len(grid[0])
	height := len(grid)

	for i := 0; i < 120; i++ {
		x := x0 + rng.Intn(max(1, x1-x0))
		y := y0 + rng.Intn(max(1, y1-y0))
		if x < 0 || x >= width || y < 0 || y >= height {
			continue
		}
		if !isResourceCandidateTerrain(grid[y][x]) {
			continue
		}
		p := mapPoint{X: x, Y: y}
		if hasPointTooClose(p, placed, minSpacing) {
			continue
		}
		return p, true
	}

	for y := y0; y < y1; y++ {
		for x := x0; x < x1; x++ {
			if x < 0 || x >= width || y < 0 || y >= height {
				continue
			}
			if !isResourceCandidateTerrain(grid[y][x]) {
				continue
			}
			p := mapPoint{X: x, Y: y}
			if hasPointTooClose(p, placed, minSpacing) {
				continue
			}
			return p, true
		}
	}

	return mapPoint{}, false
}

func isResourceCandidateTerrain(terrain string) bool {
	switch terrain {
	case "river", "water", "forbidden", "blocked":
		return false
	default:
		return true
	}
}
