// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现部长引擎的提示词模板。
package minister

import (
	"fmt"
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/llm"
)

type MinisterProfile struct {
	ID              string
	Name            string
	Role            string
	Ability         int
	Personality     string
	PersonalityDesc string
	Loyalty         int
	Ambition        int
}

type ReportPromptInput struct {
	Turn               int
	Phase              string
	PlayerID           string
	ObservationSummary string
	CurrentPolicy      string
	CurrentResearch    string
	Memory             *MinisterMemory
}

type DraftPromptInput struct {
	Turn               int
	PlayerID           string
	ObservationSummary string
	CurrentPolicy      string
	CurrentResearch    string
	Draft              domain.MinisterDraft
	Memory             *MinisterMemory
}

func BuildReportPrompt(profile MinisterProfile, input ReportPromptInput) llm.CompletionRequest {
	return llm.CompletionRequest{
		SystemPrompt: buildReportSystemPrompt(profile),
		UserPrompt:   buildReportUserPrompt(input),
	}
}

func BuildDraftPrompt(profile MinisterProfile, input DraftPromptInput) llm.CompletionRequest {
	return llm.CompletionRequest{
		SystemPrompt: buildDraftSystemPrompt(profile),
		UserPrompt:   buildDraftUserPrompt(input),
	}
}

func buildBaseSystemPrompt(profile MinisterProfile) string {
	return fmt.Sprintf(
		"你是 Panoptes 中的 %s 大臣。姓名：%s。性格：%s。描述：%s。能力：%d。忠诚：%d。野心：%d。只能基于玩家视角信息发言，不得编造隐藏信息，不得替玩家做不可逆决定，必须严格输出 JSON。所有面向玩家的自然语言内容必须使用简体中文，不得输出英文句子；仅允许 JSON 键名、action_id 以及 trend/confidence 这类枚举值保留英文。输出必须是裸 JSON 对象，不得使用 Markdown、代码块围栏、前缀说明或后缀解释。",
		strings.TrimSpace(profile.Role),
		strings.TrimSpace(profile.Name),
		strings.TrimSpace(profile.Personality),
		strings.TrimSpace(profile.PersonalityDesc),
		profile.Ability,
		profile.Loyalty,
		profile.Ambition,
	)
}

func buildReportSystemPrompt(profile MinisterProfile) string {
	return buildBaseSystemPrompt(profile) + "当前任务是 planning 阶段的局势汇报，只能输出 report、metrics、actions、action_id 四个字段。report 以及 metrics 里的玩家可读字符串都必须是简体中文，禁止夹带英文描述。当前 MVP 中 actions 必须为空数组。"
}

func buildDraftSystemPrompt(profile MinisterProfile) string {
	return buildBaseSystemPrompt(profile) + "当前任务是润色一张已由规则层选定目标的大臣建议卡。不得改写目标，不得新增字段，只能输出 title、summary、rationale、risk_note 四个字符串字段。以上四个字段都是直接展示给玩家的内容，必须使用简体中文，不得写成英文句子。"
}

func buildReportUserPrompt(input ReportPromptInput) string {
	return fmt.Sprintf(
		"turn=%d\nphase=%s\nplayer=%s\ncurrent_policy=%s\ncurrent_research=%s\nobservation_summary=%s\nmemory=\n%s\n\n注意：report、metrics[].label、metrics[].value 是展示给玩家的文字，必须全部使用简体中文，不得输出英文。输出必须是裸 JSON 对象：不要 Markdown，不要 ``` 或 ```json 代码块，不要任何前缀说明或后缀解释。\n\n输出必须是 JSON：\n{\n  \"report\": \"\",\n  \"metrics\": [{\"label\":\"\",\"value\":\"\",\"trend\":\"up|down|stable\",\"confidence\":\"high|medium|low\",\"is_delayed\":false}],\n  \"actions\": [],\n  \"action_id\": \"\"\n}\n",
		input.Turn,
		strings.TrimSpace(input.Phase),
		strings.TrimSpace(input.PlayerID),
		emptyFallback(input.CurrentPolicy, "(none)"),
		emptyFallback(input.CurrentResearch, "(none)"),
		emptyFallback(input.ObservationSummary, "(暂无观察摘要)"),
		memoryPrompt(input.Memory),
	)
}

func buildDraftUserPrompt(input DraftPromptInput) string {
	return fmt.Sprintf(
		"turn=%d\nplayer=%s\ndraft_id=%s\nminister_role=%s\nkind=%s\ntarget_id=%s\ntarget_label=%s\ncurrent_policy=%s\ncurrent_research=%s\nobservation_summary=%s\nmemory=\n%s\n\n注意：title、summary、rationale、risk_note 都是直接展示给玩家的文字，必须全部使用简体中文，不得输出英文。输出必须是裸 JSON 对象：不要 Markdown，不要 ``` 或 ```json 代码块，不要任何前缀说明或后缀解释。\n\n输出必须是 JSON：\n{\n  \"title\": \"\",\n  \"summary\": \"\",\n  \"rationale\": \"\",\n  \"risk_note\": \"\"\n}\n",
		input.Turn,
		strings.TrimSpace(input.PlayerID),
		strings.TrimSpace(input.Draft.DraftID),
		strings.TrimSpace(input.Draft.MinisterRole),
		strings.TrimSpace(string(input.Draft.Kind)),
		strings.TrimSpace(input.Draft.TargetID),
		strings.TrimSpace(input.Draft.TargetLabel),
		emptyFallback(input.CurrentPolicy, "(none)"),
		emptyFallback(input.CurrentResearch, "(none)"),
		emptyFallback(input.ObservationSummary, "(暂无观察摘要)"),
		memoryPrompt(input.Memory),
	)
}

func memoryPrompt(memory *MinisterMemory) string {
	if memory == nil {
		return "(暂无历史记忆)"
	}
	return memory.ToPromptString()
}

func emptyFallback(v string, fallback string) string {
	v = strings.TrimSpace(v)
	if v == "" {
		return fallback
	}
	return v
}
