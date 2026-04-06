package test

import (
	"testing"

	pf "github.com/elebirds/panoptes/internal/algo/pathfinding"
	"github.com/elebirds/panoptes/internal/algo/pathfinding/strategy"
)

type staticBuilder struct {
	grid pf.Grid
}

func (b staticBuilder) Build(world strategy.WorldState, unit strategy.UnitState, decision strategy.StrategyDecision) (pf.Grid, error) {
	return b.grid, nil
}

func makePlainWorld(rows, cols int) strategy.WorldState {
	terrain := make([][]strategy.TerrainTile, rows)
	for r := 0; r < rows; r++ {
		terrain[r] = make([]strategy.TerrainTile, cols)
		for c := 0; c < cols; c++ {
			terrain[r][c] = strategy.TerrainTile{X: c, Y: r, Terrain: "plain"}
		}
	}
	return strategy.WorldState{Terrain: terrain}
}

func TestPlanner_DirectReach(t *testing.T) {
	world := makePlainWorld(5, 5)
	unit := strategy.UnitState{
		UnitID:     "u1",
		UnitType:   "infantry",
		Faction:    "ally",
		X:          0,
		Y:          0,
		MovePoints: 20,
	}
	decision := strategy.StrategyDecision{
		Strategy: strategy.StrategyAttack,
		Goal:     pf.Point{X: 4, Y: 4},
	}

	cfg := strategy.DefaultGameData()
	planner := strategy.NewPlanner(strategy.NewBuilder(cfg))

	result, err := planner.Plan(world, unit, decision)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if result.ExecutionTarget.Type != strategy.ExecutionTargetGoal {
		t.Fatalf("expected goal, got %s", result.ExecutionTarget.Type)
	}
	if !result.Result.Found {
		t.Fatal("expected path found")
	}
	if !result.Result.EndPoint.Equal(pf.Point{X: 4, Y: 4}) {
		t.Fatalf("expected endpoint (4,4), got %v", result.Result.EndPoint)
	}
}

func TestPlanner_StopsOnBestPathWhenGoalFar(t *testing.T) {
	world := makePlainWorld(10, 10)
	unit := strategy.UnitState{
		UnitID:     "u1",
		UnitType:   "infantry",
		Faction:    "ally",
		X:          0,
		Y:          0,
		MovePoints: 3,
	}
	decision := strategy.StrategyDecision{
		Strategy: strategy.StrategyAttack,
		Goal:     pf.Point{X: 9, Y: 9},
	}

	cfg := strategy.DefaultGameData()
	planner := strategy.NewPlanner(strategy.NewBuilder(cfg))

	result, err := planner.Plan(world, unit, decision)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if result.ExecutionTarget.Type != strategy.ExecutionTargetGoal {
		t.Fatalf("expected goal, got %s", result.ExecutionTarget.Type)
	}
	if !result.Result.Found {
		t.Fatal("expected path found")
	}
	if result.Result.TargetReached {
		t.Fatal("goal should not be reached within budget")
	}
	if result.Result.Cost > 3 {
		t.Fatalf("path cost %d exceeds budget 3", result.Result.Cost)
	}
}

func TestPlanner_CavalryRoadSpeedBoost(t *testing.T) {
	world := makePlainWorld(1, 6)
	for c := 0; c < 6; c++ {
		world.Terrain[0][c].HasRoad = true
	}
	unit := strategy.UnitState{
		UnitID:     "u1",
		UnitType:   "cavalry",
		Faction:    "ally",
		X:          0,
		Y:          0,
		MovePoints: 5,
	}
	decision := strategy.StrategyDecision{
		Strategy: strategy.StrategyAttack,
		Goal:     pf.Point{X: 5, Y: 0},
	}

	cfg := strategy.DefaultGameData()
	planner := strategy.NewPlanner(strategy.NewBuilder(cfg))

	result, err := planner.Plan(world, unit, decision)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if !result.Result.TargetReached {
		t.Fatal("cavalry road speed bonus should reduce plain road cost to 1")
	}
	if result.Result.Cost != 5 {
		t.Fatalf("expected cost 5, got %d", result.Result.Cost)
	}
}

func TestPlanner_MountainIsPassableForCavalry(t *testing.T) {
	world := makePlainWorld(3, 3)
	world.Terrain[1][0] = strategy.TerrainTile{X: 0, Y: 1, Terrain: "mountain"}
	world.Terrain[1][1] = strategy.TerrainTile{X: 1, Y: 1, Terrain: "mountain"}
	world.Terrain[1][2] = strategy.TerrainTile{X: 2, Y: 1, Terrain: "mountain"}

	unit := strategy.UnitState{
		UnitID:     "u1",
		UnitType:   "cavalry",
		Faction:    "ally",
		X:          0,
		Y:          0,
		MovePoints: 20,
	}
	decision := strategy.StrategyDecision{
		Strategy: strategy.StrategyAttack,
		Goal:     pf.Point{X: 2, Y: 2},
	}

	cfg := strategy.DefaultGameData()
	planner := strategy.NewPlanner(strategy.NewBuilder(cfg))

	result, err := planner.Plan(world, unit, decision)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if result.ExecutionTarget.Type != strategy.ExecutionTargetGoal {
		t.Fatal("mountain should not change the execution target")
	}
	if !result.Result.Found || !result.Result.TargetReached {
		t.Fatal("mountain should remain passable for cavalry under the strict config shape")
	}
}

func TestPlanner_RiverBlockedWithoutRoad(t *testing.T) {
	world := makePlainWorld(3, 3)
	world.Terrain[1][0] = strategy.TerrainTile{X: 0, Y: 1, Terrain: "river"}
	world.Terrain[1][1] = strategy.TerrainTile{X: 1, Y: 1, Terrain: "river"}
	world.Terrain[1][2] = strategy.TerrainTile{X: 2, Y: 1, Terrain: "river"}

	unit := strategy.UnitState{
		UnitID:     "u1",
		UnitType:   "infantry",
		Faction:    "ally",
		X:          0,
		Y:          0,
		MovePoints: 20,
	}
	decision := strategy.StrategyDecision{
		Strategy: strategy.StrategyAttack,
		Goal:     pf.Point{X: 2, Y: 2},
	}

	cfg := strategy.DefaultGameData()
	planner := strategy.NewPlanner(strategy.NewBuilder(cfg))

	result, err := planner.Plan(world, unit, decision)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if result.ExecutionTarget.Type != strategy.ExecutionTargetGoal {
		t.Fatal("goal remains the execution target even when unreachable")
	}
	if result.Result.Found {
		t.Fatal("infantry should not find a path across a river wall")
	}
}

func TestPlanner_UnitsDoNotBlockPath(t *testing.T) {
	world := makePlainWorld(3, 3)
	world.Units = []strategy.UnitInfo{
		{ID: "e1", Faction: "enemy", UnitType: "infantry", X: 0, Y: 1},
		{ID: "e2", Faction: "enemy", UnitType: "infantry", X: 1, Y: 1},
		{ID: "e3", Faction: "enemy", UnitType: "infantry", X: 2, Y: 1},
	}

	unit := strategy.UnitState{
		UnitID:     "u1",
		UnitType:   "infantry",
		Faction:    "ally",
		X:          0,
		Y:          0,
		MovePoints: 20,
	}
	decision := strategy.StrategyDecision{
		Strategy: strategy.StrategyAttack,
		Goal:     pf.Point{X: 2, Y: 2},
	}

	cfg := strategy.DefaultGameData()
	planner := strategy.NewPlanner(strategy.NewBuilder(cfg))

	result, err := planner.Plan(world, unit, decision)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if result.ExecutionTarget.Type != strategy.ExecutionTargetGoal {
		t.Fatal("goal should remain the execution target")
	}
	if !result.Result.Found || !result.Result.TargetReached {
		t.Fatal("units should no longer block pathfinding")
	}
}

func TestPlanner_FollowsGlobalBestPathPrefix(t *testing.T) {
	grid := pf.NewArrayGrid(5, 3)
	grid.SetCell(2, 0, pf.Cell{Blocked: true})
	for c := 1; c <= 3; c++ {
		grid.SetCell(1, c, pf.Cell{EnterCost: 1, ExtraCost: 20})
	}

	planner := strategy.NewPlanner(staticBuilder{grid: grid})
	unit := strategy.UnitState{
		UnitID:     "u1",
		UnitType:   "infantry",
		Faction:    "ally",
		X:          0,
		Y:          1,
		MovePoints: 2,
	}
	decision := strategy.StrategyDecision{
		Strategy: strategy.StrategyPathfind,
		Goal:     pf.Point{X: 4, Y: 1},
	}

	result, err := planner.Plan(strategy.WorldState{}, unit, decision)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if result.ExecutionTarget.Type != strategy.ExecutionTargetGoal {
		t.Fatalf("expected goal, got %s", result.ExecutionTarget.Type)
	}
	if result.Result.TargetReached {
		t.Fatal("goal should not be reached within budget")
	}
	if !result.Result.EndPoint.Equal(pf.Point{X: 1, Y: 0}) {
		t.Fatalf("expected to follow best path prefix to (1,0), got %v", result.Result.EndPoint)
	}
}

func TestPlanner_AttackPrefersEnemyBuildingTile(t *testing.T) {
	world := makePlainWorld(3, 3)
	world.Terrain[0][1] = strategy.TerrainTile{X: 1, Y: 0, Terrain: "forest"}
	world.Buildings = []strategy.BuildingInfo{
		{ID: "b1", BuildingType: "tower", Faction: "enemy", X: 1, Y: 0},
	}

	unit := strategy.UnitState{
		UnitID:     "u1",
		UnitType:   "infantry",
		Faction:    "ally",
		X:          0,
		Y:          0,
		MovePoints: 20,
	}
	decision := strategy.StrategyDecision{
		Strategy: strategy.StrategyAttack,
		Goal:     pf.Point{X: 2, Y: 2},
	}

	cfg := strategy.DefaultGameData()
	planner := strategy.NewPlanner(strategy.NewBuilder(cfg))

	result, err := planner.Plan(world, unit, decision)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if !result.Result.Found {
		t.Fatal("expected path found")
	}
	foundEnemyTile := false
	for _, p := range result.Result.Path {
		if p.Equal(pf.Point{X: 1, Y: 0}) {
			foundEnemyTile = true
			break
		}
	}
	if !foundEnemyTile {
		t.Fatal("attack strategy should prefer the enemy building tile when available")
	}
}

func TestPlanner_PathfindDoesNotPreferHostileTile(t *testing.T) {
	world := makePlainWorld(3, 3)
	world.Terrain[0][1] = strategy.TerrainTile{X: 1, Y: 0, Terrain: "forest"}
	world.Buildings = []strategy.BuildingInfo{
		{ID: "b1", BuildingType: "tower", Faction: "enemy", X: 1, Y: 0},
	}

	unit := strategy.UnitState{
		UnitID:     "u1",
		UnitType:   "infantry",
		Faction:    "ally",
		X:          0,
		Y:          0,
		MovePoints: 20,
	}
	decision := strategy.StrategyDecision{
		Strategy: strategy.StrategyPathfind,
		Goal:     pf.Point{X: 2, Y: 2},
	}

	cfg := strategy.DefaultGameData()
	planner := strategy.NewPlanner(strategy.NewBuilder(cfg))

	result, err := planner.Plan(world, unit, decision)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if !result.Result.Found {
		t.Fatal("expected path found")
	}
	for _, p := range result.Result.Path {
		if p.Equal(pf.Point{X: 1, Y: 0}) {
			t.Fatal("pathfind strategy should not add a discount for hostile tiles")
		}
	}
}
