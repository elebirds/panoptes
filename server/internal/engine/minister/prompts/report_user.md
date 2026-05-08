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
- `actions` 可以为空数组，也可以包含若干动作。
- 如果 Action Candidates 不为 `(none)`，优先使用 `select_candidate` 从候选中选择，不要手写同类动作参数。
- `select_candidate` 动作需要 `params.draft_id`，其值必须来自 Action Candidates 中的 `candidate_id`。
- `build` 动作需要 `params.node_id`、`params.building_type`，可选 `params.city_id`。
- `move_units` 动作需要 `params.unit_id`、`params.target_node`。
- `unit_order` 动作需要 `params.unit_id`、`params.action`，可选 `params.target_node`、`params.target_unit`、`params.secondary_node`、`params.params`。
- `set_research` 动作需要 `params.technology_id`。
- `set_policy` 动作需要 `params.policy_id`。
- `set_institution_loadout` 动作需要 `params.policy_ids` 数组。
- `set_building_recipe` 动作需要 `params.node_id`、`params.recipe_id`。
- 不要发明新的动作类型，不要输出无关参数。

`report`、`metrics[].label`、`metrics[].value` 必须全部使用简体中文，不得输出英文。

Return exactly this JSON shape:
{
  "report": "",
  "metrics": [{"label":"","value":"","trend":"up|down|stable","confidence":"high|medium|low","is_delayed":false}],
  "actions": [],
  "action_id": ""
}
