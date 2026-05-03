// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: Recipe completion event helper.

package economy

import (
	"github.com/elebirds/panoptes/internal/building"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

type recipeCompletionEventInput struct {
	entry                   *donburi.Entry
	state                   *domain.GameState
	nodeID                  string
	owner                   string
	recipe                  staticdata.RecipeDefinition
	requiredProgress        int
	resourceCost            domain.ResourceBag
	targetConsumedResources domain.ResourceBag
	targetConsumedPoints    domain.PointBag
	resourceDelta           domain.ResourceBag
	progress                int
	remainder               int
}

func appendRecipeCompleted(events *[]event.Event, input recipeCompletionEventInput) {
	*events = append(*events, event.RecipeProgressedEvent{
		NodeID:            input.nodeID,
		ProgressTurns:     input.progress,
		RequiredTurns:     input.requiredProgress,
		ProgressRemainder: input.remainder,
		ConsumedResources: input.targetConsumedResources,
		ConsumedPoints:    input.targetConsumedPoints,
		ResourceDelta:     input.resourceDelta,
	})
	*events = append(*events, event.RecipeCompletedEvent{
		NodeID:        input.nodeID,
		Owner:         input.owner,
		CityID:        building.ResolveServiceCityID(input.entry),
		RequiredTurns: input.requiredProgress,
		Cost:          input.resourceCost,
		Resources:     input.state.ApplyResourceModifiers(input.owner, string(staticdata.ModifierTriggerRecipeResourceOutput), input.recipe.ID, toResourceBag(input.recipe.Outputs.Resources)),
		Units:         append([]string(nil), input.recipe.Outputs.Units...),
	})
	*events = append(*events, event.BuildingStatusChangedEvent{
		NodeID: input.nodeID,
		Status: "idle",
	})
}
