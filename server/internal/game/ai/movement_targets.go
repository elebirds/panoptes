// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-05-08 00:00:00 +0800
// Description: Scores rule-bot movement targets for exploration and front-line pressure.

package ai

import (
	"sort"
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	gamequery "github.com/elebirds/panoptes/internal/game/query"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
)

type pressureAnchor struct {
	pos    domain.Position
	weight int
}

// Movement target scoring borrows three common strategy-AI ideas:
// influence maps turn enemies, remembered sightings, and hostile structures into
// spatial pressure; frontier scouting prefers the border between known and
// unknown space; siege-slot distribution reserves nearby targets so a group
// forms a loose line around pressure instead of marching in a single column.
func buildNodeViewIndex(observation *gamequery.ObservationSnapshot) map[string]*pb.NodeView {
	index := make(map[string]*pb.NodeView)
	if observation == nil {
		return index
	}
	for _, node := range observation.Nodes {
		if node == nil {
			continue
		}
		nodeID := strings.TrimSpace(node.GetId())
		if nodeID == "" {
			continue
		}
		index[nodeID] = node
	}
	return index
}

func (p *ruleBotPlanner) choosePressureTargetNode(unitID string, unitPos domain.Position, visibleEnemies []*pb.UnitView, memoryEnemies []*gamequery.RememberedUnitView, enemyStructures []*pb.NodeView) string {
	anchors := p.pressureAnchors(visibleEnemies, memoryEnemies, enemyStructures)
	if len(anchors) == 0 {
		return ""
	}

	candidates := make([]movementCandidate, 0)
	for _, nodeID := range p.sortedNodeIDs() {
		if !p.canUseMovementTarget(nodeID, unitPos) {
			continue
		}
		score, ok := p.scorePressureTarget(nodeID, unitPos, anchors)
		if !ok || score <= 0 {
			continue
		}
		candidates = append(candidates, movementCandidate{
			key:    unitID + ":pressure:" + nodeID,
			score:  score,
			nodeID: nodeID,
		})
	}
	chosen, ok := pickBestMovementCandidate(p.rng, candidates)
	if !ok {
		return ""
	}
	return chosen.nodeID
}

func (p *ruleBotPlanner) chooseExplorationTargetNode(unitID string, unitPos domain.Position) string {
	if p.req.State == nil || p.observation == nil || unitID == "" {
		return ""
	}

	candidates := make([]movementCandidate, 0)
	for _, nodeID := range p.sortedNodeIDs() {
		if !p.canUseMovementTarget(nodeID, unitPos) {
			continue
		}
		score, ok := p.scoreExplorationTarget(nodeID, unitPos)
		if !ok || score <= 0 {
			continue
		}
		candidates = append(candidates, movementCandidate{
			key:    unitID + ":explore:" + nodeID,
			score:  score,
			nodeID: nodeID,
		})
	}
	chosen, ok := pickBestMovementCandidate(p.rng, candidates)
	if !ok {
		return ""
	}
	return chosen.nodeID
}

func (p *ruleBotPlanner) pressureAnchors(visibleEnemies []*pb.UnitView, memoryEnemies []*gamequery.RememberedUnitView, enemyStructures []*pb.NodeView) []pressureAnchor {
	anchors := make([]pressureAnchor, 0, len(visibleEnemies)+len(memoryEnemies)+len(enemyStructures))
	for _, enemy := range visibleEnemies {
		if enemy == nil || enemy.GetPos() == nil {
			continue
		}
		anchors = append(anchors, pressureAnchor{pos: protoPosition(enemy.GetPos()), weight: 8})
	}
	for _, enemy := range memoryEnemies {
		if enemy == nil || enemy.View == nil || enemy.View.GetPos() == nil {
			continue
		}
		anchors = append(anchors, pressureAnchor{pos: protoPosition(enemy.View.GetPos()), weight: 5})
	}
	for _, node := range enemyStructures {
		if node == nil || node.GetPos() == nil {
			continue
		}
		anchors = append(anchors, pressureAnchor{pos: protoPosition(node.GetPos()), weight: 10})
	}
	for _, node := range p.observation.Nodes {
		if node == nil || node.GetPos() == nil {
			continue
		}
		if ownerKnownAgainstPlayer(node, p.playerID) {
			anchors = append(anchors, pressureAnchor{pos: protoPosition(node.GetPos()), weight: 4})
		}
	}
	return anchors
}

func (p *ruleBotPlanner) scorePressureTarget(nodeID string, unitPos domain.Position, anchors []pressureAnchor) (int, bool) {
	targetPos, ok := p.nodePosition(nodeID)
	if !ok {
		return 0, false
	}
	bestPressure := intMin
	for _, anchor := range anchors {
		distance := targetPos.DistanceTo(anchor.pos)
		if distance > 4 {
			continue
		}
		score := anchor.weight*35 - distance*25
		if distance == 0 {
			score += 25
		} else if distance == 1 {
			score += 45
		}
		if score > bestPressure {
			bestPressure = score
		}
	}
	if bestPressure == intMin {
		return 0, false
	}

	score := bestPressure
	score += pressureOwnershipBonus(p.nodeViews[nodeID], p.playerID)
	score += p.unknownNeighborCount(nodeID) * 6
	score -= unitPos.DistanceTo(targetPos) * 5
	score -= p.friendlyCrowdingPenalty(targetPos)
	score -= p.reservedTargetPenalty(targetPos)
	score += p.rng.Intn(6)
	return score, score > 0
}

func (p *ruleBotPlanner) scoreExplorationTarget(nodeID string, unitPos domain.Position) (int, bool) {
	targetView := p.nodeViews[nodeID]
	if targetView == nil {
		return 0, false
	}
	unknownNeighbors := p.unknownNeighborCount(nodeID)
	visibleNeighbors := p.visibleNeighborCount(nodeID)
	if targetView.GetIsCurrentlyVisible() && unknownNeighbors == 0 {
		return 0, false
	}
	if !targetView.GetIsCurrentlyVisible() && visibleNeighbors == 0 && !targetView.GetIsMemory() {
		return 0, false
	}

	targetPos, ok := p.nodePosition(nodeID)
	if !ok {
		return 0, false
	}
	score := 45 + unknownNeighbors*28 + visibleNeighbors*8
	if !targetView.GetIsCurrentlyVisible() {
		score += 36
	} else {
		score -= 18
	}
	if targetView.GetIsMemory() {
		score += 12
	}
	if cityDistance := p.closestCityDistance(nodeID); cityDistance > 1 && cityDistance < intMax {
		score += min(cityDistance, 6) * 4
	}
	score -= unitPos.DistanceTo(targetPos) * 6
	score -= p.friendlyCrowdingPenalty(targetPos)
	score -= p.reservedTargetPenalty(targetPos)
	score += p.rng.Intn(10)
	return score, score > 0
}

func (p *ruleBotPlanner) canUseMovementTarget(nodeID string, unitPos domain.Position) bool {
	targetPos, ok := p.nodePosition(nodeID)
	if !ok || targetPos == unitPos {
		return false
	}
	entry, ok := p.req.State.GetNode(nodeID)
	if !ok || entry == nil || entry.HasComponent(ecs.BuildingC) {
		return false
	}
	if len(domain.GetUnitsAtNode(p.req.State.World, nodeID)) > 0 {
		return false
	}
	node := ecs.NodeC.Get(entry)
	terrain, ok := staticdata.Default().GetTerrain(string(node.Terrain))
	if !ok {
		return false
	}
	if node.HasRoad && terrain.PassableWithRoad {
		return true
	}
	return terrain.Passable
}

func (p *ruleBotPlanner) sortedNodeIDs() []string {
	if p.req.State == nil {
		return nil
	}
	nodeIDs := make([]string, 0, len(p.req.State.NodeIndex))
	for nodeID := range p.req.State.NodeIndex {
		nodeID = strings.TrimSpace(nodeID)
		if nodeID != "" {
			nodeIDs = append(nodeIDs, nodeID)
		}
	}
	sort.Strings(nodeIDs)
	return nodeIDs
}

func (p *ruleBotPlanner) nodePosition(nodeID string) (domain.Position, bool) {
	if p.req.State == nil {
		return domain.Position{}, false
	}
	entry, ok := p.req.State.GetNode(nodeID)
	if !ok || entry == nil {
		return domain.Position{}, false
	}
	pos := ecs.PositionC.Get(entry)
	return domain.Position{Q: pos.Q, R: pos.R}, true
}

func (p *ruleBotPlanner) unknownNeighborCount(nodeID string) int {
	return p.countNeighborViews(nodeID, func(view *pb.NodeView) bool {
		return view == nil || (!view.GetIsCurrentlyVisible() && !view.GetIsMemory())
	})
}

func (p *ruleBotPlanner) visibleNeighborCount(nodeID string) int {
	return p.countNeighborViews(nodeID, func(view *pb.NodeView) bool {
		return view != nil && view.GetIsCurrentlyVisible()
	})
}

func (p *ruleBotPlanner) countNeighborViews(nodeID string, match func(*pb.NodeView) bool) int {
	pos, ok := p.nodePosition(nodeID)
	if !ok {
		return 0
	}
	count := 0
	for _, neighborPos := range pos.Neighbors() {
		neighborID := p.nodeIDAtPosition(neighborPos)
		if neighborID == "" {
			continue
		}
		if match(p.nodeViews[neighborID]) {
			count++
		}
	}
	return count
}

func (p *ruleBotPlanner) friendlyCrowdingPenalty(targetPos domain.Position) int {
	if p.req.State == nil || p.req.State.World == nil {
		return 0
	}
	penalty := 0
	for _, unit := range domain.GetUnitsByFaction(p.req.State.World, p.playerID) {
		if unit == nil {
			continue
		}
		pos := domain.GetPosition(unit)
		switch distance := pos.DistanceTo(targetPos); {
		case distance == 0:
			penalty += 120
		case distance == 1:
			penalty += 30
		case distance == 2:
			penalty += 8
		}
	}
	return penalty
}

func (p *ruleBotPlanner) reservedTargetPenalty(targetPos domain.Position) int {
	penalty := 0
	for nodeID := range p.reservedNode {
		pos, ok := p.nodePosition(nodeID)
		if !ok {
			continue
		}
		switch distance := pos.DistanceTo(targetPos); {
		case distance == 0:
			penalty += 1000
		case distance == 1:
			penalty += 35
		}
	}
	return penalty
}

func (p *ruleBotPlanner) reserveMovementTarget(nodeID string) {
	nodeID = strings.TrimSpace(nodeID)
	if nodeID == "" {
		return
	}
	p.reservedNode[nodeID] = struct{}{}
}

func ownerKnownAgainstPlayer(node *pb.NodeView, playerID string) bool {
	if node == nil {
		return false
	}
	if owner := strings.TrimSpace(node.GetControllerPlayerId()); owner != "" && owner != playerID {
		return true
	}
	if owner := strings.TrimSpace(node.GetTerritoryOwnerPlayerId()); owner != "" && owner != playerID {
		return true
	}
	return false
}

func pressureOwnershipBonus(node *pb.NodeView, playerID string) int {
	if node == nil {
		return 0
	}
	switch {
	case node.GetControllerPlayerId() != "" && node.GetControllerPlayerId() != playerID:
		return 45
	case node.GetTerritoryOwnerPlayerId() != "" && node.GetTerritoryOwnerPlayerId() != playerID:
		return 30
	case node.GetControllerPlayerId() == "" && node.GetTerritoryOwnerPlayerId() == "":
		return 8
	default:
		return 0
	}
}
