// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: Recipe affordability math.

package economy

import "github.com/elebirds/panoptes/internal/domain"

func affordabilityRatioResources(available domain.ResourceBag, total domain.ResourceBag) float64 {
	if total == nil || total.IsZero() {
		return 1
	}
	// 返回“所有输入资源里最短板的可支付比例”，用于和 point ratio 共同决定 recipe 的低效推进速度。
	ratio := 1.0
	for _, key := range total.Keys() {
		required := total.Get(key)
		if required <= 0 {
			continue
		}
		current := available.Get(key)
		currentRatio := float64(current) / float64(required)
		if currentRatio < ratio {
			ratio = currentRatio
		}
	}
	if ratio < 0 {
		return 0
	}
	return ratio
}

func affordabilityRatioPoints(available domain.PointBag, total domain.PointBag) float64 {
	if total == nil || total.IsZero() {
		return 1
	}
	ratio := 1.0
	for _, key := range total.Keys() {
		required := total.Get(key)
		if required <= 0 {
			continue
		}
		current := available.Get(key)
		currentRatio := float64(current) / float64(required)
		if currentRatio < ratio {
			ratio = currentRatio
		}
	}
	if ratio < 0 {
		return 0
	}
	return ratio
}

func blockedReasonForRatios(resourceRatio float64, pointRatio float64, resources domain.ResourceBag, points domain.PointBag) string {
	if (resources != nil && !resources.IsZero()) && resourceRatio <= 0 {
		return "insufficient_resources"
	}
	if (points != nil && !points.IsZero()) && pointRatio <= 0 {
		return "insufficient_points"
	}
	if resourceRatio < pointRatio {
		return "insufficient_resources"
	}
	if pointRatio < resourceRatio {
		return "insufficient_points"
	}
	return ""
}
