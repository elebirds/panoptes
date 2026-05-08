# Runtime Context

turn={{ .Turn }}
phase={{ .Phase }}
player={{ .PlayerID }}
current_policy={{ .CurrentPolicy }}
current_research={{ .CurrentResearch }}

## Observation Summary
<highlight>
{{ .ObservationSummary }}
</highlight>

## Action Candidates
<highlight>
{{ .ActionCandidates }}
</highlight>

## Minister Memory
{{ .Memory }}

# Output Contract

<highlight>
只输出裸 JSON 对象。不要 Markdown。不要 ``` 或 ```json 代码块。不要任何前缀说明或后缀解释。
</highlight>

## Action Contract
- `proposals` 可以为空数组，也可以包含若干提案。若 Action Candidates 不为 `(none)`，且你的奏报建议了具体行动，至少给出 1-3 个提案。
- 每个 proposal 最好围绕一个清晰主题；如果一个提案包含多个动作，它们必须语义一致，且玩家批准时可以一起成立。
- `actions` 保留兼容输出。若你已经在 `proposals` 中组织提案，通常让 `actions` 为空数组即可，避免重复。
- 如果 Action Candidates 不为 `(none)`，且你的奏报建议了具体行动，必须使用 `select_candidate` 从候选中选择最匹配项，不要手写同类动作参数。
- 单位/地图行动只能通过 Action Candidates 中的 operation 候选进入 `select_candidate`，不要输出单位级 action。
- `select_candidate` 动作需要 `params.draft_id`，其值必须来自 Action Candidates 中的 `candidate_id`。
- `build` 动作需要 `params.node_id`、`params.building_type`，可选 `params.city_id`。
- `set_research` 动作需要 `params.technology_id`。
- `set_policy` 动作需要 `params.policy_id`。
- `set_institution_loadout` 动作需要 `params.institution_ids` 数组。
- `set_building_recipe` 动作需要 `params.node_id`、`params.recipe_id`。
- 不要发明新的动作类型，不要输出无关参数。
- 如果输出 proposal 或 action，可以额外给出 `title`、`summary`、`rationale`、`risk_note`，这些字段会显示为最终提案卡片文案；必须是简体中文，并且不得提到候选来源、规则规划器、内部校验或系统实现。

## Two-Track Output Contract
- `report` 是叙事轨：简体中文、1-3 句、有大臣立场，但只基于观察摘要。
- `metrics` 是数值轨：短标签、短值、趋势、置信度、是否延迟；不要写成长段解释。
- `report`、`metrics[].label`、`metrics[].value` 必须全部使用简体中文，不得输出英文。
- `metrics` 和 `actions` 没有内容时必须返回空数组 `[]`，不要省略字段。

Return exactly this JSON shape and no other fields:
{
  "report": "<简体中文字符串，1-3 句主观奏报>",
  "metrics": [
    {
      "label": "<简体中文短标签>",
      "value": "<简体中文短值或数值说明>",
      "trend": "up|down|stable",
      "confidence": "high|medium|low",
      "is_delayed": false
    }
  ],
  "proposals": [
    {
      "title": "<可选，简体中文短标题>",
      "summary": "<可选，简体中文一句话提案>",
      "rationale": "<可选，简体中文理由>",
      "risk_note": "<可选，简体中文风险提示>",
      "actions": [
        {
          "type": "select_candidate|build|set_research|set_policy|set_institution_loadout|set_building_recipe",
          "params": {}
        }
      ]
    }
  ],
  "actions": [
    {
      "type": "select_candidate|build|set_research|set_policy|set_institution_loadout|set_building_recipe",
      "params": {},
      "title": "<可选，简体中文短标题>",
      "summary": "<可选，简体中文一句话提案>",
      "rationale": "<可选，简体中文理由>",
      "risk_note": "<可选，简体中文风险提示>"
    }
  ],
  "action_id": "<简短英文或数字标识；无动作时可为空字符串>"
}
