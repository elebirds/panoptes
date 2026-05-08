// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现LLM Chat 模块的客户端连接适配。

package chatmodule

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"log/slog"
	"strings"
	"time"

	"github.com/elebirds/panoptes/internal/llm/service"
	"github.com/openai/openai-go"
	"github.com/openai/openai-go/option"
)

// sessionCtxKey 避免 context value 键名冲突
type sessionCtxKey struct{}

// ChatClient 对外暴露的统一 LLM 接口
type ChatClient interface {
	NormalChat(ctx context.Context, req *ChatRequest) (*ChatResponse, error)
	StreamChat(ctx context.Context, req *ChatRequest) (<-chan *ChatResponse, error)
	Stop(ctx context.Context, sessionID string)
}

// -------------------------------- 选项模式 --------------------------------

// Option 客户端选项函数
type Option func(*clientCfg)

type clientCfg struct {
	defaultModel string
	temperature  float64
	topP         float64
	maxTokens    int64
}

func WithModel(model string) Option {
	return func(c *clientCfg) { c.defaultModel = model }
}

func WithTemperature(t float64) Option {
	return func(c *clientCfg) { c.temperature = t }
}

func WithTopP(p float64) Option {
	return func(c *clientCfg) { c.topP = p }
}

func WithMaxTokens(n int64) Option {
	return func(c *clientCfg) { c.maxTokens = n }
}

// -------------------------------- Client --------------------------------

// Client 通用 LLM 客户端，兼容所有支持 OpenAI 格式的厂商
type Client struct {
	name string // 日志标识，如 "[通义千问]"
	cfg  clientCfg
	oai  openai.Client
}

// newClient 由各厂商预设构造函数调用，外部不直接使用
func newClient(name, apiKey, baseURL string, defaults clientCfg, opts []Option) *Client {
	for _, o := range opts {
		o(&defaults)
	}
	return &Client{
		name: name,
		cfg:  defaults,
		oai: openai.NewClient(
			option.WithAPIKey(apiKey),
			option.WithBaseURL(baseURL),
		),
	}
}

// NormalChat 非流式对话，阻塞直到 LLM 返回完整结果
func (c *Client) NormalChat(ctx context.Context, req *ChatRequest) (*ChatResponse, error) {
	if req == nil {
		return nil, errors.New("req is nil")
	}

	if req.Message == "" {
		return nil, errors.New("message is required")
	}

	model := c.resolveModel(req.Model)
	startedAt := time.Now()
	c.debugLogRequest(ctx, "NormalChat", req, "", model)
	completion, err := c.oai.Chat.Completions.New(ctx, openai.ChatCompletionNewParams{
		Messages:    buildMessages(req),
		Model:       model,
		Temperature: openai.Float(c.cfg.temperature),
		TopP:        openai.Float(c.cfg.topP),
		MaxTokens:   openai.Int(c.cfg.maxTokens),
	})
	if err != nil {
		return nil, fmt.Errorf("%s NormalChat: %w", c.name, err)
	}

	resp := &ChatResponse{}
	if len(completion.Choices) > 0 {
		resp.Role = RoleType(completion.Choices[0].Message.Role)
		resp.Content = completion.Choices[0].Message.Content
	}
	c.debugLogNormalResponse(ctx, startedAt, model, resp, completion.RawJSON())
	return resp, nil
}

// StreamChat 流式对话，返回 channel 逐 token 输出；调用 Stop() 可提前终止
func (c *Client) StreamChat(ctx context.Context, req *ChatRequest) (<-chan *ChatResponse, error) {

	if req == nil {
		return nil, errors.New("req is nil")
	}

	if req.Message == "" {
		return nil, errors.New("message is required")
	}

	sessionID := req.SessionID
	if sessionID == "" {
		var genErr error
		sessionID, genErr = service.GenerateSessionID()
		if genErr != nil {
			slog.Warn(c.name+"-[StreamChat] 会话 ID 使用 fallback", "err", genErr)
		}
	}
	if sessionID == "" {
		return nil, errors.New("sessionID is required")
	}

	jobCtx, cancel := context.WithCancel(context.WithValue(ctx, sessionCtxKey{}, sessionID))
	service.SetChatSession(sessionID, cancel)

	model := c.resolveModel(req.Model)
	startedAt := time.Now()
	c.debugLogRequest(ctx, "StreamChat", req, sessionID, model)
	stream := c.oai.Chat.Completions.NewStreaming(jobCtx, openai.ChatCompletionNewParams{
		Messages:    buildMessages(req),
		Model:       model,
		Temperature: openai.Float(c.cfg.temperature),
		TopP:        openai.Float(c.cfg.topP),
		MaxTokens:   openai.Int(c.cfg.maxTokens),
	})

	responseCh := make(chan *ChatResponse)

	go func() {
		defer close(responseCh)
		defer cancel()
		defer service.RemoveChatSession(sessionID)
		defer stream.Close()

		chunkCount := 0
		contentBytes := 0
		reasoningBytes := 0
		for stream.Next() {
			chunk := stream.Current()
			if len(chunk.Choices) == 0 {
				continue
			}
			choice := chunk.Choices[0]
			content := choice.Delta.Content
			reasoning := debugJSONFieldString(choice.Delta.RawJSON(), "reasoning_content")
			chunkCount++
			contentBytes += len(content)
			reasoningBytes += len(reasoning)
			if content == "" {
				continue
			}

			msg := &ChatResponse{
				Role:      RoleAssistant,
				Content:   content,
				SessionID: sessionID,
			}

			select {
			case responseCh <- msg:
			case <-jobCtx.Done():
				return
			}
		}

		if err := stream.Err(); err != nil && !errors.Is(err, context.Canceled) {
			slog.Error(c.name+"-[StreamChat] 流读取错误", "session_id", sessionID, "model", model, "chunks", chunkCount, "content_bytes", contentBytes, "reasoning_bytes", reasoningBytes, "elapsed_ms", time.Since(startedAt).Milliseconds(), "err", err)
			return
		}
		slog.Debug(c.name+"-[StreamChat] stream closed", "session_id", sessionID, "model", model, "chunks", chunkCount, "content_bytes", contentBytes, "reasoning_bytes", reasoningBytes, "elapsed_ms", time.Since(startedAt).Milliseconds(), "ctx_err", jobCtx.Err())
	}()
	return responseCh, nil
}

// Stop 主动终止指定会话的流式输出
func (c *Client) Stop(_ context.Context, sessionID string) {
	if cancel := service.GetChatSession(sessionID); cancel != nil {
		cancel()
	}
	service.RemoveChatSession(sessionID)
}

// -------------------------------- 工具函数 --------------------------------

func (c *Client) resolveModel(reqModel string) string {
	if reqModel != "" {
		return reqModel
	}
	return c.cfg.defaultModel
}

func (c *Client) debugLogRequest(ctx context.Context, op string, req *ChatRequest, sessionID string, model string) {
	if !slog.Default().Enabled(ctx, slog.LevelDebug) {
		return
	}
	systemPrompt := ""
	if req.Tips != nil {
		systemPrompt = req.Tips.Content
	}
	slog.Debug(c.name+"-["+op+"] request",
		"session_id", sessionID,
		"model", model,
		"temperature", c.cfg.temperature,
		"top_p", c.cfg.topP,
		"max_tokens", c.cfg.maxTokens,
		"history_count", len(req.History),
		"system_len", len(systemPrompt),
		"system_preview", debugPreviewText(systemPrompt),
		"user_len", len(req.Message),
		"user_preview", debugPreviewText(req.Message),
		"history_preview", debugHistoryPreview(req.History),
	)
}

func (c *Client) debugLogNormalResponse(ctx context.Context, startedAt time.Time, model string, resp *ChatResponse, raw string) {
	if !slog.Default().Enabled(ctx, slog.LevelDebug) {
		return
	}
	content := ""
	role := ""
	if resp != nil {
		content = resp.Content
		role = string(resp.Role)
	}
	slog.Debug(c.name+"-[NormalChat] response",
		"model", model,
		"role", role,
		"content_len", len(content),
		"content_preview", debugPreviewText(content),
		"raw_preview", debugPreviewText(raw),
		"elapsed_ms", time.Since(startedAt).Milliseconds(),
	)
}

func debugHistoryPreview(history []*ChatHistory) []map[string]any {
	out := make([]map[string]any, 0, len(history))
	for _, h := range history {
		if h == nil {
			continue
		}
		out = append(out, map[string]any{
			"role":        h.Role,
			"content_len": len(h.Content),
			"preview":     debugPreviewText(h.Content),
		})
	}
	return out
}

func debugJSONFieldString(raw string, field string) string {
	if raw == "" || field == "" {
		return ""
	}
	var payload map[string]json.RawMessage
	if err := json.Unmarshal([]byte(raw), &payload); err != nil {
		return ""
	}
	value, ok := payload[field]
	if !ok {
		return ""
	}
	var text string
	if err := json.Unmarshal(value, &text); err != nil {
		return ""
	}
	return text
}

func debugPreviewText(text string) string {
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

// buildMessages 将 ChatRequest 转换为 openai-go 消息列表
func buildMessages(req *ChatRequest) []openai.ChatCompletionMessageParamUnion {
	msgs := make([]openai.ChatCompletionMessageParamUnion, 0, len(req.History)+2)

	if req.Tips != nil && req.Tips.Content != "" {
		msgs = append(msgs, openai.SystemMessage(req.Tips.Content))
	}
	for _, h := range req.History {
		if h == nil {
			continue
		}
		switch h.Role {
		case RoleAssistant:
			msgs = append(msgs, openai.AssistantMessage(h.Content))
		case RoleSystem:
			msgs = append(msgs, openai.SystemMessage(h.Content))
		case RoleUser:
			msgs = append(msgs, openai.UserMessage(h.Content))
		default:
			continue
		}
	}
	msgs = append(msgs, openai.UserMessage(req.Message))

	return msgs
}
