// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现寻路算法模块的A* 寻路算法实现。

package pathfinding

import (
	"container/heap"

	"github.com/elebirds/panoptes/internal/algo/geometry"
	"github.com/elebirds/panoptes/internal/domain"
)

type Grid interface {
	InBounds(pos domain.Position) bool
	Neighbors(pos domain.Position) []domain.Position
	Cost(from, to domain.Position) int
	IsBlocked(pos domain.Position) bool
}

type node struct {
	pos      domain.Position
	priority int
	g        int
	index    int
}

type priorityQueue []*node

func (pq priorityQueue) Len() int           { return len(pq) }
func (pq priorityQueue) Less(i, j int) bool { return pq[i].priority < pq[j].priority }
func (pq priorityQueue) Swap(i, j int) {
	pq[i], pq[j] = pq[j], pq[i]
	pq[i].index = i
	pq[j].index = j
}
func (pq *priorityQueue) Push(x any) {
	n := x.(*node)
	n.index = len(*pq)
	*pq = append(*pq, n)
}
func (pq *priorityQueue) Pop() any {
	old := *pq
	n := len(old)
	item := old[n-1]
	old[n-1] = nil
	*pq = old[:n-1]
	return item
}

func FindPath(grid Grid, start, goal domain.Position) ([]domain.Position, bool) {
	if !grid.InBounds(start) || !grid.InBounds(goal) {
		return nil, false
	}
	if grid.IsBlocked(goal) {
		return nil, false
	}
	if start == goal {
		return []domain.Position{start}, true
	}

	cameFrom := map[domain.Position]domain.Position{}
	gScore := map[domain.Position]int{start: 0}
	open := make(priorityQueue, 0, 16)
	heap.Init(&open)
	heap.Push(&open, &node{pos: start, g: 0, priority: geometry.Manhattan(start, goal)})

	closed := map[domain.Position]bool{}

	for open.Len() > 0 {
		cur := heap.Pop(&open).(*node)
		if closed[cur.pos] {
			continue
		}
		closed[cur.pos] = true
		if cur.pos == goal {
			return rebuildPath(cameFrom, start, goal), true
		}

		for _, next := range grid.Neighbors(cur.pos) {
			if !grid.InBounds(next) || grid.IsBlocked(next) {
				continue
			}
			tentative := cur.g + grid.Cost(cur.pos, next)
			if prev, ok := gScore[next]; ok && tentative >= prev {
				continue
			}
			cameFrom[next] = cur.pos
			gScore[next] = tentative
			heap.Push(&open, &node{pos: next, g: tentative, priority: tentative + geometry.Manhattan(next, goal)})
		}
	}

	return nil, false
}

func rebuildPath(cameFrom map[domain.Position]domain.Position, start, goal domain.Position) []domain.Position {
	path := []domain.Position{goal}
	cur := goal
	for cur != start {
		prev, ok := cameFrom[cur]
		if !ok {
			break
		}
		cur = prev
		path = append(path, cur)
	}
	for i, j := 0, len(path)-1; i < j; i, j = i+1, j-1 {
		path[i], path[j] = path[j], path[i]
	}
	return path
}
