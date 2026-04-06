package test

import (
	"testing"

	pf "github.com/elebirds/panoptes/internal/algo/pathfinding"
)

func TestAStar_EmptyGrid(t *testing.T) {
	grid := pf.NewArrayGrid(5, 5)
	astar := pf.NewAStar()
	res, err := astar.Search(grid, pf.Point{0, 0}, pf.Point{4, 4})
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if !res.Found {
		t.Fatal("expected path to be found")
	}
	if !res.TargetReached {
		t.Fatal("expected target reached")
	}
	if res.Cost != 8 {
		t.Fatalf("expected cost 8, got %d", res.Cost)
	}
	if !res.Path[0].Equal(pf.Point{0, 0}) {
		t.Fatalf("path should start at (0,0), got %v", res.Path[0])
	}
	if !res.EndPoint.Equal(pf.Point{4, 4}) {
		t.Fatalf("endpoint should be (4,4), got %v", res.EndPoint)
	}
}

func TestAStar_SameStartGoal(t *testing.T) {
	grid := pf.NewArrayGrid(3, 3)
	astar := pf.NewAStar()
	res, err := astar.Search(grid, pf.Point{1, 1}, pf.Point{1, 1})
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if !res.Found || !res.TargetReached || res.Cost != 0 {
		t.Fatal("same start/goal should return immediately")
	}
}

func TestAStar_BlockedPath(t *testing.T) {
	grid := pf.NewArrayGrid(3, 3)
	for c := 0; c < 3; c++ {
		grid.SetCell(1, c, pf.Cell{Blocked: true})
	}
	astar := pf.NewAStar()
	res, err := astar.Search(grid, pf.Point{0, 0}, pf.Point{2, 2})
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if res.Found {
		t.Fatal("expected no path when fully blocked")
	}
}

func TestAStar_PrefersLowCost(t *testing.T) {
	grid := pf.NewArrayGrid(5, 1) // 1 row, 5 cols
	grid.SetCell(0, 1, pf.Cell{EnterCost: 10})
	grid.SetCell(0, 2, pf.Cell{EnterCost: 1})
	grid.SetCell(0, 3, pf.Cell{EnterCost: 10})
	astar := pf.NewAStar()
	res, err := astar.Search(grid, pf.Point{0, 0}, pf.Point{0, 4})
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if !res.Found {
		t.Fatal("expected path to be found")
	}
	if res.Cost != 22 {
		t.Fatalf("expected cost 22 (10+1+10+1), got %d", res.Cost)
	}
}

func TestAStar_ExtraCostAffectsRoute(t *testing.T) {
	grid := pf.NewArrayGrid(3, 3)
	grid.SetCell(0, 1, pf.Cell{EnterCost: 1, ExtraCost: 5})
	grid.SetCell(0, 2, pf.Cell{EnterCost: 1, ExtraCost: 5})

	astar := pf.NewAStar()
	res, err := astar.Search(grid, pf.Point{0, 0}, pf.Point{2, 2})
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if !res.Found {
		t.Fatal("expected path found")
	}
	for _, p := range res.Path[1:] {
		if p.Y == 0 && p.X > 0 {
			t.Fatalf("path should avoid expensive top row, but visited %v", p)
		}
	}
}
