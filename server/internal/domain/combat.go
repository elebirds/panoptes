package domain

// CombatAction 是结算引擎识别的最小动作集合。
// 这里故意只保留“意图层”语义，避免把具体兵种规则硬编码进指令本身。
type CombatAction string

const (
	CombatActionMove   CombatAction = "move"
	CombatActionAttack CombatAction = "attack"
	CombatActionHold   CombatAction = "hold"
	CombatActionCharge CombatAction = "charge"
	CombatActionDeploy CombatAction = "deploy"
)

// CombatOrder 是人类手操、未来 AI 部长、脚本驱动共用的统一战斗意图。
// 结算层只认识这份结构，不关心命令来源，从而保证后续可复用。
type CombatOrder struct {
	PlayerID     string
	UnitID       string
	Action       CombatAction
	TargetNodeID string
	TargetUnitID string
}

func (o CombatOrder) Normalized() CombatOrder {
	if o.Action == "" {
		o.Action = CombatActionHold
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
