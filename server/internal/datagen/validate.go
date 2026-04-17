// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-15 12:00:00 +0800
// Description: 实现数据生成模块的作者源校验逻辑。

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
	RecipeUI         jsonDocument[staticdata.RecipeCatalogUIFile]
	TerrainUI        jsonDocument[staticdata.TerrainCatalogUIFile]
	MapDefs          map[string]jsonDocument[staticdata.MapDefinition]
	MapUI            map[string]jsonDocument[staticdata.MapUICatalog]
	MapIDs           []string
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
		{Path: data.Points.Path, SchemaRel: filepath.Join("registry", "points.schema.json"), Raw: data.Points.Raw},
		{Path: data.Units.Path, SchemaRel: filepath.Join("content", "units.schema.json"), Raw: data.Units.Raw},
		{Path: data.Buildings.Path, SchemaRel: filepath.Join("content", "buildings.schema.json"), Raw: data.Buildings.Raw},
		{Path: data.Technologies.Path, SchemaRel: filepath.Join("content", "technologies.schema.json"), Raw: data.Technologies.Raw},
		{Path: data.Policies.Path, SchemaRel: filepath.Join("content", "policies.schema.json"), Raw: data.Policies.Raw},
		{Path: data.Recipes.Path, SchemaRel: filepath.Join("content", "recipes.schema.json"), Raw: data.Recipes.Raw},
		{Path: data.Terrains.Path, SchemaRel: filepath.Join("content", "terrains.schema.json"), Raw: data.Terrains.Raw},
		{Path: data.Rules.Path, SchemaRel: filepath.Join("content", "rules.schema.json"), Raw: data.Rules.Raw},
		{Path: data.Ministers.Path, SchemaRel: filepath.Join("content", "ministers.schema.json"), Raw: data.Ministers.Raw},
		{Path: data.ResourceUI.Path, SchemaRel: filepath.Join("ui", "resources.schema.json"), Raw: data.ResourceUI.Raw},
		{Path: data.PointUI.Path, SchemaRel: filepath.Join("ui", "points.schema.json"), Raw: data.PointUI.Raw},
		{Path: data.UnitUI.Path, SchemaRel: filepath.Join("ui", "units.schema.json"), Raw: data.UnitUI.Raw},
		{Path: data.BuildingUI.Path, SchemaRel: filepath.Join("ui", "buildings.schema.json"), Raw: data.BuildingUI.Raw},
		{Path: data.TechnologyUI.Path, SchemaRel: filepath.Join("ui", "technologies.schema.json"), Raw: data.TechnologyUI.Raw},
		{Path: data.TechnologyTreeUI.Path, SchemaRel: filepath.Join("ui", "technology_tree.schema.json"), Raw: data.TechnologyTreeUI.Raw},
		{Path: data.PolicyUI.Path, SchemaRel: filepath.Join("ui", "policies.schema.json"), Raw: data.PolicyUI.Raw},
		{Path: data.RecipeUI.Path, SchemaRel: filepath.Join("ui", "recipes.schema.json"), Raw: data.RecipeUI.Raw},
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
	return validateCrossReferences(data)
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

func validateCrossReferences(data *authoredData) error {
	resourceKeys := makeStringSetResource(data.Resources.Value.Resources)
	pointKeys := makeStringSetPoint(data.Points.Value.Points)
	buildingIDs := makeStringSetBuilding(data.Buildings.Value.Buildings)
	technologyIDs := makeStringSetTechnology(data.Technologies.Value.Technologies)
	policyIDs := makeStringSetPolicy(data.Policies.Value.Policies)
	recipeIDs := makeStringSetRecipe(data.Recipes.Value.Recipes)
	unitIDs := makeStringSetUnit(data.Units.Value.Units)
	allowedTriggers := make(map[string]struct{}, len(staticdata.AllowedModifierTriggers()))
	for _, trigger := range staticdata.AllowedModifierTriggers() {
		allowedTriggers[trigger] = struct{}{}
	}

	for _, recipe := range data.Recipes.Value.Recipes {
		if _, ok := buildingIDs[recipe.BuildingID]; !ok {
			return fmt.Errorf("semantic validation failed for %s: unknown building_id %q", data.Recipes.Path, recipe.BuildingID)
		}
		if err := validatePointBagKeys(recipe.PointInputs, pointKeys, data.Recipes.Path, "point input"); err != nil {
			return err
		}
		if err := validatePointBagKeys(recipe.Outputs.PointProgress, pointKeys, data.Recipes.Path, "point progress"); err != nil {
			return err
		}
		for _, unitID := range recipe.Outputs.Units {
			if _, ok := unitIDs[unitID]; !ok {
				return fmt.Errorf("semantic validation failed for %s: unknown output unit %q", data.Recipes.Path, unitID)
			}
		}
	}

	for _, building := range data.Buildings.Value.Buildings {
		if err := validatePointBagKeys(building.PointCosts, pointKeys, data.Buildings.Path, "point cost"); err != nil {
			return err
		}
		if err := validateExplicitEffects(building.ExplicitEffects, buildingIDs, recipeIDs, policyIDs, unitIDs, data.Buildings.Path); err != nil {
			return err
		}
		if err := validateModifierEffects(building.ModifierEffects, allowedTriggers, resourceKeys, pointKeys, buildingIDs, recipeIDs, unitIDs, data.Buildings.Path); err != nil {
			return err
		}
		if len(building.RecipeIDs) == 0 {
			if building.DefaultRecipeID != "" {
				return fmt.Errorf("semantic validation failed for %s: building %q has default_recipe_id %q without recipe_ids", data.Buildings.Path, building.ID, building.DefaultRecipeID)
			}
			continue
		}
		if building.DefaultRecipeID == "" {
			return fmt.Errorf("semantic validation failed for %s: building %q missing default_recipe_id", data.Buildings.Path, building.ID)
		}
		if _, ok := recipeIDs[building.DefaultRecipeID]; !ok {
			return fmt.Errorf("semantic validation failed for %s: building %q references unknown default_recipe_id %q", data.Buildings.Path, building.ID, building.DefaultRecipeID)
		}
		for _, recipeID := range building.RecipeIDs {
			if _, ok := recipeIDs[recipeID]; !ok {
				return fmt.Errorf("semantic validation failed for %s: building %q references unknown recipe_id %q", data.Buildings.Path, building.ID, recipeID)
			}
		}
	}

	for _, technology := range data.Technologies.Value.Technologies {
		if err := validatePrerequisites(technology.Prerequisites, technologyIDs, policyIDs, data.Technologies.Path); err != nil {
			return err
		}
		if err := validateExplicitEffects(technology.ExplicitEffects, buildingIDs, recipeIDs, policyIDs, unitIDs, data.Technologies.Path); err != nil {
			return err
		}
		if err := validateModifierEffects(technology.ModifierEffects, allowedTriggers, resourceKeys, pointKeys, buildingIDs, recipeIDs, unitIDs, data.Technologies.Path); err != nil {
			return err
		}
	}

	for _, policy := range data.Policies.Value.Policies {
		if err := validatePrerequisites(policy.Prerequisites, technologyIDs, policyIDs, data.Policies.Path); err != nil {
			return err
		}
		if err := validateExplicitEffects(policy.ExplicitEffects, buildingIDs, recipeIDs, policyIDs, unitIDs, data.Policies.Path); err != nil {
			return err
		}
		if err := validateModifierEffects(policy.ModifierEffects, allowedTriggers, resourceKeys, pointKeys, buildingIDs, recipeIDs, unitIDs, data.Policies.Path); err != nil {
			return err
		}
	}

	if err := validateTechnologyTreeLayout(data.Technologies.Value.Technologies, data.TechnologyTreeUI.Value, data.TechnologyTreeUI.Path); err != nil {
		return err
	}

	return nil
}

func validateTechnologyTreeLayout(technologies []staticdata.TechnologyDefinition, layout staticdata.TechnologyTreeLayoutFile, path string) error {
	nodeTechByID := make(map[string]string, len(layout.Nodes))
	techNodeByID := make(map[string]string, len(layout.Nodes))
	technologyIDs := make(map[string]struct{}, len(technologies))
	expectedEdges := make(map[string]struct{})
	actualEdges := make(map[string]struct{})

	for _, technology := range technologies {
		technologyIDs[technology.ID] = struct{}{}
		for _, prereq := range technology.Prerequisites {
			if prereq.Type != "technology_unlocked" || strings.TrimSpace(prereq.TargetID) == "" {
				continue
			}
			expectedEdges[technologyTreeEdgeKey(prereq.TargetID, technology.ID)] = struct{}{}
		}
	}

	for _, node := range layout.Nodes {
		if strings.TrimSpace(node.ID) == "" {
			continue
		}
		technologyID := strings.TrimSpace(node.TechnologyID)
		if technologyID == "" {
			return fmt.Errorf("semantic validation failed for %s: node %q missing technology_id", path, node.ID)
		}
		if _, ok := technologyIDs[technologyID]; !ok {
			return fmt.Errorf("semantic validation failed for %s: node %q references unknown technology %q", path, node.ID, technologyID)
		}
		if existingNodeID, exists := techNodeByID[technologyID]; exists && existingNodeID != node.ID {
			return fmt.Errorf("semantic validation failed for %s: technology %q is bound to multiple nodes (%q, %q)", path, technologyID, existingNodeID, node.ID)
		}
		nodeTechByID[node.ID] = technologyID
		techNodeByID[technologyID] = node.ID
	}

	for _, edge := range layout.Edges {
		fromTech, ok := nodeTechByID[edge.From]
		if !ok {
			return fmt.Errorf("semantic validation failed for %s: edge %q references unknown from node %q", path, edge.ID, edge.From)
		}
		toTech, ok := nodeTechByID[edge.To]
		if !ok {
			return fmt.Errorf("semantic validation failed for %s: edge %q references unknown to node %q", path, edge.ID, edge.To)
		}
		actualEdges[technologyTreeEdgeKey(fromTech, toTech)] = struct{}{}
	}

	for key := range expectedEdges {
		if _, ok := actualEdges[key]; ok {
			continue
		}
		fromTech, toTech := parseTechnologyTreeEdgeKey(key)
		return fmt.Errorf("semantic validation failed for %s: missing prerequisite edge %q -> %q", path, fromTech, toTech)
	}
	for key := range actualEdges {
		if _, ok := expectedEdges[key]; ok {
			continue
		}
		fromTech, toTech := parseTechnologyTreeEdgeKey(key)
		return fmt.Errorf("semantic validation failed for %s: unexpected prerequisite edge %q -> %q", path, fromTech, toTech)
	}

	return nil
}

func technologyTreeEdgeKey(fromTech string, toTech string) string {
	return fromTech + "->" + toTech
}

func parseTechnologyTreeEdgeKey(key string) (string, string) {
	parts := strings.SplitN(key, "->", 2)
	if len(parts) != 2 {
		return key, ""
	}
	return parts[0], parts[1]
}

func validatePrerequisites(prereqs []staticdata.Prerequisite, technologyIDs map[string]struct{}, policyIDs map[string]struct{}, path string) error {
	for _, prereq := range prereqs {
		switch prereq.Type {
		case "technology_unlocked":
			if _, ok := technologyIDs[prereq.TargetID]; !ok {
				return fmt.Errorf("semantic validation failed for %s: unknown prerequisite target %q", path, prereq.TargetID)
			}
		case "policy_active":
			if _, ok := policyIDs[prereq.TargetID]; !ok {
				return fmt.Errorf("semantic validation failed for %s: unknown prerequisite target %q", path, prereq.TargetID)
			}
		}
	}
	return nil
}

func validateExplicitEffects(effects []staticdata.ExplicitEffect, buildingIDs map[string]struct{}, recipeIDs map[string]struct{}, policyIDs map[string]struct{}, unitIDs map[string]struct{}, path string) error {
	for _, effect := range effects {
		switch effect.Type {
		case "unlock_building":
			if _, ok := buildingIDs[effect.TargetID]; !ok {
				return fmt.Errorf("semantic validation failed for %s: unknown building unlock target %q", path, effect.TargetID)
			}
		case "unlock_recipe":
			if _, ok := recipeIDs[effect.TargetID]; !ok {
				return fmt.Errorf("semantic validation failed for %s: unknown recipe unlock target %q", path, effect.TargetID)
			}
		case "unlock_policy":
			if _, ok := policyIDs[effect.TargetID]; !ok {
				return fmt.Errorf("semantic validation failed for %s: unknown policy unlock target %q", path, effect.TargetID)
			}
		case "add_institution_slots":
			if effect.InstitutionSlots < 0 {
				return fmt.Errorf("semantic validation failed for %s: institution slot delta must be non-negative", path)
			}
		case "grant":
			for _, unitID := range effect.GrantUnits {
				if _, ok := unitIDs[unitID]; !ok {
					return fmt.Errorf("semantic validation failed for %s: unknown grant unit %q", path, unitID)
				}
			}
		}
	}
	return nil
}

func validateModifierEffects(
	effects []staticdata.ModifierEffect,
	allowedTriggers map[string]struct{},
	resourceKeys map[string]struct{},
	pointKeys map[string]struct{},
	buildingIDs map[string]struct{},
	recipeIDs map[string]struct{},
	unitIDs map[string]struct{},
	path string,
) error {
	for _, effect := range effects {
		if _, ok := allowedTriggers[effect.Trigger]; !ok {
			return fmt.Errorf("semantic validation failed for %s: unknown modifier trigger %q", path, effect.Trigger)
		}
		switch effect.Trigger {
		case string(staticdata.ModifierTriggerPointOutput),
			string(staticdata.ModifierTriggerBuildingPointCost),
			string(staticdata.ModifierTriggerRecipePointInput):
			if effect.PointKey != "" {
				if _, ok := pointKeys[effect.PointKey]; !ok {
					return fmt.Errorf("semantic validation failed for %s: unknown point modifier target %q", path, effect.PointKey)
				}
			}
		case string(staticdata.ModifierTriggerBuildingResourceCost),
			string(staticdata.ModifierTriggerRecipeResourceInput),
			string(staticdata.ModifierTriggerRecipeResourceOutput):
			if effect.ResourceKey != "" {
				if _, ok := resourceKeys[effect.ResourceKey]; !ok {
					return fmt.Errorf("semantic validation failed for %s: unknown resource modifier target %q", path, effect.ResourceKey)
				}
			}
		}

		switch {
		case strings.HasPrefix(effect.Trigger, "building.") && effect.TargetID != "":
			if _, ok := buildingIDs[effect.TargetID]; !ok {
				return fmt.Errorf("semantic validation failed for %s: unknown building modifier target %q", path, effect.TargetID)
			}
		case strings.HasPrefix(effect.Trigger, "recipe.") && effect.TargetID != "":
			if _, ok := recipeIDs[effect.TargetID]; !ok {
				return fmt.Errorf("semantic validation failed for %s: unknown recipe modifier target %q", path, effect.TargetID)
			}
		case strings.HasPrefix(effect.Trigger, "unit.") && effect.TargetID != "":
			if _, ok := unitIDs[effect.TargetID]; !ok {
				return fmt.Errorf("semantic validation failed for %s: unknown unit modifier target %q", path, effect.TargetID)
			}
		}
	}
	return nil
}

func validatePointBagKeys(values map[string]int, allowed map[string]struct{}, path string, label string) error {
	for key := range values {
		if _, ok := allowed[key]; !ok {
			return fmt.Errorf("semantic validation failed for %s: unknown %s %q", path, label, key)
		}
	}
	return nil
}

func makeStringSetResource(resources []staticdata.ResourceDescriptor) map[string]struct{} {
	values := make(map[string]struct{}, len(resources))
	for _, resource := range resources {
		values[resource.Key] = struct{}{}
	}
	return values
}

func makeStringSetPoint(points []staticdata.PointDescriptor) map[string]struct{} {
	values := make(map[string]struct{}, len(points))
	for _, point := range points {
		values[point.Key] = struct{}{}
	}
	return values
}

func makeStringSetBuilding(buildings []staticdata.BuildingDefinition) map[string]struct{} {
	values := make(map[string]struct{}, len(buildings))
	for _, building := range buildings {
		values[building.ID] = struct{}{}
	}
	return values
}

func makeStringSetTechnology(technologies []staticdata.TechnologyDefinition) map[string]struct{} {
	values := make(map[string]struct{}, len(technologies))
	for _, technology := range technologies {
		values[technology.ID] = struct{}{}
	}
	return values
}

func makeStringSetPolicy(policies []staticdata.PolicyDefinition) map[string]struct{} {
	values := make(map[string]struct{}, len(policies))
	for _, policy := range policies {
		values[policy.ID] = struct{}{}
	}
	return values
}

func makeStringSetRecipe(recipes []staticdata.RecipeDefinition) map[string]struct{} {
	values := make(map[string]struct{}, len(recipes))
	for _, recipe := range recipes {
		values[recipe.ID] = struct{}{}
	}
	return values
}

func makeStringSetUnit(units []staticdata.UnitDefinition) map[string]struct{} {
	values := make(map[string]struct{}, len(units))
	for _, unit := range units {
		values[unit.ID] = struct{}{}
	}
	return values
}
