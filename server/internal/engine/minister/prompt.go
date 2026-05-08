// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现部长引擎的提示词模板。
package minister

import (
	"bytes"
	"embed"
	"strings"
	"text/template"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/llm"
)

//go:embed prompts/*.md
var promptFS embed.FS

var promptTemplates = template.Must(template.New("minister-prompts").
	Funcs(template.FuncMap{
		"trim":     strings.TrimSpace,
		"fallback": templateFallback,
	}).
	ParseFS(promptFS, "prompts/*.md"))

type MinisterProfile struct {
	ID              string
	Name            string
	Role            string
	Ability         int
	Personality     string
	PersonalityDesc string
	Loyalty         int
	Ambition        int

	// 性格四维度
	Cautiousness    int // 谨慎度：0-100，鲁莽 ↔ 谨慎
	Decisiveness    int // 果断度：0-100，优柔寡断 ↔ 果断
	LoyaltyTendency int // 忠诚倾向：0-100，狡猾 ↔ 忠诚
	AmbitionStyle   int // 野心表现：0-100，隐忍 ↔ 张扬
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
	return renderPromptTemplate("base_system.md", newMinisterProfileTemplateData(profile))
}

func buildReportSystemPrompt(profile MinisterProfile) string {
	return strings.TrimSpace(buildBaseSystemPrompt(profile) + "\n\n" + renderPromptTemplate("report_system.md", nil))
}

func buildDraftSystemPrompt(profile MinisterProfile) string {
	return strings.TrimSpace(buildBaseSystemPrompt(profile) + "\n\n" + renderPromptTemplate("draft_system.md", nil))
}

func buildReportUserPrompt(input ReportPromptInput) string {
	return renderPromptTemplate("report_user.md", reportPromptTemplateData{
		Turn:               input.Turn,
		Phase:              strings.TrimSpace(input.Phase),
		PlayerID:           strings.TrimSpace(input.PlayerID),
		CurrentPolicy:      emptyFallback(input.CurrentPolicy, "(none)"),
		CurrentResearch:    emptyFallback(input.CurrentResearch, "(none)"),
		ObservationSummary: emptyFallback(input.ObservationSummary, "(暂无观察摘要)"),
		Memory:             memoryPrompt(input.Memory),
	})
}

func buildDraftUserPrompt(input DraftPromptInput) string {
	return renderPromptTemplate("draft_user.md", draftPromptTemplateData{
		Turn:               input.Turn,
		PlayerID:           strings.TrimSpace(input.PlayerID),
		DraftID:            strings.TrimSpace(input.Draft.DraftID),
		MinisterRole:       strings.TrimSpace(input.Draft.MinisterRole),
		Kind:               strings.TrimSpace(string(input.Draft.Kind)),
		TargetID:           strings.TrimSpace(input.Draft.TargetID),
		TargetLabel:        strings.TrimSpace(input.Draft.TargetLabel),
		CurrentPolicy:      emptyFallback(input.CurrentPolicy, "(none)"),
		CurrentResearch:    emptyFallback(input.CurrentResearch, "(none)"),
		ObservationSummary: emptyFallback(input.ObservationSummary, "(暂无观察摘要)"),
		Memory:             memoryPrompt(input.Memory),
	})
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

type ministerProfileTemplateData struct {
	Role              string
	Name              string
	Personality       string
	PersonalityDesc   string
	Ability           int
	Loyalty           int
	Ambition          int
	Cautiousness      int
	Decisiveness      int
	LoyaltyTendency   int
	AmbitionStyle     int
	StylePressureNote string
}

type reportPromptTemplateData struct {
	Turn               int
	Phase              string
	PlayerID           string
	CurrentPolicy      string
	CurrentResearch    string
	ObservationSummary string
	Memory             string
}

type draftPromptTemplateData struct {
	Turn               int
	PlayerID           string
	DraftID            string
	MinisterRole       string
	Kind               string
	TargetID           string
	TargetLabel        string
	CurrentPolicy      string
	CurrentResearch    string
	ObservationSummary string
	Memory             string
}

func newMinisterProfileTemplateData(profile MinisterProfile) ministerProfileTemplateData {
	return ministerProfileTemplateData{
		Role:              strings.TrimSpace(profile.Role),
		Name:              strings.TrimSpace(profile.Name),
		Personality:       strings.TrimSpace(profile.Personality),
		PersonalityDesc:   strings.TrimSpace(profile.PersonalityDesc),
		Ability:           profile.Ability,
		Loyalty:           profile.Loyalty,
		Ambition:          profile.Ambition,
		Cautiousness:      profile.Cautiousness,
		Decisiveness:      profile.Decisiveness,
		LoyaltyTendency:   profile.LoyaltyTendency,
		AmbitionStyle:     profile.AmbitionStyle,
		StylePressureNote: stylePressureNote(profile),
	}
}

func stylePressureNote(profile MinisterProfile) string {
	loyalty := profile.Loyalty + profile.LoyaltyTendency/10
	ambition := profile.Ambition + profile.AmbitionStyle/10
	switch {
	case loyalty <= 10 && ambition >= 10:
		return "低忠诚和高野心会让你更倾向淡化不利信息、突出自己的功劳，并把不确定性包装成谨慎判断。"
	case profile.Cautiousness >= 70:
		return "高谨慎度会让你更强调风险边界、情报盲区和延迟信息。"
	case profile.Decisiveness >= 70:
		return "高果断度会让你给出更明确的主张，但不能越过规则层目标。"
	default:
		return "保持有立场但克制的奏报语气，既不机械复述数据，也不虚构隐藏事实。"
	}
}

func renderPromptTemplate(name string, data any) string {
	var b bytes.Buffer
	if err := promptTemplates.ExecuteTemplate(&b, name, data); err != nil {
		panic(err)
	}
	return strings.TrimSpace(b.String())
}

func templateFallback(v string, fallback string) string {
	return emptyFallback(v, fallback)
}
