// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载静态数据运行时模型、目录索引或查询逻辑。

package staticdata

import (
	"sort"
)

func (c *Catalog) Manifest() Manifest {
	if c == nil {
		return Manifest{}
	}
	return c.bundle.Manifest
}

func (c *Catalog) BundleHash() string {
	return c.Manifest().BundleHash
}

func (c *Catalog) DefaultMapID() string {
	return c.Manifest().DefaultMapID
}

func (c *Catalog) GetResource(key string) (ResourceDescriptor, bool) {
	if c == nil {
		return ResourceDescriptor{}, false
	}
	resource, ok := c.resources[key]
	return resource, ok
}

func (c *Catalog) GetPoint(key string) (PointDescriptor, bool) {
	if c == nil {
		return PointDescriptor{}, false
	}
	point, ok := c.points[key]
	return point, ok
}

func (c *Catalog) GetUnit(id string) (UnitDefinition, bool) {
	if c == nil {
		return UnitDefinition{}, false
	}
	unit, ok := c.units[id]
	return unit, ok
}

func (c *Catalog) GetBuilding(id string) (BuildingDefinition, bool) {
	if c == nil {
		return BuildingDefinition{}, false
	}
	building, ok := c.buildings[id]
	return building, ok
}

func (c *Catalog) GetTechnology(id string) (TechnologyDefinition, bool) {
	if c == nil {
		return TechnologyDefinition{}, false
	}
	technology, ok := c.technologies[id]
	return technology, ok
}

func (c *Catalog) GetPolicy(id string) (PolicyDefinition, bool) {
	if c == nil {
		return PolicyDefinition{}, false
	}
	policy, ok := c.policies[id]
	return policy, ok
}

func (c *Catalog) GetInstitutionCategory(id string) (InstitutionCategoryDefinition, bool) {
	if c == nil {
		return InstitutionCategoryDefinition{}, false
	}
	category, ok := c.institutionCategories[id]
	return category, ok
}

func (c *Catalog) GetInstitution(id string) (InstitutionDefinition, bool) {
	if c == nil {
		return InstitutionDefinition{}, false
	}
	institution, ok := c.institutions[id]
	return institution, ok
}

func (c *Catalog) GetRecipe(id string) (RecipeDefinition, bool) {
	if c == nil {
		return RecipeDefinition{}, false
	}
	recipe, ok := c.recipes[id]
	return recipe, ok
}

func (c *Catalog) Technologies() []TechnologyDefinition {
	if c == nil {
		return nil
	}
	ids := make([]string, 0, len(c.technologies))
	for id := range c.technologies {
		ids = append(ids, id)
	}
	sort.Strings(ids)
	out := make([]TechnologyDefinition, 0, len(ids))
	for _, id := range ids {
		out = append(out, c.technologies[id])
	}
	return out
}

func (c *Catalog) Policies() []PolicyDefinition {
	if c == nil {
		return nil
	}
	ids := make([]string, 0, len(c.policies))
	for id := range c.policies {
		ids = append(ids, id)
	}
	sort.Strings(ids)
	out := make([]PolicyDefinition, 0, len(ids))
	for _, id := range ids {
		out = append(out, c.policies[id])
	}
	return out
}

func (c *Catalog) InstitutionCategories() []InstitutionCategoryDefinition {
	if c == nil {
		return nil
	}
	ids := make([]string, 0, len(c.institutionCategories))
	for id := range c.institutionCategories {
		ids = append(ids, id)
	}
	sort.Strings(ids)
	out := make([]InstitutionCategoryDefinition, 0, len(ids))
	for _, id := range ids {
		out = append(out, c.institutionCategories[id])
	}
	return out
}

func (c *Catalog) Institutions() []InstitutionDefinition {
	if c == nil {
		return nil
	}
	ids := make([]string, 0, len(c.institutions))
	for id := range c.institutions {
		ids = append(ids, id)
	}
	sort.Strings(ids)
	out := make([]InstitutionDefinition, 0, len(ids))
	for _, id := range ids {
		out = append(out, c.institutions[id])
	}
	return out
}

func (c *Catalog) Buildings() []BuildingDefinition {
	if c == nil {
		return nil
	}
	ids := make([]string, 0, len(c.buildings))
	for id := range c.buildings {
		ids = append(ids, id)
	}
	sort.Strings(ids)
	out := make([]BuildingDefinition, 0, len(ids))
	for _, id := range ids {
		out = append(out, c.buildings[id])
	}
	return out
}

func (c *Catalog) BuildingIDs() []string {
	if c == nil {
		return nil
	}

	ids := make([]string, 0, len(c.buildings))
	for id := range c.buildings {
		if id == "" {
			continue
		}
		ids = append(ids, id)
	}
	sort.Strings(ids)
	return ids
}

func (c *Catalog) Recipes() []RecipeDefinition {
	if c == nil {
		return nil
	}
	ids := make([]string, 0, len(c.recipes))
	for id := range c.recipes {
		ids = append(ids, id)
	}
	sort.Strings(ids)
	out := make([]RecipeDefinition, 0, len(ids))
	for _, id := range ids {
		out = append(out, c.recipes[id])
	}
	return out
}

func (c *Catalog) RecipeIDs() []string {
	if c == nil {
		return nil
	}

	ids := make([]string, 0, len(c.recipes))
	for id := range c.recipes {
		if id == "" {
			continue
		}
		ids = append(ids, id)
	}
	sort.Strings(ids)
	return ids
}

func (c *Catalog) GetTerrain(id string) (TerrainDefinition, bool) {
	if c == nil {
		return TerrainDefinition{}, false
	}
	terrain, ok := c.terrains[id]
	return terrain, ok
}

func (c *Catalog) Rules() Rules {
	if c == nil {
		return Rules{}
	}
	return c.bundle.Rules
}

func (c *Catalog) Ministers() []Minister {
	if c == nil {
		return nil
	}
	out := make([]Minister, len(c.bundle.Ministers))
	copy(out, c.bundle.Ministers)
	return out
}

func (c *Catalog) MinisterSkillCards() []MinisterSkillCard {
	if c == nil {
		return nil
	}
	ids := make([]string, 0, len(c.ministerSkills))
	for id := range c.ministerSkills {
		ids = append(ids, id)
	}
	sort.Strings(ids)
	out := make([]MinisterSkillCard, 0, len(ids))
	for _, id := range ids {
		out = append(out, c.ministerSkills[id])
	}
	return out
}

func (c *Catalog) GetMinisterSkillCard(id string) (MinisterSkillCard, bool) {
	if c == nil {
		return MinisterSkillCard{}, false
	}
	card, ok := c.ministerSkills[id]
	return card, ok
}

func (c *Catalog) GetMap(id string) (*MapRuntimeBundle, bool) {
	if c == nil {
		return nil, false
	}
	m, ok := c.maps[id]
	return m, ok
}

func (c *Catalog) SectionPayload(sectionName string) ([]byte, bool) {
	if c == nil {
		return nil, false
	}
	payload, ok := c.sectionPayloads[sectionName]
	if !ok {
		return nil, false
	}
	out := make([]byte, len(payload))
	copy(out, payload)
	return out, true
}
