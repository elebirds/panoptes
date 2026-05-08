// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现部长引擎的引擎协调逻辑。
package minister

import (
	"context"
	"fmt"
	"log/slog"
	"strings"
	"sync"
	"time"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/event"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/llm"
	"github.com/elebirds/panoptes/internal/staticdata"
	"google.golang.org/protobuf/proto"
)

type RuntimeRoom interface {
	State() *domain.GameState
	HumanPlayerIDs() []string
	SendToPlayer(ctx context.Context, playerID string, msg proto.Message) error
	BuildMinisterReportInput(playerID string, role string) ReportPromptInput
	ApplyMinisterActions(playerID string, role string, actions []MinisterActionItem) error
}

type MinisterEngine struct {
	llmClient      llm.LLMClient
	memories       map[string]*MinisterMemory
	enabledRoles   map[string]struct{}
	requestTimeout time.Duration
	model          string
	mu             sync.Mutex
}

func NewMinisterEngine(client llm.LLMClient) *MinisterEngine {
	return &MinisterEngine{
		llmClient:      client,
		memories:       make(map[string]*MinisterMemory),
		enabledRoles:   make(map[string]struct{}),
		requestTimeout: 5 * time.Second,
	}
}

func (e *MinisterEngine) SetEnabledRoles(roles []string) {
	if e == nil {
		return
	}
	e.mu.Lock()
	defer e.mu.Unlock()
	e.enabledRoles = make(map[string]struct{}, len(roles))
	for _, role := range roles {
		role = strings.TrimSpace(role)
		if role == "" {
			continue
		}
		e.enabledRoles[role] = struct{}{}
	}
}

func (e *MinisterEngine) SetTimeout(timeout time.Duration) {
	if e == nil || timeout <= 0 {
		return
	}
	e.mu.Lock()
	defer e.mu.Unlock()
	e.requestTimeout = timeout
}

func (e *MinisterEngine) SetModel(model string) {
	if e == nil {
		return
	}
	e.mu.Lock()
	defer e.mu.Unlock()
	e.model = strings.TrimSpace(model)
}

func (e *MinisterEngine) RecordMemory(playerID string, role string, entry MemoryEntry) {
	if e == nil {
		return
	}
	e.getOrCreateMemory(playerID, role).Add(entry)
}

func (e *MinisterEngine) GenerateReports(ctx context.Context, room RuntimeRoom) {
	if room == nil || room.State() == nil {
		return
	}
	syncMode := e.llmClient == nil
	for _, playerID := range room.HumanPlayerIDs() {
		for _, profile := range pickProfiles() {
			if !e.roleEnabled(profile.Role) {
				continue
			}
			if syncMode {
				e.generateOneReport(ctx, playerID, profile, room)
				continue
			}
			go e.generateOneReport(ctx, playerID, profile, room)
		}
	}
}

func (e *MinisterEngine) generateOneReport(ctx context.Context, playerID string, profile MinisterProfile, room RuntimeRoom) {
	input := room.BuildMinisterReportInput(playerID, profile.Role)
	memory := e.getOrCreateMemory(playerID, profile.Role)
	input.Memory = memory
	prompt := BuildReportPrompt(profile, input)
	prompt.Model = e.requestModel()
	prompt.SessionID = fmt.Sprintf("%s:%s:%d", playerID, profile.Role, input.Turn)

	raw, ok := e.collectReportResponse(ctx, prompt)
	if !ok {
		return
	}
	output, err := ParseMinisterResponse(raw)
	if err != nil {
		slog.Warn("parse minister response failed", "player_id", playerID, "role", profile.Role, "raw_len", len(raw), "err", err)
		output, err = ParseMinisterResponse(fallbackJSON(chineseReportFallback))
		if err != nil {
			slog.Warn("parse minister fallback response failed", "player_id", playerID, "role", profile.Role, "err", err)
			return
		}
	}
	if err := sendMinisterReport(room, playerID, profile.Role, output.Report); err != nil {
		slog.Warn("send minister report failed", "player_id", playerID, "role", profile.Role, "err", err)
	}
	if err := room.SendToPlayer(context.Background(), playerID, &pb.MsgMinisterMetrics{
		MinisterRole: profile.Role,
		Metrics:      output.Metrics,
	}); err != nil {
		slog.Warn("send minister metrics failed", "player_id", playerID, "role", profile.Role, "err", err)
	}

	state := room.State()
	if len(output.Actions) > 0 {
		if err := room.ApplyMinisterActions(playerID, profile.Role, output.Actions); err != nil {
			slog.Warn("apply minister actions failed", "player_id", playerID, "role", profile.Role, "count", len(output.Actions), "err", err)
		}
	}
	if state != nil && state.World != nil {
		event.MinisterActedEvent{
			MinisterRole: profile.Role,
			PlayerID:     playerID,
			ActionID:     output.ActionID,
			Report:       output.Report,
		}.Apply(state.World, state)
	}

	memory.Add(MemoryEntry{Turn: input.Turn, Type: "report", Content: output.Report, Outcome: "generated", PlayerResp: "ignored"})
}

func (e *MinisterEngine) collectReportResponse(ctx context.Context, req llm.CompletionRequest) (string, bool) {
	if e.llmClient == nil {
		return fallbackJSON("目前局势稳定，建议优先巩固补给线并保持战区侦察。"), true
	}

	startedAt := time.Now()
	ctx, cancel := context.WithTimeout(ctx, e.timeout())
	defer cancel()
	stream, err := e.llmClient.Stream(ctx, req)
	if err != nil {
		slog.Warn("minister llm stream failed", "session_id", req.SessionID, "response_duration_ms", time.Since(startedAt).Milliseconds(), "err", err)
		return fallbackJSON("当前汇报链路拥堵，建议按既定国策稳步推进。"), true
	}

	var b strings.Builder
	for chunk := range stream {
		if chunk == "" {
			continue
		}
		b.WriteString(chunk)
	}
	raw := b.String()
	responseDurationMs := time.Since(startedAt).Milliseconds()
	slog.Debug("minister llm report response collected", "session_id", req.SessionID, "raw_len", len(raw), "response_duration_ms", responseDurationMs, "raw_preview", ministerDebugPreview(raw))
	if strings.TrimSpace(raw) == "" {
		slog.Warn("minister llm report response empty", "session_id", req.SessionID, "response_duration_ms", responseDurationMs)
		return fallbackJSON("当前汇报链路没有返回内容，建议按既定国策稳步推进。"), true
	}
	return raw, true
}

func sendMinisterReport(room RuntimeRoom, playerID string, role string, report string) error {
	if room == nil {
		return nil
	}
	if report != "" {
		if err := room.SendToPlayer(context.Background(), playerID, &pb.MsgMinisterReportChunk{
			MinisterRole: role,
			Chunk:        report,
			IsFinal:      false,
		}); err != nil {
			return err
		}
	}
	return room.SendToPlayer(context.Background(), playerID, &pb.MsgMinisterReportChunk{
		MinisterRole: role,
		Chunk:        "",
		IsFinal:      true,
	})
}

func (e *MinisterEngine) getOrCreateMemory(playerID, role string) *MinisterMemory {
	key := playerID + ":" + role
	e.mu.Lock()
	defer e.mu.Unlock()
	if m, ok := e.memories[key]; ok {
		return m
	}
	m := &MinisterMemory{PlayerID: playerID, Role: role, Favor: 50}
	e.memories[key] = m
	return m
}

func pickProfiles() []MinisterProfile {
	pool := staticdata.Default().Ministers()
	if len(pool) == 0 {
		return []MinisterProfile{{
			ID:              "finance",
			Name:            "财政大臣",
			Role:            "finance",
			Ability:         5,
			Personality:     "steady",
			PersonalityDesc: "稳健",
			Loyalty:         6,
			Ambition:        5,
			Cautiousness:    70,
			Decisiveness:    50,
			LoyaltyTendency: 80,
			AmbitionStyle:   30,
		}}
	}
	out := make([]MinisterProfile, 0, len(pool))
	for _, p := range pool {
		out = append(out, MinisterProfile{
			ID:              p.ID,
			Name:            p.Name,
			Role:            p.Role,
			Ability:         p.Ability,
			Personality:     p.Personality,
			PersonalityDesc: p.PersonalityDesc,
			Loyalty:         p.Loyalty,
			Ambition:        p.Ambition,
			Cautiousness:    p.Cautiousness,
			Decisiveness:    p.Decisiveness,
			LoyaltyTendency: p.LoyaltyTendency,
			AmbitionStyle:   p.AmbitionStyle,
		})
	}
	return out
}

func profileForRole(role string) (MinisterProfile, bool) {
	role = strings.TrimSpace(role)
	for _, profile := range pickProfiles() {
		if strings.TrimSpace(profile.Role) == role {
			return profile, true
		}
	}
	return MinisterProfile{}, false
}

func (e *MinisterEngine) roleEnabled(role string) bool {
	if e == nil {
		return false
	}
	role = strings.TrimSpace(role)
	e.mu.Lock()
	defer e.mu.Unlock()
	if len(e.enabledRoles) == 0 {
		return true
	}
	_, ok := e.enabledRoles[role]
	return ok
}

func (e *MinisterEngine) timeout() time.Duration {
	if e == nil || e.requestTimeout <= 0 {
		return 5 * time.Second
	}
	e.mu.Lock()
	defer e.mu.Unlock()
	if e.requestTimeout <= 0 {
		return 5 * time.Second
	}
	return e.requestTimeout
}

func (e *MinisterEngine) requestModel() string {
	if e == nil {
		return ""
	}
	e.mu.Lock()
	defer e.mu.Unlock()
	return strings.TrimSpace(e.model)
}

func fallbackJSON(report string) string {
	return `{"report":"` + report + `","metrics":[],"actions":[],"action_id":"fallback"}`
}

func ministerDebugPreview(text string) string {
	text = strings.TrimSpace(text)
	if text == "" {
		return ""
	}
	text = strings.ReplaceAll(text, "\r", "\\r")
	text = strings.ReplaceAll(text, "\n", "\\n")
	const limit = 1200
	runes := []rune(text)
	if len(runes) <= limit {
		return text
	}
	return string(runes[:limit]) + "...(truncated)"
}
