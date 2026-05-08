// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现数据生成模块的数据生成主流程。

package datagen

import (
	"fmt"

	"github.com/elebirds/panoptes/internal/staticdata"
)

type Options struct {
	RepoRoot string
}

func Validate(opts Options) error {
	_, _, _, err := loadAndCompile(opts)
	return err
}

func Generate(opts Options) error {
	bundle, maps, schemas, err := loadAndCompile(opts)
	if err != nil {
		return err
	}
	return emitGeneratedFiles(opts.RepoRoot, bundle, maps, schemas)
}

func loadAndCompile(opts Options) (staticdata.CatalogBundle, map[string]*staticdata.MapRuntimeBundle, schemaSet, error) {
	if opts.RepoRoot == "" {
		return staticdata.CatalogBundle{}, nil, nil, fmt.Errorf("repo root is required")
	}

	authored, err := loadAuthoredData(opts.RepoRoot)
	if err != nil {
		return staticdata.CatalogBundle{}, nil, nil, err
	}
	schemas := buildAuthoringSchemas(buildAuthoringSchemaContext(
		authored.Resources.Value.Resources,
		authored.Points.Value.Points,
		authored.Units.Value.Units,
		authored.Buildings.Value.Buildings,
		authored.Technologies.Value.Technologies,
		authored.Policies.Value.Policies,
		authored.Recipes.Value.Recipes,
		authored.Terrains.Value.Terrains,
	))
	if err := validateAuthoredSources(authored, schemas); err != nil {
		return staticdata.CatalogBundle{}, nil, nil, err
	}

	mergeUI(authored.Resources.Value.Resources, authored.ResourceUI.Value)
	mergePointUI(authored.Points.Value.Points, authored.PointUI.Value)
	mergeUnitUI(authored.Units.Value.Units, authored.UnitUI.Value)
	mergeBuildingUI(authored.Buildings.Value.Buildings, authored.BuildingUI.Value)
	mergeTechnologyUI(authored.Technologies.Value.Technologies, authored.TechnologyUI.Value)
	mergePolicyUI(authored.Policies.Value.Policies, authored.PolicyUI.Value)
	mergeRecipeUI(authored.Recipes.Value.Recipes, authored.RecipeUI.Value)
	mergeTerrainUI(authored.Terrains.Value.Terrains, authored.TerrainUI.Value)

	maps, entries, err := compileMaps(opts.RepoRoot)
	if err != nil {
		return staticdata.CatalogBundle{}, nil, nil, err
	}

	bundle := staticdata.CatalogBundle{
		Manifest:           authored.Manifest.Value,
		Resources:          authored.Resources.Value.Resources,
		Points:             authored.Points.Value.Points,
		Units:              authored.Units.Value.Units,
		Buildings:          authored.Buildings.Value.Buildings,
		Technologies:       authored.Technologies.Value.Technologies,
		Policies:           authored.Policies.Value.Policies,
		Recipes:            authored.Recipes.Value.Recipes,
		Terrains:           authored.Terrains.Value.Terrains,
		EmoteSeries:        authored.EmoteUI.Value.Series,
		Emotes:             authored.EmoteUI.Value.Emotes,
		Rules:              authored.Rules.Value,
		Ministers:          authored.Ministers.Value.Pool,
		MinisterSkillCards: authored.MinisterSkillCards.Value.MinisterSkillCards,
		Maps:               entries,
		UITechTreeLayout:   authored.TechnologyTreeUI.Value,
		UIBuildMenuLayout:  buildBuildMenuLayout(authored.Buildings.Value.Buildings),
		UIRecipeLayout:     buildRecipeLayout(authored.Recipes.Value.Recipes),
	}

	sectionPayloads, sectionHashes, err := staticdata.BuildSectionPayloads(bundle)
	if err != nil {
		return staticdata.CatalogBundle{}, nil, nil, err
	}
	_ = sectionPayloads
	bundle.Manifest.RequiredSections = staticdata.RequiredCatalogSections()
	bundle.Manifest.SectionHashes = sectionHashes

	hash, err := computeBundleHash(bundle, maps)
	if err != nil {
		return staticdata.CatalogBundle{}, nil, nil, err
	}
	bundle.Manifest.BundleHash = hash

	return bundle, maps, schemas, nil
}
