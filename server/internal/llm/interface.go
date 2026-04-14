// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 定义LLM 抽象层的抽象接口。

package llm

import (
	"context"

	"github.com/elebirds/panoptes/internal/llm/chatmodule"
)

type CompletionRequest struct {
	Model        string
	SystemPrompt string
	UserPrompt   string
	SessionID    string
}

type LLMClient interface {
	Stream(ctx context.Context, req CompletionRequest) (<-chan string, error)
}

type ChatClientAdapter struct {
	Client chatmodule.ChatClient
}

func (a *ChatClientAdapter) Stream(ctx context.Context, req CompletionRequest) (<-chan string, error) {
	if a == nil || a.Client == nil {
		ch := make(chan string)
		close(ch)
		return ch, nil
	}
	stream, err := a.Client.StreamChat(ctx, &chatmodule.ChatRequest{
		Model:     req.Model,
		Message:   req.UserPrompt,
		Tips:      &chatmodule.ChatMessage{Role: chatmodule.RoleSystem, Content: req.SystemPrompt},
		SessionID: req.SessionID,
	})
	if err != nil {
		return nil, err
	}
	out := make(chan string)
	go func() {
		defer close(out)
		for resp := range stream {
			if resp == nil {
				continue
			}
			out <- resp.Content
		}
	}()
	return out, nil
}
