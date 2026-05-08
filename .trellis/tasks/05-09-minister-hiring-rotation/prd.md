# brainstorm: minister hiring and rotation

## Goal

让大臣系统不再只是固定五个名字，而是具备“任免、轮换、补充候选、大臣个性差异”的政治感。玩家能雇佣、辞退、替换大臣，系统也会按回合刷新新的候选大臣，带随机或半随机特质，形成持续变化的用人局面。

## What I already know

* 当前已存在五个职责位：`domestic / works / defense / command / frontier`，并且有角色归一化与默认 roster。
* 当前大臣 LLM 已接入角色分工、候选提案、提案卡与亲政模式。
* 现在的大臣名字和人格仍偏固定，长期游玩会重复感明显。
* 代码里已有 `MinisterProfile`、`staticdata.Minister`、`BuildMinisterRosterViews()`、`MinisterEngine`、`Runtime` 的亲政/部长状态缓存等结构，可以承接“候选大臣”和“已任命大臣”两层概念。
* 现有协议和前端已经有部长视图、提案视图、部长指令入口，适合扩展，而不需要先改消息协议大结构。

## Assumptions (temporary)

* 初版只做“候选池刷新 + 雇佣/辞退 + 固定职位补位”，不做复杂的家族、派系、派驻城市或多轮晋升树。
* 候选大臣可以在回合间或若干回合定期刷新，不要求每回合都变。
* 随机属性先用有限字段集生成，避免把大臣做成过度复杂的 RPG 系统。
* 默认仍然维持五个岗位上限，先不扩成无限职位。

## Open Questions

* 候选大臣是只在回合开始刷新，还是也允许通过事件/花费立即刷新？
* 辞退大臣后，是否保留其记忆和履历，还是完全清空？
* 新大臣是否需要“忠诚/野心/能力/性格倾向”的随机分布上限，以免刷出离谱角色？

## Requirements (evolving)

* 玩家可以查看当前在职大臣与可雇佣候选大臣。
* 玩家可以雇佣一个候选大臣到空缺职位，或替换现任大臣。
* 玩家可以辞退某个在职大臣，腾出职位。
* 系统会按规则定期刷新新的候选大臣。
* 候选大臣带有随机或半随机属性，至少包含名字、职责位、能力、忠诚、野心、谨慎度、果断度、人格描述。
* 不同职责位的候选池应有不同倾向，例如工务偏产能、军备偏战斗、军令偏机动、边务偏扩张、内政偏治理。
* UI 需要能区分“在职大臣”和“候选大臣”，并能执行雇佣/辞退。
* 服务端仍然是权威，客户端只展示与发送选择，不做任免合法性判断。

## Acceptance Criteria (evolving)

* [ ] 玩家能在界面看到当前职位、在职大臣和候选大臣列表。
* [ ] 玩家能雇佣候选大臣并替换某一职位。
* [ ] 玩家能辞退在职大臣。
* [ ] 系统会按设定节奏刷新候选大臣。
* [ ] 新大臣具有随机属性，但属性范围受控。
* [ ] 已有五部门 LLM 设计仍能工作，不因任免系统破坏提案与指令流程。
* [ ] 测试覆盖任免、刷新、候选生成与 roster 归一化。

## Definition of Done

* Tests added/updated.
* Lint / typecheck / CI green.
* 相关 UI 与服务端状态流转一致。
* 文档或注释在必要时更新。

## Out of Scope

* 不做完整的派系斗争、暗线政变、家庭关系或多层晋升树。
* 不做大臣之间的复杂社交图谱。
* 不做角色语音化或额外叙事系统。
* 不改协议大结构去支持全新消息体系，除非后续确认必须。

## Technical Notes

* 参考文件：
  * `server/internal/ministerroles/roles.go`
  * `server/internal/engine/minister/engine.go`
  * `server/internal/game/query/minister_draft.go`
  * `server/internal/game/session/minister_prompt.go`
  * `server/internal/game/session/minister_candidate_pool.go`
  * `server/internal/game/planning/minister.go`
  * `server/internal/game/session/runtime.go`
  * `server/internal/staticdata/units_model.go`
* 当前设计已经把大臣分成五个职责位，适合在此基础上扩展“在职 roster + 候选池”双层模型。
* 这类改动大概率会同时碰服务端状态、静态数据、协议投递和 Unity 展示层。
