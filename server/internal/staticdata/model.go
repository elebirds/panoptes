// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 定义静态目录模块的静态数据结构。

package staticdata

type ResourceAmounts map[string]int

type Manifest struct {
	SchemaVersion  string `json:"schema_version"`
	ContentVersion string `json:"content_version"`
	DefaultLocale  string `json:"default_locale"`
	DefaultMapID   string `json:"default_map_id"`
	BundleHash     string `json:"bundle_hash"`
}

type ResourceDescriptor struct {
	Key          string   `json:"key"`
	DisplayName  string   `json:"display_name"`
	Description  string   `json:"description"`
	IconKey      string   `json:"icon_key"`
	SortOrder    int      `json:"sort_order"`
	ProtoNumber  int      `json:"proto_number"`
	VisibleInHUD bool     `json:"visible_in_hud"`
	Tags         []string `json:"tags,omitempty"`
}

type PointAmounts map[string]int

type PointDescriptor struct {
	Key          string   `json:"key"`
	DisplayName  string   `json:"display_name"`
	Description  string   `json:"description"`
	IconKey      string   `json:"icon_key"`
	SortOrder    int      `json:"sort_order"`
	VisibleInHUD bool     `json:"visible_in_hud"`
	Tags         []string `json:"tags,omitempty"`
}

type UnitFlags struct {
	CanSiege          bool    `json:"can_siege"`
	SiegeMultiplier   float64 `json:"siege_multiplier,omitempty"`
	CanDestroyRoad    bool    `json:"can_destroy_road"`
	DestroyMultiplier float64 `json:"destroy_multiplier,omitempty"`
	CanCapture        bool    `json:"can_capture"`
}

type UnitDefinition struct {
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
	TrainCost      ResourceAmounts    `json:"train_cost"`
	Upkeep         ResourceAmounts    `json:"upkeep"`
	Multipliers    map[string]float64 `json:"multipliers"`
	RoadSpeedBonus int                `json:"road_speed_bonus,omitempty"`
	ChargeBonus    float64            `json:"charge_bonus,omitempty"`
	Flags          UnitFlags          `json:"flags"`
	SortOrder      int                `json:"sort_order"`
	Tags           []string           `json:"tags,omitempty"`
}

type ModifierTrigger string

const (
	ModifierTriggerBuildingResourceCost ModifierTrigger = "building.resource_cost"
	ModifierTriggerBuildingPointCost    ModifierTrigger = "building.point_cost"
	ModifierTriggerBuildingMaxHP        ModifierTrigger = "building.max_hp"
	ModifierTriggerRecipeResourceInput  ModifierTrigger = "recipe.resource_input"
	ModifierTriggerRecipePointInput     ModifierTrigger = "recipe.point_input"
	ModifierTriggerRecipeResourceOutput ModifierTrigger = "recipe.resource_output"
	ModifierTriggerRecipeWorkAmount     ModifierTrigger = "recipe.work_amount"
	ModifierTriggerRecipeBaseProgress   ModifierTrigger = "recipe.base_progress"
	ModifierTriggerUnitAttack           ModifierTrigger = "unit.attack"
	ModifierTriggerUnitMoveRange        ModifierTrigger = "unit.move_range"
	ModifierTriggerUnitSiegeMultiplier  ModifierTrigger = "unit.siege_multiplier"
	ModifierTriggerPointOutput          ModifierTrigger = "point.output"
)

func AllowedModifierTriggers() []string {
	return []string{
		string(ModifierTriggerBuildingResourceCost),
		string(ModifierTriggerBuildingPointCost),
		string(ModifierTriggerBuildingMaxHP),
		string(ModifierTriggerRecipeResourceInput),
		string(ModifierTriggerRecipePointInput),
		string(ModifierTriggerRecipeResourceOutput),
		string(ModifierTriggerRecipeWorkAmount),
		string(ModifierTriggerRecipeBaseProgress),
		string(ModifierTriggerUnitAttack),
		string(ModifierTriggerUnitMoveRange),
		string(ModifierTriggerUnitSiegeMultiplier),
		string(ModifierTriggerPointOutput),
	}
}

type Prerequisite struct {
	Type     string `json:"type"`
	TargetID string `json:"target_id"`
}

type ExplicitEffect struct {
	Type           string          `json:"type"`
	TargetID       string          `json:"target_id,omitempty"`
	ResourceKey    string          `json:"resource_key,omitempty"`
	PointKey       string          `json:"point_key,omitempty"`
	GrantResources ResourceAmounts `json:"grant_resources,omitempty"`
	GrantUnits     []string        `json:"grant_units,omitempty"`
}

type ModifierEffect struct {
	Trigger      string  `json:"trigger"`
	TargetID     string  `json:"target_id,omitempty"`
	ResourceKey  string  `json:"resource_key,omitempty"`
	PointKey     string  `json:"point_key,omitempty"`
	ModifierType string  `json:"modifier_type"`
	Value        float64 `json:"value"`
}

type RecipeOutputs struct {
	Resources     ResourceAmounts `json:"resources,omitempty"`
	Units         []string        `json:"units,omitempty"`
	PointProgress PointAmounts    `json:"point_progress,omitempty"`
	StateChanges  map[string]int  `json:"state_changes,omitempty"`
}

type RecipeDefinition struct {
	ID             string          `json:"id"`
	Name           string          `json:"name"`
	Description    string          `json:"description"`
	IconKey        string          `json:"icon_key"`
	BuildingID     string          `json:"building_id"`
	ResourceInputs ResourceAmounts `json:"resource_inputs"`
	PointInputs    PointAmounts    `json:"point_inputs"`
	WorkAmount     int             `json:"work_amount"`
	BaseProgress   int             `json:"base_progress"`
	Outputs        RecipeOutputs   `json:"outputs"`
	SortOrder      int             `json:"sort_order"`
	Tags           []string        `json:"tags,omitempty"`
}

type TechnologyDefinition struct {
	ID              string           `json:"id"`
	Name            string           `json:"name"`
	Description     string           `json:"description"`
	IconKey         string           `json:"icon_key"`
	Branch          string           `json:"branch"`
	Tier            int              `json:"tier"`
	ResearchCost    int              `json:"research_cost"`
	Prerequisites   []Prerequisite   `json:"prerequisites"`
	ExplicitEffects []ExplicitEffect `json:"explicit_effects"`
	ModifierEffects []ModifierEffect `json:"modifier_effects"`
	SortOrder       int              `json:"sort_order"`
	Tags            []string         `json:"tags,omitempty"`
}

type PolicyDefinition struct {
	ID               string           `json:"id"`
	Name             string           `json:"name"`
	Description      string           `json:"description"`
	IconKey          string           `json:"icon_key"`
	Layer            string           `json:"layer"`
	ActivationTiming string           `json:"activation_timing"`
	Prerequisites    []Prerequisite   `json:"prerequisites"`
	ExplicitEffects  []ExplicitEffect `json:"explicit_effects"`
	ModifierEffects  []ModifierEffect `json:"modifier_effects"`
	SortOrder        int              `json:"sort_order"`
	Tags             []string         `json:"tags,omitempty"`
}

type BuildingDefinition struct {
	ID                   string          `json:"id"`
	Name                 string          `json:"name"`
	Description          string          `json:"description"`
	IconKey              string          `json:"icon_key"`
	PrefabKey            string          `json:"prefab_key"`
	PlacementKind        string          `json:"placement_kind"`
	BuildingScope        string          `json:"building_scope"`
	RequiredResourceType string          `json:"required_resource_type"`
	ResourceCosts        ResourceAmounts `json:"resource_costs"`
	PointCosts           PointAmounts    `json:"point_costs"`
	RecipeIDs            []string        `json:"recipe_ids"`
	DefaultRecipeID      string          `json:"default_recipe_id"`
	MaxHP                int             `json:"max_hp"`
	TakeoverMode         string          `json:"takeover_mode"`
	SortOrder            int             `json:"sort_order"`
	Tags                 []string        `json:"tags,omitempty"`
}

type TerrainDefinition struct {
	ID               string   `json:"id"`
	Name             string   `json:"name"`
	Description      string   `json:"description"`
	IconKey          string   `json:"icon_key"`
	MaterialKey      string   `json:"material_key"`
	MoveCostNoRoad   int      `json:"move_cost_no_road"`
	DefenseBonus     float64  `json:"defense_bonus"`
	AttackPenalty    float64  `json:"attack_penalty"`
	BlocksCavalry    bool     `json:"blocks_cavalry"`
	PassableWithRoad bool     `json:"passable_with_road"`
	Passable         bool     `json:"passable"`
	Buildable        bool     `json:"buildable"`
	SortOrder        int      `json:"sort_order"`
	Tags             []string `json:"tags,omitempty"`
}

type Rules struct {
	TurnTimeLimitPlanning      int `json:"turn_time_limit_planning"`
	TokensPerTurn              int `json:"tokens_per_turn"`
	BonusTokensPerTurn         int `json:"bonus_tokens_per_turn"`
	MaxTurns                   int `json:"max_turns"`
	CityCoreMaxHP              int `json:"city_core_max_hp"`
	SafeZoneRadius             int `json:"safe_zone_radius"`
	FacilityTakeoverTurns      int `json:"facility_takeover_turns"`
	BaseResearchOutputPerTurn  int `json:"base_research_output_per_turn"`
	BaseIndustryOutputPerTurn  int `json:"base_industry_output_per_turn"`
	MinimumCityDistance        int `json:"minimum_city_distance"`
	InitialCityTerritoryRadius int `json:"initial_city_territory_radius"`
}

type Minister struct {
	ID              string `json:"id"`
	Name            string `json:"name"`
	Role            string `json:"role"`
	Ability         int    `json:"ability"`
	Personality     string `json:"personality"`
	PersonalityDesc string `json:"personality_desc"`
	Loyalty         int    `json:"loyalty"`
	Ambition        int    `json:"ambition"`
}

type GridPoint struct {
	X int `json:"x"`
	Y int `json:"y"`
}

type SpawnPoint struct {
	Slot int `json:"slot"`
	X    int `json:"x"`
	Y    int `json:"y"`
}

type MapMeta struct {
	ID             string   `json:"id"`
	Name           string   `json:"name"`
	Width          int      `json:"width"`
	Height         int      `json:"height"`
	DefaultTerrain string   `json:"default_terrain"`
	Tags           []string `json:"tags,omitempty"`
}

type TerrainPatch struct {
	Kind    string      `json:"kind"`
	Terrain string      `json:"terrain"`
	Points  []GridPoint `json:"points,omitempty"`
	X       int         `json:"x,omitempty"`
	Y       int         `json:"y,omitempty"`
	Width   int         `json:"width,omitempty"`
	Height  int         `json:"height,omitempty"`
}

type NodeOverride struct {
	ID                 string `json:"id,omitempty"`
	X                  int    `json:"x"`
	Y                  int    `json:"y"`
	Terrain            string `json:"terrain,omitempty"`
	HasRoad            bool   `json:"has_road"`
	IsResourcePoint    bool   `json:"is_resource_point,omitempty"`
	ResourceType       string `json:"resource_type,omitempty"`
	NodeName           string `json:"node_name,omitempty"`
	Owner              string `json:"owner,omitempty"`
	OwnerSlot          *int   `json:"owner_slot,omitempty"`
	TerritoryOwner     string `json:"territory_owner,omitempty"`
	TerritoryOwnerSlot *int   `json:"territory_owner_slot,omitempty"`
	BuildingType       string `json:"building_type,omitempty"`
	BuildingHP         int    `json:"building_hp,omitempty"`
}

type ResourcePointFeature struct {
	X            int    `json:"x"`
	Y            int    `json:"y"`
	ResourceType string `json:"resource_type"`
	NodeName     string `json:"node_name,omitempty"`
}

type RoadFeature struct {
	Points []GridPoint `json:"points"`
}

type NamedNodeFeature struct {
	X    int    `json:"x"`
	Y    int    `json:"y"`
	Name string `json:"name"`
}

type MapFeatures struct {
	ResourcePoints []ResourcePointFeature `json:"resource_points"`
	Roads          []RoadFeature          `json:"roads"`
	NamedNodes     []NamedNodeFeature     `json:"named_nodes"`
	CentralPoints  []GridPoint            `json:"central_points"`
}

type TerrainBand struct {
	Max     float64 `json:"max"`
	Terrain string  `json:"terrain"`
}

type MapGenerator struct {
	Type         string        `json:"type"`
	Seed         int64         `json:"seed"`
	TerrainBands []TerrainBand `json:"terrain_bands,omitempty"`
}

type MapDefinition struct {
	Meta           MapMeta        `json:"meta"`
	TerrainPatches []TerrainPatch `json:"terrain_patches"`
	NodeOverrides  []NodeOverride `json:"node_overrides"`
	Features       MapFeatures    `json:"features"`
	SpawnPoints    []SpawnPoint   `json:"spawn_points"`
	Generator      *MapGenerator  `json:"generator,omitempty"`
}

type MapLegendEntry struct {
	ID      string `json:"id"`
	Name    string `json:"name"`
	IconKey string `json:"icon_key"`
}

type MapUICatalog struct {
	ID           string           `json:"id"`
	Name         string           `json:"name"`
	Description  string           `json:"description"`
	ThumbnailKey string           `json:"thumbnail_key"`
	Legend       []MapLegendEntry `json:"legend"`
}

type MapRuntimeNode struct {
	ID                 string `json:"id"`
	X                  int    `json:"x"`
	Y                  int    `json:"y"`
	Terrain            string `json:"terrain"`
	HasRoad            bool   `json:"has_road"`
	IsResourcePoint    bool   `json:"is_resource_point"`
	ResourceType       string `json:"resource_type,omitempty"`
	NodeName           string `json:"node_name,omitempty"`
	Owner              string `json:"owner,omitempty"`
	OwnerSlot          *int   `json:"owner_slot,omitempty"`
	TerritoryOwner     string `json:"territory_owner,omitempty"`
	TerritoryOwnerSlot *int   `json:"territory_owner_slot,omitempty"`
	BuildingType       string `json:"building_type,omitempty"`
	BuildingHP         int    `json:"building_hp,omitempty"`
}

type MapRuntimeBundle struct {
	ID            string            `json:"id"`
	Name          string            `json:"name"`
	Width         int               `json:"width"`
	Height        int               `json:"height"`
	Nodes         []MapRuntimeNode  `json:"nodes"`
	SpawnPoints   []SpawnPoint      `json:"spawn_points"`
	NamedNodes    map[string]string `json:"named_nodes"`
	CentralPoints []string          `json:"central_points"`
	Tags          []string          `json:"tags,omitempty"`
}

type MapCatalogEntry struct {
	ID           string   `json:"id"`
	Name         string   `json:"name"`
	Description  string   `json:"description"`
	ThumbnailKey string   `json:"thumbnail_key"`
	Width        int      `json:"width"`
	Height       int      `json:"height"`
	Tags         []string `json:"tags,omitempty"`
}

type CatalogBundle struct {
	Manifest     Manifest               `json:"manifest"`
	Resources    []ResourceDescriptor   `json:"resources"`
	Points       []PointDescriptor      `json:"points"`
	Units        []UnitDefinition       `json:"units"`
	Buildings    []BuildingDefinition   `json:"buildings"`
	Technologies []TechnologyDefinition `json:"technologies"`
	Policies     []PolicyDefinition     `json:"policies"`
	Recipes      []RecipeDefinition     `json:"recipes"`
	Terrains     []TerrainDefinition    `json:"terrains"`
	Rules        Rules                  `json:"rules"`
	Ministers    []Minister             `json:"ministers"`
	Maps         []MapCatalogEntry      `json:"maps"`
}

type ResourceCatalogUIFile struct {
	Resources []struct {
		ID          string   `json:"id"`
		Name        string   `json:"name"`
		Description string   `json:"description"`
		IconKey     string   `json:"icon_key"`
		SortOrder   int      `json:"sort_order"`
		Tags        []string `json:"tags"`
	} `json:"resources"`
}

type PointCatalogUIFile struct {
	Points []struct {
		ID          string   `json:"id"`
		Name        string   `json:"name"`
		Description string   `json:"description"`
		IconKey     string   `json:"icon_key"`
		SortOrder   int      `json:"sort_order"`
		Tags        []string `json:"tags"`
	} `json:"points"`
}

type UnitCatalogUIFile struct {
	Units []struct {
		ID          string   `json:"id"`
		Name        string   `json:"name"`
		Description string   `json:"description"`
		IconKey     string   `json:"icon_key"`
		PrefabKey   string   `json:"prefab_key"`
		SortOrder   int      `json:"sort_order"`
		Tags        []string `json:"tags"`
	} `json:"units"`
}

type BuildingCatalogUIFile struct {
	Buildings []struct {
		ID          string   `json:"id"`
		Name        string   `json:"name"`
		Description string   `json:"description"`
		IconKey     string   `json:"icon_key"`
		PrefabKey   string   `json:"prefab_key"`
		SortOrder   int      `json:"sort_order"`
		Tags        []string `json:"tags"`
	} `json:"buildings"`
}

type TerrainCatalogUIFile struct {
	Terrains []struct {
		ID          string   `json:"id"`
		Name        string   `json:"name"`
		Description string   `json:"description"`
		IconKey     string   `json:"icon_key"`
		MaterialKey string   `json:"material_key"`
		SortOrder   int      `json:"sort_order"`
		Tags        []string `json:"tags"`
	} `json:"terrains"`
}

type TechnologyCatalogUIFile struct {
	Technologies []struct {
		ID          string   `json:"id"`
		Name        string   `json:"name"`
		Description string   `json:"description"`
		IconKey     string   `json:"icon_key"`
		SortOrder   int      `json:"sort_order"`
		Tags        []string `json:"tags"`
	} `json:"technologies"`
}

type PolicyCatalogUIFile struct {
	Policies []struct {
		ID          string   `json:"id"`
		Name        string   `json:"name"`
		Description string   `json:"description"`
		IconKey     string   `json:"icon_key"`
		SortOrder   int      `json:"sort_order"`
		Tags        []string `json:"tags"`
	} `json:"policies"`
}

type RecipeCatalogUIFile struct {
	Recipes []struct {
		ID          string   `json:"id"`
		Name        string   `json:"name"`
		Description string   `json:"description"`
		IconKey     string   `json:"icon_key"`
		SortOrder   int      `json:"sort_order"`
		Tags        []string `json:"tags"`
	} `json:"recipes"`
}
