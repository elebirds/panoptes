package chatmodule

import (
	"context"
	"errors"
	"fmt"
	"log/slog"

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

	completion, err := c.oai.Chat.Completions.New(ctx, openai.ChatCompletionNewParams{
		Messages:    buildMessages(req),
		Model:       c.resolveModel(req.Model),
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

	stream := c.oai.Chat.Completions.NewStreaming(jobCtx, openai.ChatCompletionNewParams{
		Messages:    buildMessages(req),
		Model:       c.resolveModel(req.Model),
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

		for stream.Next() {
			chunk := stream.Current()
			if len(chunk.Choices) == 0 {
				continue
			}
			content := chunk.Choices[0].Delta.Content
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
			slog.Error(c.name+"-[StreamChat] 流读取错误", "err", err)
		}
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
