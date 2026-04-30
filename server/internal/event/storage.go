// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
package event

import (
	"fmt"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/yohamta/donburi"
)

type StorageRaidedEvent struct {
	TargetPlayerID string
	CityID         string
	RaiderID       string
	Resources      domain.ResourceBag
}

func (e StorageRaidedEvent) Apply(_ donburi.World, state *domain.GameState) {
	if state == nil || e.TargetPlayerID == "" || e.CityID == "" || e.Resources == nil || e.Resources.IsZero() {
		return
	}
	state.ConsumeResources(e.TargetPlayerID, e.CityID, e.Resources)
}

func (e StorageRaidedEvent) Kind() string { return "storage_raided" }

func (e StorageRaidedEvent) String() string {
	return fmt.Sprintf("StorageRaidedEvent city=%s target=%s raider=%s", e.CityID, e.TargetPlayerID, e.RaiderID)
}
