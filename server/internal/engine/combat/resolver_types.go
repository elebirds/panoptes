package combat

import (
	"sort"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/yohamta/donburi"
)

type CombatResolver interface {
	Resolve(world donburi.World, state *domain.GameState) []event.Event
}

// ResolutionPhase 对应规格中的一个稳定结算阶段。
// 未来即使从单步 WEGO 升级到多时间步 WEGO，这种“阶段 + 上下文”的组织方式仍可保留。
type ResolutionPhase interface {
	Apply(ctx *ResolutionContext)
}

// OrderResolver 负责把统一 UnitResolutionOrder 解释为可执行的本回合计划。
// move/attack/hold/charge 分开实现，避免一个大函数里写满动作分支。
type OrderResolver interface {
	Action() domain.UnitResolutionAction
	Plan(ctx *ResolutionContext, unit SnapshotUnit) *OrderPlan
}

// BlockRule 统一定义“什么会阻断移动”。
// V1 使用静态快照阻断，后续做多时间步时可以替换成动态阻断策略。
type BlockRule interface {
	SourceFor(ctx *ResolutionContext, unit SnapshotUnit, pos domain.Position) (BlockSource, bool)
}

// ConflictDetector 把边冲突、节点冲突拆成独立检测器。
// 这样未来新增三方冲突、追及冲突时可以并列增加实现，而不必重写主流程。
type ConflictDetector interface {
	Detect(ctx *ResolutionContext) []domain.Conflict
}

// RetaliationPolicy 统一托管反击判定，避免反击规则散落在 attack / charge / conflict 里。
type RetaliationPolicy interface {
	CanRetaliate(ctx *ResolutionContext, attackerID, defenderID string) bool
}

// DamageResolver 统一处理伤害数值，便于后续扩展地形、Buff、克制和技能。
type DamageResolver interface {
	Melee(ctx *ResolutionContext, attackerID, defenderID string, pos domain.Position, bonus float64) int
	Ranged(ctx *ResolutionContext, attackerID, defenderID string, pos domain.Position) int
}

// TerrainCostPolicy 负责解释静态地形在移动层面的语义。
// 未来若要加入科技、天气、地块改良，只需要替换这里，不必改 A* 或结算主流程。
type TerrainCostPolicy interface {
	StepCost(world donburi.World, pos domain.Position, profile domain.MovementProfile) int
	IsBlocked(world donburi.World, pos domain.Position, profile domain.MovementProfile) bool
}

// RoutePlanner 处理战略层的加权寻路与路线摘要。
// 持久行军预览和单步 WEGO 的移动规划共用同一个 planner。
type RoutePlanner interface {
	FindPath(world donburi.World, start, goal domain.Position, profile domain.MovementProfile) ([]domain.Position, bool)
	BuildPreview(world donburi.World, state *domain.GameState, unitID, destinationNodeID string) (domain.RoutePreview, bool)
}

// TurnSegmentPlanner 负责把整条路径切成“每回合能走到哪”。
// 这样 V1 持久行军和未来多时间步模拟都能复用同一套分段逻辑。
type TurnSegmentPlanner interface {
	Reachable(world donburi.World, path []domain.Position, profile domain.MovementProfile) (candidate domain.Position, fallback domain.Position)
	BuildStops(world donburi.World, path []domain.Position, profile domain.MovementProfile) []domain.Position
}

// SnapshotUnit 是战斗回合起点的只读视图。
// 后续阶段只读取快照，不直接回头读 ECS 当前状态，以保证结算确定性。
type SnapshotUnit struct {
	UnitID       string
	PlayerID     string
	Type         domain.UnitType
	Position     domain.Position
	HP           int
	MaxHP        int
	Attack       int
	AttackRange  int
	MoveRange    int
	Movement     domain.MovementProfile
	Capabilities domain.UnitCapabilities
	Order        domain.UnitResolutionOrder
}

// BlockSource 记录阻断来源，既能表达敌方单位，也能表达敌方建筑/城堡。
type BlockSource struct {
	Kind     string
	Owner    string
	Position domain.Position
	UnitID   string
	NodeID   string
}

// CombatSnapshot 是整个单步 WEGO 的输入基线。
// V1 明确规定：阻断格基于这里生成，并在本次结算过程中保持不变。
type CombatSnapshot struct {
	Units          map[string]SnapshotUnit
	OrderedUnitIDs []string
	BlockSources   map[domain.Position]BlockSource
}

// OrderPlan 是某个单位在“结算前半段”得到的执行计划。
// 它只描述候选落点、回退落点、潜在攻击目标，不直接改世界状态。
type OrderPlan struct {
	UnitID         string
	Action         domain.UnitResolutionAction
	Start          domain.Position
	Path           []domain.Position
	Candidate      domain.Position
	Fallback       domain.Position
	BlockedAt      *domain.Position
	AttackTargetID string
	ChargeTargetID string
}

// ResolutionContext 是本次战斗结算的唯一工作区。
// 所有阶段都围绕它读写中间结果，从而把“快照、规划、冲突、伤害、事件输出”串成一条清晰流水线。
type ResolutionContext struct {
	World             donburi.World
	State             *domain.GameState
	Snapshot          CombatSnapshot
	Plans             map[string]*OrderPlan
	ActualPositions   map[string]domain.Position
	CurrentHP         map[string]int
	DeadUnits         map[string]bool
	Conflicts         []domain.Conflict
	EdgeConflictUnits map[string]bool
	NodeConflictUnits map[string]bool
	Events            []event.Event
	OrderResolvers    map[domain.UnitResolutionAction]OrderResolver
	BlockRule         BlockRule
	ConflictDetectors []ConflictDetector
	RetaliationPolicy RetaliationPolicy
	DamageResolver    DamageResolver
	RoutePlanner      RoutePlanner
	TurnPlanner       TurnSegmentPlanner
}

func (ctx *ResolutionContext) UnitIDs() []string {
	out := make([]string, len(ctx.Snapshot.OrderedUnitIDs))
	copy(out, ctx.Snapshot.OrderedUnitIDs)
	return out
}

func (ctx *ResolutionContext) SnapshotUnit(unitID string) (SnapshotUnit, bool) {
	unit, ok := ctx.Snapshot.Units[unitID]
	return unit, ok
}

func (ctx *ResolutionContext) CurrentPosition(unitID string) domain.Position {
	if pos, ok := ctx.ActualPositions[unitID]; ok {
		return pos
	}
	if unit, ok := ctx.Snapshot.Units[unitID]; ok {
		return unit.Position
	}
	return domain.Position{}
}

func (ctx *ResolutionContext) HP(unitID string) int {
	if hp, ok := ctx.CurrentHP[unitID]; ok {
		return hp
	}
	if unit, ok := ctx.Snapshot.Units[unitID]; ok {
		return unit.HP
	}
	return 0
}

func (ctx *ResolutionContext) SetHP(unitID string, hp int) {
	ctx.CurrentHP[unitID] = hp
}

func (ctx *ResolutionContext) IsDead(unitID string) bool {
	return ctx.DeadUnits[unitID] || ctx.HP(unitID) <= 0
}

func sortConflictSlice(conflicts []domain.Conflict) {
	// 结算事件必须稳定排序，否则同输入多次运行会得到不同的事件顺序。
	sort.Slice(conflicts, func(i, j int) bool {
		if conflicts[i].ConflictType != conflicts[j].ConflictType {
			return conflicts[i].ConflictType < conflicts[j].ConflictType
		}
		if conflicts[i].Location != conflicts[j].Location {
			if conflicts[i].Location.X != conflicts[j].Location.X {
				return conflicts[i].Location.X < conflicts[j].Location.X
			}
			return conflicts[i].Location.Y < conflicts[j].Location.Y
		}
		if conflicts[i].UnitAID != conflicts[j].UnitAID {
			return conflicts[i].UnitAID < conflicts[j].UnitAID
		}
		return conflicts[i].UnitBID < conflicts[j].UnitBID
	})
}
