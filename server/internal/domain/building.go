package domain

import "strings"

const (
	BuildingScopeCityCore  = "city_core"
	BuildingScopeInCity    = "in_city"
	BuildingScopeOutOfCity = "out_of_city"
)

func NormalizeBuildingScope(scope string) string {
	switch strings.ToLower(strings.TrimSpace(scope)) {
	case BuildingScopeCityCore:
		return BuildingScopeCityCore
	case BuildingScopeOutOfCity:
		return BuildingScopeOutOfCity
	default:
		return BuildingScopeInCity
	}
}
