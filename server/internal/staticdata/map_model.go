// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载静态数据运行时模型、目录索引或查询逻辑。

package staticdata

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
