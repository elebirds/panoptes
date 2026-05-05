// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-15 12:00:00 +0800
// Description: 验证静态目录模块的目录加载与查询。

package staticdata_test

import (
	"os"
	"path/filepath"
	"reflect"
	"runtime"
	"testing"

	"github.com/elebirds/panoptes/internal/datagen"
	"github.com/elebirds/panoptes/internal/staticdata"
)

func TestLoadDirBuildsQueryableCatalog(t *testing.T) {
	repoRoot := t.TempDir()
	writeCatalogFixture(t, repoRoot)

	if err := datagen.Generate(datagen.Options{RepoRoot: repoRoot}); err != nil {
		t.Fatalf("Generate() error = %v", err)
	}

	catalog, err := staticdata.LoadDir(filepath.Join(repoRoot, "data/generated/server"))
	if err != nil {
		t.Fatalf("LoadDir() error = %v", err)
	}

	resource, ok := catalog.GetResource("ore")
	if !ok {
		t.Fatalf("GetResource(ore) missing")
	}
	if resource.DisplayName != "矿石" {
		t.Fatalf("resource display_name = %q", resource.DisplayName)
	}

	point, ok := catalog.GetPoint("research_output")
	if !ok {
		t.Fatalf("GetPoint(research_output) missing")
	}
	if point.DisplayName != "科研产出" {
		t.Fatalf("point display_name = %q", point.DisplayName)
	}

	unit, ok := catalog.GetUnit("infantry")
	if !ok {
		t.Fatalf("GetUnit(infantry) missing")
	}
	if unit.TrainCost["ore"] != 1 || unit.TrainCost["food"] != 1 {
		t.Fatalf("unit train_cost = %#v", unit.TrainCost)
	}

	building, ok := catalog.GetBuilding("farm")
	if !ok {
		t.Fatalf("GetBuilding(farm) missing")
	}
	if building.DefaultRecipeID != "farm_food" || building.TakeoverMode != "delayed" {
		t.Fatalf("building = %#v", building)
	}

	technology, ok := catalog.GetTechnology("agrarian_foundations")
	if !ok {
		t.Fatalf("GetTechnology(agrarian_foundations) missing")
	}
	if technology.ResearchCost != 1 || len(technology.ExplicitEffects) != 2 {
		t.Fatalf("technology = %#v", technology)
	}

	policy, ok := catalog.GetPolicy("expansion")
	if !ok {
		t.Fatalf("GetPolicy(expansion) missing")
	}
	if policy.Layer != "national" {
		t.Fatalf("policy = %#v", policy)
	}

	recipe, ok := catalog.GetRecipe("farm_food")
	if !ok {
		t.Fatalf("GetRecipe(farm_food) missing")
	}
	if recipe.Outputs.Resources["food"] != 2 || recipe.PointInputs["industry_output"] != 1 {
		t.Fatalf("recipe = %#v", recipe)
	}

	terrain, ok := catalog.GetTerrain("forest")
	if !ok {
		t.Fatalf("GetTerrain(forest) missing")
	}
	if !terrain.Passable {
		t.Fatalf("terrain forest should be passable")
	}

	rules := catalog.Rules()
	if rules.CityCoreMaxHP != 100 || rules.BaseIndustryOutputPerTurn != 2 || rules.BaseResearchOutputPerTurn != 1 || rules.TurnTimeLimitPlanning != 35 {
		t.Fatalf("rules = %#v", rules)
	}

	m, ok := catalog.GetMap("default")
	if !ok {
		t.Fatalf("GetMap(default) missing")
	}
	if m.Width != 2 || m.Height != 2 {
		t.Fatalf("map size = %dx%d", m.Width, m.Height)
	}
	if len(m.Nodes) != 4 {
		t.Fatalf("nodes len = %d", len(m.Nodes))
	}
	if m.NamedNodes["B2"] != "林地" {
		t.Fatalf("named nodes = %#v", m.NamedNodes)
	}
	if !m.Nodes[2].IsResourcePoint || m.Nodes[2].ResourceType != "food" {
		t.Fatalf("resource node = %#v", m.Nodes[2])
	}
	if catalog.BundleHash() == "" {
		t.Fatalf("bundle hash is empty")
	}
}

func TestGeneratedDefaultMapMatchesOneVsOneLayout(t *testing.T) {
	_, currentFile, _, ok := runtime.Caller(0)
	if !ok {
		t.Fatalf("runtime.Caller() failed")
	}
	repoRoot := filepath.Clean(filepath.Join(filepath.Dir(currentFile), "..", "..", ".."))
	catalog, err := staticdata.LoadDir(filepath.Join(repoRoot, "data", "generated", "server"))
	if err != nil {
		t.Fatalf("LoadDir(real content) error = %v", err)
	}

	m, ok := catalog.GetMap("default")
	if !ok {
		t.Fatalf("GetMap(default) missing")
	}
	if m.Width != 24 || m.Height != 24 {
		t.Fatalf("map size = %dx%d, want 24x24", m.Width, m.Height)
	}
	if len(m.Nodes) != 24*24 {
		t.Fatalf("nodes len = %d, want %d", len(m.Nodes), 24*24)
	}
	if len(m.SpawnPoints) != 2 {
		t.Fatalf("spawn points len = %d, want 2", len(m.SpawnPoints))
	}

	assertSpawnPoint(t, m, 0, 5, 18)
	assertSpawnPoint(t, m, 1, 18, 5)
	assertMainDiagonalMirror(t, m)

	if got := countRoadNodes(m); got != 0 {
		t.Fatalf("road nodes = %d, want 0", got)
	}
	if got := countResourceNodes(m); got != 12 {
		t.Fatalf("resource nodes = %d, want 12", got)
	}

	if node := runtimeNodeAt(t, m, 5, 18); node.NodeName != "西南营地" {
		t.Fatalf("spawn node name = %q, want 西南营地", node.NodeName)
	}
	if node := runtimeNodeAt(t, m, 18, 5); node.NodeName != "东北营地" {
		t.Fatalf("spawn node name = %q, want 东北营地", node.NodeName)
	}
	if node := runtimeNodeAt(t, m, 10, 15); node.NodeName != "南前矿道" {
		t.Fatalf("south pass node = %#v", node)
	}
	if node := runtimeNodeAt(t, m, 15, 10); node.NodeName != "北前矿道" {
		t.Fatalf("north pass node = %#v", node)
	}
}

func TestCatalogBuildingAndRecipeIDsAreSorted(t *testing.T) {
	catalog := staticdata.NewCatalog(staticdata.CatalogBundle{
		Buildings: []staticdata.BuildingDefinition{
			{ID: "watchtower"},
			{ID: "barracks"},
			{ID: "city_core"},
		},
		Recipes: []staticdata.RecipeDefinition{
			{ID: "zeta_recipe"},
			{ID: "alpha_recipe"},
			{ID: "barracks_infantry"},
		},
	})

	wantBuildings := []string{"barracks", "city_core", "watchtower"}
	if got := catalog.BuildingIDs(); !reflect.DeepEqual(got, wantBuildings) {
		t.Fatalf("BuildingIDs() = %#v, want %#v", got, wantBuildings)
	}

	wantRecipes := []string{"alpha_recipe", "barracks_infantry", "zeta_recipe"}
	if got := catalog.RecipeIDs(); !reflect.DeepEqual(got, wantRecipes) {
		t.Fatalf("RecipeIDs() = %#v, want %#v", got, wantRecipes)
	}
}

func writeCatalogFixture(t *testing.T, repoRoot string) {
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
    { "key": "ore", "display_name": "矿石", "description": "基础矿物", "icon_key": "resource_ore", "sort_order": 10, "proto_number": 1, "visible_in_hud": true },
    { "key": "wood", "display_name": "木材", "description": "基础建设材料", "icon_key": "resource_wood", "sort_order": 20, "proto_number": 2, "visible_in_hud": true },
    { "key": "food", "display_name": "粮食", "description": "补给与人口", "icon_key": "resource_food", "sort_order": 30, "proto_number": 3, "visible_in_hud": true }
  ]
}`,
		"data/registry/points.json": `{
  "$schema": "../schema/registry/points.schema.json",
  "points": [
    { "key": "research_output", "display_name": "科研产出", "description": "推进当前研究目标", "icon_key": "point_research_output", "sort_order": 10, "visible_in_hud": true },
    { "key": "industry_output", "display_name": "工业产出", "description": "推进建设与生产", "icon_key": "point_industry_output", "sort_order": 20, "visible_in_hud": true }
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
    { "id": "city_core", "placement_kind": "city_foundation_center", "building_scope": "city_core", "required_resource_type": "", "resource_costs": { "wood": 2 }, "point_costs": { "industry_output": 1 }, "recipe_ids": ["city_core_settler"], "default_recipe_id": "city_core_settler", "max_hp": 100, "takeover_mode": "disabled", "tags": ["core"] },
    { "id": "farm", "placement_kind": "resource_node", "building_scope": "out_of_city", "required_resource_type": "food", "resource_costs": { "wood": 1 }, "point_costs": { "industry_output": 1 }, "recipe_ids": ["farm_food"], "default_recipe_id": "farm_food", "max_hp": 80, "takeover_mode": "delayed", "tags": ["eco"] }
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
    }
  ]
}`,
		"data/content/policies/policies.json": `{
  "$schema": "../../schema/content/policies.schema.json",
  "policies": [
    { "id": "expansion", "layer": "national", "activation_timing": "same_turn", "prerequisites": [], "explicit_effects": [], "modifier_effects": [] }
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
    { "id": "plain", "move_cost_no_road": 2, "defense_bonus": 0.0, "attack_penalty": 0.0, "blocks_cavalry": false, "passable_with_road": false, "passable": true, "buildable": true },
    { "id": "forest", "move_cost_no_road": 3, "defense_bonus": 0.2, "attack_penalty": 0.0, "blocks_cavalry": false, "passable_with_road": false, "passable": true, "buildable": true }
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
		"data/content/maps/default/definition.json": `{
  "$schema": "../../../schema/content/maps/definition.schema.json",
  "meta": { "id": "default", "name": "标准地图", "width": 2, "height": 2, "default_terrain": "plain", "tags": ["pvp"] },
  "terrain_patches": [
    { "kind": "point", "terrain": "forest", "points": [{ "x": 1, "y": 0 }] }
  ],
  "node_overrides": [
    { "id": "B2", "x": 1, "y": 1, "terrain": "forest", "has_road": true, "building_type": "city_core", "building_hp": 100 }
  ],
  "features": {
    "resource_points": [{ "x": 0, "y": 1, "resource_type": "food", "node_name": "粮仓" }],
    "roads": [{ "points": [{ "x": 0, "y": 0 }, { "x": 1, "y": 0 }] }],
    "named_nodes": [{ "x": 1, "y": 1, "name": "林地" }],
    "central_points": []
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
    { "id": "farm", "name": "农场", "description": "基础粮食产出建筑", "icon_key": "building_farm", "prefab_key": "Farm", "sort_order": 20, "tags": ["eco"] }
  ]
}`,
		"data/ui/catalogs/technologies.json": `{
  "$schema": "../../schema/ui/technologies.schema.json",
  "technologies": [
    { "id": "agrarian_foundations", "name": "农业基础", "description": "解锁农场与基础农耕配方", "icon_key": "tech_agrarian_foundations", "sort_order": 10, "tags": ["agriculture"] }
  ]
}`,
		"data/ui/layouts/technology_tree.json": `{
  "$schema": "../../schema/ui/technology_tree.schema.json",
  "config_version": "2026-04-17",
  "nodes": [
    { "id": "node_agri", "technology_id": "agrarian_foundations", "title": "农业基础", "description": "解锁农场与基础农耕配方", "x": 0, "y": 0, "width": 360, "height": 104, "visible": true }
  ],
  "edges": []
}`,
		"data/ui/catalogs/policies.json": `{
  "$schema": "../../schema/ui/policies.schema.json",
  "policies": [
    { "id": "expansion", "name": "扩张", "description": "优先扩张国家边界。", "icon_key": "policy_expansion", "sort_order": 10, "tags": ["national"] }
  ]
}`,
		"data/ui/catalogs/recipes.json": `{
  "$schema": "../../schema/ui/recipes.schema.json",
  "recipes": [
    { "id": "city_core_settler", "name": "组织开拓", "description": "产出开拓者", "icon_key": "recipe_city_core_settler", "sort_order": 10, "tags": ["expansion"] },
    { "id": "farm_food", "name": "基础农耕", "description": "产出粮食", "icon_key": "recipe_farm_food", "sort_order": 20, "tags": ["food"] }
  ]
}`,
		"data/ui/catalogs/terrains.json": `{
  "$schema": "../../schema/ui/terrains.schema.json",
  "terrains": [
    { "id": "plain", "name": "平原", "description": "标准地块", "icon_key": "terrain_plain", "material_key": "M_Plain", "sort_order": 10, "tags": ["ground"] },
    { "id": "forest", "name": "森林", "description": "高防御地块", "icon_key": "terrain_forest", "material_key": "M_Forest", "sort_order": 20, "tags": ["ground"] }
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
		writeCatalogFixtureFile(t, repoRoot, rel, content)
	}
}

func assertSpawnPoint(t *testing.T, m *staticdata.MapRuntimeBundle, slot int, wantX int, wantY int) {
	t.Helper()

	for _, spawn := range m.SpawnPoints {
		if spawn.Slot != slot {
			continue
		}
		if spawn.X != wantX || spawn.Y != wantY {
			t.Fatalf("spawn[%d] = (%d,%d), want (%d,%d)", slot, spawn.X, spawn.Y, wantX, wantY)
		}
		return
	}

	t.Fatalf("spawn[%d] missing", slot)
}

func countRoadNodes(m *staticdata.MapRuntimeBundle) int {
	count := 0
	for _, node := range m.Nodes {
		if node.HasRoad {
			count++
		}
	}
	return count
}

func countResourceNodes(m *staticdata.MapRuntimeBundle) int {
	count := 0
	for _, node := range m.Nodes {
		if node.IsResourcePoint {
			count++
		}
	}
	return count
}

func assertMainDiagonalMirror(t *testing.T, m *staticdata.MapRuntimeBundle) {
	t.Helper()

	nodesByPos := make(map[[2]int]staticdata.MapRuntimeNode, len(m.Nodes))
	for _, node := range m.Nodes {
		nodesByPos[[2]int{node.X, node.Y}] = node
	}

	for _, node := range m.Nodes {
		mirror, ok := nodesByPos[[2]int{node.Y, node.X}]
		if !ok {
			t.Fatalf("mirror node for (%d,%d) missing", node.X, node.Y)
		}
		if node.Terrain != mirror.Terrain {
			t.Fatalf("terrain mismatch at (%d,%d) -> (%d,%d): %s != %s", node.X, node.Y, mirror.X, mirror.Y, node.Terrain, mirror.Terrain)
		}
		if node.HasRoad != mirror.HasRoad {
			t.Fatalf("road mismatch at (%d,%d) -> (%d,%d)", node.X, node.Y, mirror.X, mirror.Y)
		}
		if node.IsResourcePoint != mirror.IsResourcePoint {
			t.Fatalf("resource flag mismatch at (%d,%d) -> (%d,%d)", node.X, node.Y, mirror.X, mirror.Y)
		}
		if node.ResourceType != mirror.ResourceType {
			t.Fatalf("resource type mismatch at (%d,%d) -> (%d,%d): %s != %s", node.X, node.Y, mirror.X, mirror.Y, node.ResourceType, mirror.ResourceType)
		}
	}
}

func runtimeNodeAt(t *testing.T, m *staticdata.MapRuntimeBundle, x int, y int) staticdata.MapRuntimeNode {
	t.Helper()

	for _, node := range m.Nodes {
		if node.X == x && node.Y == y {
			return node
		}
	}

	t.Fatalf("node (%d,%d) missing", x, y)
	return staticdata.MapRuntimeNode{}
}

func writeCatalogFixtureFile(t *testing.T, repoRoot string, rel string, content string) {
	t.Helper()

	path := filepath.Join(repoRoot, rel)
	if err := os.MkdirAll(filepath.Dir(path), 0o755); err != nil {
		t.Fatalf("MkdirAll(%q) error = %v", path, err)
	}
	if err := os.WriteFile(path, []byte(content), 0o600); err != nil {
		t.Fatalf("WriteFile(%q) error = %v", path, err)
	}
}
