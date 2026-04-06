package config

import "sort"

type ResourceKey string

const (
	ResourceOre              ResourceKey = "ore"
	ResourceWood             ResourceKey = "wood"
	ResourceFood             ResourceKey = "food"
	ResourceRefinedOre       ResourceKey = "refined_ore"
	ResourceEngineerMaterial ResourceKey = "engineer_material"
	ResourceBuildPoints      ResourceKey = "build_points"
)

var (
	knownResourceKeys = map[ResourceKey]struct{}{
		ResourceOre:              {},
		ResourceWood:             {},
		ResourceFood:             {},
		ResourceRefinedOre:       {},
		ResourceEngineerMaterial: {},
		ResourceBuildPoints:      {},
	}
	protoVisibleResourceKeys = map[ResourceKey]struct{}{
		ResourceOre:              {},
		ResourceWood:             {},
		ResourceFood:             {},
		ResourceRefinedOre:       {},
		ResourceEngineerMaterial: {},
		ResourceBuildPoints:      {},
	}
)

func IsKnownResourceKey(key ResourceKey) bool {
	_, ok := knownResourceKeys[key]
	return ok
}

func IsProtoVisibleResourceKey(key ResourceKey) bool {
	_, ok := protoVisibleResourceKeys[key]
	return ok
}

func KnownResourceKeys() []ResourceKey {
	keys := make([]ResourceKey, 0, len(knownResourceKeys))
	for key := range knownResourceKeys {
		keys = append(keys, key)
	}
	sort.Slice(keys, func(i, j int) bool { return keys[i] < keys[j] })
	return keys
}
