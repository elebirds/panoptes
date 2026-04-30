// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载静态数据运行时模型、目录索引或查询逻辑。

package staticdata

type ResourceAmounts map[string]int

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
