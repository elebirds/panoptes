package config

type ResourceAmount map[ResourceKey]int

type UnitConfig struct {
	Name              string             `json:"name"`
	HP                int                `json:"hp"`
	Attack            int                `json:"attack"`
	Speed             int                `json:"speed"`
	Cost              ResourceAmount     `json:"cost"`
	Multipliers       map[string]float64 `json:"multipliers"`
	CanSiege          bool               `json:"can_siege"`
	SiegeMultiplier   float64            `json:"siege_multiplier"`
	CanDestroy        bool               `json:"can_destroy"`
	DestroyMultiplier float64            `json:"destroy_multiplier"`
	Range             int                `json:"range"`
	RoadSpeedBonus    int                `json:"road_speed_bonus"`
	ChargeBonus       float64            `json:"charge_bonus"`
}

type BuildingConfig struct {
	Name                 string         `json:"name"`
	Category             string         `json:"category"`
	BuildCost            ResourceAmount `json:"build_cost"`
	ProductionIn         ResourceAmount `json:"production_in"`
	ProductionOut        ResourceAmount `json:"production_out"`
	TerrainRequired      string         `json:"terrain_required"`
	FoodCapacityBonus    int            `json:"food_capacity_bonus"`
	Produces             []string       `json:"produces"`
	HP                   int            `json:"hp"`
	DefenseBonusPerLevel float64        `json:"defense_bonus_per_level"`
	MaxLevel             int            `json:"max_level"`
	Upkeep               ResourceAmount `json:"upkeep"`
	AttackPerTurn        int            `json:"attack_per_turn"`
	Range                int            `json:"range"`
	VisionRangeBonus     int            `json:"vision_range_bonus"`
}

type TerrainConfig struct {
	Name             string  `json:"name"`
	MoveCostNoRoad   int     `json:"move_cost_no_road"`
	DefenseBonus     float64 `json:"defense_bonus"`
	AttackPenalty    float64 `json:"attack_penalty"`
	BlocksCavalry    bool    `json:"blocks_cavalry"`
	PassableWithRoad bool    `json:"passable_with_road"`
}

type CombatConfig struct {
	WallReductionPerLevel float64 `json:"wall_reduction_per_level"`
	MaxWallReduction      float64 `json:"max_wall_reduction"`
	TowerDamagePerTower   int     `json:"tower_damage_per_tower"`
	GuardBonusPerUnit     int     `json:"guard_bonus_per_unit"`
	RoadMoveCost          int     `json:"road_move_cost"`
}

type RulesConfig struct {
	TurnTimeLimitDomestic   int `json:"turn_time_limit_domestic"`
	TurnTimeLimitCombat     int `json:"turn_time_limit_combat"`
	TokensPerTurn           int `json:"tokens_per_turn"`
	TokensRecuperationBonus int `json:"tokens_recuperation_bonus"`
	MaxTurns                int `json:"max_turns"`
	CastleBaseHP            int `json:"castle_base_hp"`
	SafeZoneRadius          int `json:"safe_zone_radius"`
	OccupyTurns             int `json:"occupy_turns"`
	BuildPointsPerTurn      int `json:"build_points_per_turn"`
	BuildPointsMax          int `json:"build_points_max"`
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

type MinisterConfig struct {
	Pool []Minister `json:"pool"`
}

type GameData struct {
	Units     map[string]UnitConfig     `json:"units"`
	Buildings map[string]BuildingConfig `json:"buildings"`
	Terrain   map[string]TerrainConfig  `json:"terrain"`
	Combat    CombatConfig              `json:"combat"`
	Rules     RulesConfig               `json:"rules"`
	Ministers MinisterConfig            `json:"ministers"`
}

var Data GameData
