// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载后端测试/调试场景的构造与布置辅助逻辑。

package scenario

import (
	"strings"
	"testing"
)

func TestPlaceBuildingAtNodeReportsMissingNode(t *testing.T) {
	t.Parallel()

	def, err := ResearchUnlockBuild()
	if err != nil {
		t.Fatalf("ResearchUnlockBuild() error = %v", err)
	}
	_, err = placeBuildingAtNode(def.State, "farm", "player-1", "A1", "missing")
	if err == nil {
		t.Fatalf("placeBuildingAtNode() error is nil")
	}
	if !strings.Contains(err.Error(), "missing node missing") {
		t.Fatalf("placeBuildingAtNode() error = %q, want missing node", err)
	}
}
