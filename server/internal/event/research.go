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

type TechnologyCompletedEvent struct {
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
	playerState.Research.SetCurrentTarget(e.TechnologyID)
}

func (e ResearchTargetChangedEvent) Kind() string { return "research_target_changed" }

func (e ResearchTargetChangedEvent) String() string {
	return fmt.Sprintf("ResearchTargetChangedEvent player=%s technology=%s", e.PlayerID, e.TechnologyID)
}

func (e TechnologyCompletedEvent) Apply(_ donburi.World, state *domain.GameState) {
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
	// completed 只表示“研究已经达标”，不在这里发放显式解锁效果。
	// 真正的 building/recipe/policy/institution 激活留到下一回合 planning start。
	playerState.Research.SetProgress(e.TechnologyID, technology.ResearchCost)
	playerState.Research.MarkTechnologyCompleted(e.TechnologyID, state.Turn)
	if playerState.Research.CurrentTargetTechnologyID == e.TechnologyID {
		playerState.Research.SetCurrentTarget("")
	}
}

func (e TechnologyCompletedEvent) Kind() string { return "technology_completed" }

func (e TechnologyCompletedEvent) String() string {
	return fmt.Sprintf("TechnologyCompletedEvent player=%s technology=%s", e.PlayerID, e.TechnologyID)
}

type TechnologyActivatedEvent struct {
	PlayerID             string
	TechnologyID         string
	UnlockBuildingIDs    []string
	UnlockRecipeIDs      []string
	UnlockPolicyIDs      []string
	UnlockInstitutionIDs []string
	AddInstitutionSlots  int
}

func (e TechnologyActivatedEvent) Apply(_ donburi.World, state *domain.GameState) {
	if state == nil {
		return
	}
	playerState, ok := state.Players[e.PlayerID]
	if !ok || playerState == nil {
		return
	}
	// activated 才是“显式效果正式生效”的边界。
	// 到这一步才会把 completed technology 提升成 active，并同步解锁所有外显内容。
	playerState.Research.MarkTechnologyActive(e.TechnologyID, state.Turn)
	for _, buildingID := range e.UnlockBuildingIDs {
		playerState.Research.UnlockBuilding(buildingID)
	}
	for _, recipeID := range e.UnlockRecipeIDs {
		playerState.Research.UnlockRecipe(recipeID)
	}
	for _, policyID := range e.UnlockPolicyIDs {
		playerState.Research.UnlockPolicyCandidate(policyID)
	}
	for _, institutionID := range e.UnlockInstitutionIDs {
		playerState.Research.UnlockInstitutionCandidate(institutionID)
		playerState.Institutions.UnlockCandidate(institutionID)
	}
	playerState.Institutions.SlotCount += e.AddInstitutionSlots
	state.RefreshBuildingMaxHPForPlayer(e.PlayerID)
}

func (e TechnologyActivatedEvent) Kind() string { return "technology_activated" }

func (e TechnologyActivatedEvent) String() string {
	return fmt.Sprintf("TechnologyActivatedEvent player=%s technology=%s", e.PlayerID, e.TechnologyID)
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
	technologyID := playerState.Research.CurrentTargetTechnologyID
	if technologyID == "" {
		return
	}
	progress := playerState.Research.ProgressForTechnology(technologyID) + e.Amount
	if progress > state.EffectiveResearchCap(e.PlayerID) {
		progress = state.EffectiveResearchCap(e.PlayerID)
	}
	playerState.Research.SetProgress(technologyID, progress)
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
	// grant 跟着 activation 一起发生，而不是跟着 completion 一起发生。
	// 这样客户端与规则都能稳定理解：本回合结算里看到 completed，下一 planning start 才真正拿到奖励。
	state.AddResources(e.PlayerID, e.Resources)
	if len(e.UnitTypes) == 0 {
		return
	}

	// grant 单位优先落在主城市，没有城市时再回退到玩家出生点。
	// 这样后续即便科技 grant 和“主城体系”继续演化，这里的行为仍然稳定可预测。
	spawnOrigin, ok := resolveGrantSpawnOrigin(world, state, e.PlayerID)
	if !ok {
		return
	}
	for _, unitType := range e.UnitTypes {
		spawnPos, ok := domain.ResolveUnitSpawnPosition(state, spawnOrigin)
		if !ok {
			continue
		}
		ecs.CreateUnit(world, unitType, e.PlayerID, spawnPos)
	}
}

func (e TechnologyGrantAppliedEvent) Kind() string { return "technology_grant_applied" }

func (e TechnologyGrantAppliedEvent) String() string {
	return fmt.Sprintf("TechnologyGrantAppliedEvent player=%s technology=%s", e.PlayerID, e.SourceTech)
}

func resolveGrantSpawnOrigin(world donburi.World, state *domain.GameState, playerID string) (domain.Position, bool) {
	if state == nil {
		return domain.Position{}, false
	}
	if city := state.PrimaryCityState(playerID); city != nil && city.CoreNodeID != "" && domain.IsCityOnline(state, city) {
		if entry, ok := state.GetNode(city.CoreNodeID); ok {
			pos := ecs.PositionC.Get(entry)
			return domain.Position{Q: pos.Q, R: pos.R}, true
		}
	}
	if playerState := state.Players[playerID]; playerState != nil {
		for _, city := range playerState.Cities {
			if city == nil || city.CoreNodeID == "" || !domain.IsCityOnline(state, city) {
				continue
			}
			if entry, ok := state.GetNode(city.CoreNodeID); ok {
				pos := ecs.PositionC.Get(entry)
				return domain.Position{Q: pos.Q, R: pos.R}, true
			}
		}
	}
	if state.Map != nil {
		if pos, ok := state.Map.PlayerSpawns[playerID]; ok {
			return pos, true
		}
	}
	return domain.Position{}, false
}
