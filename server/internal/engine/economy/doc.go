// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 声明经济结算引擎包的职责与边界。

// Package economy 承担“本回合经济结算”这条主链。
//
// 这里刻意只处理当回合的运行态、预算、科研推进、建造与配方推进；
// 科技完成后的显式激活效果不在本包处理，而是留给 planning start 阶段。
// 这样可以把“本回合花了什么”和“下一回合开始正式生效什么”拆成两条清晰的时序线。
package economy
