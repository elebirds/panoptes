package staticdata

type ResourceAmounts map[string]int

type Manifest struct {
	SchemaVersion string `json:"schema_version"`
	ContentVersion string `json:"content_version"`
	DefaultLocale string `json:"default_locale"`
	DefaultMapID string `json:"default_map_id"`
	BundleHash string `json:"bundle_hash"`
}

type ResourceDescriptor struct {
	Key string `json:"key"`
	DisplayName string `json:"display_name"`
	Description string `json:"description"`
	IconKey string `json:"icon_key"`
	SortOrder int `json:"sort_order"`
	ProtoNumber int `json:"proto_number"`
	VisibleInHUD bool `json:"visible_in_hud"`
	Tags []string `json:"tags,omitempty"`
}

type UnitFlags struct {
	CanSiege bool `json:"can_siege"`
	SiegeMultiplier float64 `json:"siege_multiplier,omitempty"`
	CanDestroyRoad bool `json:"can_destroy_road"`
	DestroyMultiplier float64 `json:"destroy_multiplier,omitempty"`
	CanCapture bool `json:"can_capture"`
}

type UnitDefinition struct {
	ID string `json:"id"`
	Name string `json:"name"`
	Description string `json:"description"`
	IconKey string `json:"icon_key"`
	PrefabKey string `json:"prefab_key"`
	Class string `json:"class"`
	MaxHP int `json:"max_hp"`
	Attack int `json:"attack"`
	AttackRange int `json:"attack_range"`
	MoveRange int `json:"move_range"`
	VisionRange int `json:"vision_range"`
	TrainCost ResourceAmounts `json:"train_cost"`
	Upkeep ResourceAmounts `json:"upkeep"`
	Multipliers map[string]float64 `json:"multipliers"`
	RoadSpeedBonus int `json:"road_speed_bonus,omitempty"`
	ChargeBonus float64 `json:"charge_bonus,omitempty"`
	Flags UnitFlags `json:"flags"`
	SortOrder int `json:"sort_order"`
	Tags []string `json:"tags,omitempty"`
}

type BuildingProduction struct {
	Input ResourceAmounts `json:"input"`
	Output ResourceAmounts `json:"output"`
	CycleTurns int `json:"cycle_turns"`
}

type BuildingCombat struct {
	MaxHP int `json:"max_hp"`
	AttackPerTurn int `json:"attack_per_turn"`
	Range int `json:"range"`
	WallLevel int `json:"wall_level"`
	Towers int `json:"towers"`
}

type BuildingLimits struct {
	MaxPerNode int `json:"max_per_node"`
	MaxPerPlayer int `json:"max_per_player"`
}

type BuildingDefinition struct {
	ID string `json:"id"`
	Name string `json:"name"`
	Description string `json:"description"`
	IconKey string `json:"icon_key"`
	PrefabKey string `json:"prefab_key"`
	Category string `json:"category"`
	PlacementRule string `json:"placement_rule"`
	RequiredResourceType string `json:"required_resource_type"`
	BuildCost ResourceAmounts `json:"build_cost"`
	Upkeep ResourceAmounts `json:"upkeep"`
	Production BuildingProduction `json:"production"`
	ProducesUnits []string `json:"produces_units"`
	Combat BuildingCombat `json:"combat"`
	Limits BuildingLimits `json:"limits"`
	SortOrder int `json:"sort_order"`
	Tags []string `json:"tags,omitempty"`
}

type TerrainDefinition struct {
	ID string `json:"id"`
	Name string `json:"name"`
	Description string `json:"description"`
	IconKey string `json:"icon_key"`
	MaterialKey string `json:"material_key"`
	MoveCostNoRoad int `json:"move_cost_no_road"`
	DefenseBonus float64 `json:"defense_bonus"`
	AttackPenalty float64 `json:"attack_penalty"`
	BlocksCavalry bool `json:"blocks_cavalry"`
	PassableWithRoad bool `json:"passable_with_road"`
	Passable bool `json:"passable"`
	Buildable bool `json:"buildable"`
	SortOrder int `json:"sort_order"`
	Tags []string `json:"tags,omitempty"`
}

type Rules struct {
	TurnTimeLimitDomestic int `json:"turn_time_limit_domestic"`
	TurnTimeLimitCombat int `json:"turn_time_limit_combat"`
	TokensPerTurn int `json:"tokens_per_turn"`
	TokensRecuperationBonus int `json:"tokens_recuperation_bonus"`
	MaxTurns int `json:"max_turns"`
	CastleBaseHP int `json:"castle_base_hp"`
	SafeZoneRadius int `json:"safe_zone_radius"`
	OccupyTurns int `json:"occupy_turns"`
	BuildPointsPerTurn int `json:"build_points_per_turn"`
	BuildPointsMax int `json:"build_points_max"`
}

type Minister struct {
	ID string `json:"id"`
	Name string `json:"name"`
	Role string `json:"role"`
	Ability int `json:"ability"`
	Personality string `json:"personality"`
	PersonalityDesc string `json:"personality_desc"`
	Loyalty int `json:"loyalty"`
	Ambition int `json:"ambition"`
}

type GridPoint struct {
	X int `json:"x"`
	Y int `json:"y"`
}

type SpawnPoint struct {
	Slot int `json:"slot"`
	X int `json:"x"`
	Y int `json:"y"`
}

type MapMeta struct {
	ID string `json:"id"`
	Name string `json:"name"`
	Width int `json:"width"`
	Height int `json:"height"`
	DefaultTerrain string `json:"default_terrain"`
	Tags []string `json:"tags,omitempty"`
}

type TerrainPatch struct {
	Kind string `json:"kind"`
	Terrain string `json:"terrain"`
	Points []GridPoint `json:"points,omitempty"`
	X int `json:"x,omitempty"`
	Y int `json:"y,omitempty"`
	Width int `json:"width,omitempty"`
	Height int `json:"height,omitempty"`
}

type NodeOverride struct {
	ID string `json:"id,omitempty"`
	X int `json:"x"`
	Y int `json:"y"`
	Terrain string `json:"terrain,omitempty"`
	HasRoad bool `json:"has_road"`
	IsResourcePoint bool `json:"is_resource_point,omitempty"`
	ResourceType string `json:"resource_type,omitempty"`
	NodeName string `json:"node_name,omitempty"`
	Owner string `json:"owner,omitempty"`
	OwnerSlot *int `json:"owner_slot,omitempty"`
	TerritoryOwner string `json:"territory_owner,omitempty"`
	TerritoryOwnerSlot *int `json:"territory_owner_slot,omitempty"`
	BuildingType string `json:"building_type,omitempty"`
	BuildingHP int `json:"building_hp,omitempty"`
}

type ResourcePointFeature struct {
	X int `json:"x"`
	Y int `json:"y"`
	ResourceType string `json:"resource_type"`
	NodeName string `json:"node_name,omitempty"`
}

type RoadFeature struct {
	Points []GridPoint `json:"points"`
}

type NamedNodeFeature struct {
	X int `json:"x"`
	Y int `json:"y"`
	Name string `json:"name"`
}

type MapFeatures struct {
	ResourcePoints []ResourcePointFeature `json:"resource_points"`
	Roads []RoadFeature `json:"roads"`
	NamedNodes []NamedNodeFeature `json:"named_nodes"`
	CentralPoints []GridPoint `json:"central_points"`
}

type TerrainBand struct {
	Max float64 `json:"max"`
	Terrain string `json:"terrain"`
}

type MapGenerator struct {
	Type string `json:"type"`
	Seed int64 `json:"seed"`
	TerrainBands []TerrainBand `json:"terrain_bands,omitempty"`
}

type MapDefinition struct {
	Meta MapMeta `json:"meta"`
	TerrainPatches []TerrainPatch `json:"terrain_patches"`
	NodeOverrides []NodeOverride `json:"node_overrides"`
	Features MapFeatures `json:"features"`
	SpawnPoints []SpawnPoint `json:"spawn_points"`
	Generator *MapGenerator `json:"generator,omitempty"`
}

type MapLegendEntry struct {
	ID string `json:"id"`
	Name string `json:"name"`
	IconKey string `json:"icon_key"`
}

type MapUICatalog struct {
	ID string `json:"id"`
	Name string `json:"name"`
	Description string `json:"description"`
	ThumbnailKey string `json:"thumbnail_key"`
	Legend []MapLegendEntry `json:"legend"`
}

type MapRuntimeNode struct {
	ID string `json:"id"`
	X int `json:"x"`
	Y int `json:"y"`
	Terrain string `json:"terrain"`
	HasRoad bool `json:"has_road"`
	IsResourcePoint bool `json:"is_resource_point"`
	ResourceType string `json:"resource_type,omitempty"`
	NodeName string `json:"node_name,omitempty"`
	Owner string `json:"owner,omitempty"`
	OwnerSlot *int `json:"owner_slot,omitempty"`
	TerritoryOwner string `json:"territory_owner,omitempty"`
	TerritoryOwnerSlot *int `json:"territory_owner_slot,omitempty"`
	BuildingType string `json:"building_type,omitempty"`
	BuildingHP int `json:"building_hp,omitempty"`
}

type MapRuntimeBundle struct {
	ID string `json:"id"`
	Name string `json:"name"`
	Width int `json:"width"`
	Height int `json:"height"`
	Nodes []MapRuntimeNode `json:"nodes"`
	SpawnPoints []SpawnPoint `json:"spawn_points"`
	NamedNodes map[string]string `json:"named_nodes"`
	CentralPoints []string `json:"central_points"`
	Tags []string `json:"tags,omitempty"`
}

type MapCatalogEntry struct {
	ID string `json:"id"`
	Name string `json:"name"`
	Description string `json:"description"`
	ThumbnailKey string `json:"thumbnail_key"`
	Width int `json:"width"`
	Height int `json:"height"`
	Tags []string `json:"tags,omitempty"`
}

type CatalogBundle struct {
	Manifest Manifest `json:"manifest"`
	Resources []ResourceDescriptor `json:"resources"`
	Units []UnitDefinition `json:"units"`
	Buildings []BuildingDefinition `json:"buildings"`
	Terrains []TerrainDefinition `json:"terrains"`
	Rules Rules `json:"rules"`
	Ministers []Minister `json:"ministers"`
	Maps []MapCatalogEntry `json:"maps"`
}

type ResourceCatalogUIFile struct {
	Resources []struct {
		ID string `json:"id"`
		Name string `json:"name"`
		Description string `json:"description"`
		IconKey string `json:"icon_key"`
		SortOrder int `json:"sort_order"`
		Tags []string `json:"tags"`
	} `json:"resources"`
}

type UnitCatalogUIFile struct {
	Units []struct {
		ID string `json:"id"`
		Name string `json:"name"`
		Description string `json:"description"`
		IconKey string `json:"icon_key"`
		PrefabKey string `json:"prefab_key"`
		SortOrder int `json:"sort_order"`
		Tags []string `json:"tags"`
	} `json:"units"`
}

type BuildingCatalogUIFile struct {
	Buildings []struct {
		ID string `json:"id"`
		Name string `json:"name"`
		Description string `json:"description"`
		IconKey string `json:"icon_key"`
		PrefabKey string `json:"prefab_key"`
		SortOrder int `json:"sort_order"`
		Tags []string `json:"tags"`
	} `json:"buildings"`
}

type TerrainCatalogUIFile struct {
	Terrains []struct {
		ID string `json:"id"`
		Name string `json:"name"`
		Description string `json:"description"`
		IconKey string `json:"icon_key"`
		MaterialKey string `json:"material_key"`
		SortOrder int `json:"sort_order"`
		Tags []string `json:"tags"`
	} `json:"terrains"`
}
