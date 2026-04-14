package domain

// MovementProfile 把单位的机动属性从兵种配置里抽出来，
// 让战略路径规划与战术结算共用同一套移动语义。
type MovementProfile struct {
	MoveBudget int
	RoadBonus  int
	RoadCost   int
	Mounted    bool
}

func (p MovementProfile) TurnBudget() int {
	budget := p.MoveBudget + p.RoadBonus
	if budget < 1 {
		return 1
	}
	return budget
}

type MarchTurnStop struct {
	TurnIndex int
	NodeID    string
}

// RoutePreview 是服务端权威的战略路线摘要。
// 它只描述静态地形上的推荐路线，不承诺规避本回合同步冲突。
type RoutePreview struct {
	PathNodeIDs     []string
	FirstTurnNodeID string
	TotalTurns      int
	TurnStops       []MarchTurnStop
}

// ActiveMarch 持久化保存 move 指令的长期目的地与最近一次路线摘要。
// 下一回合进入战斗计划阶段时，房间会据此自动生成本回合 move 意图。
type ActiveMarch struct {
	PlayerID          string
	UnitID            string
	Action            UnitResolutionAction
	DestinationNodeID string
	LastPreview       RoutePreview
}
