// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载规则 AI 规划器拆分后的候选、评分与意图生成逻辑。

package ai

import (
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	gameorders "github.com/elebirds/panoptes/internal/game/orders"
	"github.com/elebirds/panoptes/internal/game/planning"
)

func (p *ruleBotPlanner) chooseExpansionIntents() []planning.Intent {
	if p.req.State == nil {
		return nil
	}
	if p.shouldPauseExpansionForMilitaryBuildout() {
		return nil
	}
	candidates := make([]expansionCandidate, 0)
	for _, entry := range p.ownedUnitEntries() {
		stats := ecs.UnitStatsC.Get(entry)
		if stats.Type != domain.UnitTypeSettler {
			continue
		}
		unitID := strings.TrimSpace(stats.ID)
		if unitID == "" {
			continue
		}
		for _, node := range p.observation.VisibleNodes {
			if node == nil || node.GetBuildingTypeId() != "" {
				continue
			}
			nodeEntry, ok := p.req.State.GetNode(node.GetId())
			if !ok || nodeEntry == nil {
				continue
			}
			if canFound, _ := ecs.CanFoundCityAt(p.req.State, nodeEntry); !canFound {
				continue
			}
			score := p.scoreExpansion(stats.ID, node.GetId())
			if score <= 0 {
				continue
			}
			candidates = append(candidates, expansionCandidate{
				key:        unitID + ":" + node.GetId(),
				score:      score,
				unitID:     unitID,
				targetNode: node.GetId(),
			})
		}
	}
	chosen, ok := pickBestExpansionCandidate(p.rng, candidates)
	if !ok {
		return nil
	}
	p.reservedUnit[chosen.unitID] = struct{}{}
	return []planning.Intent{
		planning.IssueUnitOrderIntent{
			UnitID:       chosen.unitID,
			Action:       string(gameorders.ActionSettleCity),
			TargetNodeID: chosen.targetNode,
		},
	}
}
