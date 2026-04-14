package combat

import (
	"math"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/elebirds/panoptes/internal/staticdata"
)

type DamagePhase struct{}

func (DamagePhase) Apply(ctx *ResolutionContext) {
	// 先处理冲突，再处理显式 attack / charge，形成稳定事件顺序。
	// 虽然规格上属于同一伤害窗口，但内部仍需固定遍历顺序来保证联机确定性。
	for _, conflict := range ctx.Conflicts {
		ctx.Events = append(ctx.Events, event.ConflictResolvedEvent{
			UnitAID:      conflict.UnitAID,
			UnitBID:      conflict.UnitBID,
			Location:     conflict.Location,
			ConflictType: conflict.ConflictType,
		})
		resolveConflictDamage(ctx, conflict)
	}

	for _, unitID := range ctx.UnitIDs() {
		unit := ctx.Snapshot.Units[unitID]
		plan := ctx.Plans[unitID]
		if ctx.IsDead(unitID) {
			continue
		}
		switch plan.Action {
		case domain.UnitResolutionActionAttack:
			resolveExplicitAttack(ctx, unit, plan)
		case domain.UnitResolutionActionCharge:
			resolveChargeAttack(ctx, unit, plan)
		}
	}
}

type StaticSnapshotBlockRule struct{}

func (StaticSnapshotBlockRule) SourceFor(ctx *ResolutionContext, unit SnapshotUnit, pos domain.Position) (BlockSource, bool) {
	// 只读取快照阻断，不读取实时位置，这是“阻断格全回合不更新”的具体实现。
	source, ok := ctx.Snapshot.BlockSources[pos]
	if !ok || source.Owner == unit.PlayerID {
		return BlockSource{}, false
	}
	return source, true
}

type DefaultRetaliationPolicy struct{}

func (DefaultRetaliationPolicy) CanRetaliate(ctx *ResolutionContext, attackerID, defenderID string) bool {
	// 反击判定看“结算时是否仍保持近战接敌”，而不是只看声明指令。
	// 这样 move 失败被堵住的单位，会自然落入可反击状态。
	defender, ok := ctx.Snapshot.Units[defenderID]
	if !ok || defender.Capabilities.Civilian || !defender.Capabilities.CanReceiveMeleeRetaliation() {
		return false
	}
	if ctx.IsDead(attackerID) || ctx.IsDead(defenderID) {
		return false
	}
	return ctx.CurrentPosition(attackerID).DistanceTo(ctx.CurrentPosition(defenderID)) == 1
}

type DefaultDamageResolver struct{}

func (DefaultDamageResolver) Melee(ctx *ResolutionContext, attackerID, defenderID string, pos domain.Position, bonus float64) int {
	return resolveDamageAmount(ctx, attackerID, defenderID, pos, bonus)
}

func (DefaultDamageResolver) Ranged(ctx *ResolutionContext, attackerID, defenderID string, pos domain.Position) int {
	return resolveDamageAmount(ctx, attackerID, defenderID, pos, 1)
}

func resolveConflictDamage(ctx *ResolutionContext, conflict domain.Conflict) {
	a, okA := ctx.SnapshotUnit(conflict.UnitAID)
	b, okB := ctx.SnapshotUnit(conflict.UnitBID)
	if !okA || !okB || ctx.IsDead(a.UnitID) || ctx.IsDead(b.UnitID) {
		return
	}

	switch {
	case a.Capabilities.Civilian && b.Capabilities.Melee:
		killUnit(ctx, a.UnitID, b.UnitID, conflict.Location)
	case b.Capabilities.Civilian && a.Capabilities.Melee:
		killUnit(ctx, b.UnitID, a.UnitID, conflict.Location)
	case a.Capabilities.Melee && b.Capabilities.Melee:
		// 近战冲突天然互殴，属于显式 attack 之外的基础接敌伤害。
		damageUnit(ctx, b.UnitID, ctx.DamageResolver.Melee(ctx, a.UnitID, b.UnitID, conflict.Location, 1), "combat", a.UnitID, conflict.Location)
		damageUnit(ctx, a.UnitID, ctx.DamageResolver.Melee(ctx, b.UnitID, a.UnitID, conflict.Location, 1), "combat", b.UnitID, conflict.Location)
	case a.Capabilities.Melee:
		damageUnit(ctx, b.UnitID, ctx.DamageResolver.Melee(ctx, a.UnitID, b.UnitID, conflict.Location, 1), "combat", a.UnitID, conflict.Location)
	case b.Capabilities.Melee:
		damageUnit(ctx, a.UnitID, ctx.DamageResolver.Melee(ctx, b.UnitID, a.UnitID, conflict.Location, 1), "combat", b.UnitID, conflict.Location)
	}
}

func resolveExplicitAttack(ctx *ResolutionContext, attacker SnapshotUnit, plan *OrderPlan) {
	targetID := plan.AttackTargetID
	target, ok := ctx.SnapshotUnit(targetID)
	if !ok || ctx.IsDead(targetID) {
		return
	}
	// attack 以最终位置判定是否仍然合法命中，这是 attack-vs-move 规则的落点。
	if ctx.CurrentPosition(attacker.UnitID).DistanceTo(ctx.CurrentPosition(targetID)) > attacker.AttackRange {
		return
	}

	source := "combat"
	if attacker.Capabilities.Ranged && attacker.AttackRange > 1 {
		source = "ranged"
		damageUnit(ctx, targetID, ctx.DamageResolver.Ranged(ctx, attacker.UnitID, targetID, ctx.CurrentPosition(targetID)), source, attacker.UnitID, ctx.CurrentPosition(targetID))
		return
	}

	if target.Capabilities.Civilian && attacker.Capabilities.Melee {
		killUnit(ctx, targetID, attacker.UnitID, ctx.CurrentPosition(targetID))
		return
	}

	damageUnit(ctx, targetID, ctx.DamageResolver.Melee(ctx, attacker.UnitID, targetID, ctx.CurrentPosition(targetID), 1), source, attacker.UnitID, ctx.CurrentPosition(targetID))
	if ctx.RetaliationPolicy.CanRetaliate(ctx, attacker.UnitID, targetID) {
		damageUnit(ctx, attacker.UnitID, ctx.DamageResolver.Melee(ctx, targetID, attacker.UnitID, ctx.CurrentPosition(attacker.UnitID), 1), "combat", targetID, ctx.CurrentPosition(attacker.UnitID))
	}
}

func resolveChargeAttack(ctx *ResolutionContext, attacker SnapshotUnit, plan *OrderPlan) {
	targetID := plan.ChargeTargetID
	// 若目标在伤害窗口开始前已死亡，charge 只保留位移，不追加任何伤害或 bonus。
	if targetID == "" || ctx.IsDead(targetID) {
		return
	}
	target, ok := ctx.SnapshotUnit(targetID)
	if !ok {
		return
	}
	if ctx.CurrentPosition(attacker.UnitID).DistanceTo(ctx.CurrentPosition(target.UnitID)) != 1 {
		return
	}
	if target.Capabilities.Civilian {
		killUnit(ctx, targetID, attacker.UnitID, ctx.CurrentPosition(targetID))
		return
	}

	bonus := 1.0
	if cfg, ok := staticdata.Default().GetUnit(string(attacker.Type)); ok && cfg.ChargeBonus > 0 {
		bonus = cfg.ChargeBonus
	}
	damageUnit(ctx, targetID, ctx.DamageResolver.Melee(ctx, attacker.UnitID, targetID, ctx.CurrentPosition(targetID), bonus), "combat", attacker.UnitID, ctx.CurrentPosition(targetID))
	if ctx.RetaliationPolicy.CanRetaliate(ctx, attacker.UnitID, targetID) {
		damageUnit(ctx, attacker.UnitID, ctx.DamageResolver.Melee(ctx, targetID, attacker.UnitID, ctx.CurrentPosition(attacker.UnitID), 1), "combat", targetID, ctx.CurrentPosition(attacker.UnitID))
	}
}

func damageUnit(ctx *ResolutionContext, targetID string, damage int, source string, killerID string, pos domain.Position) {
	if damage <= 0 || ctx.IsDead(targetID) {
		return
	}
	nextHP := ctx.HP(targetID) - damage
	if nextHP < 0 {
		nextHP = 0
	}
	ctx.SetHP(targetID, nextHP)
	ctx.Events = append(ctx.Events, event.UnitDamagedEvent{UnitID: targetID, Damage: damage, HPAfter: nextHP, Source: source})
	if nextHP == 0 {
		ctx.DeadUnits[targetID] = true
		ctx.Events = append(ctx.Events, event.UnitDiedEvent{UnitID: targetID, KillerID: killerID, Pos: pos})
	}
}

func killUnit(ctx *ResolutionContext, targetID string, killerID string, pos domain.Position) {
	if ctx.IsDead(targetID) {
		return
	}
	ctx.SetHP(targetID, 0)
	ctx.DeadUnits[targetID] = true
	ctx.Events = append(ctx.Events, event.UnitDiedEvent{UnitID: targetID, KillerID: killerID, Pos: pos})
}

func resolveDamageAmount(ctx *ResolutionContext, attackerID, defenderID string, pos domain.Position, bonus float64) int {
	attacker, okA := ctx.SnapshotUnit(attackerID)
	defender, okB := ctx.SnapshotUnit(defenderID)
	if !okA || !okB || attacker.Attack <= 0 {
		return 0
	}

	multiplier := 1.0
	if cfg, ok := staticdata.Default().GetUnit(string(attacker.Type)); ok {
		if m, ok := cfg.Multipliers[string(defender.Type)]; ok && m > 0 {
			multiplier = m
		}
	}
	// 伤害公式先保持简单稳定：基础攻击 * 克制倍率 * 地形修正 * 行为 bonus。
	// 后续增添 Buff / 科技 / 将领效果时，优先在这里扩展而不是在各动作分支里散算。
	terrainFactor := 1.0
	if nodeEntry, ok := domain.GetNodeAt(ctx.World, pos); ok {
		node := ecs.NodeC.Get(nodeEntry)
		if terrain, ok := staticdata.Default().GetTerrain(string(node.Terrain)); ok {
			terrainFactor = math.Max(0.2, 1-terrain.DefenseBonus+terrain.AttackPenalty)
		}
	}

	dmg := int(math.Round(float64(attacker.Attack) * multiplier * terrainFactor * bonus))
	if dmg < 1 {
		dmg = 1
	}
	return dmg
}
