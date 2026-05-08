package app

import (
	"log/slog"
	"strings"
	"time"

	"github.com/elebirds/panoptes/internal/config"
	ministerengine "github.com/elebirds/panoptes/internal/engine/minister"
	"github.com/elebirds/panoptes/internal/llm"
	"github.com/elebirds/panoptes/internal/llm/chatmodule"
)

func buildMinisterChatClient(cfg *config.Config) chatmodule.ChatClient {
	if cfg == nil || !cfg.MinisterLLMEnabled {
		return nil
	}

	provider := strings.ToLower(strings.TrimSpace(cfg.MinisterLLMProvider))
	if provider == "" {
		provider = "qwen"
	}
	model := strings.TrimSpace(cfg.MinisterLLMModel)
	opts := make([]chatmodule.Option, 0, 1)
	if model != "" {
		opts = append(opts, chatmodule.WithModel(model))
	}

	apiKey := strings.TrimSpace(cfg.MinisterLLMAPIKey)
	if apiKey == "" {
		slog.Warn("minister llm enabled but api key missing", "provider", provider)
		return nil
	}

	client, ok := chatmodule.NewProviderClient(provider, apiKey, opts...)
	if !ok {
		slog.Warn("unsupported minister llm provider", "provider", provider)
		return nil
	}
	return client
}

func buildMinisterLLMClient(cfg *config.Config) llm.LLMClient {
	client := buildMinisterChatClient(cfg)
	if client == nil {
		return nil
	}
	return &llm.ChatClientAdapter{Client: client}
}

func buildMinisterEngineFactory(cfg *config.Config) func() *ministerengine.MinisterEngine {
	if cfg == nil || !cfg.MinisterLLMEnabled {
		return nil
	}

	llmClient := buildMinisterLLMClient(cfg)
	timeout := time.Duration(cfg.MinisterLLMTimeoutMs) * time.Millisecond
	model := strings.TrimSpace(cfg.MinisterLLMModel)

	return func() *ministerengine.MinisterEngine {
		engine := ministerengine.NewMinisterEngine(llmClient)
		engine.SetEnabledRoles(parseMinisterEnabledRoles(cfg.MinisterLLMRoles))
		engine.SetTimeout(timeout)
		engine.SetModel(model)
		return engine
	}
}

func parseMinisterEnabledRoles(raw string) []string {
	parts := strings.Split(raw, ",")
	roles := make([]string, 0, len(parts))
	seen := make(map[string]struct{}, len(parts))
	for _, part := range parts {
		role := strings.ToLower(strings.TrimSpace(part))
		if role == "" {
			continue
		}
		if _, ok := seen[role]; ok {
			continue
		}
		seen[role] = struct{}{}
		roles = append(roles, role)
	}
	if len(roles) == 0 {
		return []string{"domestic", "military"}
	}
	return roles
}
