// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载后端测试/调试场景的构造与布置辅助逻辑。

package scenario

import "github.com/elebirds/panoptes/internal/staticdata"

func baseRules() staticdata.Rules {
	return staticdata.Rules{
		TokensPerTurn:              3,
		BonusTokensPerTurn:         0,
		MaxTurns:                   4,
		CityCoreMaxHP:              100,
		SafeZoneRadius:             3,
		FacilityTakeoverTurns:      2,
		BaseResearchOutputPerTurn:  1,
		BaseIndustryOutputPerTurn:  2,
		MinimumCityDistance:        2,
		InitialCityTerritoryRadius: 1,
		TurnTimeLimitPlanning:      1,
	}
}

func manifest(defaultMapID string) staticdata.Manifest {
	return staticdata.Manifest{
		SchemaVersion:  "2026-04-15",
		ContentVersion: defaultMapID,
		BundleHash:     defaultMapID,
		DefaultLocale:  "zh-CN",
		DefaultMapID:   defaultMapID,
	}
}

func settlerDefinition() staticdata.UnitDefinition {
	return staticdata.UnitDefinition{
		ID:          "settler",
		Class:       "civilian",
		MaxHP:       12,
		Attack:      0,
		AttackRange: 0,
		MoveRange:   2,
		VisionRange: 2,
		TrainCost:   staticdata.ResourceAmounts{},
		Upkeep:      staticdata.ResourceAmounts{},
		Multipliers: map[string]float64{},
		Flags: staticdata.UnitFlags{
			CanCapture: true,
		},
	}
}

func infantryDefinition() staticdata.UnitDefinition {
	return staticdata.UnitDefinition{
		ID:          "infantry",
		Class:       "melee",
		MaxHP:       20,
		Attack:      10,
		AttackRange: 1,
		MoveRange:   2,
		VisionRange: 2,
		TrainCost:   staticdata.ResourceAmounts{},
		Upkeep:      staticdata.ResourceAmounts{},
		Multipliers: map[string]float64{},
		Flags: staticdata.UnitFlags{
			CanCapture:          true,
			CanAttackStructures: true,
		},
	}
}
