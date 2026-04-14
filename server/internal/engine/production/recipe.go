// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现经济结算引擎的配方结算逻辑。

package production

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

type RecipeSystem struct{}

func (s *RecipeSystem) Run(world donburi.World, state *domain.GameState) []event.Event {
	events := make([]event.Event, 0)
	// 先结算“玩家这轮切了什么配方”，再按切换后的运行态推进生产。
	applySelections(world, state, &events)

	ecs.NodesWithBuilding(world).Each(world, func(entry *donburi.Entry) {
		if !entry.HasComponent(ecs.BuildingOperationC) {
			return
		}
		building := ecs.BuildingC.Get(entry)
		operation := ecs.BuildingOperationC.Get(entry)
		if operation.SelectedRecipeID == "" || !state.IsRecipeUnlocked(building.Owner, operation.SelectedRecipeID) {
			return
		}

		recipe, ok := staticdata.Default().GetRecipe(operation.SelectedRecipeID)
		if !ok {
			return
		}

		cost := state.ApplyResourceModifiers(building.Owner, string(staticdata.ModifierTriggerRecipeInput), recipe.ID, toResourceBag(recipe.Cost))
		targetTurns := state.ApplyScalarModifier(building.Owner, string(staticdata.ModifierTriggerRecipeDuration), recipe.ID, "", recipe.DurationTurns) + operation.DelayTurns
		if targetTurns <= 0 {
			targetTurns = 1
		}
		if !state.CanAffordFromCastle(building.Owner, building.CastleID, cost) {
			// 缺料不失败、不回滚进度，只累计延时惩罚。
			// 这样配方会“卡住变慢”，而不是每轮重新开始。
			delayTurns := state.ApplyScalarModifier(building.Owner, string(staticdata.ModifierTriggerRecipeDelayPenalty), recipe.ID, "", recipe.DelayPenalty.Value)
			if delayTurns <= 0 {
				delayTurns = 1
			}
			events = append(events, event.RecipeDelayedEvent{
				NodeID: ecs.NodeC.Get(entry).ID, DelayTurns: delayTurns, RequiredTurns: targetTurns + delayTurns, Reason: "insufficient_resources",
			})
			return
		}

		progress := operation.ProgressTurns + 1
		if progress >= targetTurns {
			// 输入资源在完成时统一扣除，保证“本轮够不够料”和“真正结算扣料”
			// 共享同一份 modifier 后的成本视图。
			nextRequiredTurns := state.ApplyScalarModifier(building.Owner, string(staticdata.ModifierTriggerRecipeDuration), recipe.ID, "", recipe.DurationTurns)
			if nextRequiredTurns <= 0 {
				nextRequiredTurns = 1
			}
			events = append(events, event.RecipeCompletedEvent{
				NodeID:        ecs.NodeC.Get(entry).ID,
				Owner:         building.Owner,
				CastleID:      building.CastleID,
				RequiredTurns: nextRequiredTurns,
				Cost:          cost,
				Resources:     state.ApplyResourceModifiers(building.Owner, string(staticdata.ModifierTriggerRecipeOutput), recipe.ID, toResourceBag(recipe.Outputs.Resources)),
				Units:         append([]string(nil), recipe.Outputs.Units...),
			})
			return
		}

		events = append(events, event.RecipeProgressedEvent{
			NodeID: ecs.NodeC.Get(entry).ID, ProgressTurns: progress, RequiredTurns: targetTurns,
		})
	})

	return events
}

func applySelections(world donburi.World, state *domain.GameState, events *[]event.Event) {
	if state == nil {
		return
	}
	for _, selection := range state.TurnRuntime.Planning.RecipeSelections {
		if !state.IsRecipeUnlocked(selection.PlayerID, selection.RecipeID) {
			continue
		}
		recipe, ok := staticdata.Default().GetRecipe(selection.RecipeID)
		if !ok {
			continue
		}
		entry, ok := state.GetNode(selection.NodeID)
		if !ok || !entry.HasComponent(ecs.BuildingC) {
			continue
		}
		building := ecs.BuildingC.Get(entry)
		if building.Owner != selection.PlayerID {
			continue
		}
		// 切配方时就把当前 requiredTurns 写进运行态，方便客户端直接展示
		// “现在这条配方一共要几回合”，不必自行重算。
		requiredTurns := state.ApplyScalarModifier(building.Owner, string(staticdata.ModifierTriggerRecipeDuration), recipe.ID, "", recipe.DurationTurns)
		if requiredTurns <= 0 {
			requiredTurns = 1
		}
		*events = append(*events, event.RecipeSelectionChangedEvent{
			NodeID: selection.NodeID, RecipeID: selection.RecipeID, RequiredTurns: requiredTurns,
		})
	}
}
