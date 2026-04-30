// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载静态数据运行时模型、目录索引或查询逻辑。

package staticdata

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
