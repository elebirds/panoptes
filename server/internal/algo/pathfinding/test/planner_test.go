package test

import (
	"fmt"
	"strings"
	"testing"

	pf "github.com/elebirds/panoptes/internal/algo/pathfinding"
	"github.com/elebirds/panoptes/internal/algo/pathfinding/strategy"
)

func make4x4SharedWorld() strategy.WorldState {
	// 4x4 地图（X=列, Y=行），让“直走线”略贵：
	// y=0: E F P G
	// y=1: F F F F
	// y=2: F F F F
	// y=3: S P P P
	//
	// E: 敌人 (0,0)
	// S: 起点 (0,3)
	// G: 终点 (3,0)
	// P: plain(2), F: forest(3)
	terrainKinds := [][]string{
		{"plain", "forest", "plain", "plain"},
		{"forest", "forest", "forest", "forest"},
		{"forest", "forest", "forest", "forest"},
		{"plain", "plain", "plain", "plain"},
	}

	terrain := make([][]strategy.TerrainTile, 4)
	for y := 0; y < 4; y++ {
		terrain[y] = make([]strategy.TerrainTile, 4)
		for x := 0; x < 4; x++ {
			terrain[y][x] = strategy.TerrainTile{
				X:       x,
				Y:       y,
				Terrain: terrainKinds[y][x],
			}
		}
	}

	return strategy.WorldState{
		Terrain: terrain,
		Units: []strategy.UnitInfo{
			{ID: "enemy-1", Faction: "enemy", UnitType: "infantry", X: 0, Y: 0},
		},
	}
}

func renderMap(world strategy.WorldState, start, goal pf.Point) string {
	var b strings.Builder
	b.WriteString("map legend: S=start, G=goal, E=enemy, P=plain(2), F=forest(3)\n")
	for y := 0; y < len(world.Terrain); y++ {
		row := make([]string, 0, len(world.Terrain[y]))
		for x := 0; x < len(world.Terrain[y]); x++ {
			p := pf.Point{X: x, Y: y}
			cell := world.Terrain[y][x]
			ch := "P"
			if cell.Terrain == "forest" {
				ch = "F"
			}
			if p.Equal(start) {
				ch = "S"
			} else if p.Equal(goal) {
				ch = "G"
			} else {
				for _, u := range world.Units {
					if u.X == x && u.Y == y {
						ch = "E"
						break
					}
				}
			}
			row = append(row, ch)
		}
		b.WriteString(strings.Join(row, " "))
		b.WriteString("\n")
	}
	return b.String()
}

func formatPath(path []pf.Point) string {
	parts := make([]string, 0, len(path))
	for _, p := range path {
		parts = append(parts, fmt.Sprintf("(%d,%d)", p.X, p.Y))
	}
	return strings.Join(parts, " -> ")
}

func pathContains(path []pf.Point, target pf.Point) bool {
	for _, p := range path {
		if p.Equal(target) {
			return true
		}
	}
	return false
}

func TestPlanner_ThreeStrategies_OnShared4x4Map(t *testing.T) {
	world := make4x4SharedWorld()
	start := pf.Point{X: 0, Y: 3}
	goal := pf.Point{X: 3, Y: 0}
	enemy := pf.Point{X: 0, Y: 0}

	t.Log("\n" + renderMap(world, start, goal))

	unit := strategy.UnitState{
		UnitID:     "ally-1",
		UnitType:   "infantry",
		Faction:    "ally",
		X:          start.X,
		Y:          start.Y,
		MovePoints: 50, // 足够到达终点
	}

	cfg := strategy.DefaultGameData()
	planner := strategy.NewPlanner(strategy.NewBuilder(cfg))

	run := func(st strategy.StrategyType) pf.PathResult {
		decision := strategy.StrategyDecision{
			Strategy: st,
			Goal:     goal,
			Reason:   "test scenario",
		}
		out, err := planner.Plan(world, unit, decision)
		if err != nil {
			t.Fatalf("[%s] unexpected error: %v", st, err)
		}
		if !out.Result.Found || !out.Result.TargetReached {
			t.Fatalf("[%s] expected to reach goal, got found=%v reached=%v", st, out.Result.Found, out.Result.TargetReached)
		}
		t.Logf("[%s] cost=%d path=%s", st, out.Result.Cost, formatPath(out.Result.Path))
		return out.Result
	}

	attack := run(strategy.StrategyAttack)
	pathfind := run(strategy.StrategyPathfind)
	defend := run(strategy.StrategyDefend)

	// attack 倾向经过敌方所在格；defend 相反；pathfind 为纯基础代价最短路
	if !pathContains(attack.Path, enemy) {
		t.Fatalf("[attack] expected path to include enemy tile %v", enemy)
	}
	if pathContains(defend.Path, enemy) {
		t.Fatalf("[defend] expected path to avoid enemy tile %v", enemy)
	}
	if pathContains(pathfind.Path, enemy) {
		t.Fatalf("[pathfind] expected pure A* path to avoid enemy tile %v", enemy)
	}
}
