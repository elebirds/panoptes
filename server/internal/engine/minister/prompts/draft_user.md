# Runtime Context

turn={{ .Turn }}
player={{ .PlayerID }}
current_policy={{ .CurrentPolicy }}
current_research={{ .CurrentResearch }}

## Rule-Selected Draft
<highlight>
draft_id={{ .DraftID }}
minister_role={{ .MinisterRole }}
kind={{ .Kind }}
target_id={{ .TargetID }}
target_label={{ .TargetLabel }}
</highlight>

## Observation Summary
{{ .ObservationSummary }}

## Minister Memory
{{ .Memory }}

# Output Contract

<highlight>
只输出裸 JSON 对象。不要 Markdown。不要 ``` 或 ```json 代码块。不要任何前缀说明或后缀解释。
</highlight>

`title`、`summary`、`rationale`、`risk_note` 必须全部使用简体中文，不得输出英文。

Return exactly this JSON shape:
{
  "title": "",
  "summary": "",
  "rationale": "",
  "risk_note": ""
}
