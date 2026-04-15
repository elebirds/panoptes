// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-15 00:00:00 +0800
// Description: 使用 headless harness 验证调试支持模块的手工联调流程。

package debug

import (
	"os"
	"testing"
	"time"

	"github.com/elebirds/panoptes/internal/game/scenario"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
)

func TestHarnessRoundTripSmoke(t *testing.T) {
	if os.Getenv("PANOPTES_RUN_DEBUG_INTEGRATION") != "1" {
		t.Skip("set PANOPTES_RUN_DEBUG_INTEGRATION=1 to run manual integration validation")
	}

	def, err := scenario.ResearchUnlockBuild()
	if err != nil {
		t.Fatalf("ResearchUnlockBuild() error = %v", err)
	}

	h, err := NewHarness(def)
	if err != nil {
		t.Fatalf("NewHarness() error = %v", err)
	}
	if err := h.Start(); err != nil {
		t.Fatalf("Start() error = %v", err)
	}

	if _, err := h.WaitPlanningStart("player-1", 1, 2*time.Second); err != nil {
		t.Fatalf("WaitPlanningStart(turn=1) error = %v", err)
	}
	t.Log("✓ PlanningStart 已送达")

	if err := h.InjectPlanningCommand("player-1", "req-research", &pb.PlanningCommand{
		Body: &pb.PlanningCommand_SetResearchTarget{
			SetResearchTarget: &pb.MsgSetResearchTarget{TechnologyId: "agri_unlock_farm"},
		},
	}); err != nil {
		t.Fatalf("InjectPlanningCommand() error = %v", err)
	}
	t.Log("✓ 正式 planning 链路已接受研究指令")

	if err := h.SubmitTurn("player-1"); err != nil {
		t.Fatalf("SubmitTurn() error = %v", err)
	}
	record, err := h.WaitSettlement("player-1", 1, 3*time.Second)
	if err != nil {
		t.Fatalf("WaitSettlement(turn=1) error = %v", err)
	}
	if record.Settlement == nil {
		t.Fatalf("turn settlement is nil")
	}
	t.Log("✓ TurnSettlement 已记录")

	if _, err := h.WaitPlanningStart("player-1", 2, 2*time.Second); err != nil {
		t.Fatalf("WaitPlanningStart(turn=2) error = %v", err)
	}
	t.Log("✓ 对局继续进入下一回合 planning")
}
