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
	gamequery "github.com/elebirds/panoptes/internal/game/query"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/yohamta/donburi"
)

func (p *ruleBotPlanner) chooseCombatIntents() []planning.Intent {
	if p.req.State == nil {
		return nil
	}
	intents := make([]planning.Intent, 0)
	visibleEnemies := p.visibleEnemyUnits()
	memoryEnemies := p.memoryEnemyUnits()
	enemyStructures := p.enemyStructureNodes()

	for _, entry := range p.ownedUnitEntries() {
		stats := ecs.UnitStatsC.Get(entry)
		unitID := strings.TrimSpace(stats.ID)
		if unitID == "" || stats.Type == domain.UnitTypeSettler {
			continue
		}
		if _, reserved := p.reservedUnit[unitID]; reserved {
			continue
		}
		candidate := p.chooseCombatIntentForUnit(entry, visibleEnemies, memoryEnemies, enemyStructures)
		intents = append(intents, candidate.intent)
	}
	return intents
}

func (p *ruleBotPlanner) chooseCombatIntentForUnit(entry *donburi.Entry, visibleEnemies []*pb.UnitView, memoryEnemies []*gamequery.RememberedUnitView, enemyStructures []*pb.NodeView) combatCandidate {
	stats := ecs.UnitStatsC.Get(entry)
	pos := ecs.PositionC.Get(entry)
	unitPos := domain.Position{Q: pos.Q, R: pos.R}
	unitID := strings.TrimSpace(stats.ID)

	bestAttack := combatCandidate{score: intMin, intent: planning.IssueUnitOrderIntent{UnitID: unitID, Action: string(gameorders.ActionHold)}}
	for _, enemy := range visibleEnemies {
		if enemy == nil || enemy.GetPos() == nil {
			continue
		}
		targetPos := protoPosition(enemy.GetPos())
		distance := unitPos.DistanceTo(targetPos)
		if distance > stats.AttackRange {
			continue
		}
		score := 100
		if int(enemy.GetHp()) <= stats.Attack {
			score += 80
		}
		if isCivilianUnit(enemy.GetUnitType()) {
			score += 40
		}
		if score > bestAttack.score {
			bestAttack = combatCandidate{
				key:   unitID + ":attack:" + enemy.GetId(),
				score: score,
				intent: planning.IssueUnitOrderIntent{
					UnitID:       unitID,
					Action:       string(gameorders.ActionAttack),
					TargetUnitID: enemy.GetId(),
				},
			}
		}
	}
	if bestAttack.score > intMin {
		return bestAttack
	}

	if entry.HasComponent(ecs.UnitCapabilitiesC) && ecs.UnitCapabilitiesC.Get(entry).CanAttackStructures {
		for _, node := range enemyStructures {
			if node == nil || node.GetPos() == nil {
				continue
			}
			targetPos := protoPosition(node.GetPos())
			if unitPos.DistanceTo(targetPos) > stats.AttackRange {
				continue
			}
			score := 70
			if node.GetIsCityCore() {
				score += 40
			}
			return combatCandidate{
				key:   unitID + ":attack_node:" + node.GetId(),
				score: score,
				intent: planning.IssueUnitOrderIntent{
					UnitID:       unitID,
					Action:       string(gameorders.ActionAttack),
					TargetNodeID: node.GetId(),
				},
			}
		}
	}

	targetNodeID := p.closestEnemyTargetNode(unitPos, visibleEnemies, memoryEnemies, enemyStructures)
	if targetNodeID != "" {
		return combatCandidate{
			key:   unitID + ":move:" + targetNodeID,
			score: 50,
			intent: planning.IssueUnitOrderIntent{
				UnitID:       unitID,
				Action:       string(gameorders.ActionMove),
				TargetNodeID: targetNodeID,
			},
		}
	}

	if targetNodeID := p.closestExplorationTargetNode(unitID, unitPos); targetNodeID != "" {
		return combatCandidate{
			key:   unitID + ":explore:" + targetNodeID,
			score: 20,
			intent: planning.IssueUnitOrderIntent{
				UnitID:       unitID,
				Action:       string(gameorders.ActionMove),
				TargetNodeID: targetNodeID,
			},
		}
	}

	if targetNodeID := p.closestPressureTargetNode(unitPos); targetNodeID != "" {
		return combatCandidate{
			key:   unitID + ":pressure:" + targetNodeID,
			score: 10,
			intent: planning.IssueUnitOrderIntent{
				UnitID:       unitID,
				Action:       string(gameorders.ActionMove),
				TargetNodeID: targetNodeID,
			},
		}
	}

	return combatCandidate{
		key:   unitID + ":hold",
		score: 1,
		intent: planning.IssueUnitOrderIntent{
			UnitID: unitID,
			Action: string(gameorders.ActionHold),
		},
	}
}
