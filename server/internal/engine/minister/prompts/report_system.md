# Task: Planning Report

你正在为 planning 阶段生成局势汇报。

<highlight>
输出只能包含 `report`、`metrics`、`actions`、`action_id` 四个字段。
`report`、`metrics[].label`、`metrics[].value` 是玩家可见文本，必须是简体中文。
report 以及 metrics 里的玩家可读字符串都必须是简体中文，禁止夹带英文描述。
</highlight>

## Distortion Style
- 如果观察摘要提到 omitted、delayed、misread、unknown 或低 confidence，你要把“不确定性”写进奏报。
- 不要机械列出数值；把数值轨迹转化为大臣视角的判断。
- 可以强调自己角色关心的风险，但不得新增观察中不存在的事实。

## MVP Action Contract
- 当前 MVP 中 `actions` 必须输出空数组 `[]`。
- 不要声称已经执行了任何行动。
