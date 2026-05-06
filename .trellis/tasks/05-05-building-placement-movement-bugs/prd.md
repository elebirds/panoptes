# investigate building placement, movement blocking, and build ghost preview

## Goal

排查并修复当前建造相关回归，重点解决“在服务端已接受/客户端已进入合法建造流程后，没有绿色建筑虚影提示”的问题，同时不破坏既有建造与移动链路。

## What I already know

* 仓库中已经有一个与建造放置 / 移动阻塞相关的在研任务，可以直接复用其上下文。
* 用户本轮新增的具体现象是：合法的建造指令下达以后，没有出现预期的绿色建筑虚影。
* 按项目规范，客户端只能做展示和输入采集，不能在客户端实现新的游戏合法性校验。
* 因此绿色建筑虚影应当是基于现有客户端展示状态、规划草案或服务端返回状态来渲染，而不是本地重新推导规则。

## Assumptions (temporary)

* 问题大概率发生在客户端建造预览 / planning draft / presenter 绑定链路，而不是协议定义本身。
* 如果后端已正确接受建造命令，则缺失的更可能是“建造后反馈状态没有驱动到绿色虚影渲染”，或渲染被过早清理。

## Open Questions

* 绿色虚影依赖的是 build preview、planning draft，还是 game state 中的某种临时展示标记？
* 当前回归是数据没有传到 presenter，还是 presenter 收到数据但没有实例化 / 着色 / 显示？

## Requirements

* 定位合法建造后绿色建筑虚影缺失的具体根因。
* 修复客户端展示链路，恢复合法建造后的绿色建筑虚影提示。
* 修复必须遵守“客户端纯展示层”约束，不得新增本地合法性计算。
* 验证修复后不会破坏现有建造输入、预览清理和相关移动/放置流程。

## Acceptance Criteria

* [ ] 能明确说明为什么合法建造后没有绿色建筑虚影。
* [ ] 修复后，合法建造流程会再次显示绿色建筑虚影提示。
* [ ] 修复不引入客户端本地规则校验，也不修改生成协议代码。
* [ ] 相关检查或测试通过，至少覆盖受影响脚本的编译/静态验证。

## Definition of Done (team quality bar)

* Tests added/updated (unit/integration where appropriate)
* Lint / typecheck / CI green
* Docs/notes updated if behavior changes
* Rollout/rollback considered if risky

## Out of Scope

* 与本问题无关的 UI 美术重做。
* 额外扩展新的建造规则或协议重构。

## Technical Approach

先检查客户端建造输入到绿色虚影渲染的完整链路：命令触发、planning draft / preview 状态写入、presenter 订阅、ghost 实例创建与着色、清理时机；如果发现前后端状态边界不一致，只修复展示同步，不在客户端补规则。

## Technical Notes

* 优先检查 `client/Assets/Scripts/Runtime` 下与 build preview、planning draft、map input、presenter 相关的代码。
* 需要确认是否近期 reactive migration / binder 拆分导致订阅链路丢失。
