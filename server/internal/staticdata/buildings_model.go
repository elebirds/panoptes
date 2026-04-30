// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载静态数据运行时模型、目录索引或查询逻辑。

package staticdata

type BuildingDefinition struct {
	ID                   string           `json:"id"`
	Name                 string           `json:"name"`
	Description          string           `json:"description"`
	IconKey              string           `json:"icon_key"`
	PrefabKey            string           `json:"prefab_key"`
	PlacementKind        string           `json:"placement_kind"`
	BuildingScope        string           `json:"building_scope"`
	RequiredResourceType string           `json:"required_resource_type"`
	ResourceCosts        ResourceAmounts  `json:"resource_costs"`
	PointCosts           PointAmounts     `json:"point_costs"`
	RecipeIDs            []string         `json:"recipe_ids"`
	DefaultRecipeID      string           `json:"default_recipe_id"`
	ExplicitEffects      []ExplicitEffect `json:"explicit_effects"`
	ModifierEffects      []ModifierEffect `json:"modifier_effects"`
	MaxHP                int              `json:"max_hp"`
	TakeoverMode         string           `json:"takeover_mode"`
	SortOrder            int              `json:"sort_order"`
	Tags                 []string         `json:"tags,omitempty"`
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
	RoadBaseCapacity           int `json:"road_base_capacity"`
}
