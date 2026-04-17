// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 验证数据生成模块的数据生成主流程。

package datagen

import (
	"os"
	"path/filepath"
	"strings"
	"testing"
)

func TestGenerateProducesSchemasBundlesAndGeneratedSources(t *testing.T) {
	repoRoot := t.TempDir()
	writeFixtureRepo(t, repoRoot)

	if err := Generate(Options{RepoRoot: repoRoot}); err != nil {
		t.Fatalf("Generate() error = %v", err)
	}

	for _, rel := range []string{
		"data/schema/registry/manifest.schema.json",
		"data/schema/registry/resources.schema.json",
		"data/schema/registry/points.schema.json",
		"data/schema/content/units.schema.json",
		"data/schema/content/buildings.schema.json",
		"data/schema/content/technologies.schema.json",
		"data/schema/content/policies.schema.json",
		"data/schema/content/recipes.schema.json",
		"data/schema/content/terrains.schema.json",
		"data/schema/content/rules.schema.json",
		"data/schema/content/ministers.schema.json",
		"data/schema/content/maps/definition.schema.json",
		"data/schema/content/resource_amount.schema.json",
		"data/schema/ui/resources.schema.json",
		"data/schema/ui/units.schema.json",
		"data/schema/ui/buildings.schema.json",
		"data/schema/ui/technology_tree.schema.json",
		"data/schema/ui/terrains.schema.json",
		"data/schema/ui/maps/catalog.schema.json",
		"data/generated/server/sections/resources.json",
		"data/generated/server/sections/technologies.json",
		"data/generated/server/sections/ui_tech_tree_layout.json",
		"data/generated/server/sections/ui_build_menu_layout.json",
		"data/generated/server/sections/ui_recipe_layout.json",
		"client/Assets/Resources/Data/sections/resources.json",
		"client/Assets/Resources/Data/sections/ui_tech_tree_layout.json",
	} {
		if _, err := os.Stat(filepath.Join(repoRoot, rel)); err != nil {
			t.Fatalf("expected generated schema %q: %v", rel, err)
		}
	}

	assertFileContains(t, filepath.Join(repoRoot, "data/schema/content/resource_amount.schema.json"), `"wood"`)
	assertFileContains(t, filepath.Join(repoRoot, "data/schema/registry/manifest.schema.json"), `"additionalProperties": false`)
	assertFileContains(t, filepath.Join(repoRoot, "data/schema/registry/manifest.schema.json"), `"$id"`)
	assertFileContains(t, filepath.Join(repoRoot, "data/schema/content/units.schema.json"), `"additionalProperties": false`)
	assertFileContains(t, filepath.Join(repoRoot, "data/schema/content/buildings.schema.json"), `"city_foundation_center"`)
	assertFileContains(t, filepath.Join(repoRoot, "data/schema/content/buildings.schema.json"), `"explicit_effects"`)
	assertFileContains(t, filepath.Join(repoRoot, "data/schema/content/buildings.schema.json"), `"modifier_effects"`)
	assertFileContains(t, filepath.Join(repoRoot, "data/schema/content/technologies.schema.json"), `"research_cost"`)
	assertFileContains(t, filepath.Join(repoRoot, "data/schema/content/policies.schema.json"), `"national"`)
	assertFileContains(t, filepath.Join(repoRoot, "data/schema/content/recipes.schema.json"), `"point_inputs"`)
	assertFileContains(t, filepath.Join(repoRoot, "data/schema/content/rules.schema.json"), `"bonus_tokens_per_turn"`)
	assertFileNotContains(t, filepath.Join(repoRoot, "data/schema/content/rules.schema.json"), `"`+strings.Join([]string{"tokens", "recu" + "peration", "bonus"}, "_")+`"`)
	assertFileContains(t, filepath.Join(repoRoot, "data/schema/content/maps/definition.schema.json"), `"forest"`)
	assertFileContains(t, filepath.Join(repoRoot, "data/schema/ui/maps/catalog.schema.json"), `"thumbnail_key"`)
	assertFileContains(t, filepath.Join(repoRoot, "data/schema/ui/technology_tree.schema.json"), `"technology_id"`)
	assertFileContains(t, filepath.Join(repoRoot, "data/generated/server/catalog.bundle.json"), `"bundle_hash"`)
	assertFileContains(t, filepath.Join(repoRoot, "data/generated/server/catalog.bundle.json"), `"required_sections"`)
	assertFileContains(t, filepath.Join(repoRoot, "data/generated/server/catalog.bundle.json"), `"section_hashes"`)
	assertFileContains(t, filepath.Join(repoRoot, "data/generated/server/catalog.bundle.json"), `"technologies"`)
	assertFileContains(t, filepath.Join(repoRoot, "data/generated/server/catalog.bundle.json"), `"recipes"`)
	assertFileContains(t, filepath.Join(repoRoot, "data/generated/server/catalog.bundle.json"), `"points"`)
	assertFileContains(t, filepath.Join(repoRoot, "data/generated/server/catalog.bundle.json"), `"policies"`)
	assertFileContains(t, filepath.Join(repoRoot, "data/generated/server/catalog.bundle.json"), `"city_core"`)
	assertFileContains(t, filepath.Join(repoRoot, "data/generated/server/catalog.bundle.json"), `"infantry"`)
	assertFileContains(t, filepath.Join(repoRoot, "data/generated/server/maps/default.runtime.json"), `"nodes"`)
	assertFileContains(t, filepath.Join(repoRoot, "data/generated/server/sections/ui_tech_tree_layout.json"), `"agrarian_foundations"`)
	assertFileContains(t, filepath.Join(repoRoot, "data/generated/server/sections/ui_build_menu_layout.json"), `"building_order"`)
	assertFileContains(t, filepath.Join(repoRoot, "data/generated/server/sections/ui_recipe_layout.json"), `"recipe_order"`)
	assertFileContains(t, filepath.Join(repoRoot, "protocol/data_types.proto"), "message ResourceBag")
	assertFileContains(t, filepath.Join(repoRoot, "protocol/data_types.proto"), "message PointBag")
	assertFileContains(t, filepath.Join(repoRoot, "protocol/data_types.proto"), "message PointDescriptor")
	assertFileContains(t, filepath.Join(repoRoot, "protocol/data_types.proto"), "message CatalogSectionHash")
	assertFileContains(t, filepath.Join(repoRoot, "protocol/data_catalog.proto"), "message MsgStaticCatalogManifest")
	assertFileContains(t, filepath.Join(repoRoot, "protocol/data_catalog.proto"), "message MsgStaticCatalogSyncRequest")
	assertFileContains(t, filepath.Join(repoRoot, "protocol/data_catalog.proto"), "message MsgStaticCatalogSectionChunk")
	assertFileContains(t, filepath.Join(repoRoot, "protocol/data_catalog.proto"), "message MsgStaticCatalogSyncComplete")
	assertFileContains(t, filepath.Join(repoRoot, "protocol/data_catalog.proto"), "message PolicyCatalogEntry")
	assertFileContains(t, filepath.Join(repoRoot, "protocol/data_catalog.proto"), "message TechnologyCatalogEntry")
	assertFileContains(t, filepath.Join(repoRoot, "protocol/data_catalog.proto"), "message RecipeCatalogEntry")
	assertFileContains(t, filepath.Join(repoRoot, "protocol/map_catalog.proto"), "message MapCatalogEntry")
	assertFileContains(t, filepath.Join(repoRoot, "server/internal/staticdata/generated/resource_keys_gen.go"), "ResourceOre")
	assertFileContains(t, filepath.Join(repoRoot, "client/Assets/Scripts/Runtime/Core/Foundation/Domain/ResourceKeys.g.cs"), "ResourceOre")
	assertFileContains(t, filepath.Join(repoRoot, "client/Assets/Resources/Data/catalog.bundle.json"), `"default_map_id": "default"`)
	assertFileNotContains(t, filepath.Join(repoRoot, "data/generated/server/catalog.bundle.json"), `"`+strings.Join([]string{"war", "rior"}, "")+`"`)
	assertFileNotContains(t, filepath.Join(repoRoot, "data/generated/server/catalog.bundle.json"), `"`+strings.Join([]string{"build", "points"}, "_")+`"`)
	assertFileNotContains(t, filepath.Join(repoRoot, "protocol/data_catalog.proto"), "tech_point_cost")
}

func TestGenerateCompilesNoiseBackedMapDefinition(t *testing.T) {
	repoRoot := t.TempDir()
	writeFixtureRepo(t, repoRoot)
	if err := os.WriteFile(filepath.Join(repoRoot, "data/content/maps/default/definition.json"), []byte(`{
  "meta": {
    "id": "default",
    "name": "测试地图",
    "width": 4,
    "height": 4,
    "default_terrain": "plain",
    "tags": ["generated"]
  },
  "generator": {
    "type": "noise",
    "seed": 7,
    "terrain_bands": [
      { "max": 0.35, "terrain": "river" },
      { "max": 0.60, "terrain": "forest" },
      { "max": 1.0, "terrain": "plain" }
    ]
  },
  "terrain_patches": [],
  "node_overrides": [],
  "features": {
    "resource_points": [],
    "roads": [],
    "named_nodes": [],
    "central_points": []
  },
  "spawn_points": [
    { "slot": 0, "x": 0, "y": 0 },
    { "slot": 1, "x": 3, "y": 3 }
  ]
}`), 0o600); err != nil {
		t.Fatalf("WriteFile(map definition) error = %v", err)
	}

	if err := Generate(Options{RepoRoot: repoRoot}); err != nil {
		t.Fatalf("Generate() error = %v", err)
	}

	raw, err := os.ReadFile(filepath.Join(repoRoot, "data/generated/server/maps/default.runtime.json"))
	if err != nil {
		t.Fatalf("ReadFile(runtime map) error = %v", err)
	}

	text := string(raw)
	if !strings.Contains(text, `"terrain": "river"`) && !strings.Contains(text, `"terrain": "forest"`) {
		t.Fatalf("runtime map should contain generated non-default terrain, got %s", text)
	}
}

func TestGenerateCarriesPrebuiltOwnerAndBuildingIntoRuntimeMap(t *testing.T) {
	repoRoot := t.TempDir()
	writeFixtureRepo(t, repoRoot)
	if err := os.WriteFile(filepath.Join(repoRoot, "data/content/maps/default/definition.json"), []byte(`{
  "meta": {
    "id": "default",
    "name": "测试地图",
    "width": 2,
    "height": 2,
    "default_terrain": "plain",
    "tags": ["legacy"]
  },
  "terrain_patches": [],
  "node_overrides": [
    {
      "x": 1,
      "y": 1,
      "owner": "green",
      "owner_slot": 0,
      "territory_owner": "blue",
      "territory_owner_slot": 1,
      "building_type": "farm",
      "building_hp": 77
    }
  ],
  "features": {
    "resource_points": [],
    "roads": [],
    "named_nodes": [],
    "central_points": []
  },
  "spawn_points": [
    { "slot": 0, "x": 1, "y": 1 }
  ]
}`), 0o600); err != nil {
		t.Fatalf("WriteFile(map definition) error = %v", err)
	}

	if err := Generate(Options{RepoRoot: repoRoot}); err != nil {
		t.Fatalf("Generate() error = %v", err)
	}

	raw, err := os.ReadFile(filepath.Join(repoRoot, "data/generated/server/maps/default.runtime.json"))
	if err != nil {
		t.Fatalf("ReadFile(runtime map) error = %v", err)
	}

	text := string(raw)
	for _, want := range []string{
		`"owner": "green"`,
		`"owner_slot": 0`,
		`"territory_owner": "blue"`,
		`"territory_owner_slot": 1`,
		`"building_type": "farm"`,
		`"building_hp": 77`,
	} {
		if !strings.Contains(text, want) {
			t.Fatalf("runtime map missing %s:\n%s", want, text)
		}
	}
}

func TestGenerateRejectsInvalidAuthoringSources(t *testing.T) {
	tests := []struct {
		name         string
		relPath      string
		content      string
		wantPath     string
		wantContains []string
	}{
		{
			name:    "manifest missing default_map_id",
			relPath: "data/registry/manifest.json",
			content: `{
  "$schema": "../schema/registry/manifest.schema.json",
  "schema_version": "2026-04-06",
  "content_version": "2026-04-06.alpha",
  "default_locale": "zh-CN"
}`,
			wantPath:     "data/registry/manifest.json",
			wantContains: []string{"default_map_id"},
		},
		{
			name:    "points reject unknown field",
			relPath: "data/registry/points.json",
			content: `{
  "$schema": "../schema/registry/points.schema.json",
  "points": [
    {
      "key": "research_output",
      "display_name": "科研产出",
      "description": "用于推进当前研究目标。",
      "icon_key": "point_research_output",
      "sort_order": 10,
      "visible_in_hud": true,
      "unexpected": "boom"
    }
  ]
}`,
			wantPath:     "data/registry/points.json",
			wantContains: []string{"unexpected"},
		},
		{
			name:    "units reject unregistered resource key",
			relPath: "data/content/units/units.json",
			content: `{
  "$schema": "../../schema/content/units.schema.json",
  "units": [
    {
      "id": "infantry",
      "class": "melee",
      "max_hp": 30,
      "attack": 10,
      "attack_range": 1,
      "move_range": 2,
      "vision_range": 3,
      "train_cost": { "gold": 1 },
      "upkeep": { "food": 1 },
      "multipliers": {},
      "flags": { "can_siege": false, "can_attack_structures": true, "can_destroy_road": false, "can_capture": true }
    }
  ]
}`,
			wantPath:     "data/content/units/units.json",
			wantContains: []string{"gold"},
		},
		{
			name:    "buildings reject invalid takeover mode",
			relPath: "data/content/buildings/buildings.json",
			content: `{
  "$schema": "../../schema/content/buildings.schema.json",
  "buildings": [
    {
      "id": "farm",
      "placement_kind": "resource_node",
      "building_scope": "out_of_city",
      "required_resource_type": "food",
      "resource_costs": { "wood": 1 },
      "point_costs": { "industry_output": 1 },
      "production": { "input": {}, "output": { "food": 2 }, "cycle_turns": 1 },
      "recipe_ids": ["farm_food"],
      "default_recipe_id": "farm_food",
      "max_hp": 80,
      "takeover_mode": "instant_flip",
      "tags": ["extraction"]
    }
  ]
}`,
			wantPath:     "data/content/buildings/buildings.json",
			wantContains: []string{"instant_flip"},
		},
		{
			name:    "technologies reject unknown unlock target",
			relPath: "data/content/technologies/technologies.json",
			content: `{
  "$schema": "../../schema/content/technologies.schema.json",
  "technologies": [
    {
      "id": "unlock_missing_building",
      "branch": "industry",
      "tier": 1,
      "research_cost": 1,
      "prerequisites": [],
      "explicit_effects": [
        { "type": "unlock_building", "target_id": "ghost_building" }
      ],
      "modifier_effects": []
    }
  ]
}`,
			wantPath:     "data/content/technologies/technologies.json",
			wantContains: []string{"ghost_building"},
		},
		{
			name:    "policies reject unknown point modifier target",
			relPath: "data/content/policies/policies.json",
			content: `{
  "$schema": "../../schema/content/policies.schema.json",
  "policies": [
    {
      "id": "expansion",
      "layer": "national",
      "activation_timing": "same_turn",
      "prerequisites": [],
      "explicit_effects": [],
      "modifier_effects": [
        {
          "trigger": "point.output",
          "point_key": "ghost_point",
          "modifier_type": "flat",
          "value": 1
        }
      ]
    }
  ]
}`,
			wantPath:     "data/content/policies/policies.json",
			wantContains: []string{"ghost_point"},
		},
		{
			name:    "recipes reject unknown point input",
			relPath: "data/content/recipes/recipes.json",
			content: `{
  "$schema": "../../schema/content/recipes.schema.json",
  "recipes": [
    {
      "id": "ghost_recipe",
      "building_id": "farm",
      "resource_inputs": {},
      "point_inputs": { "ghost_point": 1 },
      "work_amount": 1,
      "base_progress": 1,
      "outputs": { "resources": { "food": 1 } }
    }
  ]
}`,
			wantPath:     "data/content/recipes/recipes.json",
			wantContains: []string{"ghost_point"},
		},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			repoRoot := t.TempDir()
			writeFixtureRepo(t, repoRoot)
			writeRepoFile(t, repoRoot, tc.relPath, tc.content)

			err := Generate(Options{RepoRoot: repoRoot})
			if err == nil {
				t.Fatalf("Generate() error = nil, want validation failure")
			}
			if !strings.Contains(err.Error(), tc.wantPath) {
				t.Fatalf("Generate() error = %v, want path %q", err, tc.wantPath)
			}
			for _, want := range tc.wantContains {
				if !strings.Contains(err.Error(), want) {
					t.Fatalf("Generate() error = %v, want substring %q", err, want)
				}
			}
		})
	}
}

func TestGenerateRejectsTechnologyTreeEdgeMismatch(t *testing.T) {
	repoRoot := t.TempDir()
	writeFixtureRepo(t, repoRoot)
	writeRepoFile(t, repoRoot, "data/content/technologies/technologies.json", `{
  "$schema": "../../schema/content/technologies.schema.json",
  "technologies": [
    {
      "id": "agrarian_foundations",
      "branch": "agriculture",
      "tier": 1,
      "research_cost": 1,
      "prerequisites": [],
      "explicit_effects": [],
      "modifier_effects": []
    },
    {
      "id": "organized_labor",
      "branch": "governance",
      "tier": 1,
      "research_cost": 2,
      "prerequisites": [
        { "type": "technology_unlocked", "target_id": "agrarian_foundations" }
      ],
      "explicit_effects": [],
      "modifier_effects": []
    }
  ]
}`)
	writeRepoFile(t, repoRoot, "data/ui/layouts/technology_tree.json", `{
  "$schema": "../../schema/ui/technology_tree.schema.json",
  "config_version": "2026-04-17",
  "nodes": [
    { "id": "node_agri", "technology_id": "agrarian_foundations", "title": "农业基础", "description": "农业", "x": 0, "y": 0, "width": 360, "height": 104, "visible": true },
    { "id": "node_labor", "technology_id": "organized_labor", "title": "组织化劳动", "description": "工业", "x": 420, "y": 0, "width": 360, "height": 104, "visible": true }
  ],
  "edges": []
}`)

	err := Generate(Options{RepoRoot: repoRoot})
	if err == nil {
		t.Fatalf("Generate() error = nil, want technology tree validation failure")
	}
	if !strings.Contains(err.Error(), "data/ui/layouts/technology_tree.json") {
		t.Fatalf("Generate() error = %v, want technology_tree path", err)
	}
	if !strings.Contains(err.Error(), "agrarian_foundations") || !strings.Contains(err.Error(), "organized_labor") {
		t.Fatalf("Generate() error = %v, want missing prerequisite edge detail", err)
	}
}

func writeFixtureRepo(t *testing.T, repoRoot string) {
	t.Helper()

	files := map[string]string{
		"data/registry/manifest.json": `{
  "$schema": "../schema/registry/manifest.schema.json",
  "schema_version": "2026-04-15",
  "content_version": "2026-04-15.alpha",
  "default_locale": "zh-CN",
  "default_map_id": "default"
}`,
		"data/registry/resources.json": `{
  "$schema": "../schema/registry/resources.schema.json",
  "resources": [
    {
      "key": "ore",
      "display_name": "矿石",
      "description": "基础矿物",
      "icon_key": "resource_ore",
      "sort_order": 10,
      "proto_number": 1,
      "visible_in_hud": true
    },
    {
      "key": "wood",
      "display_name": "木材",
      "description": "基础建设材料",
      "icon_key": "resource_wood",
      "sort_order": 20,
      "proto_number": 2,
      "visible_in_hud": true
    },
    {
      "key": "food",
      "display_name": "粮食",
      "description": "人口与军队消耗",
      "icon_key": "resource_food",
      "sort_order": 30,
      "proto_number": 3,
      "visible_in_hud": true
    }
  ]
}`,
		"data/registry/points.json": `{
  "$schema": "../schema/registry/points.schema.json",
  "points": [
    {
      "key": "research_output",
      "display_name": "科研产出",
      "description": "用于推进当前研究目标。",
      "icon_key": "point_research_output",
      "sort_order": 10,
      "visible_in_hud": true
    },
    {
      "key": "industry_output",
      "display_name": "工业产出",
      "description": "用于推进建设与生产。",
      "icon_key": "point_industry_output",
      "sort_order": 20,
      "visible_in_hud": true
    }
  ]
}`,
		"data/content/units/units.json": `{
  "$schema": "../../schema/content/units.schema.json",
  "units": [
    {
      "id": "settler",
      "class": "civilian",
      "max_hp": 12,
      "attack": 0,
      "attack_range": 0,
      "move_range": 2,
      "vision_range": 2,
      "train_cost": { "food": 2, "wood": 1 },
      "upkeep": { "food": 1 },
      "multipliers": {},
      "flags": { "can_siege": false, "can_attack_structures": false, "can_destroy_road": false, "can_capture": true }
    },
    {
      "id": "infantry",
      "class": "melee",
      "max_hp": 30,
      "attack": 10,
      "attack_range": 1,
      "move_range": 2,
      "vision_range": 3,
      "train_cost": { "ore": 1, "food": 1 },
      "upkeep": { "food": 1 },
      "multipliers": {},
      "flags": { "can_siege": false, "can_attack_structures": true, "can_destroy_road": false, "can_capture": true }
    }
  ]
}`,
		"data/content/buildings/buildings.json": `{
  "$schema": "../../schema/content/buildings.schema.json",
  "buildings": [
    {
      "id": "city_core",
      "placement_kind": "city_foundation_center",
      "building_scope": "city_core",
      "required_resource_type": "",
      "resource_costs": { "wood": 2 },
      "point_costs": { "industry_output": 1 },
      "recipe_ids": ["city_core_settler"],
      "default_recipe_id": "city_core_settler",
      "max_hp": 100,
      "takeover_mode": "disabled",
      "tags": ["core", "governance"]
    },
    {
      "id": "farm",
      "placement_kind": "resource_node",
      "building_scope": "out_of_city",
      "required_resource_type": "food",
      "resource_costs": { "wood": 1 },
      "point_costs": { "industry_output": 1 },
      "recipe_ids": ["farm_food"],
      "default_recipe_id": "farm_food",
      "max_hp": 80,
      "takeover_mode": "delayed",
      "tags": ["extraction"]
    },
    {
      "id": "mine",
      "placement_kind": "resource_node",
      "building_scope": "out_of_city",
      "required_resource_type": "ore",
      "resource_costs": { "wood": 1 },
      "point_costs": { "industry_output": 1 },
      "recipe_ids": ["mine_ore"],
      "default_recipe_id": "mine_ore",
      "max_hp": 80,
      "takeover_mode": "delayed",
      "tags": ["extraction"]
    },
    {
      "id": "lumber",
      "placement_kind": "resource_node",
      "building_scope": "out_of_city",
      "required_resource_type": "wood",
      "resource_costs": { "wood": 1 },
      "point_costs": { "industry_output": 1 },
      "recipe_ids": ["lumber_wood"],
      "default_recipe_id": "lumber_wood",
      "max_hp": 80,
      "takeover_mode": "delayed",
      "tags": ["extraction"]
    },
    {
      "id": "barracks",
      "placement_kind": "city_territory",
      "building_scope": "in_city",
      "required_resource_type": "",
      "resource_costs": { "wood": 1, "ore": 1 },
      "point_costs": { "industry_output": 1 },
      "recipe_ids": ["barracks_infantry"],
      "default_recipe_id": "barracks_infantry",
      "max_hp": 90,
      "takeover_mode": "city_capture",
      "tags": ["production"]
    },
    {
      "id": "wall",
      "placement_kind": "city_territory",
      "building_scope": "in_city",
      "required_resource_type": "",
      "resource_costs": { "wood": 1, "ore": 1 },
      "point_costs": { "industry_output": 1 },
      "recipe_ids": [],
      "default_recipe_id": "",
      "max_hp": 120,
      "takeover_mode": "city_capture",
      "tags": ["defense"]
    }
  ]
}`,
		"data/content/technologies/technologies.json": `{
  "$schema": "../../schema/content/technologies.schema.json",
  "technologies": [
    {
      "id": "agrarian_foundations",
      "branch": "agriculture",
      "tier": 1,
      "research_cost": 1,
      "prerequisites": [],
      "explicit_effects": [
        { "type": "unlock_building", "target_id": "farm" },
        { "type": "unlock_recipe", "target_id": "farm_food" }
      ],
      "modifier_effects": []
    },
    {
      "id": "organized_labor",
      "branch": "governance",
      "tier": 1,
      "research_cost": 2,
      "prerequisites": [
        { "type": "technology_unlocked", "target_id": "agrarian_foundations" }
      ],
      "explicit_effects": [],
      "modifier_effects": [
        {
          "trigger": "point.output",
          "point_key": "industry_output",
          "modifier_type": "flat",
          "value": 1
        }
      ]
    }
  ]
}`,
		"data/content/policies/policies.json": `{
  "$schema": "../../schema/content/policies.schema.json",
  "policies": [
    {
      "id": "expansion",
      "layer": "national",
      "activation_timing": "same_turn",
      "prerequisites": [],
      "explicit_effects": [],
      "modifier_effects": []
    },
    {
      "id": "war_preparedness",
      "layer": "national",
      "activation_timing": "same_turn",
      "prerequisites": [],
      "explicit_effects": [],
      "modifier_effects": []
    },
    {
      "id": "recovery",
      "layer": "national",
      "activation_timing": "same_turn",
      "prerequisites": [],
      "explicit_effects": [],
      "modifier_effects": []
    },
    {
      "id": "reorganization",
      "layer": "national",
      "activation_timing": "same_turn",
      "prerequisites": [],
      "explicit_effects": [],
      "modifier_effects": []
    }
  ]
}`,
		"data/content/recipes/recipes.json": `{
  "$schema": "../../schema/content/recipes.schema.json",
  "recipes": [
    {
      "id": "city_core_settler",
      "building_id": "city_core",
      "resource_inputs": { "food": 2, "wood": 1 },
      "point_inputs": { "industry_output": 1 },
      "work_amount": 2,
      "base_progress": 1,
      "outputs": { "units": ["settler"] }
    },
    {
      "id": "farm_food",
      "building_id": "farm",
      "resource_inputs": {},
      "point_inputs": { "industry_output": 1 },
      "work_amount": 1,
      "base_progress": 1,
      "outputs": { "resources": { "food": 2 } }
    },
    {
      "id": "mine_ore",
      "building_id": "mine",
      "resource_inputs": {},
      "point_inputs": { "industry_output": 1 },
      "work_amount": 1,
      "base_progress": 1,
      "outputs": { "resources": { "ore": 2 } }
    },
    {
      "id": "lumber_wood",
      "building_id": "lumber",
      "resource_inputs": {},
      "point_inputs": { "industry_output": 1 },
      "work_amount": 1,
      "base_progress": 1,
      "outputs": { "resources": { "wood": 2 } }
    },
    {
      "id": "barracks_infantry",
      "building_id": "barracks",
      "resource_inputs": { "food": 1, "ore": 1 },
      "point_inputs": { "industry_output": 1 },
      "work_amount": 2,
      "base_progress": 1,
      "outputs": { "units": ["infantry"] }
    }
  ]
}`,
		"data/content/terrains/terrains.json": `{
  "$schema": "../../schema/content/terrains.schema.json",
  "terrains": [
    {
      "id": "plain",
      "move_cost_no_road": 2,
      "defense_bonus": 0.0,
      "attack_penalty": 0.0,
      "blocks_cavalry": false,
      "passable_with_road": false,
      "passable": true,
      "buildable": true
    },
    {
      "id": "forest",
      "move_cost_no_road": 3,
      "defense_bonus": 0.2,
      "attack_penalty": 0.0,
      "blocks_cavalry": false,
      "passable_with_road": false,
      "passable": true,
      "buildable": true
    },
    {
      "id": "river",
      "move_cost_no_road": 99,
      "defense_bonus": 0.0,
      "attack_penalty": 0.0,
      "blocks_cavalry": true,
      "passable_with_road": true,
      "passable": false,
      "buildable": false
    }
  ]
}`,
		"data/content/rules/rules.json": `{
  "$schema": "../../schema/content/rules.schema.json",
  "turn_time_limit_planning": 35,
  "tokens_per_turn": 3,
  "bonus_tokens_per_turn": 1,
  "max_turns": 30,
  "city_core_max_hp": 100,
  "safe_zone_radius": 4,
  "facility_takeover_turns": 2,
  "base_research_output_per_turn": 1,
  "base_industry_output_per_turn": 2,
  "minimum_city_distance": 3,
  "initial_city_territory_radius": 1
}`,
		"data/content/ministers/ministers.json": `{
  "$schema": "../../schema/content/ministers.schema.json",
  "pool": [
    {
      "id": "m001",
      "name": "李猛",
      "role": "military",
      "ability": 8,
      "personality": "aggressive",
      "personality_desc": "果敢激进",
      "loyalty": 7,
      "ambition": 6
    }
  ]
}`,
		"data/content/maps/default/definition.json": `{
  "$schema": "../../../schema/content/maps/definition.schema.json",
  "meta": {
    "id": "default",
    "name": "标准地图",
    "width": 2,
    "height": 2,
    "default_terrain": "plain",
    "tags": ["pvp"]
  },
  "terrain_patches": [
    {
      "kind": "point",
      "terrain": "forest",
      "points": [{ "x": 1, "y": 0 }]
    }
  ],
  "node_overrides": [
    {
      "id": "B2",
      "x": 1,
      "y": 1,
      "terrain": "forest",
      "has_road": true,
      "building_type": "city_core",
      "building_hp": 100
    }
  ],
  "features": {
    "resource_points": [
      { "x": 0, "y": 1, "resource_type": "food", "node_name": "粮仓" }
    ],
    "roads": [
      { "points": [{ "x": 0, "y": 0 }, { "x": 1, "y": 0 }] }
    ],
    "named_nodes": [
      { "x": 1, "y": 1, "name": "林地" }
    ],
    "central_points": [{ "x": 1, "y": 1 }]
  },
  "spawn_points": [
    { "slot": 0, "x": 0, "y": 0 },
    { "slot": 1, "x": 1, "y": 1 }
  ]
}`,
		"data/ui/catalogs/resources.json": `{
  "$schema": "../../schema/ui/resources.schema.json",
  "resources": [
    { "id": "ore", "name": "矿石", "description": "基础矿物", "icon_key": "resource_ore", "sort_order": 10, "tags": ["base"] },
    { "id": "wood", "name": "木材", "description": "基础建设材料", "icon_key": "resource_wood", "sort_order": 20, "tags": ["base"] },
    { "id": "food", "name": "粮食", "description": "补给与人口", "icon_key": "resource_food", "sort_order": 30, "tags": ["base"] }
  ]
}`,
		"data/ui/catalogs/points.json": `{
  "$schema": "../../schema/ui/points.schema.json",
  "points": [
    { "id": "research_output", "name": "科研产出", "description": "推进当前研究目标", "icon_key": "point_research_output", "sort_order": 10, "tags": ["hud"] },
    { "id": "industry_output", "name": "工业产出", "description": "推进建设与生产", "icon_key": "point_industry_output", "sort_order": 20, "tags": ["hud"] }
  ]
}`,
		"data/ui/catalogs/units.json": `{
  "$schema": "../../schema/ui/units.schema.json",
  "units": [
    { "id": "settler", "name": "开拓者", "description": "用于建立新城市。", "icon_key": "unit_settler", "prefab_key": "Settler", "sort_order": 10, "tags": ["civilian"] },
    { "id": "infantry", "name": "步兵", "description": "基础近战战斗单位", "icon_key": "unit_infantry", "prefab_key": "Infantry", "sort_order": 20, "tags": ["frontline"] }
  ]
}`,
		"data/ui/catalogs/buildings.json": `{
  "$schema": "../../schema/ui/buildings.schema.json",
  "buildings": [
    { "id": "city_core", "name": "城市核心", "description": "定义城市存在与归属的核心建筑。", "icon_key": "building_city_core", "prefab_key": "CityCore", "sort_order": 10, "tags": ["core"] },
    { "id": "farm", "name": "农场", "description": "基础粮食产出建筑", "icon_key": "building_farm", "prefab_key": "Farm", "sort_order": 20, "tags": ["eco"] },
    { "id": "mine", "name": "矿山", "description": "基础矿石产出建筑", "icon_key": "building_mine", "prefab_key": "Mine", "sort_order": 30, "tags": ["eco"] },
    { "id": "lumber", "name": "伐木场", "description": "基础木材产出建筑", "icon_key": "building_lumber", "prefab_key": "Lumber", "sort_order": 40, "tags": ["eco"] },
    { "id": "barracks", "name": "兵营", "description": "基础军队训练建筑", "icon_key": "building_barracks", "prefab_key": "Barracks", "sort_order": 50, "tags": ["military"] },
    { "id": "wall", "name": "城墙", "description": "基础被动防御建筑", "icon_key": "building_wall", "prefab_key": "Wall", "sort_order": 60, "tags": ["defense"] }
  ]
}`,
		"data/ui/catalogs/technologies.json": `{
  "$schema": "../../schema/ui/technologies.schema.json",
  "technologies": [
    { "id": "agrarian_foundations", "name": "农业基础", "description": "解锁农场与基础农耕配方", "icon_key": "tech_agrarian_foundations", "sort_order": 10, "tags": ["agriculture"] },
    { "id": "organized_labor", "name": "组织化劳动", "description": "提升工业产出效率", "icon_key": "tech_organized_labor", "sort_order": 20, "tags": ["governance"] }
  ]
}`,
		"data/ui/layouts/technology_tree.json": `{
  "$schema": "../../schema/ui/technology_tree.schema.json",
  "config_version": "2026-04-17",
  "nodes": [
    { "id": "node_agri", "technology_id": "agrarian_foundations", "title": "农业基础", "description": "解锁农场与基础农耕配方", "x": 0, "y": 0, "width": 360, "height": 104, "visible": true },
    { "id": "node_labor", "technology_id": "organized_labor", "title": "组织化劳动", "description": "提升工业产出效率", "x": 420, "y": 0, "width": 360, "height": 104, "visible": true }
  ],
  "edges": [
    {
      "id": "edge_agri_labor",
      "from": "node_agri",
      "to": "node_labor",
      "show_arrow": true,
      "thickness": 3,
      "points": [
        { "x": 180, "y": 0 },
        { "x": 420, "y": 0 }
      ]
    }
  ]
}`,
		"data/ui/catalogs/policies.json": `{
  "$schema": "../../schema/ui/policies.schema.json",
  "policies": [
    { "id": "expansion", "name": "扩张", "description": "优先扩张国家边界。", "icon_key": "policy_expansion", "sort_order": 10, "tags": ["national"] },
    { "id": "war_preparedness", "name": "备战", "description": "优先军事准备。", "icon_key": "policy_war_preparedness", "sort_order": 20, "tags": ["national"] },
    { "id": "recovery", "name": "恢复", "description": "优先恢复国家秩序。", "icon_key": "policy_recovery", "sort_order": 30, "tags": ["national"] },
    { "id": "reorganization", "name": "整饬", "description": "优先整顿国家结构。", "icon_key": "policy_reorganization", "sort_order": 40, "tags": ["national"] }
  ]
}`,
		"data/ui/catalogs/recipes.json": `{
  "$schema": "../../schema/ui/recipes.schema.json",
  "recipes": [
    { "id": "city_core_settler", "name": "组织开拓", "description": "产出开拓者", "icon_key": "recipe_city_core_settler", "sort_order": 10, "tags": ["expansion"] },
    { "id": "farm_food", "name": "基础农耕", "description": "产出粮食", "icon_key": "recipe_farm_food", "sort_order": 20, "tags": ["food"] },
    { "id": "mine_ore", "name": "基础采矿", "description": "产出矿石", "icon_key": "recipe_mine_ore", "sort_order": 30, "tags": ["ore"] },
    { "id": "lumber_wood", "name": "基础伐木", "description": "产出木材", "icon_key": "recipe_lumber_wood", "sort_order": 40, "tags": ["wood"] },
    { "id": "barracks_infantry", "name": "训练步兵", "description": "产出步兵", "icon_key": "recipe_barracks_infantry", "sort_order": 50, "tags": ["military"] }
  ]
}`,
		"data/ui/catalogs/terrains.json": `{
  "$schema": "../../schema/ui/terrains.schema.json",
  "terrains": [
    { "id": "plain", "name": "平原", "description": "标准地块", "icon_key": "terrain_plain", "material_key": "M_Plain", "sort_order": 10, "tags": ["ground"] },
    { "id": "forest", "name": "森林", "description": "高防御地块", "icon_key": "terrain_forest", "material_key": "M_Forest", "sort_order": 20, "tags": ["ground"] },
    { "id": "river", "name": "河流", "description": "难以通行", "icon_key": "terrain_river", "material_key": "M_River", "sort_order": 30, "tags": ["ground"] }
  ]
}`,
		"data/ui/catalogs/maps/default.json": `{
  "$schema": "../../../schema/ui/maps/catalog.schema.json",
  "id": "default",
  "name": "标准地图",
  "description": "默认对战地图",
  "thumbnail_key": "map_default",
  "legend": [
    { "id": "road", "name": "道路", "icon_key": "marker_road" },
    { "id": "resource_point", "name": "资源点", "icon_key": "marker_resource" }
  ]
}`,
	}

	for rel, content := range files {
		writeRepoFile(t, repoRoot, rel, content)
	}
}

func assertFileContains(t *testing.T, path string, want string) {
	t.Helper()
	raw, err := os.ReadFile(path)
	if err != nil {
		t.Fatalf("ReadFile(%q) error = %v", path, err)
	}
	if !strings.Contains(string(raw), want) {
		t.Fatalf("%q does not contain %q:\n%s", path, want, string(raw))
	}
}

func assertFileNotContains(t *testing.T, path string, want string) {
	t.Helper()
	raw, err := os.ReadFile(path)
	if err != nil {
		t.Fatalf("ReadFile(%q) error = %v", path, err)
	}
	if strings.Contains(string(raw), want) {
		t.Fatalf("%q should not contain %q:\n%s", path, want, string(raw))
	}
}

func writeRepoFile(t *testing.T, repoRoot string, rel string, content string) {
	t.Helper()

	path := filepath.Join(repoRoot, rel)
	if err := os.MkdirAll(filepath.Dir(path), 0o755); err != nil {
		t.Fatalf("MkdirAll(%q) error = %v", path, err)
	}
	if err := os.WriteFile(path, []byte(content), 0o600); err != nil {
		t.Fatalf("WriteFile(%q) error = %v", path, err)
	}
}
