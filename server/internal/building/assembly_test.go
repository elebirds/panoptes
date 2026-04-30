// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载建筑规则、组件装配或建筑相关测试逻辑。

package building

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func TestAttachComponentsOwnsBindingAndTakeoverState(t *testing.T) {
	world := donburi.NewWorld()
	entry := world.Entry(world.Create(domain.BuildingC))
	domain.BuildingC.SetValue(entry, domain.BuildingComp{
		Type:  "farm",
		Owner: "player-1",
	})
	entry.AddComponent(domain.FacilityTakeoverC)
	domain.FacilityTakeoverC.SetValue(entry, domain.FacilityTakeoverComp{
		Mode:               "old_mode",
		Progress:           2,
		Required:           9,
		ControllerPlayerID: "player-2",
	})

	AttachComponents(entry, staticdata.BuildingDefinition{
		BuildingScope: "out_of_city",
		TakeoverMode:  "delayed",
	}, "city-a", 3)

	binding, ok := Binding(entry)
	if !ok {
		t.Fatalf("binding missing")
	}
	if binding.Scope != domain.BuildingScopeOutOfCity || binding.CityID != "city-a" || binding.ServiceCityID != "city-a" {
		t.Fatalf("binding = %#v, want out_of_city/city-a/city-a", binding)
	}
	takeover := domain.FacilityTakeoverC.Get(entry)
	if takeover.Mode != "delayed" || takeover.Required != 3 {
		t.Fatalf("takeover = %#v, want delayed required=3", takeover)
	}
	if takeover.Progress != 0 || takeover.ControllerPlayerID != "" {
		t.Fatalf("takeover progress/controller should reset, got %#v", takeover)
	}
}

func TestAttachComponentsRemovesDisabledTakeoverState(t *testing.T) {
	world := donburi.NewWorld()
	entry := world.Entry(world.Create(domain.BuildingC, domain.FacilityTakeoverC))

	AttachComponents(entry, staticdata.BuildingDefinition{
		BuildingScope: "city_core",
		TakeoverMode:  "disabled",
	}, "city-a", 3)

	if entry.HasComponent(domain.FacilityTakeoverC) {
		t.Fatalf("disabled takeover mode should remove FacilityTakeoverC")
	}
	if got := Scope(entry); got != domain.BuildingScopeCityCore {
		t.Fatalf("scope = %q, want city_core", got)
	}
}

func TestAttachDefaultOperation(t *testing.T) {
	world := donburi.NewWorld()
	entry := world.Entry(world.Create(domain.BuildingC))

	AttachDefaultOperation(entry, "farm_food", 4)

	if !entry.HasComponent(domain.BuildingOperationC) {
		t.Fatalf("operation component missing")
	}
	operation := domain.BuildingOperationC.Get(entry)
	if operation.SelectedRecipeID != "farm_food" || operation.RequiredTurns != 4 {
		t.Fatalf("operation = %#v, want farm_food required=4", operation)
	}
}
