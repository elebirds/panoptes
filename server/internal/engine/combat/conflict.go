package combat

import (
	"sort"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/yohamta/donburi"
)

type ConflictSystem struct{}

func (s *ConflictSystem) Run(world donburi.World, state *domain.GameState) []event.Event {
	conflicts := make([]domain.Conflict, 0)
	for i := 0; i < len(state.PendingMoves); i++ {
		for j := i + 1; j < len(state.PendingMoves); j++ {
			a := state.PendingMoves[i]
			b := state.PendingMoves[j]
			if a.Faction == b.Faction {
				continue
			}
			if c, ok := detectConflict(a, b); ok {
				conflicts = append(conflicts, c)
			}
		}
	}

	sort.Slice(conflicts, func(i, j int) bool {
		if conflicts[i].TimeStep != conflicts[j].TimeStep {
			return conflicts[i].TimeStep < conflicts[j].TimeStep
		}
		if conflicts[i].MaxSpeed != conflicts[j].MaxSpeed {
			return conflicts[i].MaxSpeed > conflicts[j].MaxSpeed
		}
		if conflicts[i].UnitAID != conflicts[j].UnitAID {
			return conflicts[i].UnitAID < conflicts[j].UnitAID
		}
		return conflicts[i].UnitBID < conflicts[j].UnitBID
	})

	state.PendingConflicts = conflicts
	events := make([]event.Event, 0, len(conflicts))
	for _, c := range conflicts {
		events = append(events, event.ConflictResolvedEvent{
			UnitAID:      c.UnitAID,
			UnitBID:      c.UnitBID,
			Location:     c.Location,
			ConflictType: c.ConflictType,
		})
	}
	return events
}

func detectConflict(a, b domain.PendingMove) (domain.Conflict, bool) {
	maxT := len(a.Path)
	if len(b.Path) > maxT {
		maxT = len(b.Path)
	}
	if maxT <= 1 {
		return domain.Conflict{}, false
	}

	for t := 1; t < maxT; t++ {
		aPrev, aCur := stepPos(a.Path, t-1), stepPos(a.Path, t)
		bPrev, bCur := stepPos(b.Path, t-1), stepPos(b.Path, t)

		if aPrev == bCur && aCur == bPrev && aCur != aPrev {
			return domain.Conflict{
				UnitAID:      a.UnitID,
				UnitBID:      b.UnitID,
				Location:     midpoint(aPrev, aCur),
				ConflictType: "edge",
				TimeStep:     t,
				MaxSpeed:     maxInt(a.Speed, b.Speed),
			}, true
		}

		if aCur == bCur {
			return domain.Conflict{
				UnitAID:      a.UnitID,
				UnitBID:      b.UnitID,
				Location:     aCur,
				ConflictType: "node",
				TimeStep:     t,
				MaxSpeed:     maxInt(a.Speed, b.Speed),
			}, true
		}

		aDir := domain.Position{X: aCur.X - aPrev.X, Y: aCur.Y - aPrev.Y}
		bDir := domain.Position{X: bCur.X - bPrev.X, Y: bCur.Y - bPrev.Y}
		if aDir == bDir && aCur == bPrev {
			return domain.Conflict{
				UnitAID:      a.UnitID,
				UnitBID:      b.UnitID,
				Location:     aCur,
				ConflictType: "chase",
				TimeStep:     t,
				MaxSpeed:     maxInt(a.Speed, b.Speed),
			}, true
		}
	}

	return domain.Conflict{}, false
}

func stepPos(path []domain.Position, step int) domain.Position {
	if len(path) == 0 {
		return domain.Position{}
	}
	if step < 0 {
		return path[0]
	}
	if step >= len(path) {
		return path[len(path)-1]
	}
	return path[step]
}

func midpoint(a, b domain.Position) domain.Position {
	return domain.Position{X: (a.X + b.X) / 2, Y: (a.Y + b.Y) / 2}
}

func maxInt(a, b int) int {
	if a > b {
		return a
	}
	return b
}
