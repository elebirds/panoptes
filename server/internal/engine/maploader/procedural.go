// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现地图加载引擎的程序化地图生成逻辑。

package maploader

import (
	"math/rand"

	"github.com/elebirds/panoptes/internal/staticdata"
)

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
	terrainGrid := newPlainTerrainGrid(width, height)
	area := width * height
	placeTerrainPatches(rng, terrainGrid, width, height, terrainPatchConfigs(area))

	for i := 0; i < 2; i++ {
		smoothTerrainOnce(terrainGrid, width, height)
	}

	resourceCount := clamp(area/40, playerCount*4, 24)
	if resourceCount < 8 {
		resourceCount = 8
	}
	resourcePositions := placeResourcesEvenly(rng, terrainGrid, width, height, resourceCount)
	resourceByPos := assignResourceTypes(resourcePositions)

	spawnPoints := pickSpawnPoints(rng, terrainGrid, width, height, resourceByPos, playerCount)
	baseNodesByPos := indexBaseNodesByPos(base.Nodes)
	nodes := buildRuntimeNodes(terrainGrid, width, height, baseNodesByPos, resourceByPos)

	return &staticdata.MapRuntimeBundle{
		ID:            base.ID,
		Name:          base.Name,
		Width:         width,
		Height:        height,
		Nodes:         nodes,
		SpawnPoints:   spawnPoints,
		NamedNodes:    cloneNamedNodes(base.NamedNodes),
		CentralPoints: append([]string(nil), base.CentralPoints...),
		Tags:          base.Tags,
	}
}
