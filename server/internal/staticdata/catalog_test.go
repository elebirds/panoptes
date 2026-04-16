package staticdata_test

import (
	"os"
	"path/filepath"
	"testing"

	"github.com/elebirds/panoptes/internal/datagen"
	"github.com/elebirds/panoptes/internal/staticdata"
)

func TestLoadDirBuildsQueryableCatalog(t *testing.T) {
	repoRoot := t.TempDir()
	datagenTestFixture(t, repoRoot)

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

	unit, ok := catalog.GetUnit("warrior")
	if !ok {
		t.Fatalf("GetUnit(warrior) missing")
	}
	if unit.TrainCost["ore"] != 1 || unit.TrainCost["food"] != 1 {
		t.Fatalf("unit train_cost = %#v", unit.TrainCost)
	}

	building, ok := catalog.GetBuilding("farm")
	if !ok {
		t.Fatalf("GetBuilding(farm) missing")
	}
	if building.DefaultRecipeID != "farm_food" {
		t.Fatalf("building default recipe = %q", building.DefaultRecipeID)
	}

	technology, ok := catalog.GetTechnology("agri_unlock_farm")
	if !ok {
		t.Fatalf("GetTechnology(agri_unlock_farm) missing")
	}
	if technology.TechPointCost != 1 || len(technology.Effects) != 2 {
		t.Fatalf("technology = %#v", technology)
	}

	recipe, ok := catalog.GetRecipe("farm_food")
	if !ok {
		t.Fatalf("GetRecipe(farm_food) missing")
	}
	if recipe.Outputs.Resources["food"] != 2 {
		t.Fatalf("recipe output = %#v", recipe.Outputs.Resources)
	}

	terrain, ok := catalog.GetTerrain("forest")
	if !ok {
		t.Fatalf("GetTerrain(forest) missing")
	}
	if !terrain.Passable {
		t.Fatalf("terrain forest should be passable")
	}

	rules := catalog.Rules()
	if rules.CastleBaseHP != 100 || rules.BuildPointsPerTurn != 10 || rules.TechPointsPerTurn != 1 {
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

func datagenTestFixture(t *testing.T, repoRoot string) {
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
      "description": "基础粮食",
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
    }
  ]
}`,
		"data/content/rules/rules.json": `{
  "turn_time_limit_domestic": 15,
  "turn_time_limit_combat": 20,
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
    "roads": [],
    "named_nodes": [
      { "x": 1, "y": 1, "name": "林地" }
    ],
    "central_points": []
  },
  "spawn_points": [
    { "slot": 0, "x": 0, "y": 0 },
    { "slot": 1, "x": 1, "y": 1 }
  ]
}`,
		"data/ui/catalogs/resources.json":       `{"resources":[{"id":"ore","name":"矿石","description":"基础矿物","icon_key":"resource_ore","sort_order":10,"tags":["base"]},{"id":"food","name":"粮食","description":"补给","icon_key":"resource_food","sort_order":20,"tags":["base"]}]}`,
		"data/ui/catalogs/units.json":           `{"units":[{"id":"warrior","name":"勇士","description":"基础近战战斗单位","icon_key":"unit_warrior","prefab_key":"Infantry","sort_order":10,"tags":["frontline"]}]}`,
		"data/ui/catalogs/buildings.json":       `{"buildings":[{"id":"farm","name":"农场","description":"粮食建筑","icon_key":"building_farm","prefab_key":"Farm","sort_order":10,"tags":["eco"]}]}`,
		"data/ui/catalogs/technologies.json":    `{"technologies":[{"id":"agri_unlock_farm","name":"开垦令","description":"解锁农场","icon_key":"tech_agri_unlock_farm","sort_order":10,"tags":["agriculture"]}]}`,
		"data/ui/catalogs/technology_tree.json": `{"config_version":"1.0.0","nodes":[{"id":"N_Farm","technology_id":"agri_unlock_farm","title":"农耕","description":"农场解锁：粮食","x":0,"y":0,"width":200,"height":80,"visible":true}],"edges":[]}`,
		"data/ui/catalogs/recipes.json":         `{"recipes":[{"id":"farm_food","name":"基础农耕","description":"产出粮食","icon_key":"recipe_farm_food","sort_order":10,"tags":["food"]}]}`,
		"data/ui/catalogs/terrains.json":        `{"terrains":[{"id":"plain","name":"平原","description":"标准地块","icon_key":"terrain_plain","material_key":"M_Plain","sort_order":10,"tags":["ground"]},{"id":"forest","name":"森林","description":"树林","icon_key":"terrain_forest","material_key":"M_Forest","sort_order":20,"tags":["ground"]}]}`,
		"data/ui/catalogs/maps/default.json":    `{"id":"default","name":"标准地图","description":"默认地图","thumbnail_key":"map_default","legend":[]}`,
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
