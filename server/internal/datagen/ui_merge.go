// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: Merges authored UI catalog metadata into generated runtime definitions.

package datagen

import (
	"sort"
	"strings"

	"github.com/elebirds/panoptes/internal/staticdata"
)

func mergeUI(resources []staticdata.ResourceDescriptor, ui staticdata.ResourceCatalogUIFile) {
	uiByID := make(map[string]struct {
		Name        string
		Description string
		IconKey     string
		SortOrder   int
		Tags        []string
	}, len(ui.Resources))
	for _, entry := range ui.Resources {
		uiByID[entry.ID] = struct {
			Name        string
			Description string
			IconKey     string
			SortOrder   int
			Tags        []string
		}{entry.Name, entry.Description, entry.IconKey, entry.SortOrder, entry.Tags}
	}
	for i := range resources {
		if entry, ok := uiByID[resources[i].Key]; ok {
			resources[i].DisplayName = entry.Name
			resources[i].Description = entry.Description
			resources[i].IconKey = entry.IconKey
			resources[i].SortOrder = entry.SortOrder
			resources[i].Tags = append([]string(nil), entry.Tags...)
		}
	}
}

func mergePointUI(points []staticdata.PointDescriptor, ui staticdata.PointCatalogUIFile) {
	uiByID := make(map[string]struct {
		Name        string
		Description string
		IconKey     string
		SortOrder   int
		Tags        []string
	}, len(ui.Points))
	for _, entry := range ui.Points {
		uiByID[entry.ID] = struct {
			Name        string
			Description string
			IconKey     string
			SortOrder   int
			Tags        []string
		}{entry.Name, entry.Description, entry.IconKey, entry.SortOrder, entry.Tags}
	}
	for i := range points {
		if entry, ok := uiByID[points[i].Key]; ok {
			points[i].DisplayName = entry.Name
			points[i].Description = entry.Description
			points[i].IconKey = entry.IconKey
			points[i].SortOrder = entry.SortOrder
			points[i].Tags = append([]string(nil), entry.Tags...)
		}
	}
}

func mergeUnitUI(units []staticdata.UnitDefinition, ui staticdata.UnitCatalogUIFile) {
	uiByID := make(map[string]struct {
		Name        string
		Description string
		IconKey     string
		PrefabKey   string
		SortOrder   int
		Tags        []string
	}, len(ui.Units))
	for _, entry := range ui.Units {
		uiByID[entry.ID] = struct {
			Name        string
			Description string
			IconKey     string
			PrefabKey   string
			SortOrder   int
			Tags        []string
		}{entry.Name, entry.Description, entry.IconKey, entry.PrefabKey, entry.SortOrder, entry.Tags}
	}
	for i := range units {
		if entry, ok := uiByID[units[i].ID]; ok {
			units[i].Name = entry.Name
			units[i].Description = entry.Description
			units[i].IconKey = entry.IconKey
			units[i].PrefabKey = entry.PrefabKey
			units[i].SortOrder = entry.SortOrder
			units[i].Tags = append([]string(nil), entry.Tags...)
		}
	}
}

func mergeBuildingUI(buildings []staticdata.BuildingDefinition, ui staticdata.BuildingCatalogUIFile) {
	uiByID := make(map[string]struct {
		Name        string
		Description string
		IconKey     string
		PrefabKey   string
		SortOrder   int
		Tags        []string
	}, len(ui.Buildings))
	for _, entry := range ui.Buildings {
		uiByID[entry.ID] = struct {
			Name        string
			Description string
			IconKey     string
			PrefabKey   string
			SortOrder   int
			Tags        []string
		}{entry.Name, entry.Description, entry.IconKey, entry.PrefabKey, entry.SortOrder, entry.Tags}
	}
	for i := range buildings {
		if entry, ok := uiByID[buildings[i].ID]; ok {
			buildings[i].Name = entry.Name
			buildings[i].Description = entry.Description
			buildings[i].IconKey = entry.IconKey
			buildings[i].PrefabKey = entry.PrefabKey
			buildings[i].SortOrder = entry.SortOrder
			buildings[i].Tags = append([]string(nil), entry.Tags...)
		}
	}
}

func mergeTechnologyUI(technologies []staticdata.TechnologyDefinition, ui staticdata.TechnologyCatalogUIFile) {
	uiByID := make(map[string]struct {
		Name        string
		Description string
		IconKey     string
		SortOrder   int
		Tags        []string
	}, len(ui.Technologies))
	for _, entry := range ui.Technologies {
		uiByID[entry.ID] = struct {
			Name        string
			Description string
			IconKey     string
			SortOrder   int
			Tags        []string
		}{entry.Name, entry.Description, entry.IconKey, entry.SortOrder, entry.Tags}
	}
	for i := range technologies {
		if entry, ok := uiByID[technologies[i].ID]; ok {
			technologies[i].Name = entry.Name
			technologies[i].Description = entry.Description
			technologies[i].IconKey = entry.IconKey
			technologies[i].SortOrder = entry.SortOrder
			technologies[i].Tags = append([]string(nil), entry.Tags...)
		}
	}
}

func buildBuildMenuLayout(buildings []staticdata.BuildingDefinition) staticdata.BuildMenuLayout {
	type item struct {
		ID        string
		SortOrder int
	}
	items := make([]item, 0, len(buildings))
	for _, building := range buildings {
		if strings.TrimSpace(building.ID) == "" {
			continue
		}
		items = append(items, item{ID: building.ID, SortOrder: building.SortOrder})
	}
	sort.Slice(items, func(i, j int) bool {
		if items[i].SortOrder != items[j].SortOrder {
			return items[i].SortOrder < items[j].SortOrder
		}
		return items[i].ID < items[j].ID
	})
	order := make([]string, 0, len(items))
	for _, item := range items {
		order = append(order, item.ID)
	}
	return staticdata.BuildMenuLayout{
		ConfigVersion:     "2026-04-17",
		BuildingOrder:     order,
		HiddenBuildingIDs: []string{"city_core"},
	}
}

func mergePolicyUI(policies []staticdata.PolicyDefinition, ui staticdata.PolicyCatalogUIFile) {
	uiByID := make(map[string]struct {
		Name        string
		Description string
		IconKey     string
		SortOrder   int
		Tags        []string
	}, len(ui.Policies))
	for _, entry := range ui.Policies {
		uiByID[entry.ID] = struct {
			Name        string
			Description string
			IconKey     string
			SortOrder   int
			Tags        []string
		}{entry.Name, entry.Description, entry.IconKey, entry.SortOrder, entry.Tags}
	}
	for i := range policies {
		if entry, ok := uiByID[policies[i].ID]; ok {
			policies[i].Name = entry.Name
			policies[i].Description = entry.Description
			policies[i].IconKey = entry.IconKey
			policies[i].SortOrder = entry.SortOrder
			policies[i].Tags = append([]string(nil), entry.Tags...)
		}
	}
}

func mergeRecipeUI(recipes []staticdata.RecipeDefinition, ui staticdata.RecipeCatalogUIFile) {
	uiByID := make(map[string]struct {
		Name        string
		Description string
		IconKey     string
		SortOrder   int
		Tags        []string
	}, len(ui.Recipes))
	for _, entry := range ui.Recipes {
		uiByID[entry.ID] = struct {
			Name        string
			Description string
			IconKey     string
			SortOrder   int
			Tags        []string
		}{entry.Name, entry.Description, entry.IconKey, entry.SortOrder, entry.Tags}
	}
	for i := range recipes {
		if entry, ok := uiByID[recipes[i].ID]; ok {
			recipes[i].Name = entry.Name
			recipes[i].Description = entry.Description
			recipes[i].IconKey = entry.IconKey
			recipes[i].SortOrder = entry.SortOrder
			recipes[i].Tags = append([]string(nil), entry.Tags...)
		}
	}
}

func buildRecipeLayout(recipes []staticdata.RecipeDefinition) staticdata.RecipeLayout {
	type item struct {
		ID        string
		SortOrder int
	}
	items := make([]item, 0, len(recipes))
	for _, recipe := range recipes {
		if strings.TrimSpace(recipe.ID) == "" {
			continue
		}
		items = append(items, item{ID: recipe.ID, SortOrder: recipe.SortOrder})
	}
	sort.Slice(items, func(i, j int) bool {
		if items[i].SortOrder != items[j].SortOrder {
			return items[i].SortOrder < items[j].SortOrder
		}
		return items[i].ID < items[j].ID
	})
	order := make([]string, 0, len(items))
	for _, item := range items {
		order = append(order, item.ID)
	}
	return staticdata.RecipeLayout{
		ConfigVersion: "2026-04-17",
		RecipeOrder:   order,
	}
}

func mergeTerrainUI(terrains []staticdata.TerrainDefinition, ui staticdata.TerrainCatalogUIFile) {
	uiByID := make(map[string]struct {
		Name        string
		Description string
		IconKey     string
		MaterialKey string
		SortOrder   int
		Tags        []string
	}, len(ui.Terrains))
	for _, entry := range ui.Terrains {
		uiByID[entry.ID] = struct {
			Name        string
			Description string
			IconKey     string
			MaterialKey string
			SortOrder   int
			Tags        []string
		}{entry.Name, entry.Description, entry.IconKey, entry.MaterialKey, entry.SortOrder, entry.Tags}
	}
	for i := range terrains {
		if entry, ok := uiByID[terrains[i].ID]; ok {
			terrains[i].Name = entry.Name
			terrains[i].Description = entry.Description
			terrains[i].IconKey = entry.IconKey
			terrains[i].MaterialKey = entry.MaterialKey
			terrains[i].SortOrder = entry.SortOrder
			terrains[i].Tags = append([]string(nil), entry.Tags...)
		}
	}
}
