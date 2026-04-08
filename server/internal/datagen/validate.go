package datagen

import (
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
	"sort"
	"strings"

	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/xeipuuv/gojsonschema"
)

type jsonDocument[T any] struct {
	Path  string
	Raw   []byte
	Value T
}

type authoredData struct {
	Manifest  jsonDocument[staticdata.Manifest]
	Resources jsonDocument[struct {
		Resources []staticdata.ResourceDescriptor `json:"resources"`
	}]
	Units jsonDocument[struct {
		Units []staticdata.UnitDefinition `json:"units"`
	}]
	Buildings jsonDocument[struct {
		Buildings []staticdata.BuildingDefinition `json:"buildings"`
	}]
	Terrains jsonDocument[struct {
		Terrains []staticdata.TerrainDefinition `json:"terrains"`
	}]
	Rules     jsonDocument[staticdata.Rules]
	Ministers jsonDocument[struct {
		Pool []staticdata.Minister `json:"pool"`
	}]
	ResourceUI jsonDocument[staticdata.ResourceCatalogUIFile]
	UnitUI     jsonDocument[staticdata.UnitCatalogUIFile]
	BuildingUI jsonDocument[staticdata.BuildingCatalogUIFile]
	TerrainUI  jsonDocument[staticdata.TerrainCatalogUIFile]
	MapDefs    map[string]jsonDocument[staticdata.MapDefinition]
	MapUI      map[string]jsonDocument[staticdata.MapUICatalog]
	MapIDs     []string
}

type validationTarget struct {
	Path      string
	SchemaRel string
	Raw       []byte
}

func loadAuthoredData(repoRoot string) (*authoredData, error) {
	manifest, err := readJSONDocument[staticdata.Manifest](filepath.Join(repoRoot, "data/registry/manifest.json"))
	if err != nil {
		return nil, err
	}
	resources, err := readJSONDocument[struct {
		Resources []staticdata.ResourceDescriptor `json:"resources"`
	}](filepath.Join(repoRoot, "data/registry/resources.json"))
	if err != nil {
		return nil, err
	}
	units, err := readJSONDocument[struct {
		Units []staticdata.UnitDefinition `json:"units"`
	}](filepath.Join(repoRoot, "data/content/units/units.json"))
	if err != nil {
		return nil, err
	}
	buildings, err := readJSONDocument[struct {
		Buildings []staticdata.BuildingDefinition `json:"buildings"`
	}](filepath.Join(repoRoot, "data/content/buildings/buildings.json"))
	if err != nil {
		return nil, err
	}
	terrains, err := readJSONDocument[struct {
		Terrains []staticdata.TerrainDefinition `json:"terrains"`
	}](filepath.Join(repoRoot, "data/content/terrains/terrains.json"))
	if err != nil {
		return nil, err
	}
	rules, err := readJSONDocument[staticdata.Rules](filepath.Join(repoRoot, "data/content/rules/rules.json"))
	if err != nil {
		return nil, err
	}
	ministers, err := readJSONDocument[struct {
		Pool []staticdata.Minister `json:"pool"`
	}](filepath.Join(repoRoot, "data/content/ministers/ministers.json"))
	if err != nil {
		return nil, err
	}
	resourceUI, err := readJSONDocument[staticdata.ResourceCatalogUIFile](filepath.Join(repoRoot, "data/ui/catalogs/resources.json"))
	if err != nil {
		return nil, err
	}
	unitUI, err := readJSONDocument[staticdata.UnitCatalogUIFile](filepath.Join(repoRoot, "data/ui/catalogs/units.json"))
	if err != nil {
		return nil, err
	}
	buildingUI, err := readJSONDocument[staticdata.BuildingCatalogUIFile](filepath.Join(repoRoot, "data/ui/catalogs/buildings.json"))
	if err != nil {
		return nil, err
	}
	terrainUI, err := readJSONDocument[staticdata.TerrainCatalogUIFile](filepath.Join(repoRoot, "data/ui/catalogs/terrains.json"))
	if err != nil {
		return nil, err
	}

	mapDefs, mapUI, mapIDs, err := loadMapDocuments(repoRoot)
	if err != nil {
		return nil, err
	}

	return &authoredData{
		Manifest:   manifest,
		Resources:  resources,
		Units:      units,
		Buildings:  buildings,
		Terrains:   terrains,
		Rules:      rules,
		Ministers:  ministers,
		ResourceUI: resourceUI,
		UnitUI:     unitUI,
		BuildingUI: buildingUI,
		TerrainUI:  terrainUI,
		MapDefs:    mapDefs,
		MapUI:      mapUI,
		MapIDs:     mapIDs,
	}, nil
}

func loadMapDocuments(repoRoot string) (map[string]jsonDocument[staticdata.MapDefinition], map[string]jsonDocument[staticdata.MapUICatalog], []string, error) {
	root := filepath.Join(repoRoot, "data/content/maps")
	entries, err := os.ReadDir(root)
	if err != nil {
		return nil, nil, nil, fmt.Errorf("read maps dir: %w", err)
	}

	mapDefs := make(map[string]jsonDocument[staticdata.MapDefinition])
	mapUI := make(map[string]jsonDocument[staticdata.MapUICatalog])
	mapIDs := make([]string, 0, len(entries))
	for _, entry := range entries {
		if !entry.IsDir() {
			continue
		}
		mapID := entry.Name()
		def, err := readJSONDocument[staticdata.MapDefinition](filepath.Join(root, mapID, "definition.json"))
		if err != nil {
			return nil, nil, nil, err
		}
		ui, err := readJSONDocument[staticdata.MapUICatalog](filepath.Join(repoRoot, "data/ui/catalogs/maps", mapID+".json"))
		if err != nil {
			return nil, nil, nil, err
		}
		mapDefs[mapID] = def
		mapUI[mapID] = ui
		mapIDs = append(mapIDs, mapID)
	}
	sort.Strings(mapIDs)
	return mapDefs, mapUI, mapIDs, nil
}

func buildValidationTargets(data *authoredData) []validationTarget {
	targets := []validationTarget{
		{Path: data.Manifest.Path, SchemaRel: filepath.Join("registry", "manifest.schema.json"), Raw: data.Manifest.Raw},
		{Path: data.Resources.Path, SchemaRel: filepath.Join("registry", "resources.schema.json"), Raw: data.Resources.Raw},
		{Path: data.Units.Path, SchemaRel: filepath.Join("content", "units.schema.json"), Raw: data.Units.Raw},
		{Path: data.Buildings.Path, SchemaRel: filepath.Join("content", "buildings.schema.json"), Raw: data.Buildings.Raw},
		{Path: data.Terrains.Path, SchemaRel: filepath.Join("content", "terrains.schema.json"), Raw: data.Terrains.Raw},
		{Path: data.Rules.Path, SchemaRel: filepath.Join("content", "rules.schema.json"), Raw: data.Rules.Raw},
		{Path: data.Ministers.Path, SchemaRel: filepath.Join("content", "ministers.schema.json"), Raw: data.Ministers.Raw},
		{Path: data.ResourceUI.Path, SchemaRel: filepath.Join("ui", "resources.schema.json"), Raw: data.ResourceUI.Raw},
		{Path: data.UnitUI.Path, SchemaRel: filepath.Join("ui", "units.schema.json"), Raw: data.UnitUI.Raw},
		{Path: data.BuildingUI.Path, SchemaRel: filepath.Join("ui", "buildings.schema.json"), Raw: data.BuildingUI.Raw},
		{Path: data.TerrainUI.Path, SchemaRel: filepath.Join("ui", "terrains.schema.json"), Raw: data.TerrainUI.Raw},
	}
	for _, mapID := range data.MapIDs {
		targets = append(targets,
			validationTarget{
				Path:      data.MapDefs[mapID].Path,
				SchemaRel: filepath.Join("content", "maps", "definition.schema.json"),
				Raw:       data.MapDefs[mapID].Raw,
			},
			validationTarget{
				Path:      data.MapUI[mapID].Path,
				SchemaRel: filepath.Join("ui", "maps", "catalog.schema.json"),
				Raw:       data.MapUI[mapID].Raw,
			},
		)
	}
	return targets
}

func validateAuthoredSources(data *authoredData, schemas schemaSet) error {
	for _, target := range buildValidationTargets(data) {
		schema, ok := schemas[target.SchemaRel]
		if !ok {
			return fmt.Errorf("schema %q not found for %q", target.SchemaRel, target.Path)
		}

		schemaRaw, err := json.Marshal(schema)
		if err != nil {
			return fmt.Errorf("marshal schema %q: %w", target.SchemaRel, err)
		}
		result, err := gojsonschema.Validate(
			gojsonschema.NewBytesLoader(schemaRaw),
			gojsonschema.NewBytesLoader(target.Raw),
		)
		if err != nil {
			return fmt.Errorf("validate %q with %q: %w", target.Path, target.SchemaRel, err)
		}
		if result.Valid() {
			continue
		}

		reasons := make([]string, 0, len(result.Errors()))
		for _, validationErr := range result.Errors() {
			reason := validationErr.String()
			if value := validationErr.Value(); value != nil {
				reason = fmt.Sprintf("%s (value=%v)", reason, value)
			}
			reasons = append(reasons, reason)
		}
		return fmt.Errorf("schema validation failed for %s: %s", target.Path, strings.Join(reasons, "; "))
	}
	return nil
}

func readJSONDocument[T any](path string) (jsonDocument[T], error) {
	var value T
	raw, err := os.ReadFile(path)
	if err != nil {
		return jsonDocument[T]{}, fmt.Errorf("read %q: %w", path, err)
	}
	if err := json.Unmarshal(raw, &value); err != nil {
		return jsonDocument[T]{}, fmt.Errorf("unmarshal %q: %w", path, err)
	}
	return jsonDocument[T]{
		Path:  path,
		Raw:   raw,
		Value: value,
	}, nil
}
