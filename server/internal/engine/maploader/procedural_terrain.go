// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: Procedural terrain layout generation helpers.

package maploader

import (
	"math/rand"

	"github.com/elebirds/panoptes/internal/algo/geometry"
	"github.com/elebirds/panoptes/internal/staticdata"
)

const spawnPassableRadius = 6

type terrainPatchCfg struct {
	Terrain    string
	PatchCount int
	MinSize    int
	MaxSize    int
}

func newPlainTerrainGrid(width, height int) [][]string {
	grid := make([][]string, height)
	for y := 0; y < height; y++ {
		grid[y] = make([]string, width)
		for x := 0; x < width; x++ {
			grid[y][x] = "plain"
		}
	}
	return grid
}

func terrainPatchConfigs(area int) []terrainPatchCfg {
	return []terrainPatchCfg{
		{
			Terrain:    "forest",
			PatchCount: clamp(area/220, 2, 6),
			MinSize:    clamp(area/28, 10, 38),
			MaxSize:    clamp(area/18, 16, 65),
		},
		{
			Terrain:    "mountain",
			PatchCount: clamp(area/260, 1, 5),
			MinSize:    clamp(area/36, 8, 28),
			MaxSize:    clamp(area/24, 12, 42),
		},
		{
			Terrain:    "snow",
			PatchCount: clamp(area/320, 1, 4),
			MinSize:    clamp(area/42, 6, 20),
			MaxSize:    clamp(area/30, 10, 30),
		},
		{
			Terrain:    "river",
			PatchCount: clamp(area/360, 1, 3),
			MinSize:    clamp(area/46, 6, 18),
			MaxSize:    clamp(area/34, 10, 24),
		},
	}
}

func placeTerrainPatches(rng *rand.Rand, grid [][]string, width, height int, cfgs []terrainPatchCfg) {
	for _, cfg := range cfgs {
		for i := 0; i < cfg.PatchCount; i++ {
			start, ok := pickRandomPlainCell(rng, grid, width, height)
			if !ok {
				break
			}
			target := cfg.MinSize
			if cfg.MaxSize > cfg.MinSize {
				target = cfg.MinSize + rng.Intn(cfg.MaxSize-cfg.MinSize+1)
			}
			growTerrainBlob(rng, grid, width, height, start, cfg.Terrain, target)
		}
	}
}

func growTerrainBlob(rng *rand.Rand, grid [][]string, width, height int, start mapPoint, terrain string, target int) {
	if target <= 0 {
		return
	}

	frontier := make([]mapPoint, 0, target*2)
	visited := make(map[mapPoint]struct{}, target*4)
	frontier = append(frontier, start)
	placed := 0

	for len(frontier) > 0 && placed < target {
		idx := rng.Intn(len(frontier))
		p := frontier[idx]
		frontier[idx] = frontier[len(frontier)-1]
		frontier = frontier[:len(frontier)-1]

		if p.X < 0 || p.X >= width || p.Y < 0 || p.Y >= height {
			continue
		}
		if _, seen := visited[p]; seen {
			continue
		}
		visited[p] = struct{}{}

		if grid[p.Y][p.X] != "plain" {
			continue
		}

		grid[p.Y][p.X] = terrain
		placed++

		neighbors := []mapPoint{
			{X: p.X + 1, Y: p.Y},
			{X: p.X - 1, Y: p.Y},
			{X: p.X, Y: p.Y + 1},
			{X: p.X, Y: p.Y - 1},
			{X: p.X + 1, Y: p.Y + 1},
			{X: p.X - 1, Y: p.Y - 1},
			{X: p.X + 1, Y: p.Y - 1},
			{X: p.X - 1, Y: p.Y + 1},
		}
		for _, nb := range neighbors {
			if rng.Float64() < 0.78 {
				frontier = append(frontier, nb)
			}
		}

		if len(frontier) < 4 && placed < target {
			frontier = append(frontier, mapPoint{
				X: p.X + rng.Intn(5) - 2,
				Y: p.Y + rng.Intn(5) - 2,
			})
		}
	}
}

func smoothTerrainOnce(grid [][]string, width, height int) {
	next := make([][]string, height)
	for y := 0; y < height; y++ {
		next[y] = make([]string, width)
		copy(next[y], grid[y])
	}

	for y := 0; y < height; y++ {
		for x := 0; x < width; x++ {
			counts := map[string]int{}
			for dy := -1; dy <= 1; dy++ {
				for dx := -1; dx <= 1; dx++ {
					nx := x + dx
					ny := y + dy
					if nx < 0 || nx >= width || ny < 0 || ny >= height {
						continue
					}
					counts[grid[ny][nx]]++
				}
			}

			current := grid[y][x]
			bestTerrain := current
			bestCount := counts[current]
			for t, c := range counts {
				if c > bestCount {
					bestTerrain = t
					bestCount = c
				}
			}

			if bestTerrain != current && bestCount >= 5 {
				next[y][x] = bestTerrain
			}
		}
	}

	for y := 0; y < height; y++ {
		copy(grid[y], next[y])
	}
}

func pickRandomPlainCell(rng *rand.Rand, grid [][]string, width, height int) (mapPoint, bool) {
	for i := 0; i < 256; i++ {
		x := rng.Intn(width)
		y := rng.Intn(height)
		if grid[y][x] == "plain" {
			return mapPoint{X: x, Y: y}, true
		}
	}

	for y := 0; y < height; y++ {
		for x := 0; x < width; x++ {
			if grid[y][x] == "plain" {
				return mapPoint{X: x, Y: y}, true
			}
		}
	}

	return mapPoint{}, false
}

func enforcePassableTerrainAroundSpawns(grid [][]string, width, height int, spawns []staticdata.SpawnPoint, radius int) {
	if radius < 0 || width <= 0 || height <= 0 || len(grid) == 0 {
		return
	}

	for _, spawn := range spawns {
		center := geometry.OffsetToAxial(spawn.X, spawn.Y)
		for y := 0; y < height; y++ {
			if y < 0 || y >= len(grid) {
				continue
			}
			for x := 0; x < width; x++ {
				if x < 0 || x >= len(grid[y]) {
					continue
				}
				pos := geometry.OffsetToAxial(x, y)
				if pos.DistanceTo(center) > radius {
					continue
				}
				if isProceduralTerrainImpassable(grid[y][x]) {
					grid[y][x] = "plain"
				}
			}
		}
	}
}

func isProceduralTerrainImpassable(terrain string) bool {
	if catalog := staticdata.Default(); catalog != nil {
		if def, ok := catalog.GetTerrain(terrain); ok {
			return !def.Passable
		}
	}

	switch terrain {
	case "plain", "forest":
		return false
	default:
		return true
	}
}
