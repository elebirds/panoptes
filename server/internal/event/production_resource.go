// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载领域事件写入逻辑拆分后的事件族或事件辅助逻辑。

package event

import (
	"fmt"
	"strconv"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/yohamta/donburi"
)

type ResourceProducedEvent struct {
	NodeID       string
	ResourceType string
	Amount       int
	Owner        string
	CityID       string
}

func (e ResourceProducedEvent) Apply(_ donburi.World, state *domain.GameState) {
	state.AddResource(e.Owner, domain.ResourceKey(e.ResourceType), e.Amount)
}

func (e ResourceProducedEvent) Kind() string { return "resource_produced" }

func (e ResourceProducedEvent) String() string {
	return fmt.Sprintf("ResourceProducedEvent node=%s owner=%s city=%s %s=+%d", e.NodeID, e.Owner, e.CityID, e.ResourceType, e.Amount)
}

type ResourceFlowedEvent struct {
	FromNodeID string
	ToNodeID   string
	Resources  domain.ResourceBag
}

func (e ResourceFlowedEvent) Apply(donburi.World, *domain.GameState) {}

func (e ResourceFlowedEvent) Kind() string { return "resource_flowed" }

func (e ResourceFlowedEvent) String() string {
	return fmt.Sprintf("ResourceFlowedEvent from=%s to=%s", e.FromNodeID, e.ToNodeID)
}

func formatResourceBagData(bag domain.ResourceBag) map[string]string {
	out := make(map[string]string, len(bag))
	for _, key := range bag.Keys() {
		out[string(key)] = strconv.Itoa(bag.Get(key))
	}
	return out
}
