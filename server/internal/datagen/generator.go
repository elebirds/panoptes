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
	_, _, err := loadAndCompile(opts)
	return err
}

func Generate(opts Options) error {
	bundle, maps, err := loadAndCompile(opts)
	if err != nil {
		return err
	}
	return emitGeneratedFiles(opts.RepoRoot, bundle, maps)
}

func loadAndCompile(opts Options) (staticdata.CatalogBundle, map[string]*staticdata.MapRuntimeBundle, error) {
	if opts.RepoRoot == "" {
		return staticdata.CatalogBundle{}, nil, fmt.Errorf("repo root is required")
	}

	manifest, err := readJSON[staticdata.Manifest](filepath.Join(opts.RepoRoot, "data/registry/manifest.json"))
	if err != nil {
		return staticdata.CatalogBundle{}, nil, err
	}
	resourcesFile, err := readJSON[struct {
		Resources []staticdata.ResourceDescriptor `json:"resources"`
	}](filepath.Join(opts.RepoRoot, "data/registry/resources.json"))
	if err != nil {
		return staticdata.CatalogBundle{}, nil, err
	}
	unitsContent, err := readJSON[struct {
		Units []staticdata.UnitDefinition `json:"units"`
	}](filepath.Join(opts.RepoRoot, "data/content/units/units.json"))
	if err != nil {
		return staticdata.CatalogBundle{}, nil, err
	}
	buildingsContent, err := readJSON[struct {
		Buildings []staticdata.BuildingDefinition `json:"buildings"`
	}](filepath.Join(opts.RepoRoot, "data/content/buildings/buildings.json"))
	if err != nil {
		return staticdata.CatalogBundle{}, nil, err
	}
	terrainsContent, err := readJSON[struct {
		Terrains []staticdata.TerrainDefinition `json:"terrains"`
	}](filepath.Join(opts.RepoRoot, "data/content/terrains/terrains.json"))
	if err != nil {
		return staticdata.CatalogBundle{}, nil, err
	}
	rules, err := readJSON[staticdata.Rules](filepath.Join(opts.RepoRoot, "data/content/rules/rules.json"))
	if err != nil {
		return staticdata.CatalogBundle{}, nil, err
	}
	ministersContent, err := readJSON[struct {
		Pool []staticdata.Minister `json:"pool"`
	}](filepath.Join(opts.RepoRoot, "data/content/ministers/ministers.json"))
	if err != nil {
		return staticdata.CatalogBundle{}, nil, err
	}
	resourceUI, err := readJSON[staticdata.ResourceCatalogUIFile](filepath.Join(opts.RepoRoot, "data/ui/catalogs/resources.json"))
	if err != nil {
		return staticdata.CatalogBundle{}, nil, err
	}
	unitUI, err := readJSON[staticdata.UnitCatalogUIFile](filepath.Join(opts.RepoRoot, "data/ui/catalogs/units.json"))
	if err != nil {
		return staticdata.CatalogBundle{}, nil, err
	}
	buildingUI, err := readJSON[staticdata.BuildingCatalogUIFile](filepath.Join(opts.RepoRoot, "data/ui/catalogs/buildings.json"))
	if err != nil {
		return staticdata.CatalogBundle{}, nil, err
	}
	terrainUI, err := readJSON[staticdata.TerrainCatalogUIFile](filepath.Join(opts.RepoRoot, "data/ui/catalogs/terrains.json"))
	if err != nil {
		return staticdata.CatalogBundle{}, nil, err
	}

	mergeUI(resourcesFile.Resources, resourceUI)
	mergeUnitUI(unitsContent.Units, unitUI)
	mergeBuildingUI(buildingsContent.Buildings, buildingUI)
	mergeTerrainUI(terrainsContent.Terrains, terrainUI)

	maps, entries, err := compileMaps(opts.RepoRoot)
	if err != nil {
		return staticdata.CatalogBundle{}, nil, err
	}

	bundle := staticdata.CatalogBundle{
		Manifest:  manifest,
		Resources: resourcesFile.Resources,
		Units:     unitsContent.Units,
		Buildings: buildingsContent.Buildings,
		Terrains:  terrainsContent.Terrains,
		Rules:     rules,
		Ministers: ministersContent.Pool,
		Maps:      entries,
	}

	hash, err := computeBundleHash(bundle, maps)
	if err != nil {
		return staticdata.CatalogBundle{}, nil, err
	}
	bundle.Manifest.BundleHash = hash

	return bundle, maps, nil
}

func emitGeneratedFiles(repoRoot string, bundle staticdata.CatalogBundle, maps map[string]*staticdata.MapRuntimeBundle) error {
	serverGen := filepath.Join(repoRoot, "data/generated/server")
	clientData := filepath.Join(repoRoot, "client/Assets/Resources/Data")
	schemaDir := filepath.Join(repoRoot, "data/schema")
	serverGoGen := filepath.Join(repoRoot, "server/internal/staticdata/generated")
	clientCodeGen := filepath.Join(repoRoot, "client/Assets/Scripts/Runtime/Data/Generated")
	protocolDir := filepath.Join(repoRoot, "protocol")

	dirs := []string{
		serverGen,
		filepath.Join(serverGen, "maps"),
		clientData,
		filepath.Join(clientData, "maps"),
		filepath.Join(schemaDir, "registry"),
		filepath.Join(schemaDir, "content"),
		filepath.Join(schemaDir, "ui"),
		serverGoGen,
		clientCodeGen,
		protocolDir,
	}
	for _, dir := range dirs {
		if err := os.MkdirAll(dir, 0o755); err != nil {
			return fmt.Errorf("mkdir %q: %w", dir, err)
		}
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

	if err := emitSchemas(schemaDir, bundle.Resources); err != nil {
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

func emitSchemas(schemaDir string, resources []staticdata.ResourceDescriptor) error {
	props := make(map[string]any, len(resources))
	for _, resource := range resources {
		props[resource.Key] = map[string]any{
			"type": "integer",
			"minimum": 0,
			"title": resource.DisplayName,
		}
	}
	schema := map[string]any{
		"$schema": "https://json-schema.org/draft/2020-12/schema",
		"type": "object",
		"properties": props,
		"additionalProperties": false,
	}
	files := map[string]any{
		filepath.Join(schemaDir, "registry/resources.schema.json"): schema,
		filepath.Join(schemaDir, "content/resource_amount.schema.json"): schema,
		filepath.Join(schemaDir, "ui/resource_catalog.schema.json"): map[string]any{
			"$schema": "https://json-schema.org/draft/2020-12/schema",
			"type": "object",
		},
	}
	for path, value := range files {
		if err := writePrettyJSON(path, value); err != nil {
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
			ID: mapID,
			Name: ui.Name,
			Description: ui.Description,
			ThumbnailKey: ui.ThumbnailKey,
			Width: runtime.Width,
			Height: runtime.Height,
			Tags: runtime.Tags,
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
				ID: coordinateNodeID(x, y),
				X: x,
				Y: y,
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
		ID: def.Meta.ID,
		Name: def.Meta.Name,
		Width: def.Meta.Width,
		Height: def.Meta.Height,
		Nodes: nodes,
		SpawnPoints: def.SpawnPoints,
		NamedNodes: namedNodes,
		CentralPoints: centralPoints,
		Tags: def.Meta.Tags,
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
		Name string
		Description string
		IconKey string
		SortOrder int
		Tags []string
	}, len(ui.Resources))
	for _, entry := range ui.Resources {
		uiByID[entry.ID] = struct {
			Name string
			Description string
			IconKey string
			SortOrder int
			Tags []string
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

func mergeUnitUI(units []staticdata.UnitDefinition, ui staticdata.UnitCatalogUIFile) {
	uiByID := make(map[string]struct {
		Name string
		Description string
		IconKey string
		PrefabKey string
		SortOrder int
		Tags []string
	}, len(ui.Units))
	for _, entry := range ui.Units {
		uiByID[entry.ID] = struct {
			Name string
			Description string
			IconKey string
			PrefabKey string
			SortOrder int
			Tags []string
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
		Name string
		Description string
		IconKey string
		PrefabKey string
		SortOrder int
		Tags []string
	}, len(ui.Buildings))
	for _, entry := range ui.Buildings {
		uiByID[entry.ID] = struct {
			Name string
			Description string
			IconKey string
			PrefabKey string
			SortOrder int
			Tags []string
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

func mergeTerrainUI(terrains []staticdata.TerrainDefinition, ui staticdata.TerrainCatalogUIFile) {
	uiByID := make(map[string]struct {
		Name string
		Description string
		IconKey string
		MaterialKey string
		SortOrder int
		Tags []string
	}, len(ui.Terrains))
	for _, entry := range ui.Terrains {
		uiByID[entry.ID] = struct {
			Name string
			Description string
			IconKey string
			MaterialKey string
			SortOrder int
			Tags []string
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

message ResourceDescriptor {
  string key = 1;
  string display_name = 2;
  string description = 3;
  string icon_key = 4;
  int32 sort_order = 5;
  int32 proto_number = 6;
  bool visible_in_hud = 7;
}

message StaticCatalogManifest {
  string schema_version = 1;
  string content_version = 2;
  string bundle_hash = 3;
  string default_locale = 4;
  string default_map_id = 5;
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
}

message BuildingCatalogEntry {
  string id = 1;
  string name = 2;
  string description = 3;
  string icon_key = 4;
  string prefab_key = 5;
}

message TerrainCatalogEntry {
  string id = 1;
  string name = 2;
  string description = 3;
  string icon_key = 4;
  string material_key = 5;
}

message StaticCatalogSnapshot {
  StaticCatalogManifest manifest = 1;
  repeated ResourceDescriptor resources = 2;
  repeated UnitCatalogEntry units = 3;
  repeated BuildingCatalogEntry buildings = 4;
  repeated TerrainCatalogEntry terrains = 5;
}

message MsgStaticCatalogManifest {
  StaticCatalogManifest manifest = 1;
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
	builder.WriteString("namespace Panoptes.Runtime.Data.Generated\n{\n    public static class ResourceKeys\n    {\n")
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
