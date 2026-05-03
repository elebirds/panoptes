// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载经济结算中配方推进拆分后的局部规则逻辑。

package economy

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
)

func TestRecipeAffordabilityRatiosUseScarcestInput(t *testing.T) {
	available := domain.NewResourceBag()
	available.Set("food", 5)
	available.Set("wood", 2)
	total := domain.NewResourceBag()
	total.Set("food", 10)
	total.Set("wood", 8)

	if got := affordabilityRatioResources(available, total); got != 0.25 {
		t.Fatalf("resource ratio = %v, want 0.25", got)
	}

	points := domain.NewPointBag()
	points.Set("labor", 3)
	pointCost := domain.NewPointBag()
	pointCost.Set("labor", 6)
	if got := affordabilityRatioPoints(points, pointCost); got != 0.5 {
		t.Fatalf("point ratio = %v, want 0.5", got)
	}
}

func TestRecipeBlockedReasonPrefersZeroAndScarcestInput(t *testing.T) {
	resources := domain.NewResourceBag()
	resources.Set("food", 10)
	points := domain.NewPointBag()
	points.Set("labor", 5)

	if got := blockedReasonForRatios(0, 0.5, resources, points); got != "insufficient_resources" {
		t.Fatalf("zero resource reason = %q, want insufficient_resources", got)
	}
	if got := blockedReasonForRatios(0.5, 0, resources, points); got != "insufficient_points" {
		t.Fatalf("zero point reason = %q, want insufficient_points", got)
	}
	if got := blockedReasonForRatios(0.25, 0.75, resources, points); got != "insufficient_resources" {
		t.Fatalf("scarce resource reason = %q, want insufficient_resources", got)
	}
	if got := blockedReasonForRatios(0.75, 0.25, resources, points); got != "insufficient_points" {
		t.Fatalf("scarce point reason = %q, want insufficient_points", got)
	}
}

func TestRecipeConsumptionMathUsesCumulativeProgress(t *testing.T) {
	total := domain.NewResourceBag()
	total.Set("food", 9)
	target := proportionalResourceBag(total, 1500, 3000)
	if got := target.Get("food"); got != 4 {
		t.Fatalf("proportional resources = %d, want 4", got)
	}

	consumed := domain.NewResourceBag()
	consumed.Set("food", 1)
	delta := subtractResourceBags(target, consumed)
	if got := delta.Get("food"); got != 3 {
		t.Fatalf("resource delta = %d, want 3", got)
	}
}
