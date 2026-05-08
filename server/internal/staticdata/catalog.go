// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现静态目录模块的目录加载与查询。

package staticdata

type Catalog struct {
	bundle          CatalogBundle
	resources       map[string]ResourceDescriptor
	points          map[string]PointDescriptor
	units           map[string]UnitDefinition
	buildings       map[string]BuildingDefinition
	technologies    map[string]TechnologyDefinition
	policies        map[string]PolicyDefinition
	recipes         map[string]RecipeDefinition
	terrains        map[string]TerrainDefinition
	emoteSeries     map[string]EmoteSeriesDefinition
	emotes          map[string]EmoteDefinition
	ministerSkills  map[string]MinisterSkillCard
	maps            map[string]*MapRuntimeBundle
	sectionPayloads map[string][]byte
}
