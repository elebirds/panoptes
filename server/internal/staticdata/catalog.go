// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现静态目录模块的目录加载与查询。

package staticdata

import (
	"crypto/sha256"
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
	"sort"
	"sync"
)

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
	maps            map[string]*MapRuntimeBundle
	sectionPayloads map[string][]byte
}

var (
	defaultCatalogMu sync.RWMutex
	defaultCatalog   *Catalog
)

func SetDefault(c *Catalog) {
	defaultCatalogMu.Lock()
	defer defaultCatalogMu.Unlock()
	defaultCatalog = c
}

func Default() *Catalog {
	defaultCatalogMu.RLock()
	defer defaultCatalogMu.RUnlock()
	return defaultCatalog
}

func LoadDir(dir string) (*Catalog, error) {
	raw, err := os.ReadFile(filepath.Join(dir, "catalog.bundle.json"))
	if err != nil {
		return nil, fmt.Errorf("read catalog bundle: %w", err)
	}

	var bundle CatalogBundle
	if err := json.Unmarshal(raw, &bundle); err != nil {
		return nil, fmt.Errorf("unmarshal catalog bundle: %w", err)
	}

	catalog := NewCatalog(bundle)
	for _, entry := range bundle.Maps {
		path := filepath.Join(dir, "maps", entry.ID+".runtime.json")
		mapRaw, err := os.ReadFile(path)
		if err != nil {
			return nil, fmt.Errorf("read map bundle %q: %w", entry.ID, err)
		}
		var runtime MapRuntimeBundle
		if err := json.Unmarshal(mapRaw, &runtime); err != nil {
			return nil, fmt.Errorf("unmarshal map bundle %q: %w", entry.ID, err)
		}
		runtimeCopy := runtime
		catalog.maps[entry.ID] = &runtimeCopy
	}

	return catalog, nil
}

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
		bundle:          bundle,
		resources:       make(map[string]ResourceDescriptor, len(bundle.Resources)),
		points:          make(map[string]PointDescriptor, len(bundle.Points)),
		units:           make(map[string]UnitDefinition, len(bundle.Units)),
		buildings:       make(map[string]BuildingDefinition, len(bundle.Buildings)),
		technologies:    make(map[string]TechnologyDefinition, len(bundle.Technologies)),
		policies:        make(map[string]PolicyDefinition, len(bundle.Policies)),
		recipes:         make(map[string]RecipeDefinition, len(bundle.Recipes)),
		terrains:        make(map[string]TerrainDefinition, len(bundle.Terrains)),
		maps:            make(map[string]*MapRuntimeBundle, len(bundle.Maps)+len(maps)),
		sectionPayloads: sectionPayloads,
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
	for _, recipe := range bundle.Recipes {
		catalog.recipes[recipe.ID] = recipe
	}
	for _, terrain := range bundle.Terrains {
		catalog.terrains[terrain.ID] = terrain
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

func computeCatalogBundleHash(bundle CatalogBundle, maps []*MapRuntimeBundle) (string, error) {
	hash := sha256.New()
	raw, err := json.Marshal(bundle)
	if err != nil {
		return "", err
	}
	_, _ = hash.Write(raw)

	mapByID := make(map[string]*MapRuntimeBundle, len(maps))
	ids := make([]string, 0, len(maps))
	for _, runtime := range maps {
		if runtime == nil || runtime.ID == "" {
			continue
		}
		mapByID[runtime.ID] = runtime
		ids = append(ids, runtime.ID)
	}
	sort.Strings(ids)
	for _, id := range ids {
		raw, err := json.Marshal(mapByID[id])
		if err != nil {
			return "", err
		}
		_, _ = hash.Write(raw)
	}
	return fmt.Sprintf("%x", hash.Sum(nil)), nil
}

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
