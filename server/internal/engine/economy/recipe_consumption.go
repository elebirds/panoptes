// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: Recipe resource and point consumption math.

package economy

import "github.com/elebirds/panoptes/internal/domain"

func proportionalResourceBag(total domain.ResourceBag, scaledProgress int, scaledRequired int) domain.ResourceBag {
	if total == nil || total.IsZero() || scaledRequired <= 0 {
		return domain.NewResourceBag()
	}
	// 把“完整 recipe 的总成本”按当前累计进度折算成“理论上此刻应累计消耗多少”。
	out := domain.NewResourceBag()
	for _, key := range total.Keys() {
		amount := int(float64(total.Get(key)) * float64(scaledProgress) / float64(scaledRequired))
		if amount > total.Get(key) {
			amount = total.Get(key)
		}
		out.Set(key, amount)
	}
	return out
}

func proportionalPointBag(total domain.PointBag, scaledProgress int, scaledRequired int) domain.PointBag {
	if total == nil || total.IsZero() || scaledRequired <= 0 {
		return domain.NewPointBag()
	}
	out := domain.NewPointBag()
	for _, key := range total.Keys() {
		amount := int(float64(total.Get(key)) * float64(scaledProgress) / float64(scaledRequired))
		if amount > total.Get(key) {
			amount = total.Get(key)
		}
		out.Set(key, amount)
	}
	return out
}

func subtractResourceBags(total domain.ResourceBag, consumed domain.ResourceBag) domain.ResourceBag {
	out := domain.NewResourceBag()
	for _, key := range total.Keys() {
		delta := total.Get(key) - consumed.Get(key)
		out.Set(key, delta)
	}
	return out
}

func subtractPointBags(total domain.PointBag, consumed domain.PointBag) domain.PointBag {
	out := domain.NewPointBag()
	for _, key := range total.Keys() {
		delta := total.Get(key) - consumed.Get(key)
		out.Set(key, delta)
	}
	return out
}

func cloneResourceBag(src domain.ResourceBag) domain.ResourceBag {
	if src == nil {
		return domain.NewResourceBag()
	}
	return src.Clone()
}

func clonePointBag(src domain.PointBag) domain.PointBag {
	if src == nil {
		return domain.NewPointBag()
	}
	return src.Clone()
}
