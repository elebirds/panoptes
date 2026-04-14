// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 定义领域模型的单位结算事件与领域类型。

package domain

// UnitResolutionAction 是 resolving 链识别的最小单位动作集合。
// 这里故意只保留“意图层”语义，避免把具体兵种规则硬编码进指令本身。
type UnitResolutionAction string

const (
	UnitResolutionActionMove   UnitResolutionAction = "move"
	UnitResolutionActionAttack UnitResolutionAction = "attack"
	UnitResolutionActionHold   UnitResolutionAction = "hold"
	UnitResolutionActionCharge UnitResolutionAction = "charge"
)

// UnitResolutionOrder 是人类手操、未来 AI 部长、脚本驱动共用的统一单位 resolving 意图。
// 结算层只认识这份结构，不关心命令来源，从而保证后续可复用。
type UnitResolutionOrder struct {
	PlayerID     string
	UnitID       string
	Action       UnitResolutionAction
	TargetNodeID string
	TargetUnitID string
	PathNodeIDs  []string
}

func (o UnitResolutionOrder) Normalized() UnitResolutionOrder {
	if o.Action == "" {
		o.Action = UnitResolutionActionHold
	}
	return o
}

// UnitCapabilities 使用能力组合而不是兵种继承。
// 这样 V1 的 warrior/archer/cavalry/settler 与未来新增兵种都能复用同一套结算流程。
type UnitCapabilities struct {
	Civilian    bool
	Melee       bool
	Ranged      bool
	Charge      bool
	Siege       bool
	DestroyRoad bool
}

func (c UnitCapabilities) CanAttack() bool {
	return !c.Civilian && (c.Melee || c.Ranged || c.Charge || c.Siege || c.DestroyRoad)
}

// CanReceiveMeleeRetaliation 目前只表达“是否具备基础近战反击资格”。
// 更复杂的 ZOC、借机攻击、特殊反击会在后续策略对象中扩展，而不是污染能力位。
func (c UnitCapabilities) CanReceiveMeleeRetaliation() bool {
	return c.Melee
}
