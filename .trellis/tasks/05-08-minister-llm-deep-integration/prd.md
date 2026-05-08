# 大臣LLM深度集成：从润色工具到决策代理人

## Goal

将大模型从"文案润色工具"升级为"决策代理人"，实现信息失真、亲政令牌、大臣人格动态化等核心体验，让玩家真正体验到"大臣驱动游戏，领导者只能宫斗"的愿景。

**核心问题**：当前 LLM 只能润色文案，不能影响游戏状态；信息层接近透明；玩家可全权操作，没有"宫斗"的经济基础。

## What I already know

### 项目愿景（来自 GDD）
- "P 社让你当上帝，我们让你当人"
- 大臣是玩家感知世界的**唯一接口**，不是建议提示框
- AI 是游戏的**感知层和执行层**，去掉 AI 游戏无法运行
- 信息不对称三层结构：`truth → observed → reported`
- 亲政令牌：每回合 3 个，用于绕过大臣直接操作
- 大臣会越权、会抗命、会撒谎

### 代码现状（来自研究）
- **LLM 只能润色**：`MinisterEngine.PolishDraft()` 只修改 Title/Summary/Rationale/RiskNote
- **Actions 被忽略**：`ExecuteActions` 存在但当前 MVP 阶段不执行
- **信息失真是元数据级**：只有 omitted/delayed/misread 计数，没有内容级改写
- **亲政令牌不存在**：最接近的是 `TokensLeft`（通用行动点），不是亲政系统
- **大臣属性不影响游戏**：Personality/Loyalty/Ambition 只注入提示词，不影响机制
- **记忆是内存级**：5 条滑动窗口，无持久化，无语义搜索
- **单次 LLM 调用**：无对话历史，记忆作为文本注入用户提示词
- **单一观察摘要**：所有大臣角色共享同一份观察数据，无角色级过滤

### 关键架构约束
1. **规则权威**：LLM 不决定可执行目标，规则层先生成
2. **中文安全**：所有玩家可见文本必须简体中文
3. **JSON 输出**：LLM 必须输出裸 JSON
4. **线程安全**：`MinisterEngine.mu` 保护共享状态
5. **回退链**：LLM nil → 硬编码 JSON → 解析错误 → 回退 JSON

## Assumptions (temporary)

- 大臣的"失真"行为应该通过 LLM 提示词工程实现，而非硬编码规则
- 亲政令牌应该与现有 `TokensLeft` 系统整合，而非独立系统
- 信息失真应该从元数据级升级到内容级，让 LLM 生成带有主观色彩的叙述
- 大臣属性（忠诚度、野心）应该影响 LLM 的提示词，进而影响输出
- MVP 阶段先实现"信息失真"和"亲政令牌"，"大臣人格动态化"作为后续迭代

## Decision (ADR-lite)

### 决策 1：亲政令牌与 TokensLeft 的关系

**Context**：现有 TokensLeft 只用于 reveal 操作，与亲政令牌设计意图高度重叠。

**Decision**：直接将 TokensLeft 改造为亲政令牌，取消"通用行动点"概念。

**Consequences**：
- 语义清晰：亲政令牌 = "君主特权"
- 符合愿景：大臣默认执行，玩家只在关键点介入
- 改动最小：复用现有 TokensLeft 架构

### 决策 2：LLM 参与程度开关

**Context**：不同玩家对"大臣驱动"的接受程度不同。

**Decision**：实现两种参与模式：

**强参与模式**（理想状态）：
- 玩家只能选择拒绝/接受/会话引导
- 想越过大臣做决策需要消耗亲政令牌
- 有好感度机制（大臣对玩家的态度影响行为）
- 大臣默认执行大部分操作

**弱参与模式**：
- 玩家可以自主决策
- 大臣只是建议，不自动执行
- 亲政令牌用于"查看真实状态"等特权操作

**Consequences**：
- 满足不同玩家偏好
- 强参与模式实现"宫斗"体验
- 弱参与模式保持传统策略游戏体验

## Decision (ADR-lite) - 补充

### 决策 3：信息失真实现方式

**Context**：信息失真是"宫斗"体验的核心。

**Decision**：方案 A - LLM 提示词注入失真参数。

**Consequences**：
- 利用 LLM 的生成能力，失真自然、多样
- 大臣的忠诚度、性格影响叙述
- 同一份 truth 产生不同叙述

### 决策 4：LLM Actions 执行范围

**Context**：某些行动（如兵种移动）不可穷举。

**Decision**：方案 C - 混合方案。
- 可穷举行动（建造、研究、政策）：从候选列表选择
- 不可穷举行动（兵种移动）：LLM 生成意图，规则层验证
- 特殊行动（否决、紧急调兵）：LLM 自由生成，规则层验证

**Consequences**：
- 防止 LLM 幻觉（不会生成不存在的行动）
- 保持游戏平衡（规则层验证）
- LLM 有自主性（可以选择移动到哪）

### 决策 5：大臣记忆持久化

**Context**：大臣需要"历史感"，但实现复杂度需要控制。

**Decision**：方案 B - 内存级记忆 + 行为影响。

**Consequences**：
- 实现简单，无持久化开销
- 大臣有"学习"能力，符合角色设定
- 服务器重启后行为重置（可接受）

### 决策 6：大臣行为影响系统

**Context**：大臣行为受多维度因素影响。

**Decision**：多维度行为影响系统。
- 能力 (Ability)：观察质量、决策准确性
- 忠诚 (Loyalty)：信息失真程度、是否越权
- 野心 (Ambition)：是否越权、是否争功
- 好感度 (Favor)：汇报态度、建议积极性
- 记忆 (Memory)：行为模式调整

**Consequences**：
- 大臣行为复杂、真实
- 玩家需要"宫斗"——管理大臣关系
- 增加游戏深度

### 决策 7：性格系统

**Context**：性格影响大臣的行为方式。

**Decision**：四维度性格系统。
- 谨慎度 (Cautiousness)：鲁莽 ↔ 谨慎
- 果断度 (Decisiveness)：优柔寡断 ↔ 果断
- 忠诚度倾向 (Loyalty Tendency)：狡猾 ↔ 忠诚
- 野心表现 (Ambition Style)：隐忍 ↔ 张扬

**Consequences**：
- 大臣行为多样化
- 性格与属性交互，产生复杂行为
- 增加角色扮演深度

## Open Questions

（所有关键问题已解决）

## Requirements (evolving)

### 核心需求
1. **信息失真层**：大臣的汇报应该带有主观色彩，而非客观数据的翻译
2. **亲政令牌系统**：玩家每回合有限的"亲政"资源，用于绕过大臣直接操作
3. **LLM Actions 执行**：LLM 生成的行动应该进入真实执行流程
4. **大臣人格动态化**：大臣属性应该影响行为，而非仅注入提示词
5. **LLM 参与程度开关**：强参与模式（大臣驱动）+ 弱参与模式（玩家自主）

### 次要需求
6. **双轨信息呈现**：叙事轨（LLM）+ 数值轨（规则），可能矛盾
7. **大臣间互动**：大臣互相弹劾、争功、推卸责任
8. **叙事系统**：节点命名、历史叙事、胜负总结

## Acceptance Criteria (evolving)

### MVP 核心体验（本次实现）
- [x] 大臣汇报带有主观色彩，同一份 truth 产生不同叙述（信息失真）
- [x] 玩家每回合有 3 个亲政令牌，用于查看真实状态或亲自操作（亲政令牌）
- [x] LLM 选择的行动进入锁定/批准流程（LLM Actions）
- [x] 大臣忠诚度影响汇报的失真程度（行为影响）
- [x] 大臣记忆影响后续行为（被拒绝→保守）（行为影响）
- [x] 支持强参与模式和弱参与模式（LLM 参与程度开关）

### 已完成补充（2026-05-08）
- `MINISTER_LLM_PARTICIPATION_MODE=weak|strong` 已接入后端配置。
- 弱模式保持既有玩家自由 planning 命令行为。
- 强模式下，直接玩法命令必须先通过 `direct_command` 消耗亲政令牌进入 mandate mode；否则 planning 层会在命令处理前拒绝并保持状态不变。
- LLM report `actions` 不再直接落 planning order，而是先转为 `MinisterDraft` / `MinisterProposalView`，等待玩家通过现有 accept/reject 指令批准或否决。
- 每回合默认 `tokens_per_turn=3` 作为亲政令牌；`reveal`、`mandate_override`、`direct_command` 走同一 token 消耗语义。
- `BuildMinisterObservationSummary` 会把 `report_mode`、`report_confidence`、`reported_omitted`、`reported_delayed`、`reported_misread` 注入大臣 prompt，让 LLM 把规则层失真 metadata 转成叙事奏报。
- 同一观察摘要会因大臣画像产生不同 system prompt：谨慎大臣强调风险边界，低忠诚/高野心大臣更倾向淡化不利信息或强调自身功劳。
- `MinisterMemory` 根据玩家 accept/reject/stale 反馈调整 favor；低 favor prompt 会提示大臣因多次被否决而更保守、更强调风险。
- 2026-05-08 新增回归覆盖：`prompt_test` 验证同观察不同画像的主观压力，`minister_prompt_test` 验证失真 metadata 进入观察摘要。

### 后续迭代
- [ ] 客户端同时展示叙事轨和数值轨（双轨信息呈现）
- [ ] 大臣间互动（弹劾、争功）
- [ ] 叙事系统（节点命名、历史叙事）

## Technical Approach

### 分阶段实现计划

#### Phase 1：基础架构（1-2 天）
1. **亲政令牌系统**
   - 将 TokensLeft 语义改为亲政令牌
   - 新增亲政操作：否决大臣行动、亲自下达命令
   - 实现亲政令牌消耗逻辑

2. **大臣属性扩展**
   - 扩展 MinisterProfile，增加性格四维度
   - 扩展 MinisterMemory，增加好感度追踪
   - 实现属性动态变化机制

#### Phase 2：信息失真（2-3 天）
3. **LLM 提示词注入**
   - 重构提示词系统，注入大臣属性（能力、忠诚、野心、好感度、性格）
   - 实现观察摘要的角色级过滤
   - 实现信息失真的提示词注入

4. **双轨信息呈现**
   - 后端支持同时发送叙事轨（LLM）和数值轨（规则）
   - 客户端支持双轨展示（可选）

#### Phase 3：LLM Actions（3-4 天）
5. **行动候选列表**
   - 规则层生成可穷举行动列表（建造、研究、政策）
   - 实现行动验证逻辑

6. **LLM 行动选择**
   - LLM 从候选列表中选择并排序
   - 实现行动锁定/批准流程
   - 实现亲政令牌消耗（绕过大臣）

7. **不可穷举行动**
   - LLM 生成移动意图（目标节点）
   - 规则层验证移动合法性
   - 实现移动执行逻辑

#### Phase 4：行为影响（2-3 天）
8. **好感度系统**
   - 实现好感度变化逻辑
   - 好感度影响 LLM 提示词

9. **记忆行为影响**
   - 记忆影响大臣行为模式
   - 实现行为调整逻辑

10. **LLM 参与程度开关**
    - 实现强参与模式和弱参与模式
    - 模式切换逻辑

#### Phase 5：集成测试（1-2 天）
11. **端到端测试**
    - 测试完整流程：大臣汇报 → 玩家决策 → 大臣执行
    - 测试信息失真效果
    - 测试亲政令牌消耗

12. **平衡性调整**
    - 调整属性参数（忠诚度、好感度变化值）
    - 调整信息失真程度
    - 调整亲政令牌数量

### 关键文件修改

| 文件 | 修改内容 |
|------|----------|
| `server/internal/domain/state.go` | 扩展 PlayerState，增加亲政令牌语义 |
| `server/internal/staticdata/units_model.go` | 扩展 MinisterProfile，增加性格维度 |
| `server/internal/engine/minister/memory.go` | 扩展记忆系统，增加好感度追踪 |
| `server/internal/engine/minister/prompt.go` | 重构提示词，注入属性和失真参数 |
| `server/internal/engine/minister/engine.go` | 实现 LLM Actions 选择逻辑 |
| `server/internal/engine/minister/parser.go` | 扩展行动解析，支持新行动类型 |
| `server/internal/game/planning/reveal_submit.go` | 实现亲政令牌消耗逻辑 |
| `server/internal/game/session/minister_draft.go` | 实现行动候选列表生成 |
| `protocol/panoptes/proto/v1/minister.proto` | 扩展协议，支持新消息类型 |

## Definition of Done (team quality bar)

- Tests added/updated (unit/integration where appropriate)
- Lint / typecheck / CI green
- Docs/notes updated if behavior changes
- Rollout/rollback considered if risky

## Expansion Sweep (DIVERGE)

### 1. Future evolution（未来演化）

- **大臣间互动**：大臣互相弹劾、争功、推卸责任，增加"宫斗"深度
- **叙事系统**：节点命名、历史叙事、胜负总结，增强沉浸感
- **大臣记忆持久化**：跨服务器重启保持记忆，大臣有完整"历史感"
- **对话式 LLM 交互**：玩家可以与大臣对话，而非单向汇报
- **大臣成长系统**：大臣能力随时间提升，忠诚度/野心动态变化

### 2. Related scenarios（相关场景）

- **多人游戏**：不同玩家的大臣可能互相影响（如间谍、策反）
- **PVE 国家**：AI 控制的国家也有大臣系统，增加对抗深度
- **大臣叛变**：忠诚度极低的大臣可能叛变，需要处理
- **大臣死亡**：大臣可能死亡（战死、暗杀），需要继承机制

### 3. Failure & edge cases（失败/边缘案例）

- **LLM 服务不可用**：回退到规则模式，大臣行为变得机械
- **LLM 幻觉**：生成非法行动，规则层拒绝并记录
- **大臣属性极端值**：忠诚度 0 或 100 时的行为边界
- **好感度极端值**：好感度 0 时大臣可能消极怠工，好感度 100 时可能过度积极
- **记忆窗口溢出**：超过 5 条记忆时的淘汰策略
- **信息失真过度**：大臣汇报与真实状态差异过大，玩家失去信任

## Out of Scope (explicit)

- 大臣间互动（弹劾、争功）— 后续迭代
- 叙事系统（节点命名、历史叙事）— 后续迭代
- 大臣记忆持久化 — 后续迭代
- 对话式 LLM 交互 — 后续迭代
- 大臣成长系统 — 后续迭代
- 多人游戏大臣互动 — 后续迭代
- 大臣叛变/死亡机制 — 后续迭代

## Technical Notes

- 现有架构分析：`.trellis/tasks/05-08-minister-llm-deep-integration/research/codebase-analysis.md`
- 关键扩展点：`MinisterEngine`、`MinisterMemory`、`ExecuteActions`、`BuildInformationReport`
- 约束：规则权威、中文安全、JSON 输出、线程安全、回退链

## Research References

- [`research/codebase-analysis.md`](research/codebase-analysis.md) — 大臣系统 4 层架构分析，27 个源文件，关键接口和扩展点
