// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现地图加载引擎的程序化地图生成逻辑。

package maploader

import (
	"math"
	"math/rand"

	"github.com/elebirds/panoptes/internal/staticdata"
)

type mapPoint struct {
	X int
	Y int
}

type terrainPatchCfg struct {
	Terrain    string
	PatchCount int
	MinSize    int
	MaxSize    int
}

// GenerateProceduralMap builds a runtime map with clustered terrain, even-ish resource spread,
// and randomized spawn points with minimum spacing.
func GenerateProceduralMap(base *staticdata.MapRuntimeBundle, playerCount int, seed int64) *staticdata.MapRuntimeBundle {
	if base == nil {
		return nil
	}

	width := base.Width
	height := base.Height
	if width <= 0 {
		width = 20
	}
	if height <= 0 {
		height = 20
	}

	if playerCount < 2 {
		playerCount = 2
	}

	rng := rand.New(rand.NewSource(seed))
	terrainGrid := make([][]string, height)
	for y := 0; y < height; y++ {
		terrainGrid[y] = make([]string, width)
		for x := 0; x < width; x++ {
			terrainGrid[y][x] = "plain"
		}
	}

	area := width * height
	cfgs := []terrainPatchCfg{
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

	for _, cfg := range cfgs {
		for i := 0; i < cfg.PatchCount; i++ {
			start, ok := pickRandomPlainCell(rng, terrainGrid, width, height)
			if !ok {
				break
			}
			target := cfg.MinSize
			if cfg.MaxSize > cfg.MinSize {
				target = cfg.MinSize + rng.Intn(cfg.MaxSize-cfg.MinSize+1)
			}
			growTerrainBlob(rng, terrainGrid, width, height, start, cfg.Terrain, target)
		}
	}

	for i := 0; i < 2; i++ {
		smoothTerrainOnce(terrainGrid, width, height)
	}

	resourceCount := clamp(area/40, playerCount*4, 24)
	if resourceCount < 8 {
		resourceCount = 8
	}
	resourcePositions := placeResourcesEvenly(rng, terrainGrid, width, height, resourceCount)
	resourceByPos := make(map[mapPoint]string, len(resourcePositions))
	resourceKinds := []string{"food", "wood", "ore"}
	for i, p := range resourcePositions {
		resourceByPos[p] = resourceKinds[i%len(resourceKinds)]
	}

	spawnPoints := pickSpawnPoints(rng, terrainGrid, width, height, resourceByPos, playerCount)

	nodes := make([]staticdata.MapRuntimeNode, 0, area)
	for y := 0; y < height; y++ {
		for x := 0; x < width; x++ {
			p := mapPoint{X: x, Y: y}
			resourceType := ""
			isResource := false
			if rt, ok := resourceByPos[p]; ok {
				isResource = true
				resourceType = rt
			}

			nodes = append(nodes, staticdata.MapRuntimeNode{
				ID:              makeNodeID(x, y),
				X:               x,
				Y:               y,
				Terrain:         terrainGrid[y][x],
				HasRoad:         false,
				IsResourcePoint: isResource,
				ResourceType:    resourceType,
				Owner:           "",
				TerritoryOwner:  "",
				BuildingType:    "",
				BuildingHP:      0,
			})
		}
	}

	return &staticdata.MapRuntimeBundle{
		ID:            base.ID,
		Name:          base.Name,
		Width:         width,
		Height:        height,
		Nodes:         nodes,
		SpawnPoints:   spawnPoints,
		NamedNodes:    map[string]string{},
		CentralPoints: []string{},
		Tags:          base.Tags,
	}
}

func makeNodeID(x, y int) string {
	return "N_" + itoa(x) + "_" + itoa(y)
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

func pickSpawnPoints(rng *rand.Rand, grid [][]string, width, height int, resources map[mapPoint]string, playerCount int) []staticdata.SpawnPoint {
	candidates := make([]mapPoint, 0, width*height)
	for y := 0; y < height; y++ {
		for x := 0; x < width; x++ {
			p := mapPoint{X: x, Y: y}
			if _, hasResource := resources[p]; hasResource {
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

func minDistanceToSet(p mapPoint, set []mapPoint) int {
	if len(set) == 0 {
		return int(^uint(0) >> 1)
	}
	best := int(^uint(0) >> 1)
	for _, q := range set {
		d := manhattan(p, q)
		if d < best {
			best = d
		}
	}
	return best
}

func hasPointTooClose(p mapPoint, set []mapPoint, minDist int) bool {
	for _, q := range set {
		if manhattan(p, q) < minDist {
			return true
		}
	}
	return false
}

func manhattan(a, b mapPoint) int {
	dx := a.X - b.X
	if dx < 0 {
		dx = -dx
	}
	dy := a.Y - b.Y
	if dy < 0 {
		dy = -dy
	}
	return dx + dy
}

func clamp(v, minV, maxV int) int {
	if v < minV {
		return minV
	}
	if v > maxV {
		return maxV
	}
	return v
}

func min(a, b int) int {
	if a < b {
		return a
	}
	return b
}

func max(a, b int) int {
	if a > b {
		return a
	}
	return b
}

func itoa(v int) string {
	if v == 0 {
		return "0"
	}
	sign := ""
	if v < 0 {
		sign = "-"
		v = -v
	}
	buf := [20]byte{}
	i := len(buf)
	for v > 0 {
		i--
		buf[i] = byte('0' + v%10)
		v /= 10
	}
	return sign + string(buf[i:])
}
