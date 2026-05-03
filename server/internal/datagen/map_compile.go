// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: Compiles authored map definitions into runtime map bundles.

package datagen

import (
	"fmt"
	"math"
	"os"
	"path/filepath"
	"sort"

	"github.com/elebirds/panoptes/internal/staticdata"
)

func compileMaps(repoRoot string) (map[string]*staticdata.MapRuntimeBundle, []staticdata.MapCatalogEntry, error) {
	root := filepath.Join(repoRoot, "data/content/maps")
	entries, err := os.ReadDir(root)
	if err != nil {
		return nil, nil, fmt.Errorf("read maps dir: %w", err)
	}
	bundles := make(map[string]*staticdata.MapRuntimeBundle)
	catalogEntries := make([]staticdata.MapCatalogEntry, 0, len(entries))
	for _, entry := range entries {
		if !entry.IsDir() {
			continue
		}
		mapID := entry.Name()
		def, err := readJSON[staticdata.MapDefinition](filepath.Join(root, mapID, "definition.json"))
		if err != nil {
			return nil, nil, err
		}
		uiPath := filepath.Join(repoRoot, "data/ui/catalogs/maps", mapID+".json")
		ui, err := readJSON[staticdata.MapUICatalog](uiPath)
		if err != nil {
			return nil, nil, err
		}
		runtime := compileMapDefinition(def)
		bundles[mapID] = runtime
		catalogEntries = append(catalogEntries, staticdata.MapCatalogEntry{
			ID:           mapID,
			Name:         ui.Name,
			Description:  ui.Description,
			ThumbnailKey: ui.ThumbnailKey,
			Width:        runtime.Width,
			Height:       runtime.Height,
			Tags:         runtime.Tags,
		})
	}
	sort.Slice(catalogEntries, func(i, j int) bool { return catalogEntries[i].ID < catalogEntries[j].ID })
	return bundles, catalogEntries, nil
}

func compileMapDefinition(def staticdata.MapDefinition) *staticdata.MapRuntimeBundle {
	nodes := make([]staticdata.MapRuntimeNode, 0, def.Meta.Width*def.Meta.Height)
	index := make(map[[2]int]int, def.Meta.Width*def.Meta.Height)
	for y := 0; y < def.Meta.Height; y++ {
		for x := 0; x < def.Meta.Width; x++ {
			node := staticdata.MapRuntimeNode{
				ID:      coordinateNodeID(x, y),
				X:       x,
				Y:       y,
				Terrain: def.Meta.DefaultTerrain,
			}
			index[[2]int{x, y}] = len(nodes)
			nodes = append(nodes, node)
		}
	}

	if def.Generator != nil {
		applyGenerator(def.Generator, nodes, index)
	}
	for _, patch := range def.TerrainPatches {
		applyTerrainPatch(patch, nodes, index)
	}
	for _, road := range def.Features.Roads {
		for _, point := range road.Points {
			if idx, ok := index[[2]int{point.X, point.Y}]; ok {
				nodes[idx].HasRoad = true
			}
		}
	}
	namedNodes := make(map[string]string)
	for _, feature := range def.Features.ResourcePoints {
		if idx, ok := index[[2]int{feature.X, feature.Y}]; ok {
			nodes[idx].IsResourcePoint = true
			nodes[idx].ResourceType = feature.ResourceType
			if feature.NodeName != "" {
				nodes[idx].NodeName = feature.NodeName
				namedNodes[nodes[idx].ID] = feature.NodeName
			}
		}
	}
	for _, feature := range def.Features.NamedNodes {
		if idx, ok := index[[2]int{feature.X, feature.Y}]; ok {
			nodes[idx].NodeName = feature.Name
			namedNodes[nodes[idx].ID] = feature.Name
		}
	}
	for _, override := range def.NodeOverrides {
		if idx, ok := index[[2]int{override.X, override.Y}]; ok {
			if override.ID != "" {
				nodes[idx].ID = override.ID
			}
			if override.Terrain != "" {
				nodes[idx].Terrain = override.Terrain
			}
			nodes[idx].HasRoad = nodes[idx].HasRoad || override.HasRoad
			if override.IsResourcePoint {
				nodes[idx].IsResourcePoint = true
			}
			if override.ResourceType != "" {
				nodes[idx].ResourceType = override.ResourceType
			}
			if override.NodeName != "" {
				nodes[idx].NodeName = override.NodeName
				namedNodes[nodes[idx].ID] = override.NodeName
			}
			if override.Owner != "" {
				nodes[idx].Owner = override.Owner
			}
			if override.OwnerSlot != nil {
				slot := *override.OwnerSlot
				nodes[idx].OwnerSlot = &slot
			}
			if override.TerritoryOwner != "" {
				nodes[idx].TerritoryOwner = override.TerritoryOwner
			}
			if override.TerritoryOwnerSlot != nil {
				slot := *override.TerritoryOwnerSlot
				nodes[idx].TerritoryOwnerSlot = &slot
			}
			if override.BuildingType != "" {
				nodes[idx].BuildingType = override.BuildingType
			}
			if override.BuildingHP > 0 {
				nodes[idx].BuildingHP = override.BuildingHP
			}
		}
	}

	centralPoints := make([]string, 0, len(def.Features.CentralPoints))
	for _, point := range def.Features.CentralPoints {
		if idx, ok := index[[2]int{point.X, point.Y}]; ok {
			centralPoints = append(centralPoints, nodes[idx].ID)
		}
	}

	return &staticdata.MapRuntimeBundle{
		ID:            def.Meta.ID,
		Name:          def.Meta.Name,
		Width:         def.Meta.Width,
		Height:        def.Meta.Height,
		Nodes:         nodes,
		SpawnPoints:   def.SpawnPoints,
		NamedNodes:    namedNodes,
		CentralPoints: centralPoints,
		Tags:          def.Meta.Tags,
	}
}

func applyGenerator(generator *staticdata.MapGenerator, nodes []staticdata.MapRuntimeNode, index map[[2]int]int) {
	if generator == nil || generator.Type == "" {
		return
	}
	switch generator.Type {
	case "noise":
		for pos, idx := range index {
			value := pseudoNoise(generator.Seed, pos[0], pos[1])
			for _, band := range generator.TerrainBands {
				if value <= band.Max {
					nodes[idx].Terrain = band.Terrain
					break
				}
			}
		}
	}
}

func applyTerrainPatch(patch staticdata.TerrainPatch, nodes []staticdata.MapRuntimeNode, index map[[2]int]int) {
	switch patch.Kind {
	case "rect":
		for y := patch.Y; y < patch.Y+patch.Height; y++ {
			for x := patch.X; x < patch.X+patch.Width; x++ {
				if idx, ok := index[[2]int{x, y}]; ok {
					nodes[idx].Terrain = patch.Terrain
				}
			}
		}
	default:
		for _, point := range patch.Points {
			if idx, ok := index[[2]int{point.X, point.Y}]; ok {
				nodes[idx].Terrain = patch.Terrain
			}
		}
	}
}

func pseudoNoise(seed int64, x, y int) float64 {
	value := math.Sin(float64((x+1)*(y+3)) + float64(seed)*0.173)
	return value - math.Floor(value)
}

func coordinateNodeID(x, y int) string {
	return excelColumn(x) + fmt.Sprintf("%d", y+1)
}

func excelColumn(x int) string {
	value := x + 1
	if value <= 0 {
		return "A"
	}
	parts := make([]byte, 0, 4)
	for value > 0 {
		value--
		parts = append([]byte{byte('A' + (value % 26))}, parts...)
		value /= 26
	}
	return string(parts)
}
