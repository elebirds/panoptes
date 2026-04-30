// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: Procedural placement validation and numeric helpers.

package maploader

func minDistanceToSet(p mapPoint, set []mapPoint) int {
	if len(set) == 0 {
		return int(^uint(0) >> 1)
	}
	best := int(^uint(0) >> 1)
	for _, q := range set {
		d := manhattan(p, q)
		if d < best {
			best = d
		}
	}
	return best
}

func hasPointTooClose(p mapPoint, set []mapPoint, minDist int) bool {
	for _, q := range set {
		if manhattan(p, q) < minDist {
			return true
		}
	}
	return false
}

func manhattan(a, b mapPoint) int {
	dx := a.X - b.X
	if dx < 0 {
		dx = -dx
	}
	dy := a.Y - b.Y
	if dy < 0 {
		dy = -dy
	}
	return dx + dy
}

func clamp(v, minV, maxV int) int {
	if v < minV {
		return minV
	}
	if v > maxV {
		return maxV
	}
	return v
}

func min(a, b int) int {
	if a < b {
		return a
	}
	return b
}

func max(a, b int) int {
	if a > b {
		return a
	}
	return b
}

func itoa(v int) string {
	if v == 0 {
		return "0"
	}
	sign := ""
	if v < 0 {
		sign = "-"
		v = -v
	}
	buf := [20]byte{}
	i := len(buf)
	for v > 0 {
		i--
		buf[i] = byte('0' + v%10)
		v /= 10
	}
	return sign + string(buf[i:])
}
