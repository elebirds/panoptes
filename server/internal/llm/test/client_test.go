package test_test

import (
	"context"
	"fmt"
	"strings"
	"testing"

	"github.com/elebirds/panoptes/internal/llm/chatmodule"
)

const testAPIKey = ""

func newQWenClient(t *testing.T) chatmodule.ChatClient {
	t.Helper()
	return chatmodule.NewQWenClient(testAPIKey)
}

// TestNormalChat 非流式调用
func TestNormalChat(t *testing.T) {
	client := newQWenClient(t)

	resp, err := client.NormalChat(context.Background(), &chatmodule.ChatRequest{
		Message: "用一句话介绍你自己",
	})
	if err != nil {
		t.Fatalf("NormalChat 失败: %v", err)
	}
	if resp.Content == "" {
		t.Fatal("返回内容为空")
	}
	t.Logf("回复: %s", resp.Content)
}

// TestNormalChatWithSystemPrompt 带系统提示词
func TestNormalChatWithSystemPrompt(t *testing.T) {
	client := newQWenClient(t)

	resp, err := client.NormalChat(context.Background(), &chatmodule.ChatRequest{
		Tips:    &chatmodule.ChatMessage{Content: "你是一个围棋专家，回答要简洁专业"},
		Message: "什么是天元？",
	})
	if err != nil {
		t.Fatalf("NormalChat 失败: %v", err)
	}
	t.Logf("回复: %s", resp.Content)
}

// TestNormalChatWithHistory 带历史对话
func TestNormalChatWithHistory(t *testing.T) {
	client := newQWenClient(t)

	resp, err := client.NormalChat(context.Background(), &chatmodule.ChatRequest{
		History: []*chatmodule.ChatHistory{
			{ChatMessage: chatmodule.ChatMessage{Role: chatmodule.IdUser, Content: "我叫小明"}},
			{ChatMessage: chatmodule.ChatMessage{Role: chatmodule.IdBot, Content: "你好，小明！"}},
		},
		Message: "你还记得我叫什么吗？",
	})
	if err != nil {
		t.Fatalf("NormalChat 失败: %v", err)
	}
	t.Logf("回复: %s", resp.Content)
}

// TestStreamChat 流式调用
func TestStreamChat(t *testing.T) {
	client := newQWenClient(t)

	ch, err := client.StreamChat(context.Background(), &chatmodule.ChatRequest{
		SessionId: "test-session-001",
		Message:   "用三句话介绍围棋",
	})
	if err != nil {
		t.Fatalf("StreamChat 启动失败: %v", err)
	}

	var sb strings.Builder
	for msg := range ch {
		fmt.Print(msg.Content)
		sb.WriteString(msg.Content)
	}
	fmt.Println()

	if sb.Len() == 0 {
		t.Fatal("流式返回内容为空")
	}
	t.Logf("完整内容(%d字): %s", sb.Len(), sb.String())
}

// TestStreamChatStop 测试主动终止流式输出
func TestStreamChatStop(t *testing.T) {
	client := newQWenClient(t)

	sessionId := "test-session-stop"
	ch, err := client.StreamChat(context.Background(), &chatmodule.ChatRequest{
		SessionId: sessionId,
		Message:   "请写一篇500字的文章，主题是围棋的历史",
	})
	if err != nil {
		t.Fatalf("StreamChat 启动失败: %v", err)
	}

	count := 0
	for msg := range ch {
		count++
		fmt.Print(msg.Content)
		if count >= 3 {
			client.Stop(context.Background(), sessionId)
			break
		}
	}
	for range ch {
	}
	fmt.Println()
	t.Logf("收到 %d 块后主动终止", count)
}
