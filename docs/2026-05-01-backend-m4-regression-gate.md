# Panoptes M4 后端回归门记录

> 日期：2026-05-01  
> 范围：M4 优先级物流与工业链  
> 结论：通过，可以进入 M5 科技、制度与国家修正深化

## 1. 完成范围

- 新增政策静态字段 `logistics_priority`，可按 recipe id 或 recipe tag 配置物流优先级。
- 配方结算先收集需求，再按物流优先级排序，最后消费共享库存和共享道路运量。
- M3 的道路基础运量从“每个需求可用”升级为“同一 donor -> target 城市路线在本结算阶段共享”。
- 战备政策优先保障军事 recipe，扩张政策优先保障扩张 recipe。
- 工业链短缺传播有回归场景：上游矿石当回合产出不会被同阶段下游凭空使用，下回合进入可达仓储后下游恢复。

## 2. 验证命令

| 检查 | 命令 | 结果 |
|---|---|---|
| 数据生成 | `make data-gen` | 通过 |
| 聚焦测试 | `cd server && go test ./internal/engine/economy` | 通过 |
| 全量测试 | `cd server && go test -count=1 ./...` | 通过 |
| Lint | `make lint` | 通过 |
| diff 空白检查 | `git diff --check` | 通过 |

## 3. M4 完成定义对照

| Roadmap item | 状态 |
|---|---|
| 增加 logistics demand 模型 | 完成；recipe progress stage 收集 demand candidate 后排序 |
| 增加 demand priority tier | 完成；优先级由 policy profile 输出整数 tier |
| 国策/政策提供 priority profile | 完成；`PolicyDefinition.logistics_priority` |
| 引入 priority-aware flow allocation | 完成；高优先级先消费共享库存和路线运量 |
| 多层资源链：原料、中间品、军工/工程材料 | 完成基础版；现有 raw resource -> military recipe 链进入验收 |
| 配方链和下游短缺传播 | 完成 |
| 为 min-cost flow 做算法封装 | 完成轻量预留；当前为 deterministic priority allocator，不做完整求解器 |

## 4. M5 入口边界

- M4 的 policy profile 已可被制度复用。
- M5 应把科技/制度候选、制度槽位和制度修正接入到物流优先级或运量修正上。
- 完整 min-cost flow、复杂路径成本和大臣细分优先级仍然不在 M4 内实现。
