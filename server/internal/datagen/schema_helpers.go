// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: Shared JSON schema construction helpers for authored data.

package datagen

import (
	"path/filepath"
	"sort"

	"github.com/elebirds/panoptes/internal/staticdata"
)

func collectResourceKeys(resources []staticdata.ResourceDescriptor) []string {
	keys := make([]string, 0, len(resources))
	for _, resource := range resources {
		keys = append(keys, resource.Key)
	}
	sort.Strings(keys)
	return keys
}

func collectPointKeys(points []staticdata.PointDescriptor) []string {
	keys := make([]string, 0, len(points))
	for _, point := range points {
		keys = append(keys, point.Key)
	}
	sort.Strings(keys)
	return keys
}

func collectUnitIDs(units []staticdata.UnitDefinition) []string {
	ids := make([]string, 0, len(units))
	for _, unit := range units {
		ids = append(ids, unit.ID)
	}
	sort.Strings(ids)
	return ids
}

func collectBuildingIDs(buildings []staticdata.BuildingDefinition) []string {
	ids := make([]string, 0, len(buildings))
	for _, building := range buildings {
		ids = append(ids, building.ID)
	}
	sort.Strings(ids)
	return ids
}

func collectRecipeIDs(recipes []staticdata.RecipeDefinition) []string {
	ids := make([]string, 0, len(recipes))
	for _, recipe := range recipes {
		ids = append(ids, recipe.ID)
	}
	sort.Strings(ids)
	return ids
}

func collectTechnologyIDs(technologies []staticdata.TechnologyDefinition) []string {
	ids := make([]string, 0, len(technologies))
	for _, technology := range technologies {
		ids = append(ids, technology.ID)
	}
	sort.Strings(ids)
	return ids
}

func collectPolicyIDs(policies []staticdata.PolicyDefinition) []string {
	ids := make([]string, 0, len(policies))
	for _, policy := range policies {
		ids = append(ids, policy.ID)
	}
	sort.Strings(ids)
	return ids
}

func collectTerrainIDs(terrains []staticdata.TerrainDefinition) []string {
	ids := make([]string, 0, len(terrains))
	for _, terrain := range terrains {
		ids = append(ids, terrain.ID)
	}
	sort.Strings(ids)
	return ids
}

func schemaDocument(rel string, root any, defs map[string]any) map[string]any {
	schema := map[string]any{
		"$schema": schemaVersion,
		"$id":     filepath.ToSlash(rel),
	}
	if rootMap, ok := root.(map[string]any); ok {
		for key, value := range rootMap {
			schema[key] = value
		}
	}
	if defs != nil && len(defs) > 0 {
		schema["$defs"] = defs
	}
	return schema
}

func authoredRootSchema(properties map[string]any, required []string) map[string]any {
	rootProperties := cloneSchemaMap(properties)
	rootProperties["$schema"] = stringSchema(nil)
	return objectSchema(rootProperties, required)
}

func objectSchema(properties map[string]any, required []string) map[string]any {
	schema := map[string]any{
		"type":                 "object",
		"properties":           properties,
		"additionalProperties": false,
	}
	if len(required) > 0 {
		schema["required"] = copyStrings(required)
	}
	return schema
}

func arraySchema(items any, extra map[string]any) map[string]any {
	schema := map[string]any{
		"type":  "array",
		"items": items,
	}
	for key, value := range extra {
		schema[key] = value
	}
	return schema
}

func enumSchema(values []string) map[string]any {
	items := make([]any, 0, len(values))
	for _, value := range values {
		items = append(items, value)
	}
	return map[string]any{
		"type": "string",
		"enum": items,
	}
}

func refSchema(ref string) map[string]any {
	return map[string]any{
		"$ref": ref,
	}
}

func stringSchema(extra map[string]any) map[string]any {
	schema := map[string]any{
		"type": "string",
	}
	for key, value := range extra {
		schema[key] = value
	}
	return schema
}

func intSchema(extra map[string]any) map[string]any {
	schema := map[string]any{
		"type": "integer",
	}
	for key, value := range extra {
		schema[key] = value
	}
	return schema
}

func numberSchema(extra map[string]any) map[string]any {
	schema := map[string]any{
		"type": "number",
	}
	for key, value := range extra {
		schema[key] = value
	}
	return schema
}

func boolSchema() map[string]any {
	return map[string]any{
		"type": "boolean",
	}
}

func resourceAmountSchema(keys []string) map[string]any {
	properties := make(map[string]any, len(keys))
	for _, key := range keys {
		properties[key] = intSchema(map[string]any{"minimum": 0})
	}
	return objectSchema(properties, nil)
}

func additionalPropertyNumberObject() map[string]any {
	return map[string]any{
		"type":                 "object",
		"additionalProperties": numberSchema(nil),
	}
}

func additionalPropertyIntObject() map[string]any {
	return map[string]any{
		"type":                 "object",
		"additionalProperties": intSchema(nil),
	}
}

func uiCatalogSchema(key string, itemProperties map[string]any, required []string) map[string]any {
	return authoredRootSchema(
		map[string]any{
			key: arraySchema(objectSchema(itemProperties, required), map[string]any{"minItems": 1}),
		},
		[]string{key},
	)
}

func cloneSchemaMap(src map[string]any) map[string]any {
	dst := make(map[string]any, len(src))
	for key, value := range src {
		dst[key] = value
	}
	return dst
}

func copyStrings(values []string) []string {
	cloned := make([]string, len(values))
	copy(cloned, values)
	return cloned
}
