// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载静态数据运行时模型、目录索引或查询逻辑。

package staticdata

import (
	"fmt"
)

func NewCatalog(bundle CatalogBundle, maps ...*MapRuntimeBundle) *Catalog {
	sectionPayloads, sectionHashes, err := BuildSectionPayloads(bundle)
	if err != nil {
		panic(fmt.Errorf("build section payloads: %w", err))
	}
	if len(bundle.Manifest.RequiredSections) == 0 {
		bundle.Manifest.RequiredSections = RequiredCatalogSections()
	}
	if len(bundle.Manifest.SectionHashes) == 0 {
		bundle.Manifest.SectionHashes = sectionHashes
	}
	if bundle.Manifest.BundleHash == "" {
		hash, err := computeCatalogBundleHash(bundle, maps)
		if err != nil {
			panic(fmt.Errorf("compute bundle hash: %w", err))
		}
		bundle.Manifest.BundleHash = hash
	}

	catalog := &Catalog{
		bundle:                bundle,
		resources:             make(map[string]ResourceDescriptor, len(bundle.Resources)),
		points:                make(map[string]PointDescriptor, len(bundle.Points)),
		units:                 make(map[string]UnitDefinition, len(bundle.Units)),
		buildings:             make(map[string]BuildingDefinition, len(bundle.Buildings)),
		technologies:          make(map[string]TechnologyDefinition, len(bundle.Technologies)),
		policies:              make(map[string]PolicyDefinition, len(bundle.Policies)),
		institutionCategories: make(map[string]InstitutionCategoryDefinition, len(bundle.InstitutionCategories)),
		institutions:          make(map[string]InstitutionDefinition, len(bundle.Institutions)),
		recipes:               make(map[string]RecipeDefinition, len(bundle.Recipes)),
		terrains:              make(map[string]TerrainDefinition, len(bundle.Terrains)),
		ministerSkills:        make(map[string]MinisterSkillCard, len(bundle.MinisterSkillCards)),
		maps:                  make(map[string]*MapRuntimeBundle, len(bundle.Maps)+len(maps)),
		sectionPayloads:       sectionPayloads,
	}

	for _, resource := range bundle.Resources {
		catalog.resources[resource.Key] = resource
	}
	for _, point := range bundle.Points {
		catalog.points[point.Key] = point
	}
	for _, unit := range bundle.Units {
		catalog.units[unit.ID] = unit
	}
	for _, building := range bundle.Buildings {
		catalog.buildings[building.ID] = building
	}
	for _, technology := range bundle.Technologies {
		catalog.technologies[technology.ID] = technology
	}
	for _, policy := range bundle.Policies {
		catalog.policies[policy.ID] = policy
	}
	for _, category := range bundle.InstitutionCategories {
		catalog.institutionCategories[category.ID] = category
	}
	for _, institution := range bundle.Institutions {
		catalog.institutions[institution.ID] = institution
	}
	for _, recipe := range bundle.Recipes {
		catalog.recipes[recipe.ID] = recipe
	}
	for _, terrain := range bundle.Terrains {
		catalog.terrains[terrain.ID] = terrain
	}
	for _, skill := range bundle.MinisterSkillCards {
		catalog.ministerSkills[skill.ID] = skill
	}
	for _, runtime := range maps {
		if runtime == nil {
			continue
		}
		runtimeCopy := *runtime
		catalog.maps[runtime.ID] = &runtimeCopy
	}

	return catalog
}
