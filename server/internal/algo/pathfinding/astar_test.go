package pathfinding

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
)

type axialTestGrid struct {
	nodes   map[domain.Position]struct{}
	blocked map[domain.Position]struct{}
}

func newAxialTestGrid(radius int) axialTestGrid {
	nodes := map[domain.Position]struct{}{}
	for q := -radius; q <= radius; q++ {
		for r := -radius; r <= radius; r++ {
			pos := domain.Position{Q: q, R: r}
			if (domain.Position{}).DistanceTo(pos) <= radius {
				nodes[pos] = struct{}{}
			}
		}
	}
	return axialTestGrid{nodes: nodes, blocked: map[domain.Position]struct{}{}}
}

func (g axialTestGrid) InBounds(pos domain.Position) bool {
	_, ok := g.nodes[pos]
	return ok
}

func (g axialTestGrid) Neighbors(pos domain.Position) []domain.Position {
	return pos.Neighbors()
}

func (g axialTestGrid) Cost(_, _ domain.Position) int {
	return 1
}

func (g axialTestGrid) IsBlocked(pos domain.Position) bool {
	_, ok := g.blocked[pos]
	return ok
}

func TestFindPathUsesHexAxialNeighbors(t *testing.T) {
	grid := newAxialTestGrid(3)
	start := domain.Position{Q: 0, R: 0}
	goal := domain.Position{Q: 2, R: -2}

	path, ok := FindPath(grid, start, goal)
	if !ok {
		t.Fatalf("FindPath() = false")
	}
	if got, want := len(path), 3; got != want {
		t.Fatalf("path len = %d, want %d; path=%#v", got, want, path)
	}
	if path[0] != start || path[len(path)-1] != goal {
		t.Fatalf("path endpoints = %#v", path)
	}
	for i := 1; i < len(path); i++ {
		if path[i-1].DistanceTo(path[i]) != 1 {
			t.Fatalf("path step %d is not axial-adjacent: %#v -> %#v", i, path[i-1], path[i])
		}
	}
}
