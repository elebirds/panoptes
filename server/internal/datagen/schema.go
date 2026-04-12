package datagen

import (
	"path/filepath"
	"sort"

	"github.com/elebirds/panoptes/internal/staticdata"
)

const schemaVersion = "https://json-schema.org/draft/2020-12/schema"

type schemaSet map[string]any

type authoringSchemaContext struct {
	ResourceKeys []string
	UnitIDs      []string
	TerrainIDs   []string
}

func buildAuthoringSchemaContext(
	resources []staticdata.ResourceDescriptor,
	units []staticdata.UnitDefinition,
	terrains []staticdata.TerrainDefinition,
) authoringSchemaContext {
	return authoringSchemaContext{
		ResourceKeys: collectResourceKeys(resources),
		UnitIDs:      collectUnitIDs(units),
		TerrainIDs:   collectTerrainIDs(terrains),
	}
}

func buildAuthoringSchemas(ctx authoringSchemaContext) schemaSet {
	defs := map[string]any{
		"resource_amount": resourceAmountSchema(ctx.ResourceKeys),
		"grid_point": objectSchema(
			map[string]any{
				"x": intSchema(map[string]any{"minimum": 0}),
				"y": intSchema(map[string]any{"minimum": 0}),
			},
			[]string{"x", "y"},
		),
		"spawn_point": objectSchema(
			map[string]any{
				"slot": intSchema(map[string]any{"minimum": 0}),
				"x":    intSchema(map[string]any{"minimum": 0}),
				"y":    intSchema(map[string]any{"minimum": 0}),
			},
			[]string{"slot", "x", "y"},
		),
		"terrain_band": objectSchema(
			map[string]any{
				"max":     numberSchema(map[string]any{"minimum": 0}),
				"terrain": enumSchema(ctx.TerrainIDs),
			},
			[]string{"max", "terrain"},
		),
	}

	defs["map_generator"] = objectSchema(
		map[string]any{
			"type":          enumSchema([]string{"noise"}),
			"seed":          intSchema(nil),
			"terrain_bands": arraySchema(refSchema("#/$defs/terrain_band"), nil),
		},
		[]string{"type", "seed"},
	)

	return schemaSet{
		filepath.Join("registry", "manifest.schema.json"): schemaDocument(
			objectSchema(
				map[string]any{
					"schema_version":  stringSchema(nil),
					"content_version": stringSchema(nil),
					"default_locale":  stringSchema(nil),
					"default_map_id":  stringSchema(nil),
					"bundle_hash":     stringSchema(nil),
				},
				[]string{"schema_version", "content_version", "default_locale", "default_map_id"},
			),
			nil,
		),
		filepath.Join("registry", "resources.schema.json"): schemaDocument(
			objectSchema(
				map[string]any{
					"resources": arraySchema(
						objectSchema(
							map[string]any{
								"key":            stringSchema(nil),
								"display_name":   stringSchema(nil),
								"description":    stringSchema(nil),
								"icon_key":       stringSchema(nil),
								"sort_order":     intSchema(map[string]any{"minimum": 0}),
								"proto_number":   intSchema(map[string]any{"minimum": 1}),
								"visible_in_hud": boolSchema(),
								"tags":           arraySchema(stringSchema(nil), nil),
							},
							[]string{"key", "display_name", "description", "icon_key", "sort_order", "proto_number", "visible_in_hud"},
						),
						map[string]any{"minItems": 1},
					),
				},
				[]string{"resources"},
			),
			nil,
		),
		filepath.Join("content", "resource_amount.schema.json"): schemaDocument(
			refSchema("#/$defs/resource_amount"),
			map[string]any{"resource_amount": defs["resource_amount"]},
		),
		filepath.Join("content", "units.schema.json"): schemaDocument(
			objectSchema(
				map[string]any{
					"units": arraySchema(
						objectSchema(
							map[string]any{
								"id":               stringSchema(nil),
								"class":            stringSchema(nil),
								"max_hp":           intSchema(map[string]any{"minimum": 0}),
								"attack":           intSchema(map[string]any{"minimum": 0}),
								"attack_range":     intSchema(map[string]any{"minimum": 0}),
								"move_range":       intSchema(map[string]any{"minimum": 0}),
								"vision_range":     intSchema(map[string]any{"minimum": 0}),
								"train_cost":       refSchema("#/$defs/resource_amount"),
								"upkeep":           refSchema("#/$defs/resource_amount"),
								"multipliers":      additionalPropertyNumberObject(),
								"road_speed_bonus": intSchema(map[string]any{"minimum": 0}),
								"charge_bonus":     numberSchema(map[string]any{"minimum": 0}),
								"flags": objectSchema(
									map[string]any{
										"can_siege":          boolSchema(),
										"siege_multiplier":   numberSchema(map[string]any{"minimum": 0}),
										"can_destroy_road":   boolSchema(),
										"destroy_multiplier": numberSchema(map[string]any{"minimum": 0}),
										"can_capture":        boolSchema(),
									},
									[]string{"can_siege", "can_destroy_road", "can_capture"},
								),
							},
							[]string{
								"id", "class", "max_hp", "attack", "attack_range", "move_range",
								"vision_range", "train_cost", "upkeep", "multipliers", "flags",
							},
						),
						map[string]any{"minItems": 1},
					),
				},
				[]string{"units"},
			),
			map[string]any{"resource_amount": defs["resource_amount"]},
		),
		filepath.Join("content", "buildings.schema.json"): schemaDocument(
			objectSchema(
				map[string]any{
					"buildings": arraySchema(
						objectSchema(
							map[string]any{
								"id":                     stringSchema(nil),
								"category":               enumSchema([]string{"production", "military_production", "military"}),
								"placement_rule":         enumSchema([]string{"resource_only", "city_only"}),
								"required_resource_type": enumSchema(append(copyStrings(ctx.ResourceKeys), "")),
								"build_cost":             refSchema("#/$defs/resource_amount"),
								"upkeep":                 refSchema("#/$defs/resource_amount"),
								"production": objectSchema(
									map[string]any{
										"input":       refSchema("#/$defs/resource_amount"),
										"output":      refSchema("#/$defs/resource_amount"),
										"cycle_turns": intSchema(map[string]any{"minimum": 0}),
									},
									[]string{"input", "output", "cycle_turns"},
								),
								"produces_units": arraySchema(enumSchema(ctx.UnitIDs), nil),
								"combat": objectSchema(
									map[string]any{
										"max_hp":          intSchema(map[string]any{"minimum": 0}),
										"attack_per_turn": intSchema(map[string]any{"minimum": 0}),
										"range":           intSchema(map[string]any{"minimum": 0}),
										"wall_level":      intSchema(map[string]any{"minimum": 0}),
										"towers":          intSchema(map[string]any{"minimum": 0}),
									},
									[]string{"max_hp", "attack_per_turn", "range", "wall_level", "towers"},
								),
								"limits": objectSchema(
									map[string]any{
										"max_per_node":   intSchema(map[string]any{"minimum": 0}),
										"max_per_player": intSchema(map[string]any{"minimum": -1}),
									},
									[]string{"max_per_node", "max_per_player"},
								),
							},
							[]string{
								"id", "category", "placement_rule", "required_resource_type",
								"build_cost", "upkeep", "production", "produces_units", "combat", "limits",
							},
						),
						map[string]any{"minItems": 1},
					),
				},
				[]string{"buildings"},
			),
			map[string]any{"resource_amount": defs["resource_amount"]},
		),
		filepath.Join("content", "terrains.schema.json"): schemaDocument(
			objectSchema(
				map[string]any{
					"terrains": arraySchema(
						objectSchema(
							map[string]any{
								"id":                 stringSchema(nil),
								"move_cost_no_road":  intSchema(map[string]any{"minimum": 0}),
								"defense_bonus":      numberSchema(map[string]any{"minimum": 0}),
								"attack_penalty":     numberSchema(map[string]any{"minimum": 0}),
								"blocks_cavalry":     boolSchema(),
								"passable_with_road": boolSchema(),
								"passable":           boolSchema(),
								"buildable":          boolSchema(),
							},
							[]string{
								"id", "move_cost_no_road", "defense_bonus", "attack_penalty",
								"blocks_cavalry", "passable_with_road", "passable", "buildable",
							},
						),
						map[string]any{"minItems": 1},
					),
				},
				[]string{"terrains"},
			),
			nil,
		),
		filepath.Join("content", "rules.schema.json"): schemaDocument(
			objectSchema(
				map[string]any{
					"turn_time_limit_domestic":  intSchema(map[string]any{"minimum": 1}),
					"turn_time_limit_combat":    intSchema(map[string]any{"minimum": 1}),
					"tokens_per_turn":           intSchema(map[string]any{"minimum": 0}),
					"tokens_recuperation_bonus": intSchema(map[string]any{"minimum": 0}),
					"max_turns":                 intSchema(map[string]any{"minimum": 1}),
					"castle_base_hp":            intSchema(map[string]any{"minimum": 0}),
					"safe_zone_radius":          intSchema(map[string]any{"minimum": 0}),
					"occupy_turns":              intSchema(map[string]any{"minimum": 0}),
					"build_points_per_turn":     intSchema(map[string]any{"minimum": 0}),
					"build_points_max":          intSchema(map[string]any{"minimum": 0}),
				},
				[]string{
					"turn_time_limit_domestic", "turn_time_limit_combat", "tokens_per_turn",
					"tokens_recuperation_bonus", "max_turns", "castle_base_hp", "safe_zone_radius",
					"occupy_turns", "build_points_per_turn", "build_points_max",
				},
			),
			nil,
		),
		filepath.Join("content", "ministers.schema.json"): schemaDocument(
			objectSchema(
				map[string]any{
					"pool": arraySchema(
						objectSchema(
							map[string]any{
								"id":               stringSchema(nil),
								"name":             stringSchema(nil),
								"role":             stringSchema(nil),
								"ability":          intSchema(map[string]any{"minimum": 0}),
								"personality":      stringSchema(nil),
								"personality_desc": stringSchema(nil),
								"loyalty":          intSchema(map[string]any{"minimum": 0}),
								"ambition":         intSchema(map[string]any{"minimum": 0}),
							},
							[]string{"id", "name", "role", "ability", "personality", "personality_desc", "loyalty", "ambition"},
						),
						map[string]any{"minItems": 1},
					),
				},
				[]string{"pool"},
			),
			nil,
		),
		filepath.Join("content", "maps", "definition.schema.json"): schemaDocument(
			objectSchema(
				map[string]any{
					"meta": objectSchema(
						map[string]any{
							"id":              stringSchema(nil),
							"name":            stringSchema(nil),
							"width":           intSchema(map[string]any{"minimum": 1}),
							"height":          intSchema(map[string]any{"minimum": 1}),
							"default_terrain": enumSchema(ctx.TerrainIDs),
							"tags":            arraySchema(stringSchema(nil), nil),
						},
						[]string{"id", "name", "width", "height", "default_terrain"},
					),
					"terrain_patches": arraySchema(
						map[string]any{
							"oneOf": []any{
								objectSchema(
									map[string]any{
										"kind":    enumSchema([]string{"point"}),
										"terrain": enumSchema(ctx.TerrainIDs),
										"points":  arraySchema(refSchema("#/$defs/grid_point"), map[string]any{"minItems": 1}),
									},
									[]string{"kind", "terrain", "points"},
								),
								objectSchema(
									map[string]any{
										"kind":    enumSchema([]string{"rect"}),
										"terrain": enumSchema(ctx.TerrainIDs),
										"x":       intSchema(map[string]any{"minimum": 0}),
										"y":       intSchema(map[string]any{"minimum": 0}),
										"width":   intSchema(map[string]any{"minimum": 1}),
										"height":  intSchema(map[string]any{"minimum": 1}),
									},
									[]string{"kind", "terrain", "x", "y", "width", "height"},
								),
							},
						},
						nil,
					),
					"node_overrides": arraySchema(
						objectSchema(
							map[string]any{
								"id":                   stringSchema(nil),
								"x":                    intSchema(map[string]any{"minimum": 0}),
								"y":                    intSchema(map[string]any{"minimum": 0}),
								"terrain":              enumSchema(ctx.TerrainIDs),
								"has_road":             boolSchema(),
								"is_resource_point":    boolSchema(),
								"resource_type":        enumSchema(ctx.ResourceKeys),
								"node_name":            stringSchema(nil),
								"owner":                stringSchema(nil),
								"owner_slot":           intSchema(map[string]any{"minimum": 0}),
								"territory_owner":      stringSchema(nil),
								"territory_owner_slot": intSchema(map[string]any{"minimum": 0}),
								"building_type":        stringSchema(nil),
								"building_hp":          intSchema(map[string]any{"minimum": 0}),
							},
							[]string{"x", "y"},
						),
						nil,
					),
					"features": objectSchema(
						map[string]any{
							"resource_points": arraySchema(
								objectSchema(
									map[string]any{
										"x":             intSchema(map[string]any{"minimum": 0}),
										"y":             intSchema(map[string]any{"minimum": 0}),
										"resource_type": enumSchema(ctx.ResourceKeys),
										"node_name":     stringSchema(nil),
									},
									[]string{"x", "y", "resource_type"},
								),
								nil,
							),
							"roads": arraySchema(
								objectSchema(
									map[string]any{
										"points": arraySchema(refSchema("#/$defs/grid_point"), map[string]any{"minItems": 1}),
									},
									[]string{"points"},
								),
								nil,
							),
							"named_nodes": arraySchema(
								objectSchema(
									map[string]any{
										"x":    intSchema(map[string]any{"minimum": 0}),
										"y":    intSchema(map[string]any{"minimum": 0}),
										"name": stringSchema(nil),
									},
									[]string{"x", "y", "name"},
								),
								nil,
							),
							"central_points": arraySchema(refSchema("#/$defs/grid_point"), nil),
						},
						[]string{"resource_points", "roads", "named_nodes", "central_points"},
					),
					"spawn_points": arraySchema(refSchema("#/$defs/spawn_point"), map[string]any{"minItems": 1}),
					"generator":    refSchema("#/$defs/map_generator"),
				},
				[]string{"meta", "terrain_patches", "node_overrides", "features", "spawn_points"},
			),
			defs,
		),
		filepath.Join("ui", "resources.schema.json"): schemaDocument(
			uiCatalogSchema("resources", map[string]any{
				"id":          stringSchema(nil),
				"name":        stringSchema(nil),
				"description": stringSchema(nil),
				"icon_key":    stringSchema(nil),
				"sort_order":  intSchema(map[string]any{"minimum": 0}),
				"tags":        arraySchema(stringSchema(nil), nil),
			}, []string{"id", "name", "description", "icon_key", "sort_order", "tags"}),
			nil,
		),
		filepath.Join("ui", "units.schema.json"): schemaDocument(
			uiCatalogSchema("units", map[string]any{
				"id":          stringSchema(nil),
				"name":        stringSchema(nil),
				"description": stringSchema(nil),
				"icon_key":    stringSchema(nil),
				"prefab_key":  stringSchema(nil),
				"sort_order":  intSchema(map[string]any{"minimum": 0}),
				"tags":        arraySchema(stringSchema(nil), nil),
			}, []string{"id", "name", "description", "icon_key", "prefab_key", "sort_order", "tags"}),
			nil,
		),
		filepath.Join("ui", "buildings.schema.json"): schemaDocument(
			uiCatalogSchema("buildings", map[string]any{
				"id":          stringSchema(nil),
				"name":        stringSchema(nil),
				"description": stringSchema(nil),
				"icon_key":    stringSchema(nil),
				"prefab_key":  stringSchema(nil),
				"sort_order":  intSchema(map[string]any{"minimum": 0}),
				"tags":        arraySchema(stringSchema(nil), nil),
			}, []string{"id", "name", "description", "icon_key", "prefab_key", "sort_order", "tags"}),
			nil,
		),
		filepath.Join("ui", "terrains.schema.json"): schemaDocument(
			uiCatalogSchema("terrains", map[string]any{
				"id":           stringSchema(nil),
				"name":         stringSchema(nil),
				"description":  stringSchema(nil),
				"icon_key":     stringSchema(nil),
				"material_key": stringSchema(nil),
				"sort_order":   intSchema(map[string]any{"minimum": 0}),
				"tags":         arraySchema(stringSchema(nil), nil),
			}, []string{"id", "name", "description", "icon_key", "material_key", "sort_order", "tags"}),
			nil,
		),
		filepath.Join("ui", "maps", "catalog.schema.json"): schemaDocument(
			objectSchema(
				map[string]any{
					"id":            stringSchema(nil),
					"name":          stringSchema(nil),
					"description":   stringSchema(nil),
					"thumbnail_key": stringSchema(nil),
					"legend": arraySchema(
						objectSchema(
							map[string]any{
								"id":       stringSchema(nil),
								"name":     stringSchema(nil),
								"icon_key": stringSchema(nil),
							},
							[]string{"id", "name", "icon_key"},
						),
						nil,
					),
				},
				[]string{"id", "name", "description", "thumbnail_key", "legend"},
			),
			nil,
		),
	}
}

func collectResourceKeys(resources []staticdata.ResourceDescriptor) []string {
	keys := make([]string, 0, len(resources))
	for _, resource := range resources {
		keys = append(keys, resource.Key)
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

func collectTerrainIDs(terrains []staticdata.TerrainDefinition) []string {
	ids := make([]string, 0, len(terrains))
	for _, terrain := range terrains {
		ids = append(ids, terrain.ID)
	}
	sort.Strings(ids)
	return ids
}

func schemaDocument(root any, defs map[string]any) map[string]any {
	schema := map[string]any{
		"$schema": schemaVersion,
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

func resourceAmountSchema(resourceKeys []string) map[string]any {
	properties := make(map[string]any, len(resourceKeys))
	for _, key := range resourceKeys {
		properties[key] = intSchema(map[string]any{"minimum": 0})
	}
	return objectSchema(properties, nil)
}

func additionalPropertyNumberObject() map[string]any {
	return map[string]any{
		"type":                 "object",
		"additionalProperties": numberSchema(map[string]any{"minimum": 0}),
	}
}

func uiCatalogSchema(key string, itemProperties map[string]any, required []string) map[string]any {
	return objectSchema(
		map[string]any{
			key: arraySchema(objectSchema(itemProperties, required), map[string]any{"minItems": 1}),
		},
		[]string{key},
	)
}

func copyStrings(values []string) []string {
	cloned := make([]string, len(values))
	copy(cloned, values)
	return cloned
}
