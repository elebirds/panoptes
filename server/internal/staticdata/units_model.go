// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载静态数据运行时模型、目录索引或查询逻辑。

package staticdata

type UnitFlags struct {
	CanSiege            bool    `json:"can_siege"`
	SiegeMultiplier     float64 `json:"siege_multiplier,omitempty"`
	CanAttackStructures bool    `json:"can_attack_structures"`
	CanDestroyRoad      bool    `json:"can_destroy_road"`
	DestroyMultiplier   float64 `json:"destroy_multiplier,omitempty"`
	CanCapture          bool    `json:"can_capture"`
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

type Minister struct {
	ID              string `json:"id"`
	Name            string `json:"name"`
	Role            string `json:"role"`
	IconKey         string `json:"icon_key"`
	Ability         int    `json:"ability"`
	Personality     string `json:"personality"`
	PersonalityDesc string `json:"personality_desc"`
	Loyalty         int    `json:"loyalty"`
	Ambition        int    `json:"ambition"`

	// 性格四维度
	Cautiousness      int `json:"cautiousness"`       // 谨慎度：0-100，鲁莽 ↔ 谨慎
	Decisiveness      int `json:"decisiveness"`       // 果断度：0-100，优柔寡断 ↔ 果断
	LoyaltyTendency   int `json:"loyalty_tendency"`   // 忠诚倾向：0-100，狡猾 ↔ 忠诚
	AmbitionStyle     int `json:"ambition_style"`     // 野心表现：0-100，隐忍 ↔ 张扬
}
