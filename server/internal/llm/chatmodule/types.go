// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 定义LLM Chat 模块的公共类型。

package chatmodule

type RoleType string

const (
	RoleUser      RoleType = "user"
	RoleSystem    RoleType = "system"
	RoleAssistant RoleType = "assistant"
)

// ChatRequest 调用方传入的对话请求
type ChatRequest struct {
	Model     string         `json:"model"`             // 请求模型，为空时使用厂商默认值
	Message   string         `json:"message"`           // 本轮用户输入
	History   []*ChatHistory `json:"history,omitempty"` // 历史对话上下文
	Tips      *ChatMessage   `json:"tips,omitempty"`    // 系统提示词（system prompt）
	SessionID string         `json:"session_id"`        // 会话 ID，用于流式终止
}

// ChatMessage 单条消息
type ChatMessage struct {
	Role    RoleType `json:"role"`
	Content string   `json:"content"`
}

// ChatHistory 历史消息，附带创建时间供上层排序/截断使用
type ChatHistory struct {
	ChatMessage
	CreateTime int64 `json:"create_time"`
}

// ChatResponse LLM 返回的单条内容（流式下每次推送一块）
type ChatResponse struct {
	Role      RoleType `json:"role"`
	Content   string   `json:"content"`
	SessionID string   `json:"session_id"`
}
