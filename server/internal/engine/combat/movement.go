package combat

import (
	"sort"

	"github.com/elebirds/panoptes/internal/algo/pathfinding"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/yohamta/donburi"
)

type MovementSystem struct{}

func (s *MovementSystem) Run(world donburi.World, state *domain.GameState) []event.Event {
	events := make([]event.Event, 0)
	state.PendingMoves = state.PendingMoves[:0]

	entries := make([]*donburi.Entry, 0)
	ecs.UnitsWithMoveIntent(world).Each(world, func(entry *donburi.Entry) {
		entries = append(entries, entry)
	})
	sort.Slice(entries, func(i, j int) bool {
		return ecs.UnitStatsC.Get(entries[i]).ID < ecs.UnitStatsC.Get(entries[j]).ID
	})

	grid := &gameGrid{world: world}
	timestamp := 0
	for _, entry := range entries {
		stats := ecs.UnitStatsC.Get(entry)
		posComp := ecs.PositionC.Get(entry)
		intent := ecs.MoveIntentC.Get(entry)
		start := domain.Position{X: posComp.X, Y: posComp.Y}
		path := intent.Path
		if len(path) == 0 {
			computed, ok := pathfinding.FindPath(grid, start, intent.Target)
			if !ok || len(computed) == 0 {
				continue
			}
			path = computed
		}
		if len(path) <= 1 {
			continue
		}

		maxStep := effectiveUnitMoveRange(state, stats.Faction, stats.Type, stats.Speed)
		if maxStep < 1 {
			maxStep = 1
		}
		maxIndex := maxStep
		if maxIndex > len(path)-1 {
			maxIndex = len(path) - 1
		}
		movePath := append([]domain.Position(nil), path[:maxIndex+1]...)
		to := movePath[len(movePath)-1]
		events = append(events, event.UnitMovedEvent{UnitID: stats.ID, From: start, To: to, Timestamp: timestamp})
		state.PendingMoves = append(state.PendingMoves, domain.PendingMove{
			UnitID:    stats.ID,
			Faction:   stats.Faction,
			Speed:     maxStep,
			Path:      movePath,
			Timestamp: timestamp,
		})
		timestamp++
	}

	return events
}

type gameGrid struct {
	world donburi.World
}

func (g *gameGrid) InBounds(pos domain.Position) bool {
	_, ok := domain.GetNodeAt(g.world, pos)
	return ok
}

func (g *gameGrid) Neighbors(pos domain.Position) []domain.Position {
	neighbors := pos.Neighbors()
	out := make([]domain.Position, 0, len(neighbors))
	for _, next := range neighbors {
		if g.InBounds(next) {
			out = append(out, next)
		}
	}
	return out
}

func (g *gameGrid) Cost(from, to domain.Position) int {
	entry, ok := domain.GetNodeAt(g.world, to)
	if !ok {
		return 99
	}
	node := ecs.NodeC.Get(entry)
	if node.Terrain == domain.TerrainRiver && !node.HasRoad {
		return 99
	}
	if node.HasRoad {
		return 1
	}
	return 2
}

func (g *gameGrid) IsBlocked(pos domain.Position) bool {
	nodeEntry, ok := domain.GetNodeAt(g.world, pos)
	if !ok {
		return true
	}
	node := ecs.NodeC.Get(nodeEntry)
	if node.Owner == "" {
		return false
	}
	units := domain.GetUnitsByNode(g.world, pos)
	if len(units) == 0 {
		return true
	}
	faction := ecs.UnitStatsC.Get(units[0]).Faction
	for _, u := range units[1:] {
		if ecs.UnitStatsC.Get(u).Faction == faction {
			continue
		}
		return false
	}
	return node.Owner != faction
}
