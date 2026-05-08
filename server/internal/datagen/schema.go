// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-15 12:00:00 +0800
// Description: 构建数据生成模块的作者源 Schema 构建逻辑。

package datagen

import (
	"path/filepath"

	"github.com/elebirds/panoptes/internal/staticdata"
)

const schemaVersion = "https://json-schema.org/draft/2020-12/schema"

type schemaSet map[string]any

type authoringSchemaContext struct {
	ResourceKeys  []string
	PointKeys     []string
	UnitIDs       []string
	BuildingIDs   []string
	RecipeIDs     []string
	TechnologyIDs []string
	PolicyIDs     []string
	TerrainIDs    []string
}

func buildAuthoringSchemaContext(
	resources []staticdata.ResourceDescriptor,
	points []staticdata.PointDescriptor,
	units []staticdata.UnitDefinition,
	buildings []staticdata.BuildingDefinition,
	technologies []staticdata.TechnologyDefinition,
	policies []staticdata.PolicyDefinition,
	recipes []staticdata.RecipeDefinition,
	terrains []staticdata.TerrainDefinition,
) authoringSchemaContext {
	return authoringSchemaContext{
		ResourceKeys:  collectResourceKeys(resources),
		PointKeys:     collectPointKeys(points),
		UnitIDs:       collectUnitIDs(units),
		BuildingIDs:   collectBuildingIDs(buildings),
		RecipeIDs:     collectRecipeIDs(recipes),
		TechnologyIDs: collectTechnologyIDs(technologies),
		PolicyIDs:     collectPolicyIDs(policies),
		TerrainIDs:    collectTerrainIDs(terrains),
	}
}

func buildAuthoringSchemas(ctx authoringSchemaContext) schemaSet {
	defs := map[string]any{
		"resource_amount": resourceAmountSchema(ctx.ResourceKeys),
		"point_amount":    resourceAmountSchema(ctx.PointKeys),
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
		"prerequisite": objectSchema(
			map[string]any{
				"type":      enumSchema([]string{"technology_unlocked", "policy_active"}),
				"target_id": stringSchema(nil),
			},
			[]string{"type", "target_id"},
		),
		"modifier_effect": objectSchema(
			map[string]any{
				"trigger":       enumSchema(staticdata.AllowedModifierTriggers()),
				"target_id":     stringSchema(nil),
				"resource_key":  enumSchema(append(copyStrings(ctx.ResourceKeys), "")),
				"point_key":     enumSchema(append(copyStrings(ctx.PointKeys), "")),
				"modifier_type": enumSchema([]string{"flat", "percent", "multiplier"}),
				"value":         numberSchema(nil),
			},
			[]string{"trigger", "modifier_type", "value"},
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

	defs["technology_effect"] = map[string]any{
		"oneOf": []any{
			objectSchema(
				map[string]any{
					"type":      enumSchema([]string{"unlock_building"}),
					"target_id": stringSchema(nil),
				},
				[]string{"type", "target_id"},
			),
			objectSchema(
				map[string]any{
					"type":      enumSchema([]string{"unlock_recipe"}),
					"target_id": stringSchema(nil),
				},
				[]string{"type", "target_id"},
			),
			objectSchema(
				map[string]any{
					"type":      enumSchema([]string{"unlock_policy"}),
					"target_id": stringSchema(nil),
				},
				[]string{"type", "target_id"},
			),
			objectSchema(
				map[string]any{
					"type":              enumSchema([]string{"add_institution_slots"}),
					"institution_slots": intSchema(map[string]any{"minimum": 1}),
				},
				[]string{"type"},
			),
			objectSchema(
				map[string]any{
					"type":            enumSchema([]string{"grant"}),
					"grant_resources": refSchema("#/$defs/resource_amount"),
					"grant_units":     arraySchema(stringSchema(nil), nil),
				},
				[]string{"type"},
			),
		},
	}

	return schemaSet{
		filepath.Join("registry", "manifest.schema.json"): schemaDocument(
			filepath.Join("registry", "manifest.schema.json"),
			authoredRootSchema(
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
			filepath.Join("registry", "resources.schema.json"),
			authoredRootSchema(
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
		filepath.Join("registry", "points.schema.json"): schemaDocument(
			filepath.Join("registry", "points.schema.json"),
			authoredRootSchema(
				map[string]any{
					"points": arraySchema(
						objectSchema(
							map[string]any{
								"key":            stringSchema(nil),
								"display_name":   stringSchema(nil),
								"description":    stringSchema(nil),
								"icon_key":       stringSchema(nil),
								"sort_order":     intSchema(map[string]any{"minimum": 0}),
								"visible_in_hud": boolSchema(),
								"tags":           arraySchema(stringSchema(nil), nil),
							},
							[]string{"key", "display_name", "description", "icon_key", "sort_order", "visible_in_hud"},
						),
						map[string]any{"minItems": 1},
					),
				},
				[]string{"points"},
			),
			nil,
		),
		filepath.Join("content", "resource_amount.schema.json"): schemaDocument(
			filepath.Join("content", "resource_amount.schema.json"),
			refSchema("#/$defs/resource_amount"),
			map[string]any{"resource_amount": defs["resource_amount"]},
		),
		filepath.Join("content", "point_amount.schema.json"): schemaDocument(
			filepath.Join("content", "point_amount.schema.json"),
			refSchema("#/$defs/point_amount"),
			map[string]any{"point_amount": defs["point_amount"]},
		),
		filepath.Join("content", "units.schema.json"): schemaDocument(
			filepath.Join("content", "units.schema.json"),
			authoredRootSchema(
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
										"can_siege":             boolSchema(),
										"siege_multiplier":      numberSchema(map[string]any{"minimum": 0}),
										"can_attack_structures": boolSchema(),
										"can_destroy_road":      boolSchema(),
										"destroy_multiplier":    numberSchema(map[string]any{"minimum": 0}),
										"can_capture":           boolSchema(),
									},
									[]string{"can_siege", "can_attack_structures", "can_destroy_road", "can_capture"},
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
			filepath.Join("content", "buildings.schema.json"),
			authoredRootSchema(
				map[string]any{
					"buildings": arraySchema(
						objectSchema(
							map[string]any{
								"id":                     stringSchema(nil),
								"placement_kind":         enumSchema([]string{"resource_node", "city_territory", "city_foundation_center"}),
								"building_scope":         enumSchema([]string{"out_of_city", "in_city", "city_core"}),
								"required_resource_type": enumSchema(append(copyStrings(ctx.ResourceKeys), "")),
								"resource_costs":         refSchema("#/$defs/resource_amount"),
								"point_costs":            refSchema("#/$defs/point_amount"),
								"recipe_ids":             arraySchema(stringSchema(nil), nil),
								"default_recipe_id":      stringSchema(nil),
								"explicit_effects":       arraySchema(refSchema("#/$defs/technology_effect"), nil),
								"modifier_effects":       arraySchema(refSchema("#/$defs/modifier_effect"), nil),
								"max_hp":                 intSchema(map[string]any{"minimum": 0}),
								"takeover_mode":          enumSchema([]string{"disabled", "delayed", "city_capture"}),
								"tags":                   arraySchema(stringSchema(nil), nil),
							},
							[]string{
								"id", "placement_kind", "building_scope", "required_resource_type",
								"resource_costs", "point_costs", "recipe_ids", "default_recipe_id",
								"max_hp", "takeover_mode",
							},
						),
						map[string]any{"minItems": 1},
					),
				},
				[]string{"buildings"},
			),
			map[string]any{
				"resource_amount":   defs["resource_amount"],
				"point_amount":      defs["point_amount"],
				"technology_effect": defs["technology_effect"],
				"modifier_effect":   defs["modifier_effect"],
			},
		),
		filepath.Join("content", "technologies.schema.json"): schemaDocument(
			filepath.Join("content", "technologies.schema.json"),
			authoredRootSchema(
				map[string]any{
					"technologies": arraySchema(
						objectSchema(
							map[string]any{
								"id":               stringSchema(nil),
								"branch":           stringSchema(nil),
								"tier":             intSchema(map[string]any{"minimum": 1}),
								"research_cost":    intSchema(map[string]any{"minimum": 0}),
								"prerequisites":    arraySchema(refSchema("#/$defs/prerequisite"), nil),
								"explicit_effects": arraySchema(refSchema("#/$defs/technology_effect"), nil),
								"modifier_effects": arraySchema(refSchema("#/$defs/modifier_effect"), nil),
							},
							[]string{"id", "branch", "tier", "research_cost", "prerequisites", "explicit_effects", "modifier_effects"},
						),
						map[string]any{"minItems": 1},
					),
				},
				[]string{"technologies"},
			),
			map[string]any{
				"resource_amount":   defs["resource_amount"],
				"prerequisite":      defs["prerequisite"],
				"technology_effect": defs["technology_effect"],
				"modifier_effect":   defs["modifier_effect"],
			},
		),
		filepath.Join("content", "policies.schema.json"): schemaDocument(
			filepath.Join("content", "policies.schema.json"),
			authoredRootSchema(
				map[string]any{
					"policies": arraySchema(
						objectSchema(
							map[string]any{
								"id":                 stringSchema(nil),
								"layer":              enumSchema([]string{"national", "institutional"}),
								"activation_timing":  enumSchema([]string{"same_turn", "next_turn"}),
								"prerequisites":      arraySchema(refSchema("#/$defs/prerequisite"), nil),
								"explicit_effects":   arraySchema(refSchema("#/$defs/technology_effect"), nil),
								"modifier_effects":   arraySchema(refSchema("#/$defs/modifier_effect"), nil),
								"logistics_priority": arraySchema(refSchema("#/$defs/logistics_priority"), nil),
							},
							[]string{"id", "layer", "activation_timing", "prerequisites", "explicit_effects", "modifier_effects"},
						),
						map[string]any{"minItems": 1},
					),
				},
				[]string{"policies"},
			),
			map[string]any{
				"resource_amount":   defs["resource_amount"],
				"prerequisite":      defs["prerequisite"],
				"technology_effect": defs["technology_effect"],
				"modifier_effect":   defs["modifier_effect"],
				"logistics_priority": objectSchema(
					map[string]any{
						"target_id": stringSchema(nil),
						"tag":       stringSchema(nil),
						"priority":  intSchema(nil),
					},
					[]string{"priority"},
				),
			},
		),
		filepath.Join("content", "recipes.schema.json"): schemaDocument(
			filepath.Join("content", "recipes.schema.json"),
			authoredRootSchema(
				map[string]any{
					"recipes": arraySchema(
						objectSchema(
							map[string]any{
								"id":              stringSchema(nil),
								"building_id":     enumSchema(ctx.BuildingIDs),
								"resource_inputs": refSchema("#/$defs/resource_amount"),
								"point_inputs":    refSchema("#/$defs/point_amount"),
								"work_amount":     intSchema(map[string]any{"minimum": 1}),
								"base_progress":   intSchema(map[string]any{"minimum": 1}),
								"outputs": objectSchema(
									map[string]any{
										"resources":      refSchema("#/$defs/resource_amount"),
										"units":          arraySchema(enumSchema(ctx.UnitIDs), nil),
										"point_progress": refSchema("#/$defs/point_amount"),
										"state_changes":  additionalPropertyIntObject(),
									},
									[]string{},
								),
							},
							[]string{"id", "building_id", "resource_inputs", "point_inputs", "work_amount", "base_progress", "outputs"},
						),
						map[string]any{"minItems": 1},
					),
				},
				[]string{"recipes"},
			),
			map[string]any{
				"resource_amount": defs["resource_amount"],
				"point_amount":    defs["point_amount"],
			},
		),
		filepath.Join("content", "terrains.schema.json"): schemaDocument(
			filepath.Join("content", "terrains.schema.json"),
			authoredRootSchema(
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
			filepath.Join("content", "rules.schema.json"),
			authoredRootSchema(
				map[string]any{
					"turn_time_limit_planning":      intSchema(map[string]any{"minimum": 1}),
					"tokens_per_turn":               intSchema(map[string]any{"minimum": 0}),
					"bonus_tokens_per_turn":         intSchema(map[string]any{"minimum": 0}),
					"max_turns":                     intSchema(map[string]any{"minimum": 1}),
					"city_core_max_hp":              intSchema(map[string]any{"minimum": 0}),
					"safe_zone_radius":              intSchema(map[string]any{"minimum": 0}),
					"facility_takeover_turns":       intSchema(map[string]any{"minimum": 0}),
					"base_research_output_per_turn": intSchema(map[string]any{"minimum": 0}),
					"base_industry_output_per_turn": intSchema(map[string]any{"minimum": 0}),
					"minimum_city_distance":         intSchema(map[string]any{"minimum": 0}),
					"initial_city_territory_radius": intSchema(map[string]any{"minimum": 0}),
					"road_base_capacity":            intSchema(map[string]any{"minimum": 0}),
					"storage_raid_amount":           intSchema(map[string]any{"minimum": 0}),
				},
				[]string{
					"turn_time_limit_planning", "tokens_per_turn", "bonus_tokens_per_turn",
					"max_turns", "city_core_max_hp", "safe_zone_radius", "facility_takeover_turns",
					"base_research_output_per_turn", "base_industry_output_per_turn",
					"minimum_city_distance", "initial_city_territory_radius",
				},
			),
			nil,
		),
		filepath.Join("content", "ministers.schema.json"): schemaDocument(
			filepath.Join("content", "ministers.schema.json"),
			authoredRootSchema(
				map[string]any{
					"pool": arraySchema(
						objectSchema(
							map[string]any{
								"id":               stringSchema(nil),
								"name":             stringSchema(nil),
								"role":             stringSchema(nil),
								"icon_key":         stringSchema(nil),
								"ability":          intSchema(map[string]any{"minimum": 0}),
								"personality":      stringSchema(nil),
								"personality_desc": stringSchema(nil),
								"loyalty":          intSchema(map[string]any{"minimum": 0}),
								"ambition":         intSchema(map[string]any{"minimum": 0}),
							},
							[]string{"id", "name", "role", "icon_key", "ability", "personality", "personality_desc", "loyalty", "ambition"},
						),
						map[string]any{"minItems": 1},
					),
				},
				[]string{"pool"},
			),
			nil,
		),
		filepath.Join("content", "minister_skill_cards.schema.json"): schemaDocument(
			filepath.Join("content", "minister_skill_cards.schema.json"),
			authoredRootSchema(
				map[string]any{
					"minister_skill_cards": arraySchema(
						objectSchema(
							map[string]any{
								"id":             stringSchema(nil),
								"name":           stringSchema(nil),
								"description":    stringSchema(nil),
								"icon_key":       stringSchema(nil),
								"role_tags":      arraySchema(stringSchema(nil), nil),
								"rarity":         stringSchema(nil),
								"effect_key":     stringSchema(nil),
								"trigger_timing": enumSchema([]string{"activated", "passive", "planning_start"}),
								"delay_turns":    intSchema(map[string]any{"minimum": 0}),
								"duration_turns": intSchema(map[string]any{"minimum": 1}),
								"sort_order":     intSchema(nil),
								"tags":           arraySchema(stringSchema(nil), nil),
							},
							[]string{"id", "name", "description", "icon_key", "role_tags", "rarity", "effect_key", "trigger_timing", "delay_turns", "duration_turns", "sort_order"},
						),
						map[string]any{"minItems": 1},
					),
				},
				[]string{"minister_skill_cards"},
			),
			nil,
		),
		filepath.Join("content", "maps", "definition.schema.json"): schemaDocument(
			filepath.Join("content", "maps", "definition.schema.json"),
			authoredRootSchema(
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
								"terrain":              enumSchema(append(copyStrings(ctx.TerrainIDs), "")),
								"has_road":             boolSchema(),
								"is_resource_point":    boolSchema(),
								"resource_type":        enumSchema(append(copyStrings(ctx.ResourceKeys), "")),
								"node_name":            stringSchema(nil),
								"owner":                stringSchema(nil),
								"owner_slot":           intSchema(map[string]any{"minimum": 0}),
								"territory_owner":      stringSchema(nil),
								"territory_owner_slot": intSchema(map[string]any{"minimum": 0}),
								"building_type":        enumSchema(append(copyStrings(ctx.BuildingIDs), "")),
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
			filepath.Join("ui", "resources.schema.json"),
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
		filepath.Join("ui", "points.schema.json"): schemaDocument(
			filepath.Join("ui", "points.schema.json"),
			uiCatalogSchema("points", map[string]any{
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
			filepath.Join("ui", "units.schema.json"),
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
			filepath.Join("ui", "buildings.schema.json"),
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
		filepath.Join("ui", "technologies.schema.json"): schemaDocument(
			filepath.Join("ui", "technologies.schema.json"),
			uiCatalogSchema("technologies", map[string]any{
				"id":          stringSchema(nil),
				"name":        stringSchema(nil),
				"description": stringSchema(nil),
				"icon_key":    stringSchema(nil),
				"sort_order":  intSchema(map[string]any{"minimum": 0}),
				"tags":        arraySchema(stringSchema(nil), nil),
			}, []string{"id", "name", "description", "icon_key", "sort_order", "tags"}),
			nil,
		),
		filepath.Join("ui", "technology_tree.schema.json"): schemaDocument(
			filepath.Join("ui", "technology_tree.schema.json"),
			authoredRootSchema(
				map[string]any{
					"config_version": stringSchema(nil),
					"nodes": arraySchema(
						objectSchema(
							map[string]any{
								"id":            stringSchema(nil),
								"technology_id": stringSchema(nil),
								"title":         stringSchema(nil),
								"description":   stringSchema(nil),
								"x":             numberSchema(nil),
								"y":             numberSchema(nil),
								"width":         numberSchema(map[string]any{"exclusiveMinimum": 0}),
								"height":        numberSchema(map[string]any{"exclusiveMinimum": 0}),
								"visible":       boolSchema(),
							},
							[]string{"id", "technology_id", "x", "y", "width", "height", "visible"},
						),
						map[string]any{"minItems": 1},
					),
					"edges": arraySchema(
						objectSchema(
							map[string]any{
								"id":         stringSchema(nil),
								"from":       stringSchema(nil),
								"to":         stringSchema(nil),
								"show_arrow": boolSchema(),
								"arrow":      stringSchema(nil),
								"thickness":  numberSchema(map[string]any{"exclusiveMinimum": 0}),
								"points": arraySchema(
									objectSchema(
										map[string]any{
											"x": numberSchema(nil),
											"y": numberSchema(nil),
										},
										[]string{"x", "y"},
									),
									map[string]any{"minItems": 2},
								),
							},
							[]string{"id", "from", "to", "show_arrow", "thickness", "points"},
						),
						nil,
					),
				},
				[]string{"config_version", "nodes", "edges"},
			),
			nil,
		),
		filepath.Join("ui", "policies.schema.json"): schemaDocument(
			filepath.Join("ui", "policies.schema.json"),
			uiCatalogSchema("policies", map[string]any{
				"id":          stringSchema(nil),
				"name":        stringSchema(nil),
				"description": stringSchema(nil),
				"icon_key":    stringSchema(nil),
				"sort_order":  intSchema(map[string]any{"minimum": 0}),
				"tags":        arraySchema(stringSchema(nil), nil),
			}, []string{"id", "name", "description", "icon_key", "sort_order", "tags"}),
			nil,
		),
		filepath.Join("ui", "recipes.schema.json"): schemaDocument(
			filepath.Join("ui", "recipes.schema.json"),
			uiCatalogSchema("recipes", map[string]any{
				"id":          stringSchema(nil),
				"name":        stringSchema(nil),
				"description": stringSchema(nil),
				"icon_key":    stringSchema(nil),
				"sort_order":  intSchema(map[string]any{"minimum": 0}),
				"tags":        arraySchema(stringSchema(nil), nil),
			}, []string{"id", "name", "description", "icon_key", "sort_order", "tags"}),
			nil,
		),
		filepath.Join("ui", "terrains.schema.json"): schemaDocument(
			filepath.Join("ui", "terrains.schema.json"),
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
		filepath.Join("ui", "emotes.schema.json"): schemaDocument(
			filepath.Join("ui", "emotes.schema.json"),
			authoredRootSchema(
				map[string]any{
					"series": arraySchema(
						objectSchema(
							map[string]any{
								"id":           stringSchema(nil),
								"display_name": stringSchema(nil),
								"icon_key":     stringSchema(nil),
								"sort_order":   intSchema(map[string]any{"minimum": 0}),
							},
							[]string{"id", "display_name", "icon_key", "sort_order"},
						),
						map[string]any{"minItems": 1},
					),
					"emotes": arraySchema(
						objectSchema(
							map[string]any{
								"id":           stringSchema(nil),
								"series_id":    stringSchema(nil),
								"display_name": stringSchema(nil),
								"asset_key":    stringSchema(nil),
								"sort_order":   intSchema(map[string]any{"minimum": 0}),
								"tags":         arraySchema(stringSchema(nil), nil),
							},
							[]string{"id", "series_id", "display_name", "asset_key", "sort_order", "tags"},
						),
						map[string]any{"minItems": 1},
					),
				},
				[]string{"series", "emotes"},
			),
			nil,
		),
		filepath.Join("ui", "maps", "catalog.schema.json"): schemaDocument(
			filepath.Join("ui", "maps", "catalog.schema.json"),
			authoredRootSchema(
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
