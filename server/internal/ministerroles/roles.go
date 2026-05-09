// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-05-09 00:00:00 +0800
// Description: Shared minister role definitions and fallback roster normalization.

package ministerroles

import (
	"strings"

	"github.com/elebirds/panoptes/internal/staticdata"
)

const (
	Domestic = "domestic"
	Works    = "works"
	Defense  = "defense"
	Command  = "command"
	Frontier = "frontier"

	LegacyMilitary = "military"
)

func Canonical(role string) string {
	role = strings.ToLower(strings.TrimSpace(role))
	switch role {
	case LegacyMilitary:
		return Command
	default:
		return role
	}
}

func OrderedRoles() []string {
	return []string{Domestic, Works, Defense, Command, Frontier}
}

func NormalizeMinisters(pool []staticdata.Minister) []staticdata.Minister {
	normalized := make(map[string]staticdata.Minister, len(pool))
	for _, minister := range pool {
		role := Canonical(minister.Role)
		if role == "" {
			continue
		}
		minister.Role = role
		if minister.IconKey == "" {
			minister.IconKey = role
		}
		if _, ok := normalized[role]; ok {
			continue
		}
		normalized[role] = minister
	}

	out := make([]staticdata.Minister, 0, len(OrderedRoles()))
	for _, role := range OrderedRoles() {
		if minister, ok := normalized[role]; ok {
			out = append(out, minister)
			continue
		}
		out = append(out, DefaultMinister(role))
	}
	return out
}

func DefaultMinister(role string) staticdata.Minister {
	switch Canonical(role) {
	case Domestic:
		return staticdata.Minister{
			ID:              "m-domestic",
			Name:            "沈衡",
			Role:            Domestic,
			IconKey:         Domestic,
			Ability:         7,
			Personality:     "steady",
			PersonalityDesc: "稳健审慎",
			Loyalty:         8,
			Ambition:        4,
			Cautiousness:    76,
			Decisiveness:    58,
			LoyaltyTendency: 84,
			AmbitionStyle:   32,
		}
	case Works:
		return staticdata.Minister{
			ID:              "m-works",
			Name:            "许衡",
			Role:            Works,
			IconKey:         Works,
			Ability:         7,
			Personality:     "pragmatic",
			PersonalityDesc: "务实精算",
			Loyalty:         7,
			Ambition:        5,
			Cautiousness:    64,
			Decisiveness:    63,
			LoyaltyTendency: 76,
			AmbitionStyle:   41,
		}
	case Defense:
		return staticdata.Minister{
			ID:              "m-defense",
			Name:            "韩戎",
			Role:            Defense,
			IconKey:         Defense,
			Ability:         8,
			Personality:     "disciplined",
			PersonalityDesc: "严整强硬",
			Loyalty:         8,
			Ambition:        6,
			Cautiousness:    58,
			Decisiveness:    78,
			LoyaltyTendency: 72,
			AmbitionStyle:   54,
		}
	case Command:
		return staticdata.Minister{
			ID:              "m-command",
			Name:            "李猛",
			Role:            Command,
			IconKey:         Command,
			Ability:         8,
			Personality:     "aggressive",
			PersonalityDesc: "果敢激进",
			Loyalty:         7,
			Ambition:        6,
			Cautiousness:    35,
			Decisiveness:    82,
			LoyaltyTendency: 68,
			AmbitionStyle:   65,
		}
	case Frontier:
		return staticdata.Minister{
			ID:              "m-frontier",
			Name:            "林远",
			Role:            Frontier,
			IconKey:         Frontier,
			Ability:         7,
			Personality:     "opportunistic",
			PersonalityDesc: "善于开边",
			Loyalty:         6,
			Ambition:        7,
			Cautiousness:    61,
			Decisiveness:    66,
			LoyaltyTendency: 66,
			AmbitionStyle:   58,
		}
	default:
		return staticdata.Minister{
			ID:              "m-" + Canonical(role),
			Name:            strings.TrimSpace(role),
			Role:            Canonical(role),
			IconKey:         Canonical(role),
			Ability:         6,
			Personality:     "steady",
			PersonalityDesc: "稳健",
			Loyalty:         6,
			Ambition:        5,
			Cautiousness:    60,
			Decisiveness:    60,
			LoyaltyTendency: 70,
			AmbitionStyle:   40,
		}
	}
}
