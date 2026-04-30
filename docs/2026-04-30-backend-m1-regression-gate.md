# Panoptes M1 后端回归门记录

> 日期：2026-04-30  
> 范围：M1 后端规则底座长期化  
> 结论：通过，可以进入 M2 道路/连通与网络规则任务

## 1. 验证命令

| 检查 | 命令 | 结果 |
|---|---|---|
| 全量测试 | `cd server && go test -count=1 ./...` | 通过 |
| 全量构建 | `cd server && go build ./...` | 通过 |
| Go vet | `cd server && go vet ./...` | 通过 |
| 协议 lint | `cd protocol && go run github.com/bufbuild/buf/cmd/buf@latest lint` | 通过 |
| diff 空白检查 | `git diff --check` | 通过 |

备注：`make lint` 依赖本机 `buf` 可执行文件，当前环境没有安装该命令；本次用 `go run github.com/bufbuild/buf/cmd/buf@latest lint` 覆盖协议 lint。

## 2. Diff 范围确认

M1 的已跟踪文件改动集中在：

- Trellis 后端规范：`.trellis/spec/backend/directory-structure.md`
- 后端架构文档：`docs/SERVER_RUNTIME_ARCHITECTURE.md`
- 规则底座代码与测试：`server/internal/domain`, `server/internal/event`, `server/internal/game`

M1 新增文件集中在：

- Final/M0/M1 设计与验收文档：`docs/2026-04-30-backend-*.md`
- M1 Trellis 任务目录：`.trellis/tasks/04-30-m1-*`
- 事件审计契约测试：`server/internal/event/audit_contract_test.go`

没有发现未说明的 `protocol/`, `data/`, `db/`, `client/` 变更，也没有手动修改生成代码目录。

## 3. M1 子任务状态

| 子任务 | 结论 |
|---|---|
| M1.1 Resolving Stage Contract | 完成；stage 顺序与 fatal short-circuit 有测试和文档 |
| M1.2 Event Audit Contract | 完成；事件分类、projection 过滤和审计契约有测试和文档 |
| M1.3 Reserved Command Boundary | 完成；未接线命令稳定拒绝且不写 planning/resolving |
| M1.4 Headless Acceptance Harness | 完成；后端-only 验收 conventions 与场景约束已记录 |
| M1.5 Durable State vs Runtime State | 完成；durable/runtime/truth/observed/reported 边界已记录 |
| M1.6 Backend Regression Gate | 完成；质量门通过 |

## 4. M2 入口风险

- M2 可以从道路/连通规则开始，但仍需要先设计道路状态的权威归属：地图节点状态、玩家订单、事件审计、projection 输出。
- M1 只稳定拒绝 `build_road`, `repair_road`, `build_improvement`, `repair_improvement`，没有实现这些命令的真实效果。
- 物流、仓储、优先级 profile、信息不对称和大臣默认执行仍是后续里程碑，不能在 M2 隐式混入。
- 未来如果 M2 修改 proto 或静态数据，必须从源文件和生成链路进入，不能直接编辑生成文件。
