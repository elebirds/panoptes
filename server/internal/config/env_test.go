// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 验证配置模块的环境变量加载与默认配置装配。

package config

import (
	"os"
	"path/filepath"
	"testing"

	"github.com/elebirds/panoptes/internal/datagen"
	"github.com/elebirds/panoptes/internal/staticdata"
)

func TestLoadReadsDevModeAndGameDefaults(t *testing.T) {
	repoRoot := t.TempDir()
	writeConfigFixture(t, repoRoot)
	if err := datagen.Generate(datagen.Options{RepoRoot: repoRoot}); err != nil {
		t.Fatalf("Generate() error = %v", err)
	}

	t.Setenv("DEV_MODE", "true")
	t.Setenv("DATA_ROOT", filepath.Join(repoRoot, "data"))
	t.Setenv("MAP_ID", "default")
	t.Setenv("MINISTER_LLM_ENABLED", "true")
	t.Setenv("MINISTER_LLM_PROVIDER", "qwen")
	t.Setenv("MINISTER_LLM_API_KEY", "test-llm-key")
	t.Setenv("MINISTER_LLM_MODEL", "qwen-plus")
	t.Setenv("MINISTER_LLM_TIMEOUT_MS", "4200")
	t.Setenv("MINISTER_LLM_ENABLED_ROLES", "domestic,military")
	t.Setenv("MINISTER_LLM_PARTICIPATION_MODE", "strong")

	cfg, err := Load()
	if err != nil {
		t.Fatalf("Load() error = %v", err)
	}

	if !cfg.DevMode {
		t.Fatalf("DevMode = false")
	}
	if cfg.MapID != "default" {
		t.Fatalf("MapID = %q", cfg.MapID)
	}
	if !cfg.MinisterLLMEnabled {
		t.Fatalf("MinisterLLMEnabled = false")
	}
	if cfg.MinisterLLMProvider != "qwen" {
		t.Fatalf("MinisterLLMProvider = %q", cfg.MinisterLLMProvider)
	}
	if cfg.MinisterLLMAPIKey != "test-llm-key" {
		t.Fatalf("MinisterLLMAPIKey = %q", cfg.MinisterLLMAPIKey)
	}
	if cfg.MinisterLLMModel != "qwen-plus" {
		t.Fatalf("MinisterLLMModel = %q", cfg.MinisterLLMModel)
	}
	if cfg.MinisterLLMTimeoutMs != 4200 {
		t.Fatalf("MinisterLLMTimeoutMs = %d", cfg.MinisterLLMTimeoutMs)
	}
	if cfg.MinisterLLMRoles != "domestic,military" {
		t.Fatalf("MinisterLLMRoles = %q", cfg.MinisterLLMRoles)
	}
	if cfg.MinisterLLMParticipationMode != "strong" {
		t.Fatalf("MinisterLLMParticipationMode = %q", cfg.MinisterLLMParticipationMode)
	}
	if staticdata.Default() == nil {
		t.Fatalf("static data default catalog not loaded")
	}
	if staticdata.Default().Rules().CityCoreMaxHP != 100 {
		t.Fatalf("CityCoreMaxHP = %d", staticdata.Default().Rules().CityCoreMaxHP)
	}
}

func writeConfigFixture(t *testing.T, repoRoot string) {
	t.Helper()
	files := map[string]string{
		"data/registry/manifest.json": `{
  "$schema": "../schema/registry/manifest.schema.json",
  "schema_version": "2026-04-15",
  "content_version": "test",
  "default_locale": "zh-CN",
  "default_map_id": "default"
}`,
		"data/registry/resources.json": `{
  "$schema": "../schema/registry/resources.schema.json",
  "resources": [
    { "key": "ore", "display_name": "矿石", "description": "基础矿石", "icon_key": "resource_ore", "sort_order": 10, "proto_number": 1, "visible_in_hud": true },
    { "key": "wood", "display_name": "木材", "description": "基础木材", "icon_key": "resource_wood", "sort_order": 20, "proto_number": 2, "visible_in_hud": true },
    { "key": "food", "display_name": "食物", "description": "基础食物", "icon_key": "resource_food", "sort_order": 30, "proto_number": 3, "visible_in_hud": true }
  ]
}`,
		"data/registry/points.json": `{
  "$schema": "../schema/registry/points.schema.json",
  "points": [
    { "key": "research_output", "display_name": "科研产出", "description": "推进研究目标", "icon_key": "point_research_output", "sort_order": 10, "visible_in_hud": true },
    { "key": "industry_output", "display_name": "工业产出", "description": "推进建设与生产", "icon_key": "point_industry_output", "sort_order": 20, "visible_in_hud": true }
  ]
}`,
		"data/content/units/units.json": `{
  "$schema": "../../schema/content/units.schema.json",
  "units": [
    { "id": "settler", "class": "civilian", "max_hp": 12, "attack": 0, "attack_range": 0, "move_range": 2, "vision_range": 2, "train_cost": { "food": 2, "wood": 1 }, "upkeep": { "food": 1 }, "multipliers": {}, "flags": { "can_siege": false, "can_attack_structures": false, "can_destroy_road": false, "can_capture": true } },
    { "id": "infantry", "class": "melee", "max_hp": 30, "attack": 10, "attack_range": 1, "move_range": 2, "vision_range": 3, "train_cost": { "food": 1, "ore": 1 }, "upkeep": { "food": 1 }, "multipliers": {}, "flags": { "can_siege": false, "can_attack_structures": true, "can_destroy_road": false, "can_capture": true } }
  ]
}`,
		"data/content/buildings/buildings.json": `{
  "$schema": "../../schema/content/buildings.schema.json",
  "buildings": [
    { "id": "city_core", "placement_kind": "city_foundation_center", "building_scope": "city_core", "required_resource_type": "", "resource_costs": { "wood": 2 }, "point_costs": { "industry_output": 1 }, "recipe_ids": ["city_core_settler"], "default_recipe_id": "city_core_settler", "max_hp": 100, "takeover_mode": "disabled" },
    { "id": "farm", "placement_kind": "resource_node", "building_scope": "out_of_city", "required_resource_type": "food", "resource_costs": { "wood": 1 }, "point_costs": { "industry_output": 1 }, "recipe_ids": ["farm_food"], "default_recipe_id": "farm_food", "max_hp": 80, "takeover_mode": "delayed" }
  ]
}`,
		"data/content/technologies/technologies.json": `{
  "$schema": "../../schema/content/technologies.schema.json",
  "technologies": [
    { "id": "agrarian_foundations", "branch": "agriculture", "tier": 1, "research_cost": 1, "prerequisites": [], "explicit_effects": [{ "type": "unlock_building", "target_id": "farm" }, { "type": "unlock_recipe", "target_id": "farm_food" }], "modifier_effects": [] }
  ]
}`,
		"data/content/policies/policies.json": `{
  "$schema": "../../schema/content/policies.schema.json",
  "policies": [
    { "id": "expansion", "layer": "national", "activation_timing": "same_turn", "prerequisites": [], "explicit_effects": [], "modifier_effects": [] },
    { "id": "war_preparedness", "layer": "national", "activation_timing": "same_turn", "prerequisites": [], "explicit_effects": [], "modifier_effects": [] },
    { "id": "recovery", "layer": "national", "activation_timing": "same_turn", "prerequisites": [], "explicit_effects": [], "modifier_effects": [] },
    { "id": "reorganization", "layer": "national", "activation_timing": "same_turn", "prerequisites": [], "explicit_effects": [], "modifier_effects": [] }
  ]
}`,
		"data/content/institutions/institutions.json": `{
  "$schema": "../../schema/content/institutions.schema.json",
  "categories": [
    { "id": "administration", "name": "行政制度", "description": "决定中央执行链条。", "sort_order": 10, "tags": [] }
  ],
  "institutions": [
    { "id": "academy_charter", "category": "administration", "activation_timing": "next_turn", "prerequisites": [], "explicit_effects": [], "modifier_effects": [] }
  ]
}`,
		"data/content/recipes/recipes.json": `{
  "$schema": "../../schema/content/recipes.schema.json",
  "recipes": [
    { "id": "city_core_settler", "building_id": "city_core", "resource_inputs": { "food": 2, "wood": 1 }, "point_inputs": { "industry_output": 1 }, "work_amount": 2, "base_progress": 1, "outputs": { "units": ["settler"] } },
    { "id": "farm_food", "building_id": "farm", "resource_inputs": {}, "point_inputs": { "industry_output": 1 }, "work_amount": 1, "base_progress": 1, "outputs": { "resources": { "food": 2 } } }
  ]
}`,
		"data/content/terrains/terrains.json": `{
  "$schema": "../../schema/content/terrains.schema.json",
  "terrains": [
    { "id": "plain", "move_cost_no_road": 2, "defense_bonus": 0.0, "attack_penalty": 0.0, "blocks_cavalry": false, "passable_with_road": false, "passable": true, "buildable": true }
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
    { "id": "m001", "name": "李猛", "role": "military", "icon_key": "military", "ability": 8, "personality": "aggressive", "personality_desc": "果敢激进", "loyalty": 7, "ambition": 6 }
  ]
}`,
		"data/content/ministers/skill_cards.json": `{
  "$schema": "../../schema/content/minister_skill_cards.schema.json",
  "minister_skill_cards": [
    { "id": "stargazing", "name": "观星", "description": "下一回合展开全图视野。", "icon_key": "skill_stargazing", "role_tags": ["military"], "rarity": "rare", "effect_key": "next_turn_full_map_vision", "trigger_timing": "activated", "delay_turns": 1, "duration_turns": 1, "sort_order": 10 }
  ]
}`,
		"data/content/maps/default/definition.json": `{
  "$schema": "../../../schema/content/map_definition.schema.json",
  "meta": { "id": "default", "name": "默认地图", "width": 1, "height": 1, "default_terrain": "plain" },
  "terrain_patches": [],
  "node_overrides": [],
  "features": { "resource_points": [], "roads": [], "named_nodes": [], "central_points": [] },
  "spawn_points": [{ "slot": 0, "x": 0, "y": 0 }]
}`,
		"data/ui/catalogs/resources.json": `{
  "$schema": "../../schema/ui/resources.schema.json",
  "resources": [
    { "id": "ore", "name": "矿石", "description": "基础矿石", "icon_key": "resource_ore", "sort_order": 10, "tags": [] },
    { "id": "wood", "name": "木材", "description": "基础木材", "icon_key": "resource_wood", "sort_order": 20, "tags": [] },
    { "id": "food", "name": "食物", "description": "基础食物", "icon_key": "resource_food", "sort_order": 30, "tags": [] }
  ]
}`,
		"data/ui/catalogs/points.json": `{
  "$schema": "../../schema/ui/points.schema.json",
  "points": [
    { "id": "research_output", "name": "科研产出", "description": "推进研究目标", "icon_key": "point_research_output", "sort_order": 10, "tags": [] },
    { "id": "industry_output", "name": "工业产出", "description": "推进建设与生产", "icon_key": "point_industry_output", "sort_order": 20, "tags": [] }
  ]
}`,
		"data/ui/catalogs/units.json": `{
  "$schema": "../../schema/ui/units.schema.json",
  "units": [
    { "id": "settler", "name": "开拓者", "description": "建立新城市", "icon_key": "unit_settler", "prefab_key": "Settler", "sort_order": 10, "tags": [] },
    { "id": "infantry", "name": "步兵", "description": "基础近战单位", "icon_key": "unit_infantry", "prefab_key": "Infantry", "sort_order": 20, "tags": [] }
  ]
}`,
		"data/ui/catalogs/buildings.json": `{
  "$schema": "../../schema/ui/buildings.schema.json",
  "buildings": [
    { "id": "city_core", "name": "城市核心", "description": "城市核心节点", "icon_key": "building_city_core", "prefab_key": "CityCore", "sort_order": 10, "tags": [] },
    { "id": "farm", "name": "农场", "description": "产出食物", "icon_key": "building_farm", "prefab_key": "Farm", "sort_order": 20, "tags": [] }
  ]
}`,
		"data/ui/catalogs/technologies.json": `{
  "$schema": "../../schema/ui/technologies.schema.json",
  "technologies": [
    { "id": "agrarian_foundations", "name": "农业基础", "description": "解锁农场生产链", "icon_key": "tech_agrarian_foundations", "sort_order": 10, "tags": [] }
  ]
}`,
		"data/ui/layouts/technology_tree.json": `{
  "$schema": "../../schema/ui/technology_tree.schema.json",
  "config_version": "2026-04-17",
  "nodes": [
    { "id": "node_agri", "technology_id": "agrarian_foundations", "title": "农业基础", "description": "解锁农场生产链", "x": 0, "y": 0, "width": 360, "height": 104, "visible": true }
  ],
  "edges": []
}`,
		"data/ui/catalogs/policies.json": `{
  "$schema": "../../schema/ui/policies.schema.json",
  "policies": [
    { "id": "expansion", "name": "扩张", "description": "鼓励扩张", "icon_key": "policy_expansion", "sort_order": 10, "tags": [] },
    { "id": "war_preparedness", "name": "战备", "description": "强化战备", "icon_key": "policy_war_preparedness", "sort_order": 20, "tags": [] },
    { "id": "recovery", "name": "恢复", "description": "强调恢复", "icon_key": "policy_recovery", "sort_order": 30, "tags": [] },
    { "id": "reorganization", "name": "重组", "description": "推进重组", "icon_key": "policy_reorganization", "sort_order": 40, "tags": [] }
  ]
}`,
		"data/ui/catalogs/institutions.json": `{
  "$schema": "../../schema/ui/institutions.schema.json",
  "institutions": [
    { "id": "academy_charter", "name": "学术特许", "description": "提升科研产出。", "icon_key": "policy_academy_charter", "sort_order": 50, "tags": [] }
  ]
}`,
		"data/ui/catalogs/recipes.json": `{
  "$schema": "../../schema/ui/recipes.schema.json",
  "recipes": [
    { "id": "city_core_settler", "name": "训练开拓者", "description": "训练基础扩张单位", "icon_key": "recipe_city_core_settler", "sort_order": 10, "tags": [] },
    { "id": "farm_food", "name": "种植食物", "description": "产出食物", "icon_key": "recipe_farm_food", "sort_order": 20, "tags": [] }
  ]
}`,
		"data/ui/catalogs/terrains.json": `{
  "$schema": "../../schema/ui/terrains.schema.json",
  "terrains": [
    { "id": "plain", "name": "平原", "description": "平原", "icon_key": "terrain_plain", "material_key": "M_Plain", "sort_order": 10, "tags": [] }
  ]
}`,
		"data/ui/catalogs/maps/default.json": `{
  "$schema": "../../schema/ui/map_catalog.schema.json",
  "id": "default",
  "name": "默认地图",
  "description": "默认地图",
  "thumbnail_key": "map_default",
  "legend": []
}`,
	}
	for rel, content := range files {
		path := filepath.Join(repoRoot, rel)
		if err := os.MkdirAll(filepath.Dir(path), 0o755); err != nil {
			t.Fatalf("MkdirAll(%q) error = %v", path, err)
		}
		if err := os.WriteFile(path, []byte(content), 0o600); err != nil {
			t.Fatalf("WriteFile(%q) error = %v", path, err)
		}
	}
}
