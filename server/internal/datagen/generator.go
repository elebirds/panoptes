// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现数据生成模块的数据生成主流程。

package datagen

import (
	"crypto/sha256"
	"encoding/json"
	"fmt"
	"math"
	"os"
	"path/filepath"
	"sort"
	"strings"

	"github.com/elebirds/panoptes/internal/staticdata"
)

type Options struct {
	RepoRoot string
}

func Validate(opts Options) error {
	_, _, _, err := loadAndCompile(opts)
	return err
}

func Generate(opts Options) error {
	bundle, maps, schemas, err := loadAndCompile(opts)
	if err != nil {
		return err
	}
	return emitGeneratedFiles(opts.RepoRoot, bundle, maps, schemas)
}

func loadAndCompile(opts Options) (staticdata.CatalogBundle, map[string]*staticdata.MapRuntimeBundle, schemaSet, error) {
	if opts.RepoRoot == "" {
		return staticdata.CatalogBundle{}, nil, nil, fmt.Errorf("repo root is required")
	}

	authored, err := loadAuthoredData(opts.RepoRoot)
	if err != nil {
		return staticdata.CatalogBundle{}, nil, nil, err
	}
	schemas := buildAuthoringSchemas(buildAuthoringSchemaContext(
		authored.Resources.Value.Resources,
		authored.Points.Value.Points,
		authored.Units.Value.Units,
		authored.Buildings.Value.Buildings,
		authored.Technologies.Value.Technologies,
		authored.Policies.Value.Policies,
		authored.Recipes.Value.Recipes,
		authored.Terrains.Value.Terrains,
	))
	if err := validateAuthoredSources(authored, schemas); err != nil {
		return staticdata.CatalogBundle{}, nil, nil, err
	}

	mergeUI(authored.Resources.Value.Resources, authored.ResourceUI.Value)
	mergePointUI(authored.Points.Value.Points, authored.PointUI.Value)
	mergeUnitUI(authored.Units.Value.Units, authored.UnitUI.Value)
	mergeBuildingUI(authored.Buildings.Value.Buildings, authored.BuildingUI.Value)
	mergeTechnologyUI(authored.Technologies.Value.Technologies, authored.TechnologyUI.Value)
	mergePolicyUI(authored.Policies.Value.Policies, authored.PolicyUI.Value)
	mergeRecipeUI(authored.Recipes.Value.Recipes, authored.RecipeUI.Value)
	mergeTerrainUI(authored.Terrains.Value.Terrains, authored.TerrainUI.Value)

	maps, entries, err := compileMaps(opts.RepoRoot)
	if err != nil {
		return staticdata.CatalogBundle{}, nil, nil, err
	}

	bundle := staticdata.CatalogBundle{
		Manifest:          authored.Manifest.Value,
		Resources:         authored.Resources.Value.Resources,
		Points:            authored.Points.Value.Points,
		Units:             authored.Units.Value.Units,
		Buildings:         authored.Buildings.Value.Buildings,
		Technologies:      authored.Technologies.Value.Technologies,
		Policies:          authored.Policies.Value.Policies,
		Recipes:           authored.Recipes.Value.Recipes,
		Terrains:          authored.Terrains.Value.Terrains,
		Rules:             authored.Rules.Value,
		Ministers:         authored.Ministers.Value.Pool,
		Maps:              entries,
		UITechTreeLayout:  authored.TechnologyTreeUI.Value,
		UIBuildMenuLayout: buildBuildMenuLayout(authored.Buildings.Value.Buildings),
		UIRecipeLayout:    buildRecipeLayout(authored.Recipes.Value.Recipes),
	}

	sectionPayloads, sectionHashes, err := staticdata.BuildSectionPayloads(bundle)
	if err != nil {
		return staticdata.CatalogBundle{}, nil, nil, err
	}
	_ = sectionPayloads
	bundle.Manifest.RequiredSections = staticdata.RequiredCatalogSections()
	bundle.Manifest.SectionHashes = sectionHashes

	hash, err := computeBundleHash(bundle, maps)
	if err != nil {
		return staticdata.CatalogBundle{}, nil, nil, err
	}
	bundle.Manifest.BundleHash = hash

	return bundle, maps, schemas, nil
}

func emitGeneratedFiles(repoRoot string, bundle staticdata.CatalogBundle, maps map[string]*staticdata.MapRuntimeBundle, schemas schemaSet) error {
	serverGen := filepath.Join(repoRoot, "data/generated/server")
	clientData := filepath.Join(repoRoot, "client/Assets/Resources/Data")
	schemaDir := filepath.Join(repoRoot, "data/schema")
	serverGoGen := filepath.Join(repoRoot, "server/internal/staticdata/generated")
	clientCodeGen := filepath.Join(repoRoot, "client/Assets/Scripts/Runtime/Core/Foundation/Domain")
	protocolDir := filepath.Join(repoRoot, "protocol")

	dirs := []string{
		serverGen,
		filepath.Join(serverGen, "maps"),
		filepath.Join(serverGen, "sections"),
		clientData,
		filepath.Join(clientData, "maps"),
		filepath.Join(clientData, "sections"),
		filepath.Join(schemaDir, "registry"),
		filepath.Join(schemaDir, "content"),
		filepath.Join(schemaDir, "content", "maps"),
		filepath.Join(schemaDir, "ui"),
		filepath.Join(schemaDir, "ui", "maps"),
		serverGoGen,
		clientCodeGen,
		protocolDir,
	}
	for _, dir := range dirs {
		if err := os.MkdirAll(dir, 0o755); err != nil {
			return fmt.Errorf("mkdir %q: %w", dir, err)
		}
	}
	if err := removeLegacyGeneratedFiles(repoRoot, schemaDir); err != nil {
		return err
	}

	if err := writePrettyJSON(filepath.Join(serverGen, "catalog.bundle.json"), bundle); err != nil {
		return err
	}
	if err := writePrettyJSON(filepath.Join(clientData, "catalog.bundle.json"), bundle); err != nil {
		return err
	}
	for id, runtime := range maps {
		if err := writePrettyJSON(filepath.Join(serverGen, "maps", id+".runtime.json"), runtime); err != nil {
			return err
		}
		if err := writePrettyJSON(filepath.Join(clientData, "maps", id+".runtime.json"), runtime); err != nil {
			return err
		}
	}
	for _, section := range staticdata.CatalogSectionValues(bundle) {
		if err := writePrettyJSON(filepath.Join(serverGen, "sections", section.Name+".json"), section.Value); err != nil {
			return err
		}
		if err := writePrettyJSON(filepath.Join(clientData, "sections", section.Name+".json"), section.Value); err != nil {
			return err
		}
	}

	if err := emitSchemas(schemaDir, schemas); err != nil {
		return err
	}
	if err := os.WriteFile(filepath.Join(protocolDir, "data_types.proto"), []byte(renderDataTypesProto()), 0o600); err != nil {
		return fmt.Errorf("write data_types.proto: %w", err)
	}
	if err := os.WriteFile(filepath.Join(protocolDir, "data_catalog.proto"), []byte(renderDataCatalogProto()), 0o600); err != nil {
		return fmt.Errorf("write data_catalog.proto: %w", err)
	}
	if err := os.WriteFile(filepath.Join(protocolDir, "map_catalog.proto"), []byte(renderMapCatalogProto()), 0o600); err != nil {
		return fmt.Errorf("write map_catalog.proto: %w", err)
	}
	if err := os.WriteFile(filepath.Join(serverGoGen, "resource_keys_gen.go"), []byte(renderGoResourceKeys(bundle)), 0o600); err != nil {
		return fmt.Errorf("write resource_keys_gen.go: %w", err)
	}
	if err := os.WriteFile(filepath.Join(clientCodeGen, "ResourceKeys.g.cs"), []byte(renderCSharpResourceKeys(bundle)), 0o600); err != nil {
		return fmt.Errorf("write ResourceKeys.g.cs: %w", err)
	}

	return nil
}

func removeLegacyGeneratedFiles(repoRoot string, schemaDir string) error {
	legacyFiles := []string{
		filepath.Join(schemaDir, "ui", "resource_catalog.schema.json"),
		filepath.Join(repoRoot, "client/Assets/Scripts/Runtime/Data/Generated/ResourceKeys.g.cs"),
		filepath.Join(repoRoot, "client/Assets/Scripts/Runtime/Data/Generated/ResourceKeys.g.cs.meta"),
		filepath.Join(repoRoot, "client/Assets/Scripts/Runtime/Data/Generated.meta"),
		filepath.Join(repoRoot, "client/Assets/Scripts/Runtime/Data.meta"),
	}
	for _, path := range legacyFiles {
		err := os.Remove(path)
		if err != nil && !os.IsNotExist(err) {
			return fmt.Errorf("remove legacy generated file %q: %w", path, err)
		}
	}
	_ = os.Remove(filepath.Join(repoRoot, "client/Assets/Scripts/Runtime/Data/Generated"))
	_ = os.Remove(filepath.Join(repoRoot, "client/Assets/Scripts/Runtime/Data"))
	return nil
}

func emitSchemas(schemaDir string, schemas schemaSet) error {
	for rel, value := range schemas {
		if err := writePrettyJSON(filepath.Join(schemaDir, rel), value); err != nil {
			return err
		}
	}
	return nil
}

func computeBundleHash(bundle staticdata.CatalogBundle, maps map[string]*staticdata.MapRuntimeBundle) (string, error) {
	h := sha256.New()
	raw, err := json.Marshal(bundle)
	if err != nil {
		return "", fmt.Errorf("marshal bundle hash input: %w", err)
	}
	_, _ = h.Write(raw)
	ids := make([]string, 0, len(maps))
	for id := range maps {
		ids = append(ids, id)
	}
	sort.Strings(ids)
	for _, id := range ids {
		mapRaw, err := json.Marshal(maps[id])
		if err != nil {
			return "", fmt.Errorf("marshal map hash input %q: %w", id, err)
		}
		_, _ = h.Write(mapRaw)
	}
	return fmt.Sprintf("%x", h.Sum(nil)), nil
}

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

func mergeUI(resources []staticdata.ResourceDescriptor, ui staticdata.ResourceCatalogUIFile) {
	uiByID := make(map[string]struct {
		Name        string
		Description string
		IconKey     string
		SortOrder   int
		Tags        []string
	}, len(ui.Resources))
	for _, entry := range ui.Resources {
		uiByID[entry.ID] = struct {
			Name        string
			Description string
			IconKey     string
			SortOrder   int
			Tags        []string
		}{entry.Name, entry.Description, entry.IconKey, entry.SortOrder, entry.Tags}
	}
	for i := range resources {
		if entry, ok := uiByID[resources[i].Key]; ok {
			resources[i].DisplayName = entry.Name
			resources[i].Description = entry.Description
			resources[i].IconKey = entry.IconKey
			resources[i].SortOrder = entry.SortOrder
			resources[i].Tags = append([]string(nil), entry.Tags...)
		}
	}
}

func mergePointUI(points []staticdata.PointDescriptor, ui staticdata.PointCatalogUIFile) {
	uiByID := make(map[string]struct {
		Name        string
		Description string
		IconKey     string
		SortOrder   int
		Tags        []string
	}, len(ui.Points))
	for _, entry := range ui.Points {
		uiByID[entry.ID] = struct {
			Name        string
			Description string
			IconKey     string
			SortOrder   int
			Tags        []string
		}{entry.Name, entry.Description, entry.IconKey, entry.SortOrder, entry.Tags}
	}
	for i := range points {
		if entry, ok := uiByID[points[i].Key]; ok {
			points[i].DisplayName = entry.Name
			points[i].Description = entry.Description
			points[i].IconKey = entry.IconKey
			points[i].SortOrder = entry.SortOrder
			points[i].Tags = append([]string(nil), entry.Tags...)
		}
	}
}

func mergeUnitUI(units []staticdata.UnitDefinition, ui staticdata.UnitCatalogUIFile) {
	uiByID := make(map[string]struct {
		Name        string
		Description string
		IconKey     string
		PrefabKey   string
		SortOrder   int
		Tags        []string
	}, len(ui.Units))
	for _, entry := range ui.Units {
		uiByID[entry.ID] = struct {
			Name        string
			Description string
			IconKey     string
			PrefabKey   string
			SortOrder   int
			Tags        []string
		}{entry.Name, entry.Description, entry.IconKey, entry.PrefabKey, entry.SortOrder, entry.Tags}
	}
	for i := range units {
		if entry, ok := uiByID[units[i].ID]; ok {
			units[i].Name = entry.Name
			units[i].Description = entry.Description
			units[i].IconKey = entry.IconKey
			units[i].PrefabKey = entry.PrefabKey
			units[i].SortOrder = entry.SortOrder
			units[i].Tags = append([]string(nil), entry.Tags...)
		}
	}
}

func mergeBuildingUI(buildings []staticdata.BuildingDefinition, ui staticdata.BuildingCatalogUIFile) {
	uiByID := make(map[string]struct {
		Name        string
		Description string
		IconKey     string
		PrefabKey   string
		SortOrder   int
		Tags        []string
	}, len(ui.Buildings))
	for _, entry := range ui.Buildings {
		uiByID[entry.ID] = struct {
			Name        string
			Description string
			IconKey     string
			PrefabKey   string
			SortOrder   int
			Tags        []string
		}{entry.Name, entry.Description, entry.IconKey, entry.PrefabKey, entry.SortOrder, entry.Tags}
	}
	for i := range buildings {
		if entry, ok := uiByID[buildings[i].ID]; ok {
			buildings[i].Name = entry.Name
			buildings[i].Description = entry.Description
			buildings[i].IconKey = entry.IconKey
			buildings[i].PrefabKey = entry.PrefabKey
			buildings[i].SortOrder = entry.SortOrder
			buildings[i].Tags = append([]string(nil), entry.Tags...)
		}
	}
}

func mergeTechnologyUI(technologies []staticdata.TechnologyDefinition, ui staticdata.TechnologyCatalogUIFile) {
	uiByID := make(map[string]struct {
		Name        string
		Description string
		IconKey     string
		SortOrder   int
		Tags        []string
	}, len(ui.Technologies))
	for _, entry := range ui.Technologies {
		uiByID[entry.ID] = struct {
			Name        string
			Description string
			IconKey     string
			SortOrder   int
			Tags        []string
		}{entry.Name, entry.Description, entry.IconKey, entry.SortOrder, entry.Tags}
	}
	for i := range technologies {
		if entry, ok := uiByID[technologies[i].ID]; ok {
			technologies[i].Name = entry.Name
			technologies[i].Description = entry.Description
			technologies[i].IconKey = entry.IconKey
			technologies[i].SortOrder = entry.SortOrder
			technologies[i].Tags = append([]string(nil), entry.Tags...)
		}
	}
}

func buildBuildMenuLayout(buildings []staticdata.BuildingDefinition) staticdata.BuildMenuLayout {
	type item struct {
		ID        string
		SortOrder int
	}
	items := make([]item, 0, len(buildings))
	for _, building := range buildings {
		if strings.TrimSpace(building.ID) == "" {
			continue
		}
		items = append(items, item{ID: building.ID, SortOrder: building.SortOrder})
	}
	sort.Slice(items, func(i, j int) bool {
		if items[i].SortOrder != items[j].SortOrder {
			return items[i].SortOrder < items[j].SortOrder
		}
		return items[i].ID < items[j].ID
	})
	order := make([]string, 0, len(items))
	for _, item := range items {
		order = append(order, item.ID)
	}
	return staticdata.BuildMenuLayout{
		ConfigVersion:     "2026-04-17",
		BuildingOrder:     order,
		HiddenBuildingIDs: []string{"city_core"},
	}
}

func mergePolicyUI(policies []staticdata.PolicyDefinition, ui staticdata.PolicyCatalogUIFile) {
	uiByID := make(map[string]struct {
		Name        string
		Description string
		IconKey     string
		SortOrder   int
		Tags        []string
	}, len(ui.Policies))
	for _, entry := range ui.Policies {
		uiByID[entry.ID] = struct {
			Name        string
			Description string
			IconKey     string
			SortOrder   int
			Tags        []string
		}{entry.Name, entry.Description, entry.IconKey, entry.SortOrder, entry.Tags}
	}
	for i := range policies {
		if entry, ok := uiByID[policies[i].ID]; ok {
			policies[i].Name = entry.Name
			policies[i].Description = entry.Description
			policies[i].IconKey = entry.IconKey
			policies[i].SortOrder = entry.SortOrder
			policies[i].Tags = append([]string(nil), entry.Tags...)
		}
	}
}

func mergeRecipeUI(recipes []staticdata.RecipeDefinition, ui staticdata.RecipeCatalogUIFile) {
	uiByID := make(map[string]struct {
		Name        string
		Description string
		IconKey     string
		SortOrder   int
		Tags        []string
	}, len(ui.Recipes))
	for _, entry := range ui.Recipes {
		uiByID[entry.ID] = struct {
			Name        string
			Description string
			IconKey     string
			SortOrder   int
			Tags        []string
		}{entry.Name, entry.Description, entry.IconKey, entry.SortOrder, entry.Tags}
	}
	for i := range recipes {
		if entry, ok := uiByID[recipes[i].ID]; ok {
			recipes[i].Name = entry.Name
			recipes[i].Description = entry.Description
			recipes[i].IconKey = entry.IconKey
			recipes[i].SortOrder = entry.SortOrder
			recipes[i].Tags = append([]string(nil), entry.Tags...)
		}
	}
}

func buildRecipeLayout(recipes []staticdata.RecipeDefinition) staticdata.RecipeLayout {
	type item struct {
		ID        string
		SortOrder int
	}
	items := make([]item, 0, len(recipes))
	for _, recipe := range recipes {
		if strings.TrimSpace(recipe.ID) == "" {
			continue
		}
		items = append(items, item{ID: recipe.ID, SortOrder: recipe.SortOrder})
	}
	sort.Slice(items, func(i, j int) bool {
		if items[i].SortOrder != items[j].SortOrder {
			return items[i].SortOrder < items[j].SortOrder
		}
		return items[i].ID < items[j].ID
	})
	order := make([]string, 0, len(items))
	for _, item := range items {
		order = append(order, item.ID)
	}
	return staticdata.RecipeLayout{
		ConfigVersion: "2026-04-17",
		RecipeOrder:   order,
	}
}

func mergeTerrainUI(terrains []staticdata.TerrainDefinition, ui staticdata.TerrainCatalogUIFile) {
	uiByID := make(map[string]struct {
		Name        string
		Description string
		IconKey     string
		MaterialKey string
		SortOrder   int
		Tags        []string
	}, len(ui.Terrains))
	for _, entry := range ui.Terrains {
		uiByID[entry.ID] = struct {
			Name        string
			Description string
			IconKey     string
			MaterialKey string
			SortOrder   int
			Tags        []string
		}{entry.Name, entry.Description, entry.IconKey, entry.MaterialKey, entry.SortOrder, entry.Tags}
	}
	for i := range terrains {
		if entry, ok := uiByID[terrains[i].ID]; ok {
			terrains[i].Name = entry.Name
			terrains[i].Description = entry.Description
			terrains[i].IconKey = entry.IconKey
			terrains[i].MaterialKey = entry.MaterialKey
			terrains[i].SortOrder = entry.SortOrder
			terrains[i].Tags = append([]string(nil), entry.Tags...)
		}
	}
}

func renderDataTypesProto() string {
	return `syntax = "proto3";

package panoptes.proto.v1;

option go_package = "github.com/elebirds/panoptes/internal/gen/proto/v1;protov1";
option csharp_namespace = "Panoptes.Protocol.V1";

message ResourceValue {
  string key = 1;
  int32 amount = 2;
}

message ResourceBag {
  repeated ResourceValue items = 1;
}

message PointValue {
  string key = 1;
  int32 amount = 2;
}

message PointBag {
  repeated PointValue items = 1;
}

message ResourceDescriptor {
  string key = 1;
  string display_name = 2;
  string description = 3;
  string icon_key = 4;
  int32 sort_order = 5;
  int32 proto_number = 6;
  bool visible_in_hud = 7;
}

message PointDescriptor {
  string key = 1;
  string display_name = 2;
  string description = 3;
  string icon_key = 4;
  int32 sort_order = 5;
  bool visible_in_hud = 6;
}

message CatalogSectionHash {
  string section_name = 1;
  string hash = 2;
}

message StaticCatalogManifest {
  string schema_version = 1;
  string content_version = 2;
  string bundle_hash = 3;
  string default_locale = 4;
  string default_map_id = 5;
  repeated string required_sections = 6;
  repeated CatalogSectionHash section_hashes = 7;
}`
}

func renderDataCatalogProto() string {
	return `syntax = "proto3";

package panoptes.proto.v1;

import "data_types.proto";

option go_package = "github.com/elebirds/panoptes/internal/gen/proto/v1;protov1";
option csharp_namespace = "Panoptes.Protocol.V1";

message UnitCatalogEntry {
  string id = 1;
  string name = 2;
  string description = 3;
  string icon_key = 4;
  string prefab_key = 5;
  repeated string tags = 6;
  bool can_attack_structures = 7;
}

message BuildingCatalogEntry {
  string id = 1;
  string name = 2;
  string description = 3;
  string icon_key = 4;
  string prefab_key = 5;
  string placement_kind = 6;
  string building_scope = 7;
  string required_resource_type = 8;
  string takeover_mode = 9;
  repeated string tags = 10;
}

message TechnologyCatalogEntry {
  string id = 1;
  string name = 2;
  string description = 3;
  string icon_key = 4;
  string branch = 5;
  int32 tier = 6;
  int32 research_cost = 7;
  repeated string tags = 8;
}

message PolicyCatalogEntry {
  string id = 1;
  string name = 2;
  string description = 3;
  string icon_key = 4;
  string layer = 5;
  string activation_timing = 6;
  repeated string tags = 7;
}

message RecipeCatalogEntry {
  string id = 1;
  string name = 2;
  string description = 3;
  string icon_key = 4;
  string building_id = 5;
  int32 work_amount = 6;
  int32 base_progress = 7;
  repeated string tags = 8;
}

message TerrainCatalogEntry {
  string id = 1;
  string name = 2;
  string description = 3;
  string icon_key = 4;
  string material_key = 5;
  repeated string tags = 6;
}

message StaticCatalogSnapshot {
  StaticCatalogManifest manifest = 1;
  repeated ResourceDescriptor resources = 2;
  repeated PointDescriptor points = 3;
  repeated UnitCatalogEntry units = 4;
  repeated BuildingCatalogEntry buildings = 5;
  repeated TechnologyCatalogEntry technologies = 6;
  repeated PolicyCatalogEntry policies = 7;
  repeated RecipeCatalogEntry recipes = 8;
  repeated TerrainCatalogEntry terrains = 9;
}

message MsgStaticCatalogManifest {
  StaticCatalogManifest manifest = 1;
}

message MsgStaticCatalogSyncRequest {
  string bundle_hash = 1;
  repeated string section_names = 2;
  bool force_full_sync = 3;
}

message MsgStaticCatalogSectionChunk {
  string section_name = 1;
  string section_hash = 2;
  uint32 chunk_index = 3;
  uint32 chunk_count = 4;
  string compression = 5;
  bytes payload = 6;
}

message MsgStaticCatalogSyncComplete {
  string applied_bundle_hash = 1;
  bool success = 2;
  string error = 3;
}

message MsgStaticCatalogSnapshot {
  StaticCatalogSnapshot snapshot = 1;
}`
}

func renderMapCatalogProto() string {
	return `syntax = "proto3";

package panoptes.proto.v1;

option go_package = "github.com/elebirds/panoptes/internal/gen/proto/v1;protov1";
option csharp_namespace = "Panoptes.Protocol.V1";

message MapLegendEntry {
  string id = 1;
  string name = 2;
  string icon_key = 3;
}

message MapCatalogEntry {
  string id = 1;
  string name = 2;
  string description = 3;
  string thumbnail_key = 4;
  int32 width = 5;
  int32 height = 6;
}

message MapCatalog {
  repeated MapCatalogEntry maps = 1;
}`
}

func renderGoResourceKeys(bundle staticdata.CatalogBundle) string {
	builder := &strings.Builder{}
	builder.WriteString("package generated\n\nconst (\n")
	for _, resource := range bundle.Resources {
		builder.WriteString(fmt.Sprintf("\tResource%s = %q\n", exportedIdentifier(resource.Key), resource.Key))
	}
	builder.WriteString(")\n")
	return builder.String()
}

func renderCSharpResourceKeys(bundle staticdata.CatalogBundle) string {
	builder := &strings.Builder{}
	builder.WriteString("namespace Panoptes.Core.Domain\n{\n    public static class ResourceKeys\n    {\n")
	for _, resource := range bundle.Resources {
		builder.WriteString(fmt.Sprintf("        public const string Resource%s = \"%s\";\n", exportedIdentifier(resource.Key), resource.Key))
	}
	builder.WriteString("    }\n}\n")
	return builder.String()
}

func exportedIdentifier(key string) string {
	parts := strings.FieldsFunc(key, func(r rune) bool { return r == '_' || r == '-' })
	for i := range parts {
		if parts[i] == "" {
			continue
		}
		parts[i] = strings.ToUpper(parts[i][:1]) + parts[i][1:]
	}
	return strings.Join(parts, "")
}

func writePrettyJSON(path string, value any) error {
	raw, err := json.MarshalIndent(value, "", "  ")
	if err != nil {
		return fmt.Errorf("marshal %q: %w", path, err)
	}
	raw = append(raw, '\n')
	if err := os.WriteFile(path, raw, 0o600); err != nil {
		return fmt.Errorf("write %q: %w", path, err)
	}
	return nil
}

func readJSON[T any](path string) (T, error) {
	var value T
	raw, err := os.ReadFile(path)
	if err != nil {
		return value, fmt.Errorf("read %q: %w", path, err)
	}
	if err := json.Unmarshal(raw, &value); err != nil {
		return value, fmt.Errorf("unmarshal %q: %w", path, err)
	}
	return value, nil
}
