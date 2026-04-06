package pathfinding

import "container/heap"

// AStar implements a standard A* search on a whiteboard Grid.
// It knows nothing about terrain names, unit types, or strategies.
type AStar struct{}

func NewAStar() *AStar { return &AStar{} }

func (a *AStar) Search(grid Grid, start, goal Point) (PathResult, error) {
	if !grid.InBounds(start) || !grid.InBounds(goal) {
		return PathResult{Found: false, EndPoint: start}, nil
	}
	if start.Equal(goal) {
		return PathResult{Found: true, TargetReached: true, Cost: 0, Path: []Point{start}, EndPoint: start}, nil
	}

	open := &nodeHeap{}
	heap.Init(open)

	gScore := map[Point]int{start: 0}
	cameFrom := map[Point]Point{}
	inClosed := map[Point]bool{}

	heap.Push(open, &aNode{point: start, g: 0, f: Manhattan(start, goal)})

	for open.Len() > 0 {
		cur := heap.Pop(open).(*aNode)
		if cur.point.Equal(goal) {
			path := reconstructPath(cameFrom, goal)
			return PathResult{Found: true, TargetReached: true, Cost: cur.g, Path: path, EndPoint: goal}, nil
		}
		if inClosed[cur.point] {
			continue
		}
		inClosed[cur.point] = true

		for _, nb := range grid.Neighbors(cur.point) {
			if inClosed[nb] {
				continue
			}
			cell := grid.CellAt(nb)
			if cell.Blocked {
				continue
			}
			tentG := cur.g + cell.TotalCost()
			if prev, ok := gScore[nb]; ok && tentG >= prev {
				continue
			}
			gScore[nb] = tentG
			cameFrom[nb] = cur.point
			heap.Push(open, &aNode{point: nb, g: tentG, f: tentG + Manhattan(nb, goal)})
		}
	}

	return PathResult{Found: false, EndPoint: start}, nil
}

func reconstructPath(cameFrom map[Point]Point, cur Point) []Point {
	path := []Point{cur}
	for {
		prev, ok := cameFrom[cur]
		if !ok {
			break
		}
		path = append(path, prev)
		cur = prev
	}
	for i, j := 0, len(path)-1; i < j; i, j = i+1, j-1 {
		path[i], path[j] = path[j], path[i]
	}
	return path
}

// ---------------------------------------------------------------------------
// min-heap for A*
// ---------------------------------------------------------------------------

type aNode struct {
	point Point
	g, f  int
	index int
}

type nodeHeap []*aNode

func (h nodeHeap) Len() int            { return len(h) }
func (h nodeHeap) Less(i, j int) bool  { return h[i].f < h[j].f }
func (h nodeHeap) Swap(i, j int)       { h[i], h[j] = h[j], h[i]; h[i].index = i; h[j].index = j }
func (h *nodeHeap) Push(x interface{}) { n := x.(*aNode); n.index = len(*h); *h = append(*h, n) }
func (h *nodeHeap) Pop() interface{} {
	old := *h
	n := old[len(old)-1]
	old[len(old)-1] = nil
	*h = old[:len(old)-1]
	return n
}
