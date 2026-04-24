// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现几何算法模块的距离计算辅助。

package geometry

import "github.com/elebirds/panoptes/internal/domain"

func OffsetToAxial(col, row int) domain.Position {
	return domain.Position{
		Q: col - (row-(row&1))/2,
		R: row,
	}
}

func AxialToOffset(pos domain.Position) (int, int) {
	return pos.Q + (pos.R-(pos.R&1))/2, pos.R
}

func AxialDistance(a, b domain.Position) int {
	dq := abs(a.Q - b.Q)
	dr := abs(a.R - b.R)
	ds := abs(a.Q + a.R - b.Q - b.R)
	return (dq + ds + dr) / 2
}

func abs(value int) int {
	if value < 0 {
		return -value
	}
	return value
}
