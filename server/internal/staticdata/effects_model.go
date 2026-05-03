// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载静态数据运行时模型、目录索引或查询逻辑。

package staticdata

type ModifierTrigger string

const (
	ModifierTriggerBuildingResourceCost  ModifierTrigger = "building.resource_cost"
	ModifierTriggerBuildingPointCost     ModifierTrigger = "building.point_cost"
	ModifierTriggerBuildingMaxHP         ModifierTrigger = "building.max_hp"
	ModifierTriggerRecipeResourceInput   ModifierTrigger = "recipe.resource_input"
	ModifierTriggerRecipePointInput      ModifierTrigger = "recipe.point_input"
	ModifierTriggerRecipeResourceOutput  ModifierTrigger = "recipe.resource_output"
	ModifierTriggerRecipeWorkAmount      ModifierTrigger = "recipe.work_amount"
	ModifierTriggerRecipeBaseProgress    ModifierTrigger = "recipe.base_progress"
	ModifierTriggerUnitAttack            ModifierTrigger = "unit.attack"
	ModifierTriggerUnitMoveRange         ModifierTrigger = "unit.move_range"
	ModifierTriggerUnitSiegeMultiplier   ModifierTrigger = "unit.siege_multiplier"
	ModifierTriggerLogisticsRoadCapacity ModifierTrigger = "logistics.road_capacity"
	ModifierTriggerPointOutput           ModifierTrigger = "point.output"
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
		string(ModifierTriggerLogisticsRoadCapacity),
		string(ModifierTriggerPointOutput),
	}
}

type Prerequisite struct {
	Type     string `json:"type"`
	TargetID string `json:"target_id"`
}

type ExplicitEffect struct {
	Type             string          `json:"type"`
	TargetID         string          `json:"target_id,omitempty"`
	ResourceKey      string          `json:"resource_key,omitempty"`
	PointKey         string          `json:"point_key,omitempty"`
	InstitutionSlots int             `json:"institution_slots,omitempty"`
	GrantResources   ResourceAmounts `json:"grant_resources,omitempty"`
	GrantUnits       []string        `json:"grant_units,omitempty"`
}

type ModifierEffect struct {
	Trigger      string  `json:"trigger"`
	TargetID     string  `json:"target_id,omitempty"`
	ResourceKey  string  `json:"resource_key,omitempty"`
	PointKey     string  `json:"point_key,omitempty"`
	ModifierType string  `json:"modifier_type"`
	Value        float64 `json:"value"`
}

type LogisticsPriorityDefinition struct {
	TargetID string `json:"target_id,omitempty"`
	Tag      string `json:"tag,omitempty"`
	Priority int    `json:"priority"`
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
	ID                string                        `json:"id"`
	Name              string                        `json:"name"`
	Description       string                        `json:"description"`
	IconKey           string                        `json:"icon_key"`
	Layer             string                        `json:"layer"`
	ActivationTiming  string                        `json:"activation_timing"`
	Prerequisites     []Prerequisite                `json:"prerequisites"`
	ExplicitEffects   []ExplicitEffect              `json:"explicit_effects"`
	ModifierEffects   []ModifierEffect              `json:"modifier_effects"`
	LogisticsPriority []LogisticsPriorityDefinition `json:"logistics_priority,omitempty"`
	SortOrder         int                           `json:"sort_order"`
	Tags              []string                      `json:"tags,omitempty"`
}
