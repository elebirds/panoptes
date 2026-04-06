package strategy

import (
	pf "github.com/elebirds/panoptes/internal/algo/pathfinding"
)

// Builder implements CostBuilder.
type Builder struct {
	Config GameDataConfig
}

func NewBuilder(cfg GameDataConfig) *Builder {
	return &Builder{Config: cfg}
}

func (b *Builder) Build(world WorldState, unit UnitState, decision StrategyDecision) (pf.Grid, error) {
	h := len(world.Terrain)
	if h == 0 {
		return pf.NewArrayGrid(0, 0), nil
	}
	w := len(world.Terrain[0])
	grid := pf.NewArrayGrid(w, h)
	hostilePoints := buildHostileSet(world, unit)

	for r := 0; r < h; r++ {
		for c := 0; c < w; c++ {
			tile := world.Terrain[r][c]
			cell := b.tileToCell(tile, unit.UnitType)
			cell.ExtraCost = b.strategyExtraCost(decision, pf.Point{X: c, Y: r}, hostilePoints)
			if cell.EnterCost+cell.ExtraCost < 0 {
				cell.ExtraCost = -cell.EnterCost
			}
			grid.SetCell(r, c, cell)
		}
	}
	return grid, nil
}

func (b *Builder) tileToCell(tile TerrainTile, unitType string) pf.Cell {
	tc, ok := b.terrainByID(tile.Terrain)
	if !ok {
		return pf.Cell{EnterCost: MaxInt, Blocked: true}
	}

	if !tc.Passable {
		return pf.Cell{Blocked: true}
	}

	enterCost := tc.MoveCostNoRoad
	if tile.HasRoad {
		if uc, ok := b.unitByID(unitType); ok && uc.RoadSpeedBonus > 0 {
			enterCost -= uc.RoadSpeedBonus
			if enterCost < 1 {
				enterCost = 1
			}
		}
	}

	return pf.Cell{EnterCost: enterCost}
}

func (b *Builder) strategyExtraCost(decision StrategyDecision, p pf.Point, hostilePoints map[pf.Point]bool) int {
	switch decision.Strategy {
	case StrategyAttack:
		if hostilePoints[p] {
			return -2
		}
	case StrategyPathfind:
		return 0
	case StrategyDefend:
		dist := pf.Manhattan(p, decision.Goal)
		if dist > 4 {
			return 2
		}
	}
	return 0
}

func buildHostileSet(world WorldState, self UnitState) map[pf.Point]bool {
	hostile := make(map[pf.Point]bool)
	for _, u := range world.Units {
		if u.ID == self.UnitID || u.Faction == "" || u.Faction == self.Faction {
			continue
		}
		hostile[pf.Point{X: u.X, Y: u.Y}] = true
	}
	for _, b := range world.Buildings {
		if b.Faction == "" || b.Faction == self.Faction {
			continue
		}
		hostile[pf.Point{X: b.X, Y: b.Y}] = true
	}
	return hostile
}

func (b *Builder) terrainByID(id string) (TerrainTileDef, bool) {
	for _, tile := range b.Config.MapTile.TerrainTiles {
		if tile.ID == id {
			return tile, true
		}
	}
	return TerrainTileDef{}, false
}

func (b *Builder) unitByID(id string) (UnitDef, bool) {
	for _, unit := range b.Config.Army.Units {
		if unit.ID == id {
			return unit, true
		}
	}
	return UnitDef{}, false
}
