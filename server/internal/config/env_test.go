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
	if staticdata.Default() == nil {
		t.Fatalf("static data default catalog not loaded")
	}
	if staticdata.Default().Rules().CastleBaseHP != 100 {
		t.Fatalf("CastleBaseHP = %d", staticdata.Default().Rules().CastleBaseHP)
	}
}

func writeConfigFixture(t *testing.T, repoRoot string) {
	t.Helper()
	files := map[string]string{
		"data/registry/manifest.json":                 `{"schema_version":"2026-04-06","content_version":"test","default_locale":"zh-CN","default_map_id":"default"}`,
		"data/registry/resources.json":                `{"resources":[{"key":"ore","display_name":"矿石","description":"基础矿石","icon_key":"resource_ore","sort_order":10,"proto_number":1,"visible_in_hud":true},{"key":"build_points","display_name":"建造点","description":"额度","icon_key":"resource_build_points","sort_order":20,"proto_number":2,"visible_in_hud":true}]}`,
		"data/content/units/units.json":               `{"units":[{"id":"warrior","class":"melee","max_hp":30,"attack":10,"attack_range":1,"move_range":2,"vision_range":3,"train_cost":{"ore":1},"upkeep":{},"multipliers":{},"flags":{"can_siege":false,"can_destroy_road":false,"can_capture":true}}]}`,
		"data/content/buildings/buildings.json":       `{"buildings":[{"id":"farm","category":"production","placement_rule":"resource_only","required_resource_type":"ore","build_cost":{"build_points":1},"upkeep":{},"recipe_ids":["farm_ore"],"default_recipe_id":"farm_ore","combat":{"max_hp":80,"attack_per_turn":0,"range":0,"wall_level":0,"towers":0},"limits":{"max_per_node":1,"max_per_player":-1}}]}`,
		"data/content/technologies/technologies.json": `{"technologies":[{"id":"unlock_farm","branch":"industry","tier":1,"tech_point_cost":0,"prerequisites":[],"effects":[{"type":"unlock_building","target_id":"farm"},{"type":"unlock_recipe","target_id":"farm_ore"}]}]}`,
		"data/content/recipes/recipes.json":           `{"recipes":[{"id":"farm_ore","building_id":"farm","cost":{},"duration_turns":1,"delay_penalty":{"mode":"add_turns","value":1},"outputs":{"resources":{"ore":1}}}]}`,
		"data/content/terrains/terrains.json":         `{"terrains":[{"id":"plain","move_cost_no_road":2,"defense_bonus":0.0,"attack_penalty":0.0,"blocks_cavalry":false,"passable_with_road":false,"passable":true,"buildable":true}]}`,
		"data/content/rules/rules.json":               `{"turn_time_limit_domestic":15,"turn_time_limit_combat":20,"tokens_per_turn":3,"tokens_recuperation_bonus":1,"max_turns":30,"castle_base_hp":100,"safe_zone_radius":4,"occupy_turns":1,"starting_tech_points":0,"tech_points_per_turn":1,"tech_points_max":3,"build_points_per_turn":10,"build_points_max":30}`,
		"data/content/ministers/ministers.json":       `{"pool":[{"id":"m001","name":"李猛","role":"military","ability":8,"personality":"aggressive","personality_desc":"果敢激进","loyalty":7,"ambition":6}]}`,
		"data/content/maps/default/definition.json":   `{"meta":{"id":"default","name":"默认地图","width":1,"height":1,"default_terrain":"plain"},"terrain_patches":[],"node_overrides":[],"features":{"resource_points":[],"roads":[],"named_nodes":[],"central_points":[]},"spawn_points":[{"slot":0,"x":0,"y":0}]}`,
		"data/ui/catalogs/resources.json":             `{"resources":[{"id":"ore","name":"矿石","description":"基础矿石","icon_key":"resource_ore","sort_order":10,"tags":[]},{"id":"build_points","name":"建造点","description":"额度","icon_key":"resource_build_points","sort_order":20,"tags":[]}]}`,
		"data/ui/catalogs/units.json":                 `{"units":[{"id":"warrior","name":"勇士","description":"勇士","icon_key":"unit_warrior","prefab_key":"Infantry","sort_order":10,"tags":[]}]}`,
		"data/ui/catalogs/buildings.json":             `{"buildings":[{"id":"farm","name":"农场","description":"农场","icon_key":"building_farm","prefab_key":"Farm","sort_order":10,"tags":[]}]}`,
		"data/ui/catalogs/technologies.json":          `{"technologies":[{"id":"unlock_farm","name":"开垦令","description":"解锁农场","icon_key":"tech_unlock_farm","sort_order":10,"tags":[]}]}`,
		"data/ui/catalogs/technology_tree.json":       `{"config_version":"1.0.0","nodes":[{"id":"N_Farm","technology_id":"unlock_farm","title":"开垦令","description":"解锁农场","x":0,"y":0,"width":200,"height":80,"visible":true}],"edges":[]}`,
		"data/ui/catalogs/recipes.json":               `{"recipes":[{"id":"farm_ore","name":"采矿试作","description":"产出矿石","icon_key":"recipe_farm_ore","sort_order":10,"tags":[]}]}`,
		"data/ui/catalogs/terrains.json":              `{"terrains":[{"id":"plain","name":"平原","description":"平原","icon_key":"terrain_plain","material_key":"M_Plain","sort_order":10,"tags":[]}]}`,
		"data/ui/catalogs/maps/default.json":          `{"id":"default","name":"默认地图","description":"默认地图","thumbnail_key":"map_default","legend":[]}`,
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
