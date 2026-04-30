// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载领域事件写入逻辑拆分后的事件族或事件辅助逻辑。

package event

import (
	"fmt"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/yohamta/donburi"
)

type RoadBuiltEvent struct {
	FromNode string
	ToNode   string
	Owner    string
	Cost     int
}

func (e RoadBuiltEvent) Apply(world donburi.World, state *domain.GameState) {
	fromEntry, okFrom := findNodeByID(world, state, e.FromNode)
	toEntry, okTo := findNodeByID(world, state, e.ToNode)
	if okFrom {
		n := ecs.NodeC.Get(fromEntry)
		n.HasRoad = true
	}
	if okTo {
		n := ecs.NodeC.Get(toEntry)
		n.HasRoad = true
	}
	if okFrom && okTo {
		fromPos := ecs.PositionC.Get(fromEntry)
		toPos := ecs.PositionC.Get(toEntry)
		current := domain.Position{Q: fromPos.Q, R: fromPos.R}
		target := domain.Position{Q: toPos.Q, R: toPos.R}
		for current != target {
			next, ok := nextRoadStep(world, current, target)
			if !ok {
				break
			}
			current = next
			markRoadAt(world, current)
		}
	}
	state.ConsumeResources(e.Owner, "", domain.ResourceBag{domain.ResourceIndustryOutput: e.Cost})
}

func (e RoadBuiltEvent) Kind() string { return "road_built" }

func (e RoadBuiltEvent) String() string {
	return fmt.Sprintf("RoadBuiltEvent %s->%s owner=%s cost=%d", e.FromNode, e.ToNode, e.Owner, e.Cost)
}

func markRoadAt(world donburi.World, pos domain.Position) {
	entry, ok := domain.GetNodeAt(world, pos)
	if !ok {
		return
	}
	n := ecs.NodeC.Get(entry)
	n.HasRoad = true
}

func nextRoadStep(world donburi.World, current, target domain.Position) (domain.Position, bool) {
	bestDistance := current.DistanceTo(target)
	for _, candidate := range current.Neighbors() {
		if _, ok := domain.GetNodeAt(world, candidate); !ok {
			continue
		}
		distance := candidate.DistanceTo(target)
		if distance < bestDistance {
			return candidate, true
		}
	}
	return domain.Position{}, false
}
