// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载静态数据运行时模型、目录索引或查询逻辑。

package staticdata

type Manifest struct {
	SchemaVersion    string               `json:"schema_version"`
	ContentVersion   string               `json:"content_version"`
	DefaultLocale    string               `json:"default_locale"`
	DefaultMapID     string               `json:"default_map_id"`
	BundleHash       string               `json:"bundle_hash"`
	RequiredSections []string             `json:"required_sections,omitempty"`
	SectionHashes    []CatalogSectionHash `json:"section_hashes,omitempty"`
}

type CatalogSectionHash struct {
	SectionName string `json:"section_name"`
	Hash        string `json:"hash"`
}

type CatalogBundle struct {
	Manifest              Manifest                        `json:"manifest"`
	Resources             []ResourceDescriptor            `json:"resources"`
	Points                []PointDescriptor               `json:"points"`
	Units                 []UnitDefinition                `json:"units"`
	Buildings             []BuildingDefinition            `json:"buildings"`
	Technologies          []TechnologyDefinition          `json:"technologies"`
	Policies              []PolicyDefinition              `json:"policies"`
	InstitutionCategories []InstitutionCategoryDefinition `json:"institution_categories"`
	Institutions          []InstitutionDefinition         `json:"institutions"`
	Recipes               []RecipeDefinition              `json:"recipes"`
	Terrains              []TerrainDefinition             `json:"terrains"`
	Rules                 Rules                           `json:"rules"`
	Ministers             []Minister                      `json:"ministers"`
	MinisterSkillCards    []MinisterSkillCard             `json:"minister_skill_cards"`
	Maps                  []MapCatalogEntry               `json:"maps"`
	UITechTreeLayout      TechnologyTreeLayout            `json:"ui_tech_tree_layout"`
	UIBuildMenuLayout     BuildMenuLayout                 `json:"ui_build_menu_layout"`
	UIRecipeLayout        RecipeLayout                    `json:"ui_recipe_layout"`
}
