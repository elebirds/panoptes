package staticdata

import (
	"crypto/sha256"
	"encoding/json"
	"fmt"
	"sort"
)

const (
	SectionResources        = "resources"
	SectionPoints           = "points"
	SectionUnits            = "units"
	SectionBuildings        = "buildings"
	SectionTechnologies     = "technologies"
	SectionPolicies         = "policies"
	SectionInstitutions     = "institutions"
	SectionRecipes          = "recipes"
	SectionTerrains         = "terrains"
	SectionRules            = "rules"
	SectionMinisters        = "ministers"
	SectionMaps             = "maps"
	SectionUITechTreeLayout = "ui_tech_tree_layout"
	SectionUIBuildMenu      = "ui_build_menu_layout"
	SectionUIRecipeLayout   = "ui_recipe_layout"
)

type sectionSpec struct {
	Name  string
	Value any
}

type resourcesSection struct {
	Resources []ResourceDescriptor `json:"resources"`
}

type pointsSection struct {
	Points []PointDescriptor `json:"points"`
}

type unitsSection struct {
	Units []UnitDefinition `json:"units"`
}

type buildingsSection struct {
	Buildings []BuildingDefinition `json:"buildings"`
}

type technologiesSection struct {
	Technologies []TechnologyDefinition `json:"technologies"`
}

type policiesSection struct {
	Policies []PolicyDefinition `json:"policies"`
}

type institutionsSection struct {
	Categories   []InstitutionCategoryDefinition `json:"categories"`
	Institutions []InstitutionDefinition         `json:"institutions"`
}

type recipesSection struct {
	Recipes []RecipeDefinition `json:"recipes"`
}

type terrainsSection struct {
	Terrains []TerrainDefinition `json:"terrains"`
}

type rulesSection struct {
	Rules Rules `json:"rules"`
}

type ministersSection struct {
	Ministers []Minister `json:"ministers"`
}

type mapsSection struct {
	Maps []MapCatalogEntry `json:"maps"`
}

func RequiredCatalogSections() []string {
	return []string{
		SectionResources,
		SectionPoints,
		SectionUnits,
		SectionBuildings,
		SectionTechnologies,
		SectionPolicies,
		SectionInstitutions,
		SectionRecipes,
		SectionTerrains,
		SectionRules,
		SectionMinisters,
		SectionMaps,
		SectionUITechTreeLayout,
		SectionUIBuildMenu,
		SectionUIRecipeLayout,
	}
}

func CatalogSectionValues(bundle CatalogBundle) []struct {
	Name  string
	Value any
} {
	return []struct {
		Name  string
		Value any
	}{
		{Name: SectionResources, Value: resourcesSection{Resources: append([]ResourceDescriptor(nil), bundle.Resources...)}},
		{Name: SectionPoints, Value: pointsSection{Points: append([]PointDescriptor(nil), bundle.Points...)}},
		{Name: SectionUnits, Value: unitsSection{Units: append([]UnitDefinition(nil), bundle.Units...)}},
		{Name: SectionBuildings, Value: buildingsSection{Buildings: append([]BuildingDefinition(nil), bundle.Buildings...)}},
		{Name: SectionTechnologies, Value: technologiesSection{Technologies: append([]TechnologyDefinition(nil), bundle.Technologies...)}},
		{Name: SectionPolicies, Value: policiesSection{Policies: append([]PolicyDefinition(nil), bundle.Policies...)}},
		{Name: SectionInstitutions, Value: institutionsSection{Categories: append([]InstitutionCategoryDefinition(nil), bundle.InstitutionCategories...), Institutions: append([]InstitutionDefinition(nil), bundle.Institutions...)}},
		{Name: SectionRecipes, Value: recipesSection{Recipes: append([]RecipeDefinition(nil), bundle.Recipes...)}},
		{Name: SectionTerrains, Value: terrainsSection{Terrains: append([]TerrainDefinition(nil), bundle.Terrains...)}},
		{Name: SectionRules, Value: rulesSection{Rules: bundle.Rules}},
		{Name: SectionMinisters, Value: ministersSection{Ministers: append([]Minister(nil), bundle.Ministers...)}},
		{Name: SectionMaps, Value: mapsSection{Maps: append([]MapCatalogEntry(nil), bundle.Maps...)}},
		{Name: SectionUITechTreeLayout, Value: bundle.UITechTreeLayout},
		{Name: SectionUIBuildMenu, Value: bundle.UIBuildMenuLayout},
		{Name: SectionUIRecipeLayout, Value: bundle.UIRecipeLayout},
	}
}

func BuildSectionPayloads(bundle CatalogBundle) (map[string][]byte, []CatalogSectionHash, error) {
	specs := CatalogSectionValues(bundle)
	payloads := make(map[string][]byte, len(specs))
	hashes := make([]CatalogSectionHash, 0, len(specs))
	for _, spec := range specs {
		raw, err := json.Marshal(spec.Value)
		if err != nil {
			return nil, nil, fmt.Errorf("marshal section %s: %w", spec.Name, err)
		}
		payloads[spec.Name] = raw
		sum := sha256.Sum256(raw)
		hashes = append(hashes, CatalogSectionHash{
			SectionName: spec.Name,
			Hash:        fmt.Sprintf("%x", sum[:]),
		})
	}
	sort.Slice(hashes, func(i, j int) bool {
		return hashes[i].SectionName < hashes[j].SectionName
	})
	return payloads, hashes, nil
}
