// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14
// Description: 验证数据生成器会产出 schema、bundle 和共享代码。

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
		"data/schema/content/units.schema.json",
		"data/schema/content/buildings.schema.json",
		"data/schema/content/technologies.schema.json",
		"data/schema/content/recipes.schema.json",
		"data/schema/content/terrains.schema.json",
		"data/schema/content/rules.schema.json",
		"data/schema/content/ministers.schema.json",
		"data/schema/content/maps/definition.schema.json",
		"data/schema/content/resource_amount.schema.json",
		"data/schema/ui/resources.schema.json",
		"data/schema/ui/units.schema.json",
		"data/schema/ui/buildings.schema.json",
		"data/schema/ui/terrains.schema.json",
		"data/schema/ui/maps/catalog.schema.json",
	} {
		if _, err := os.Stat(filepath.Join(repoRoot, rel)); err != nil {
			t.Fatalf("expected generated schema %q: %v", rel, err)
		}
	}

	assertFileContains(t, filepath.Join(repoRoot, "data/schema/content/resource_amount.schema.json"), `"ore"`)
	assertFileContains(t, filepath.Join(repoRoot, "data/schema/registry/manifest.schema.json"), `"additionalProperties": false`)
	assertFileContains(t, filepath.Join(repoRoot, "data/schema/content/units.schema.json"), `"additionalProperties": false`)
	assertFileContains(t, filepath.Join(repoRoot, "data/schema/content/buildings.schema.json"), `"warrior"`)
	assertFileContains(t, filepath.Join(repoRoot, "data/schema/content/technologies.schema.json"), `"unlock_recipe"`)
	assertFileContains(t, filepath.Join(repoRoot, "data/schema/content/recipes.schema.json"), `"delay_penalty"`)
	assertFileContains(t, filepath.Join(repoRoot, "data/schema/content/maps/definition.schema.json"), `"forest"`)
	assertFileContains(t, filepath.Join(repoRoot, "data/schema/ui/maps/catalog.schema.json"), `"thumbnail_key"`)
	assertFileContains(t, filepath.Join(repoRoot, "data/generated/server/catalog.bundle.json"), `"bundle_hash"`)
	assertFileContains(t, filepath.Join(repoRoot, "data/generated/server/catalog.bundle.json"), `"technologies"`)
	assertFileContains(t, filepath.Join(repoRoot, "data/generated/server/catalog.bundle.json"), `"recipes"`)
	assertFileContains(t, filepath.Join(repoRoot, "data/generated/server/maps/default.runtime.json"), `"nodes"`)
	assertFileContains(t, filepath.Join(repoRoot, "protocol/data_types.proto"), "message ResourceBag")
	assertFileContains(t, filepath.Join(repoRoot, "protocol/data_catalog.proto"), "message MsgStaticCatalogManifest")
	assertFileContains(t, filepath.Join(repoRoot, "protocol/data_catalog.proto"), "message TechnologyCatalogEntry")
	assertFileContains(t, filepath.Join(repoRoot, "protocol/data_catalog.proto"), "message RecipeCatalogEntry")
	assertFileContains(t, filepath.Join(repoRoot, "protocol/map_catalog.proto"), "message MapCatalogEntry")
	assertFileContains(t, filepath.Join(repoRoot, "server/internal/staticdata/generated/resource_keys_gen.go"), "ResourceOre")
	assertFileContains(t, filepath.Join(repoRoot, "client/Assets/Scripts/Runtime/Core/Foundation/Domain/ResourceKeys.g.cs"), "ResourceOre")
	assertFileContains(t, filepath.Join(repoRoot, "client/Assets/Resources/Data/catalog.bundle.json"), `"default_map_id": "default"`)
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
  "schema_version": "2026-04-06",
  "content_version": "2026-04-06.alpha",
  "default_locale": "zh-CN"
}`,
			wantPath:     "data/registry/manifest.json",
			wantContains: []string{"default_map_id"},
		},
		{
			name:    "resources reject unknown field",
			relPath: "data/registry/resources.json",
			content: `{
  "resources": [
    {
      "key": "ore",
      "display_name": "矿石",
      "description": "基础矿物",
      "icon_key": "resource_ore",
      "sort_order": 10,
      "proto_number": 1,
      "visible_in_hud": true,
      "unexpected": "boom"
    }
  ]
}`,
			wantPath:     "data/registry/resources.json",
			wantContains: []string{"unexpected"},
		},
		{
			name:    "units reject unregistered resource key",
			relPath: "data/content/units/units.json",
			content: `{
  "units": [
    {
      "id": "warrior",
      "class": "melee",
      "max_hp": 30,
      "attack": 10,
      "attack_range": 1,
      "move_range": 2,
      "vision_range": 3,
      "train_cost": { "gold": 1 },
      "upkeep": { "food": 1 },
      "multipliers": {},
      "flags": { "can_siege": false, "can_destroy_road": false, "can_capture": true }
    }
  ]
}`,
			wantPath:     "data/content/units/units.json",
			wantContains: []string{"gold"},
		},
		{
			name:    "buildings reject unknown produced unit",
			relPath: "data/content/buildings/buildings.json",
			content: `{
  "buildings": [
    {
      "id": "farm",
      "category": "production",
      "placement_rule": "resource_only",
      "required_resource_type": "food",
      "build_cost": { "food": 1 },
      "upkeep": {},
      "production": { "input": {}, "output": { "food": 2 }, "cycle_turns": 1 },
      "produces_units": ["ghost"],
      "combat": { "max_hp": 80, "attack_per_turn": 0, "range": 0, "wall_level": 0, "towers": 0 },
      "limits": { "max_per_node": 1, "max_per_player": -1 }
    }
  ]
}`,
			wantPath:     "data/content/buildings/buildings.json",
			wantContains: []string{"ghost"},
		},
		{
			name:    "technologies reject unknown unlock target",
			relPath: "data/content/technologies/technologies.json",
			content: `{
  "technologies": [
    {
      "id": "unlock_missing_building",
      "branch": "industry",
      "tier": 1,
      "tech_point_cost": 1,
      "prerequisites": [],
      "effects": [
        { "type": "unlock_building", "target_id": "ghost_building" }
      ]
    }
  ]
}`,
			wantPath:     "data/content/technologies/technologies.json",
			wantContains: []string{"ghost_building"},
		},
		{
			name:    "recipes reject unknown modifier trigger",
			relPath: "data/content/technologies/technologies.json",
			content: `{
  "technologies": [
    {
      "id": "bad_modifier",
      "branch": "industry",
      "tier": 1,
      "tech_point_cost": 1,
      "prerequisites": [],
      "effects": [
        {
          "type": "modifier",
          "trigger": "recipe.unknown",
          "target_id": "farm_food",
          "modifier_type": "flat",
          "value": 1
        }
      ]
    }
  ]
}`,
			wantPath:     "data/content/technologies/technologies.json",
			wantContains: []string{"recipe.unknown"},
		},
		{
			name:    "recipes reject unknown building reference",
			relPath: "data/content/recipes/recipes.json",
			content: `{
  "recipes": [
    {
      "id": "ghost_recipe",
      "building_id": "ghost_building",
      "cost": {},
      "duration_turns": 1,
      "delay_penalty": { "mode": "add_turns", "value": 1 },
      "outputs": { "resources": { "food": 1 } }
    }
  ]
}`,
			wantPath:     "data/content/recipes/recipes.json",
			wantContains: []string{"ghost_building"},
		},
		{
			name:    "maps reject unknown default terrain",
			relPath: "data/content/maps/default/definition.json",
			content: `{
  "meta": {
    "id": "default",
    "name": "测试地图",
    "width": 2,
    "height": 2,
    "default_terrain": "lava",
    "tags": ["pvp"]
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
    { "slot": 0, "x": 0, "y": 0 }
  ]
}`,
			wantPath:     "data/content/maps/default/definition.json",
			wantContains: []string{"lava"},
		},
		{
			name:    "map ui rejects missing thumbnail key",
			relPath: "data/ui/catalogs/maps/default.json",
			content: `{
  "id": "default",
  "name": "标准地图",
  "description": "默认对战地图",
  "legend": [
    { "id": "road", "name": "道路", "icon_key": "marker_road" }
  ]
}`,
			wantPath:     "data/ui/catalogs/maps/default.json",
			wantContains: []string{"thumbnail_key"},
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

func writeFixtureRepo(t *testing.T, repoRoot string) {
	t.Helper()

	files := map[string]string{
		"data/registry/manifest.json": `{
  "schema_version": "2026-04-06",
  "content_version": "2026-04-06.alpha",
  "default_locale": "zh-CN",
  "default_map_id": "default"
}`,
		"data/registry/resources.json": `{
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
      "key": "food",
      "display_name": "粮食",
      "description": "人口与军队消耗",
      "icon_key": "resource_food",
      "sort_order": 20,
      "proto_number": 2,
      "visible_in_hud": true
    }
  ]
}`,
		"data/content/units/units.json": `{
  "units": [
    {
      "id": "warrior",
      "class": "melee",
      "max_hp": 30,
      "attack": 10,
      "attack_range": 1,
      "move_range": 2,
      "vision_range": 3,
      "train_cost": { "ore": 1, "food": 1 },
      "upkeep": { "food": 1 },
      "multipliers": {},
      "flags": { "can_siege": false, "can_destroy_road": false, "can_capture": true }
    }
  ]
}`,
		"data/content/buildings/buildings.json": `{
  "buildings": [
    {
      "id": "farm",
      "category": "production",
      "placement_rule": "resource_only",
      "required_resource_type": "food",
      "build_cost": { "food": 1 },
      "upkeep": {},
      "recipe_ids": ["farm_food"],
      "default_recipe_id": "farm_food",
      "combat": { "max_hp": 80, "attack_per_turn": 0, "range": 0, "wall_level": 0, "towers": 0 },
      "limits": { "max_per_node": 1, "max_per_player": -1 }
    }
  ]
}`,
		"data/content/technologies/technologies.json": `{
  "technologies": [
    {
      "id": "agri_unlock_farm",
      "branch": "agriculture",
      "tier": 1,
      "tech_point_cost": 1,
      "prerequisites": [],
      "effects": [
        { "type": "unlock_building", "target_id": "farm" },
        { "type": "unlock_recipe", "target_id": "farm_food" }
      ]
    },
    {
      "id": "agri_prod_1",
      "branch": "agriculture",
      "tier": 2,
      "tech_point_cost": 1,
      "prerequisites": [
        { "type": "technology_unlocked", "target_id": "agri_unlock_farm" }
      ],
      "effects": [
        {
          "type": "modifier",
          "trigger": "recipe.output",
          "target_id": "farm_food",
          "resource_key": "food",
          "modifier_type": "flat",
          "value": 1
        }
      ]
    }
  ]
}`,
		"data/content/recipes/recipes.json": `{
  "recipes": [
    {
      "id": "farm_food",
      "building_id": "farm",
      "cost": {},
      "duration_turns": 1,
      "delay_penalty": { "mode": "add_turns", "value": 1 },
      "outputs": { "resources": { "food": 2 } }
    }
  ]
}`,
		"data/content/terrains/terrains.json": `{
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
  "turn_time_limit_planning": 35,
  "tokens_per_turn": 3,
  "tokens_recuperation_bonus": 1,
  "max_turns": 30,
  "castle_base_hp": 100,
  "safe_zone_radius": 4,
  "occupy_turns": 1,
  "starting_tech_points": 1,
  "tech_points_per_turn": 1,
  "tech_points_max": 5,
  "build_points_per_turn": 10,
  "build_points_max": 30
}`,
		"data/content/ministers/ministers.json": `{
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
      "has_road": true
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
  "resources": [
    { "id": "ore", "name": "矿石", "description": "基础矿物", "icon_key": "resource_ore", "sort_order": 10, "tags": ["base"] },
    { "id": "food", "name": "粮食", "description": "补给与人口", "icon_key": "resource_food", "sort_order": 20, "tags": ["base"] }
  ]
}`,
		"data/ui/catalogs/units.json": `{
  "units": [
    { "id": "warrior", "name": "勇士", "description": "基础近战战斗单位", "icon_key": "unit_warrior", "prefab_key": "Infantry", "sort_order": 10, "tags": ["frontline"] }
  ]
}`,
		"data/ui/catalogs/buildings.json": `{
  "buildings": [
    { "id": "farm", "name": "农场", "description": "基础粮食产出建筑", "icon_key": "building_farm", "prefab_key": "Farm", "sort_order": 10, "tags": ["eco"] }
  ]
}`,
		"data/ui/catalogs/technologies.json": `{
  "technologies": [
    { "id": "agri_unlock_farm", "name": "开垦令", "description": "解锁农场与基础农耕配方", "icon_key": "tech_agri_unlock_farm", "sort_order": 10, "tags": ["agriculture"] },
    { "id": "agri_prod_1", "name": "精耕细作", "description": "提高农场产出", "icon_key": "tech_agri_prod_1", "sort_order": 20, "tags": ["agriculture"] }
  ]
}`,
		"data/ui/catalogs/recipes.json": `{
  "recipes": [
    { "id": "farm_food", "name": "基础农耕", "description": "产出粮食", "icon_key": "recipe_farm_food", "sort_order": 10, "tags": ["food"] }
  ]
}`,
		"data/ui/catalogs/terrains.json": `{
  "terrains": [
    { "id": "plain", "name": "平原", "description": "标准地块", "icon_key": "terrain_plain", "material_key": "M_Plain", "sort_order": 10, "tags": ["ground"] },
    { "id": "forest", "name": "森林", "description": "高防御地块", "icon_key": "terrain_forest", "material_key": "M_Forest", "sort_order": 20, "tags": ["ground"] },
    { "id": "river", "name": "河流", "description": "难以通行", "icon_key": "terrain_river", "material_key": "M_River", "sort_order": 30, "tags": ["ground"] }
  ]
}`,
		"data/ui/catalogs/maps/default.json": `{
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
