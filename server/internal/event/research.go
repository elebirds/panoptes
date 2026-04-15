// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现事件模型的科研结算逻辑。

package event

import (
	"fmt"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

type TechnologyUnlockedEvent struct {
	PlayerID     string
	TechnologyID string
	Cost         int
}

type ResearchTargetChangedEvent struct {
	PlayerID     string
	TechnologyID string
}

func (e ResearchTargetChangedEvent) Apply(_ donburi.World, state *domain.GameState) {
	if state == nil {
		return
	}
	playerState, ok := state.Players[e.PlayerID]
	if !ok || playerState == nil {
		return
	}
	playerState.Research.CurrentTargetTechnologyID = e.TechnologyID
}

func (e ResearchTargetChangedEvent) Kind() string { return "research_target_changed" }

func (e ResearchTargetChangedEvent) String() string {
	return fmt.Sprintf("ResearchTargetChangedEvent player=%s technology=%s", e.PlayerID, e.TechnologyID)
}

func (e TechnologyUnlockedEvent) Apply(_ donburi.World, state *domain.GameState) {
	if state == nil {
		return
	}
	playerState, ok := state.Players[e.PlayerID]
	if !ok || playerState == nil {
		return
	}
	technology, ok := staticdata.Default().GetTechnology(e.TechnologyID)
	if !ok {
		return
	}
	// 这里只落正式状态：扣研究进度、写已解锁集合、同步 unlock 效果。
	// 科技在 settlement 中完成，但这些解锁内容要到下一回合的指令阶段才会被使用。
	playerState.Research.UnlockTechnology(e.TechnologyID)
	if playerState.Research.CurrentTargetTechnologyID == e.TechnologyID {
		playerState.Research.CurrentTargetTechnologyID = ""
	}
	playerState.Research.CurrentProgress -= e.Cost
	if playerState.Research.CurrentProgress < 0 {
		playerState.Research.CurrentProgress = 0
	}
	resolved := domain.ResolveExplicitEffects(technology.ExplicitEffects)
	for _, buildingID := range resolved.UnlockBuildingIDs {
		playerState.Research.UnlockBuilding(buildingID)
	}
	for _, recipeID := range resolved.UnlockRecipeIDs {
		playerState.Research.UnlockRecipe(recipeID)
	}
	if cap := state.EffectiveResearchCap(e.PlayerID); playerState.Research.CurrentProgress > cap {
		playerState.Research.CurrentProgress = cap
	}
}

func (e TechnologyUnlockedEvent) Kind() string { return "technology_completed" }

func (e TechnologyUnlockedEvent) String() string {
	return fmt.Sprintf("TechnologyUnlockedEvent player=%s technology=%s", e.PlayerID, e.TechnologyID)
}

type ResearchProgressAppliedEvent struct {
	PlayerID string
	Amount   int
}

func (e ResearchProgressAppliedEvent) Apply(_ donburi.World, state *domain.GameState) {
	if state == nil {
		return
	}
	playerState, ok := state.Players[e.PlayerID]
	if !ok || playerState == nil {
		return
	}
	playerState.Research.CurrentProgress += e.Amount
	if playerState.Research.CurrentProgress > state.EffectiveResearchCap(e.PlayerID) {
		playerState.Research.CurrentProgress = state.EffectiveResearchCap(e.PlayerID)
	}
}

func (e ResearchProgressAppliedEvent) Kind() string { return "technology_progressed" }

func (e ResearchProgressAppliedEvent) String() string {
	return fmt.Sprintf("ResearchProgressAppliedEvent player=%s amount=%d", e.PlayerID, e.Amount)
}

type TechnologyGrantAppliedEvent struct {
	PlayerID   string
	Resources  domain.ResourceBag
	UnitTypes  []string
	SourceTech string
}

func (e TechnologyGrantAppliedEvent) Apply(world donburi.World, state *domain.GameState) {
	if state == nil {
		return
	}
	state.AddResources(e.PlayerID, e.Resources)
	if len(e.UnitTypes) == 0 {
		return
	}

	// grant 单位优先落在主城市，没有城市时再回退到玩家出生点。
	// 这样后续即便科技 grant 和“主城体系”继续演化，这里的行为仍然稳定可预测。
	spawnPos, ok := resolveGrantSpawnPosition(world, state, e.PlayerID)
	if !ok {
		return
	}
	for _, unitType := range e.UnitTypes {
		ecs.CreateUnit(world, unitType, e.PlayerID, spawnPos)
	}
}

func (e TechnologyGrantAppliedEvent) Kind() string { return "technology_grant_applied" }

func (e TechnologyGrantAppliedEvent) String() string {
	return fmt.Sprintf("TechnologyGrantAppliedEvent player=%s technology=%s", e.PlayerID, e.SourceTech)
}

func resolveGrantSpawnPosition(world donburi.World, state *domain.GameState, playerID string) (domain.Position, bool) {
	if state == nil {
		return domain.Position{}, false
	}
	if city := state.PrimaryCityState(playerID); city != nil && city.CoreNodeID != "" {
		if entry, ok := state.GetNode(city.CoreNodeID); ok {
			pos := ecs.PositionC.Get(entry)
			return domain.Position{X: pos.X, Y: pos.Y}, true
		}
	}
	if state.Map != nil {
		if pos, ok := state.Map.PlayerSpawns[playerID]; ok {
			return pos, true
		}
	}
	return domain.Position{}, false
}
