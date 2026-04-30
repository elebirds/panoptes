// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载建筑规则、组件装配或建筑相关测试逻辑。

package building

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

// AttachComponents 只负责建筑专属组件装配，不负责“是否允许建造”的规则判断。
// ecs.CreateBuilding 决定实体何时存在；binding、takeover 等建筑生命周期语义由 building 包持有。
func AttachComponents(entry *donburi.Entry, cfg staticdata.BuildingDefinition, cityID string, takeoverTurns int) {
	if entry == nil {
		return
	}
	removeAttachmentComponents(entry)
	SetBinding(entry, cfg.BuildingScope, cityID, cityID)

	if normalizeToken(cfg.TakeoverMode) != "disabled" {
		current := domain.FacilityTakeoverComp{}
		if entry.HasComponent(domain.FacilityTakeoverC) {
			current = *domain.FacilityTakeoverC.Get(entry)
		} else {
			entry.AddComponent(domain.FacilityTakeoverC)
		}
		current.Mode = cfg.TakeoverMode
		current.Required = takeoverTurns
		domain.FacilityTakeoverC.SetValue(entry, current)
	}
}

func AttachDefaultOperation(entry *donburi.Entry, recipeID string, requiredTurns int) {
	if entry == nil || recipeID == "" {
		return
	}
	if !entry.HasComponent(domain.BuildingOperationC) {
		entry.AddComponent(domain.BuildingOperationC)
	}
	domain.BuildingOperationC.SetValue(entry, domain.BuildingOperationComp{
		SelectedRecipeID: recipeID,
		RequiredTurns:    requiredTurns,
	})
}

func removeAttachmentComponents(entry *donburi.Entry) {
	if entry == nil {
		return
	}
	for _, component := range []donburi.IComponentType{
		domain.BuildingBindingC,
		domain.FacilityTakeoverC,
	} {
		if entry.HasComponent(component) {
			entry.RemoveComponent(component)
		}
	}
}
