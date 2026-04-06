package strategy

// ---------------------------------------------------------------------------
// 配置映射（对齐 config 目录）
// ---------------------------------------------------------------------------

type ResourceMap map[string]int

type ConfigUI struct {
	SortOrder int `json:"sort_order"`
}

type TerrainTileDef struct {
	ID             string `json:"id"`
	Name           string `json:"name"`
	IconKey        string `json:"icon_key"`
	MaterialKey    string `json:"material_key"`
	MoveCostNoRoad int    `json:"move_cost_no_road"`
	Passable       bool   `json:"passable"`
	Buildable      bool   `json:"buildable"`
}

type OverlayMarkerDef struct {
	ID          string `json:"id"`
	Name        string `json:"name"`
	IconKey     string `json:"icon_key"`
	SourceField string `json:"source_field"`
}

type MapTileConfig struct {
	Schema          string             `json:"$schema,omitempty"`
	ConfigVersion   string             `json:"config_version"`
	DefaultTileSize float64            `json:"default_tile_size"`
	TerrainTiles    []TerrainTileDef   `json:"terrain_tiles"`
	OverlayMarkers  []OverlayMarkerDef `json:"overlay_markers"`
}

type UnitFlags struct {
	CanSiege          bool    `json:"can_siege"`
	SiegeMultiplier   float64 `json:"siege_multiplier,omitempty"`
	CanDestroyRoad    bool    `json:"can_destroy_road"`
	DestroyMultiplier float64 `json:"destroy_multiplier,omitempty"`
	CanCapture        bool    `json:"can_capture"`
}

type UnitDef struct {
	ID             string             `json:"id"`
	Name           string             `json:"name"`
	Description    string             `json:"description"`
	IconKey        string             `json:"icon_key"`
	PrefabKey      string             `json:"prefab_key"`
	Class          string             `json:"class"`
	MaxHP          int                `json:"max_hp"`
	Attack         int                `json:"attack"`
	AttackRange    int                `json:"attack_range"`
	MoveRange      int                `json:"move_range"`
	VisionRange    int                `json:"vision_range"`
	TrainCost      ResourceMap        `json:"train_cost"`
	Upkeep         ResourceMap        `json:"upkeep"`
	Multipliers    map[string]float64 `json:"multipliers"`
	RoadSpeedBonus int                `json:"road_speed_bonus,omitempty"`
	ChargeBonus    float64            `json:"charge_bonus,omitempty"`
	Flags          UnitFlags          `json:"flags"`
	UI             ConfigUI           `json:"ui"`
	Tags           []string           `json:"tags,omitempty"`
}

type ArmyConfig struct {
	Schema        string    `json:"$schema,omitempty"`
	ConfigVersion string    `json:"config_version"`
	DefaultLocale string    `json:"default_locale"`
	Units         []UnitDef `json:"units"`
}

type BuildingProduction struct {
	Input      ResourceMap `json:"input"`
	Output     ResourceMap `json:"output"`
	CycleTurns int         `json:"cycle_turns"`
}

type BuildingCombat struct {
	MaxHP                int     `json:"max_hp"`
	AttackPerTurn        int     `json:"attack_per_turn,omitempty"`
	Range                int     `json:"range,omitempty"`
	DefenseBonusPerLevel float64 `json:"defense_bonus_per_level,omitempty"`
	MaxLevel             int     `json:"max_level,omitempty"`
	VisionRangeBonus     int     `json:"vision_range_bonus,omitempty"`
}

type BuildingLimits struct {
	MaxPerNode   int `json:"max_per_node"`
	MaxPerPlayer int `json:"max_per_player"`
}

type BuildingDef struct {
	ID                   string             `json:"id"`
	Name                 string             `json:"name"`
	Description          string             `json:"description"`
	IconKey              string             `json:"icon_key"`
	PrefabKey            string             `json:"prefab_key"`
	Category             string             `json:"category"`
	PlacementRule        string             `json:"placement_rule"`
	RequiredResourceType string             `json:"required_resource_type,omitempty"`
	BuildCost            ResourceMap        `json:"build_cost"`
	Upkeep               ResourceMap        `json:"upkeep"`
	Production           BuildingProduction `json:"production"`
	ProducesUnits        []string           `json:"produces_units,omitempty"`
	Combat               BuildingCombat     `json:"combat"`
	Limits               BuildingLimits     `json:"limits"`
	UI                   ConfigUI           `json:"ui"`
	Tags                 []string           `json:"tags,omitempty"`
}

type BuildConfig struct {
	Schema        string        `json:"$schema,omitempty"`
	ConfigVersion string        `json:"config_version"`
	DefaultLocale string        `json:"default_locale"`
	Buildings     []BuildingDef `json:"buildings"`
}

type GameDataConfig struct {
	Army    ArmyConfig    `json:"army"`
	Build   BuildConfig   `json:"build"`
	MapTile MapTileConfig `json:"map_tile"`
}

// DefaultGameData returns a hardcoded config matching the files in config/.
func DefaultGameData() GameDataConfig {
	return GameDataConfig{
		Army: ArmyConfig{
			Schema:        "./armyconfig.schema.json",
			ConfigVersion: "2026-04-06.army.v1",
			DefaultLocale: "zh-CN",
			Units: []UnitDef{
				{
					ID:          "infantry",
					Name:        "步兵",
					Description: "均衡近战单位。",
					IconKey:     "unit_infantry",
					PrefabKey:   "Infantry",
					Class:       "melee",
					MaxHP:       30,
					Attack:      10,
					AttackRange: 1,
					MoveRange:   2,
					VisionRange: 3,
					TrainCost:   ResourceMap{"ore": 1, "food": 1},
					Upkeep:      ResourceMap{"food": 1},
					Multipliers: map[string]float64{"saboteur": 1.5},
					Flags:       UnitFlags{CanSiege: false, CanDestroyRoad: false, CanCapture: true},
					UI:          ConfigUI{SortOrder: 10},
					Tags:        []string{"frontline"},
				},
				{
					ID:          "archer",
					Name:        "弓手",
					Description: "远程压制单位。",
					IconKey:     "unit_archer",
					PrefabKey:   "Archer",
					Class:       "ranged",
					MaxHP:       20,
					Attack:      8,
					AttackRange: 2,
					MoveRange:   1,
					VisionRange: 4,
					TrainCost:   ResourceMap{"wood": 1, "food": 1},
					Upkeep:      ResourceMap{"food": 1},
					Multipliers: map[string]float64{"cavalry": 1.5},
					Flags:       UnitFlags{CanSiege: false, CanDestroyRoad: false, CanCapture: true},
					UI:          ConfigUI{SortOrder: 11},
					Tags:        []string{"ranged"},
				},
				{
					ID:             "cavalry",
					Name:           "骑兵",
					Description:    "高速机动单位。",
					IconKey:        "unit_cavalry",
					PrefabKey:      "Cavalry",
					Class:          "mobile",
					MaxHP:          25,
					Attack:         12,
					AttackRange:    1,
					MoveRange:      3,
					VisionRange:    4,
					TrainCost:      ResourceMap{"refined_ore": 1, "food": 2},
					Upkeep:         ResourceMap{"food": 2},
					Multipliers:    map[string]float64{"infantry": 1.5},
					RoadSpeedBonus: 1,
					ChargeBonus:    1.5,
					Flags:          UnitFlags{CanSiege: false, CanDestroyRoad: false, CanCapture: true},
					UI:             ConfigUI{SortOrder: 12},
					Tags:           []string{"flank", "charge"},
				},
				{
					ID:          "siege",
					Name:        "攻城兵",
					Description: "擅长攻击建筑与主城。",
					IconKey:     "unit_siege",
					PrefabKey:   "Siege",
					Class:       "siege",
					MaxHP:       35,
					Attack:      5,
					AttackRange: 1,
					MoveRange:   1,
					VisionRange: 3,
					TrainCost:   ResourceMap{"refined_ore": 2, "food": 1},
					Upkeep:      ResourceMap{"food": 1},
					Multipliers: map[string]float64{},
					Flags:       UnitFlags{CanSiege: true, SiegeMultiplier: 3.0, CanDestroyRoad: false, CanCapture: true},
					UI:          ConfigUI{SortOrder: 13},
					Tags:        []string{"anti_building"},
				},
			},
		},
		Build: BuildConfig{
			Schema:        "./buildconfig.schema.json",
			ConfigVersion: "2026-04-06.build.v1",
			DefaultLocale: "zh-CN",
			Buildings: []BuildingDef{
				{
					ID:                   "farm",
					Name:                 "农场",
					Description:          "基础粮食产出建筑。",
					IconKey:              "building_farm",
					PrefabKey:            "Farm",
					Category:             "production",
					PlacementRule:        "resource_only",
					RequiredResourceType: "food",
					BuildCost:            ResourceMap{"build_points": 2},
					Upkeep:               ResourceMap{"food": 0},
					Production:           BuildingProduction{Input: ResourceMap{}, Output: ResourceMap{"food": 2}, CycleTurns: 1},
					Combat:               BuildingCombat{MaxHP: 80},
					Limits:               BuildingLimits{MaxPerNode: 1, MaxPerPlayer: -1},
					UI:                   ConfigUI{SortOrder: 10},
					Tags:                 []string{"eco", "food"},
				},
			},
		},
		MapTile: MapTileConfig{
			Schema:          "./maptileconfig.schema.json",
			ConfigVersion:   "2026-04-06.tile.v1",
			DefaultTileSize: 1.0,
			TerrainTiles: []TerrainTileDef{
				{ID: "plain", Name: "Plain", IconKey: "terrain_plain", MaterialKey: "M_Ground_Lit", MoveCostNoRoad: 2, Passable: true, Buildable: true},
				{ID: "mountain", Name: "Mountain", IconKey: "terrain_mountain", MaterialKey: "M_Ground_Mountain_Lit", MoveCostNoRoad: 3, Passable: true, Buildable: false},
				{ID: "forest", Name: "Forest", IconKey: "terrain_forest", MaterialKey: "M_Ground_Forest_Lit", MoveCostNoRoad: 3, Passable: true, Buildable: true},
				{ID: "river", Name: "River", IconKey: "terrain_river", MaterialKey: "M_Ground_River_Lit", MoveCostNoRoad: 99, Passable: false, Buildable: false},
			},
			OverlayMarkers: []OverlayMarkerDef{
				{ID: "road", Name: "Road", IconKey: "marker_road", SourceField: "hasRoad"},
				{ID: "resource_point", Name: "Resource Point", IconKey: "marker_resource", SourceField: "isResourcePoint"},
				{ID: "safe_zone", Name: "Safe Zone", IconKey: "marker_safe_zone", SourceField: "isSafeZone"},
			},
		},
	}
}
