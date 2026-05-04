// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载静态数据运行时模型、目录索引或查询逻辑。

package staticdata

type TechnologyTreeLayoutPoint struct {
	X float64 `json:"x"`
	Y float64 `json:"y"`
}

type TechnologyTreeLayoutNode struct {
	ID           string  `json:"id"`
	TechnologyID string  `json:"technology_id,omitempty"`
	Title        string  `json:"title,omitempty"`
	Description  string  `json:"description,omitempty"`
	X            float64 `json:"x"`
	Y            float64 `json:"y"`
	Width        float64 `json:"width"`
	Height       float64 `json:"height"`
	Visible      bool    `json:"visible"`
}

type TechnologyTreeLayoutEdge struct {
	ID        string                      `json:"id"`
	From      string                      `json:"from"`
	To        string                      `json:"to"`
	ShowArrow bool                        `json:"show_arrow"`
	Arrow     string                      `json:"arrow,omitempty"`
	Thickness float64                     `json:"thickness"`
	Points    []TechnologyTreeLayoutPoint `json:"points"`
}

type TechnologyTreeLayout struct {
	ConfigVersion string                     `json:"config_version"`
	Nodes         []TechnologyTreeLayoutNode `json:"nodes"`
	Edges         []TechnologyTreeLayoutEdge `json:"edges"`
}

type BuildMenuLayout struct {
	ConfigVersion     string   `json:"config_version"`
	BuildingOrder     []string `json:"building_order"`
	HiddenBuildingIDs []string `json:"hidden_building_ids,omitempty"`
}

type RecipeLayout struct {
	ConfigVersion string   `json:"config_version"`
	RecipeOrder   []string `json:"recipe_order"`
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

type TechnologyTreeLayoutFile = TechnologyTreeLayout
