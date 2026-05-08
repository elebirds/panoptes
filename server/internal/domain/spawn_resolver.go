package domain

import (
	"strings"

	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

const (
	unitSpawnSearchMinRing = 1
	unitSpawnSearchMaxRing = 3
)

// ResolveUnitSpawnPosition finds a safe nearby spawn tile around the origin.
// Candidates are limited to rings 1..3 and must also be able to reach a
// passable tile beyond ring 3 so spawned units do not get trapped locally.
func ResolveUnitSpawnPosition(state *GameState, origin Position) (Position, bool) {
	if state == nil || state.World == nil {
		return Position{}, false
	}

	reserved := plannedBuildReservations(state)
	for _, candidate := range positionsWithinRings(origin, unitSpawnSearchMaxRing) {
		distance := origin.DistanceTo(candidate)
		if distance < unitSpawnSearchMinRing || distance > unitSpawnSearchMaxRing {
			continue
		}
		if !unitSpawnCandidateAvailable(state, candidate, reserved) {
			continue
		}
		if !unitSpawnCanEscape(state, origin, candidate, reserved) {
			continue
		}
		return candidate, true
	}
	return Position{}, false
}

func plannedBuildReservations(state *GameState) map[string]struct{} {
	if state == nil {
		return nil
	}
	reserved := make(map[string]struct{}, len(state.TurnRuntime.Planning.BuildOrders))
	for _, order := range state.TurnRuntime.Planning.BuildOrders {
		nodeID := strings.TrimSpace(order.NodeID)
		if nodeID == "" {
			continue
		}
		reserved[nodeID] = struct{}{}
	}
	return reserved
}

func positionsWithinRings(origin Position, maxDistance int) []Position {
	if maxDistance <= 0 {
		return nil
	}
	type queueItem struct {
		pos      Position
		distance int
	}
	out := make([]Position, 0, maxDistance*6)
	queue := []queueItem{{pos: origin, distance: 0}}
	visited := map[Position]struct{}{origin: {}}
	for len(queue) > 0 {
		current := queue[0]
		queue = queue[1:]
		if current.distance >= maxDistance {
			continue
		}
		for _, next := range current.pos.Neighbors() {
			if _, seen := visited[next]; seen {
				continue
			}
			visited[next] = struct{}{}
			nextDistance := current.distance + 1
			queue = append(queue, queueItem{pos: next, distance: nextDistance})
			out = append(out, next)
		}
	}
	return out
}

func unitSpawnCandidateAvailable(state *GameState, pos Position, reserved map[string]struct{}) bool {
	entry, ok := GetNodeAt(state.World, pos)
	if !ok || entry == nil {
		return false
	}
	if !unitSpawnNodePassable(entry) || entry.HasComponent(BuildingC) {
		return false
	}
	if unitSpawnNodeReserved(entry, reserved) || HasUnitAtNode(state.World, pos) {
		return false
	}
	return true
}

func unitSpawnCanEscape(state *GameState, origin Position, start Position, reserved map[string]struct{}) bool {
	type queueItem struct {
		pos Position
	}
	queue := []queueItem{{pos: start}}
	visited := map[Position]struct{}{start: {}}
	for len(queue) > 0 {
		current := queue[0]
		queue = queue[1:]
		if origin.DistanceTo(current.pos) > unitSpawnSearchMaxRing {
			return true
		}
		for _, next := range current.pos.Neighbors() {
			if _, seen := visited[next]; seen {
				continue
			}
			entry, ok := GetNodeAt(state.World, next)
			if !ok || entry == nil {
				continue
			}
			if !unitSpawnNodePassable(entry) || entry.HasComponent(BuildingC) || unitSpawnNodeReserved(entry, reserved) {
				continue
			}
			visited[next] = struct{}{}
			queue = append(queue, queueItem{pos: next})
		}
	}
	return false
}

func unitSpawnNodeReserved(entry *donburi.Entry, reserved map[string]struct{}) bool {
	if len(reserved) == 0 || entry == nil {
		return false
	}
	_, exists := reserved[strings.TrimSpace(NodeC.Get(entry).ID)]
	return exists
}

func unitSpawnNodePassable(entry *donburi.Entry) bool {
	if entry == nil {
		return false
	}
	node := NodeC.Get(entry)
	terrain, ok := staticdata.Default().GetTerrain(string(node.Terrain))
	if !ok {
		return false
	}
	if node.HasRoad && terrain.PassableWithRoad {
		return true
	}
	return terrain.Passable
}
