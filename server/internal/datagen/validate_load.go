// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: Loads authored source documents for static data validation.

package datagen

import (
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
	"sort"

	"github.com/elebirds/panoptes/internal/staticdata"
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
	Points jsonDocument[struct {
		Points []staticdata.PointDescriptor `json:"points"`
	}]
	Units jsonDocument[struct {
		Units []staticdata.UnitDefinition `json:"units"`
	}]
	Buildings jsonDocument[struct {
		Buildings []staticdata.BuildingDefinition `json:"buildings"`
	}]
	Technologies jsonDocument[struct {
		Technologies []staticdata.TechnologyDefinition `json:"technologies"`
	}]
	Policies jsonDocument[struct {
		Policies []staticdata.PolicyDefinition `json:"policies"`
	}]
	Institutions jsonDocument[struct {
		Categories   []staticdata.InstitutionCategoryDefinition `json:"categories"`
		Institutions []staticdata.InstitutionDefinition         `json:"institutions"`
	}]
	Recipes jsonDocument[struct {
		Recipes []staticdata.RecipeDefinition `json:"recipes"`
	}]
	Terrains jsonDocument[struct {
		Terrains []staticdata.TerrainDefinition `json:"terrains"`
	}]
	Rules     jsonDocument[staticdata.Rules]
	Ministers jsonDocument[struct {
		Pool []staticdata.Minister `json:"pool"`
	}]
	ResourceUI       jsonDocument[staticdata.ResourceCatalogUIFile]
	PointUI          jsonDocument[staticdata.PointCatalogUIFile]
	UnitUI           jsonDocument[staticdata.UnitCatalogUIFile]
	BuildingUI       jsonDocument[staticdata.BuildingCatalogUIFile]
	TechnologyUI     jsonDocument[staticdata.TechnologyCatalogUIFile]
	TechnologyTreeUI jsonDocument[staticdata.TechnologyTreeLayoutFile]
	PolicyUI         jsonDocument[staticdata.PolicyCatalogUIFile]
	InstitutionUI    jsonDocument[staticdata.InstitutionCatalogUIFile]
	RecipeUI         jsonDocument[staticdata.RecipeCatalogUIFile]
	TerrainUI        jsonDocument[staticdata.TerrainCatalogUIFile]
	MapDefs          map[string]jsonDocument[staticdata.MapDefinition]
	MapUI            map[string]jsonDocument[staticdata.MapUICatalog]
	MapIDs           []string
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
	points, err := readJSONDocument[struct {
		Points []staticdata.PointDescriptor `json:"points"`
	}](filepath.Join(repoRoot, "data/registry/points.json"))
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
	technologies, err := readJSONDocument[struct {
		Technologies []staticdata.TechnologyDefinition `json:"technologies"`
	}](filepath.Join(repoRoot, "data/content/technologies/technologies.json"))
	if err != nil {
		return nil, err
	}
	policies, err := readJSONDocument[struct {
		Policies []staticdata.PolicyDefinition `json:"policies"`
	}](filepath.Join(repoRoot, "data/content/policies/policies.json"))
	if err != nil {
		return nil, err
	}
	institutions, err := readJSONDocument[struct {
		Categories   []staticdata.InstitutionCategoryDefinition `json:"categories"`
		Institutions []staticdata.InstitutionDefinition         `json:"institutions"`
	}](filepath.Join(repoRoot, "data/content/institutions/institutions.json"))
	if err != nil {
		return nil, err
	}
	recipes, err := readJSONDocument[struct {
		Recipes []staticdata.RecipeDefinition `json:"recipes"`
	}](filepath.Join(repoRoot, "data/content/recipes/recipes.json"))
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
	pointUI, err := readJSONDocument[staticdata.PointCatalogUIFile](filepath.Join(repoRoot, "data/ui/catalogs/points.json"))
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
	technologyUI, err := readJSONDocument[staticdata.TechnologyCatalogUIFile](filepath.Join(repoRoot, "data/ui/catalogs/technologies.json"))
	if err != nil {
		return nil, err
	}
	technologyTreeUI, err := readJSONDocument[staticdata.TechnologyTreeLayoutFile](filepath.Join(repoRoot, "data/ui/layouts/technology_tree.json"))
	if err != nil {
		return nil, err
	}
	policyUI, err := readJSONDocument[staticdata.PolicyCatalogUIFile](filepath.Join(repoRoot, "data/ui/catalogs/policies.json"))
	if err != nil {
		return nil, err
	}
	institutionUI, err := readJSONDocument[staticdata.InstitutionCatalogUIFile](filepath.Join(repoRoot, "data/ui/catalogs/institutions.json"))
	if err != nil {
		return nil, err
	}
	recipeUI, err := readJSONDocument[staticdata.RecipeCatalogUIFile](filepath.Join(repoRoot, "data/ui/catalogs/recipes.json"))
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
		Manifest:         manifest,
		Resources:        resources,
		Points:           points,
		Units:            units,
		Buildings:        buildings,
		Technologies:     technologies,
		Policies:         policies,
		Institutions:     institutions,
		Recipes:          recipes,
		Terrains:         terrains,
		Rules:            rules,
		Ministers:        ministers,
		ResourceUI:       resourceUI,
		PointUI:          pointUI,
		UnitUI:           unitUI,
		BuildingUI:       buildingUI,
		TechnologyUI:     technologyUI,
		TechnologyTreeUI: technologyTreeUI,
		PolicyUI:         policyUI,
		InstitutionUI:    institutionUI,
		RecipeUI:         recipeUI,
		TerrainUI:        terrainUI,
		MapDefs:          mapDefs,
		MapUI:            mapUI,
		MapIDs:           mapIDs,
	}, nil
}

func loadMapDocuments(repoRoot string) (map[string]jsonDocument[staticdata.MapDefinition], map[string]jsonDocument[staticdata.MapUICatalog], []string, error) {
	root := filepath.Join(repoRoot, "data/content/maps")
	entries, err := os.ReadDir(root)
	if err != nil {
		return nil, nil, nil, fmt.Errorf("read maps dir: %w", err)
	}
	defs := make(map[string]jsonDocument[staticdata.MapDefinition])
	uiDocs := make(map[string]jsonDocument[staticdata.MapUICatalog])
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
		defs[mapID] = def
		uiDocs[mapID] = ui
		mapIDs = append(mapIDs, mapID)
	}
	sort.Strings(mapIDs)
	return defs, uiDocs, mapIDs, nil
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
	return jsonDocument[T]{Path: path, Raw: raw, Value: value}, nil
}
