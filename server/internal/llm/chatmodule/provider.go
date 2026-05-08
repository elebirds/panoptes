// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现LLM Chat 模块的模型提供方适配。

package chatmodule

import "strings"

// provider.go 各厂商预设配置。
// 所有厂商均走 OpenAI 兼容格式，只是 baseURL 和默认模型不同。
// 使用 Option 函数可在调用时覆盖任意默认值。

const (
	baseURLQwen     = "https://dashscope.aliyuncs.com/compatible-mode/v1"
	baseURLDeepSeek = "https://api.deepseek.com"
	baseURLOpenAI   = "https://api.openai.com/v1"
	baseURLMoonshot = "https://api.moonshot.cn/v1"
)

func qwenDefaults() clientCfg {
	return clientCfg{
		defaultModel: "qwen3.6-flash",
		temperature:  0.8,
		topP:         0.8,
		maxTokens:    1500,
		extraFields: map[string]any{
			"enable_thinking": false,
		},
	}
}

func deepSeekDefaults() clientCfg {
	return clientCfg{
		defaultModel: "deepseek-v4-flash",
		temperature:  0.7,
		topP:         0.9,
		maxTokens:    2048,
		extraFields: map[string]any{
			"thinking": map[string]any{
				"type": "disabled",
			},
		},
	}
}

func openAIDefaults() clientCfg {
	return clientCfg{defaultModel: "gpt-4o", temperature: 0.8, topP: 0.9, maxTokens: 2048}
}

func moonshotDefaults() clientCfg {
	return clientCfg{defaultModel: "moonshot-v1-8k", temperature: 0.3, topP: 1.0, maxTokens: 2048}
}

// NewProviderClient 根据 provider 名称创建 OpenAI 兼容客户端。
func NewProviderClient(provider, apiKey string, opts ...Option) (*Client, bool) {
	switch strings.ToLower(strings.TrimSpace(provider)) {
	case "", "qwen":
		return NewQwenClient(apiKey, opts...), true
	case "deepseek":
		return NewDeepSeekClient(apiKey, opts...), true
	case "openai":
		return NewOpenAIClient(apiKey, opts...), true
	case "moonshot":
		return NewMoonshotClient(apiKey, opts...), true
	default:
		return nil, false
	}
}

// NewQwenClient 通义千问（DashScope OpenAI 兼容模式）
func NewQwenClient(apiKey string, opts ...Option) *Client {
	return newClient("[通义千问]", apiKey, baseURLQwen, qwenDefaults(), opts)
}

// NewDeepSeekClient DeepSeek
func NewDeepSeekClient(apiKey string, opts ...Option) *Client {
	return newClient("[DeepSeek]", apiKey, baseURLDeepSeek, deepSeekDefaults(), opts)
}

// NewOpenAIClient 标准 OpenAI
func NewOpenAIClient(apiKey string, opts ...Option) *Client {
	return newClient("[OpenAI]", apiKey, baseURLOpenAI, openAIDefaults(), opts)
}

// NewMoonshotClient Moonshot（月之暗面）
func NewMoonshotClient(apiKey string, opts ...Option) *Client {
	return newClient("[Moonshot]", apiKey, baseURLMoonshot, moonshotDefaults(), opts)
}

// NewCustomClient 自定义厂商，完全由调用方指定 baseURL 和配置
func NewCustomClient(name, apiKey, baseURL string, opts ...Option) *Client {
	defaults := clientCfg{defaultModel: "gpt-4o", temperature: 0.8, topP: 0.9, maxTokens: 2048}
	return newClient(name, apiKey, baseURL, defaults, opts)
}
