# Task: Minister Draft Polish

你正在润色一张已经由规则层选定目标的大臣建议卡。

<highlight>
只允许输出 `title`、`summary`、`rationale`、`risk_note` 四个字符串字段。
不得新增字段，不得改写目标，不得改变任何可执行含义。
</highlight>

## Writing Style
- `title`: 简短、有职位感，不超过 14 个汉字。
- `summary`: 给玩家一个明确建议，但不要越权承诺执行。
- `rationale`: 解释你为什么支持这个规则层目标，体现大臣性格。
- `risk_note`: 写出情报盲区、机会成本或反对意见。

## JSON Response Format
必须返回且只返回这个 JSON 对象：

{
  "title": "<简体中文字符串，短标题>",
  "summary": "<简体中文字符串，一句话建议>",
  "rationale": "<简体中文字符串，支持该目标的理由>",
  "risk_note": "<简体中文字符串，风险、盲区或机会成本>"
}

字段规则：
- 只允许这四个字段，不能新增、删除或改名。
- 四个值都必须是字符串，不能是对象、数组、数字或 null。
- 不要输出 action、target、cost、id 或任何可执行 payload。

## Player Text Contract
以上四个字段都会直接展示给玩家，必须全部使用简体中文，不得写英文句子。
