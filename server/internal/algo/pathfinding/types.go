package pathfinding

// ---------------------------------------------------------------------------
// 基础坐标
// ---------------------------------------------------------------------------

type Point struct {
	X int `json:"x"`
	Y int `json:"y"`
}

func (p Point) Equal(o Point) bool { return p.X == o.X && p.Y == o.Y }

func Manhattan(a, b Point) int {
	dx := a.X - b.X
	dy := a.Y - b.Y
	if dx < 0 {
		dx = -dx
	}
	if dy < 0 {
		dy = -dy
	}
	return dx + dy
}

// ---------------------------------------------------------------------------
// 白板寻路层（A* 只依赖这部分）
// ---------------------------------------------------------------------------

type Cell struct {
	EnterCost int  `json:"enter_cost"`
	ExtraCost int  `json:"extra_cost"`
	Blocked   bool `json:"blocked"`
}

func (c Cell) TotalCost() int { return c.EnterCost + c.ExtraCost }

type Grid interface {
	Width() int
	Height() int
	InBounds(p Point) bool
	CellAt(p Point) Cell
	Neighbors(p Point) []Point
}

type PathResult struct {
	Found         bool    `json:"found"`
	TargetReached bool    `json:"target_reached"`
	Cost          int     `json:"cost"`
	Path          []Point `json:"path"`
	EndPoint      Point   `json:"end_point"`
}

// ---------------------------------------------------------------------------
// ArrayGrid — Grid 的默认数组实现
// ---------------------------------------------------------------------------

type ArrayGrid struct {
	W     int
	H     int
	Cells [][]Cell
}

func NewArrayGrid(w, h int) *ArrayGrid {
	cells := make([][]Cell, h)
	for r := range cells {
		cells[r] = make([]Cell, w)
		for c := range cells[r] {
			cells[r][c] = Cell{EnterCost: 1}
		}
	}
	return &ArrayGrid{W: w, H: h, Cells: cells}
}

func (g *ArrayGrid) Width() int  { return g.W }
func (g *ArrayGrid) Height() int { return g.H }

func (g *ArrayGrid) InBounds(p Point) bool {
	return p.Y >= 0 && p.Y < g.H && p.X >= 0 && p.X < g.W
}

func (g *ArrayGrid) CellAt(p Point) Cell { return g.Cells[p.Y][p.X] }

func (g *ArrayGrid) Neighbors(p Point) []Point {
	dirs := [4]Point{{X: -1, Y: 0}, {X: 1, Y: 0}, {X: 0, Y: -1}, {X: 0, Y: 1}}
	out := make([]Point, 0, 4)
	for _, d := range dirs {
		n := Point{X: p.X + d.X, Y: p.Y + d.Y}
		if g.InBounds(n) {
			out = append(out, n)
		}
	}
	return out
}

func (g *ArrayGrid) SetCell(row, col int, c Cell) {
	g.Cells[row][col] = c
}
