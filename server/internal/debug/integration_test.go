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

	pb "github.com/elebirds/panoptes/internal/gen/proto"
)

func TestHarnessRoundTripSmoke(t *testing.T) {
	if os.Getenv("PANOPTES_RUN_DEBUG_INTEGRATION") != "1" {
		t.Skip("set PANOPTES_RUN_DEBUG_INTEGRATION=1 to run manual integration validation")
	}

	def := newRealContentHappyPathDefinition(t)
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
			SetResearchTarget: &pb.MsgSetResearchTarget{TechnologyId: "agrarian_foundations"},
		},
	}); err != nil {
		t.Fatalf("InjectPlanningCommand() error = %v", err)
	}
	t.Log("✓ 已注入真实内容科技研究指令")

	if err := h.SubmitTurn("player-1"); err != nil {
		t.Fatalf("SubmitTurn() error = %v", err)
	}
	record, err := h.WaitGameSync("player-1", 1, 3*time.Second)
	if err != nil {
		t.Fatalf("WaitGameSync(turn=1) error = %v", err)
	}
	if record.GameSync == nil {
		t.Fatalf("game sync is nil")
	}
	t.Log("✓ GameSync 已记录，科技完成可观测")

	if _, err := h.WaitPlanningStart("player-1", 2, 2*time.Second); err != nil {
		t.Fatalf("WaitPlanningStart(turn=2) error = %v", err)
	}
	t.Log("✓ 对局继续进入下一回合 planning")

	if err := h.InjectPlanningCommand("player-1", "req-build-farm", &pb.PlanningCommand{
		Body: &pb.PlanningCommand_BuildStructure{
			BuildStructure: &pb.MsgBuildStructure{NodeId: "B2", BuildingTypeId: "farm", CityId: "A2"},
		},
	}); err != nil {
		t.Fatalf("InjectPlanningCommand(build) error = %v", err)
	}
	if err := h.SubmitTurn("player-1"); err != nil {
		t.Fatalf("SubmitTurn(turn=2) error = %v", err)
	}
	turn2, err := h.WaitGameSync("player-1", 2, 3*time.Second)
	if err != nil {
		t.Fatalf("WaitGameSync(turn=2) error = %v", err)
	}
	if turn2.GameSync == nil {
		t.Fatalf("turn 2 game sync is nil")
	}
	t.Log("✓ 已记录农场建造与开拓者产出")

	settlerID := findOwnedUnitIDByType(t, h.room.State(), "player-1", "settler")
	clearSelectedRecipe(t, h.room.State(), "A2")

	if _, err := h.WaitPlanningStart("player-1", 3, 2*time.Second); err != nil {
		t.Fatalf("WaitPlanningStart(turn=3) error = %v", err)
	}
	if err := h.InjectPlanningCommand("player-1", "req-settle", &pb.PlanningCommand{
		Body: &pb.PlanningCommand_IssueUnitOrder{
			IssueUnitOrder: &pb.MsgIssueUnitOrder{
				UnitId:       settlerID,
				Action:       "settle_city",
				TargetNodeId: "D2",
			},
		},
	}); err != nil {
		t.Fatalf("InjectPlanningCommand(settle) error = %v", err)
	}
	if err := h.SubmitTurn("player-1"); err != nil {
		t.Fatalf("SubmitTurn(turn=3) error = %v", err)
	}
	if _, err := h.WaitGameSync("player-1", 3, 3*time.Second); err != nil {
		t.Fatalf("WaitGameSync(turn=3) error = %v", err)
	}
	t.Log("✓ 新城建立成功，进入后续生产链路")

	if _, err := h.WaitPlanningStart("player-1", 4, 2*time.Second); err != nil {
		t.Fatalf("WaitPlanningStart(turn=4) error = %v", err)
	}
	if err := h.InjectPlanningCommand("player-1", "req-build-barracks", &pb.PlanningCommand{
		Body: &pb.PlanningCommand_BuildStructure{
			BuildStructure: &pb.MsgBuildStructure{NodeId: "E2", BuildingTypeId: "barracks", CityId: "D2"},
		},
	}); err != nil {
		t.Fatalf("InjectPlanningCommand(build barracks) error = %v", err)
	}
	if err := h.SubmitTurn("player-1"); err != nil {
		t.Fatalf("SubmitTurn(turn=4) error = %v", err)
	}
	if _, err := h.WaitGameSync("player-1", 4, 3*time.Second); err != nil {
		t.Fatalf("WaitGameSync(turn=4) error = %v", err)
	}
	t.Log("✓ 兵营建造完成，等待训练步兵")

	if _, err := h.WaitPlanningStart("player-1", 5, 2*time.Second); err != nil {
		t.Fatalf("WaitPlanningStart(turn=5) error = %v", err)
	}
	if err := h.SubmitTurn("player-1"); err != nil {
		t.Fatalf("SubmitTurn(turn=5) error = %v", err)
	}
	if _, err := h.WaitGameSync("player-1", 5, 3*time.Second); err != nil {
		t.Fatalf("WaitGameSync(turn=5) error = %v", err)
	}
	t.Log("✓ 步兵训练已推进一回合")

	if _, err := h.WaitPlanningStart("player-1", 6, 2*time.Second); err != nil {
		t.Fatalf("WaitPlanningStart(turn=6) error = %v", err)
	}
	if err := h.SubmitTurn("player-1"); err != nil {
		t.Fatalf("SubmitTurn(turn=6) error = %v", err)
	}
	if _, err := h.WaitGameSync("player-1", 6, 3*time.Second); err != nil {
		t.Fatalf("WaitGameSync(turn=6) error = %v", err)
	}
	infantryID := findOwnedUnitIDByType(t, h.room.State(), "player-1", "infantry")
	t.Log("✓ 步兵已产出，准备进入前线")

	if _, err := h.WaitPlanningStart("player-1", 7, 2*time.Second); err != nil {
		t.Fatalf("WaitPlanningStart(turn=7) error = %v", err)
	}
	if err := h.InjectPlanningCommand("player-1", "req-move-frontline", &pb.PlanningCommand{
		Body: &pb.PlanningCommand_IssueUnitOrder{
			IssueUnitOrder: &pb.MsgIssueUnitOrder{
				UnitId:       infantryID,
				Action:       "move",
				TargetNodeId: "F2",
			},
		},
	}); err != nil {
		t.Fatalf("InjectPlanningCommand(move) error = %v", err)
	}
	if err := h.SubmitTurn("player-1"); err != nil {
		t.Fatalf("SubmitTurn(turn=7) error = %v", err)
	}
	if _, err := h.WaitGameSync("player-1", 7, 3*time.Second); err != nil {
		t.Fatalf("WaitGameSync(turn=7) error = %v", err)
	}
	t.Log("✓ 步兵已推进到前线节点")

	if _, err := h.WaitPlanningStart("player-1", 8, 2*time.Second); err != nil {
		t.Fatalf("WaitPlanningStart(turn=8) error = %v", err)
	}
	if err := h.InjectPlanningCommand("player-1", "req-attack-capital", &pb.PlanningCommand{
		Body: &pb.PlanningCommand_IssueUnitOrder{
			IssueUnitOrder: &pb.MsgIssueUnitOrder{
				UnitId:       infantryID,
				Action:       "attack",
				TargetNodeId: "G2",
			},
		},
	}); err != nil {
		t.Fatalf("InjectPlanningCommand(attack) error = %v", err)
	}
	if err := h.SubmitTurn("player-1"); err != nil {
		t.Fatalf("SubmitTurn(turn=8) error = %v", err)
	}
	finalRecord, err := h.WaitGameSync("player-1", 8, 3*time.Second)
	if err != nil {
		t.Fatalf("WaitGameSync(turn=8) error = %v", err)
	}
	if finalRecord.GameOver == nil {
		t.Fatalf("turn 8 game over is nil")
	}
	t.Logf("✓ GameOver 已送达，reason=%s winner=%s", finalRecord.GameOver.GetReason(), finalRecord.Summary.WinnerID)
}
