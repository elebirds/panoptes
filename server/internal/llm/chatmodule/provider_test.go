package chatmodule

import (
	"encoding/json"
	"strings"
	"testing"
)

func TestQwenClientDisablesThinkingByDefault(t *testing.T) {
	client := NewQwenClient("test-key")
	raw := marshalChatParamsForTest(t, client)

	if !strings.Contains(raw, `"enable_thinking":false`) {
		t.Fatalf("qwen params = %s, want enable_thinking false", raw)
	}
}

func TestDeepSeekClientDisablesThinkingByDefault(t *testing.T) {
	client := NewDeepSeekClient("test-key")
	raw := marshalChatParamsForTest(t, client)

	if !strings.Contains(raw, `"thinking":{"type":"disabled"}`) {
		t.Fatalf("deepseek params = %s, want thinking disabled", raw)
	}
}

func TestOpenAIClientDoesNotSendProviderThinkingFields(t *testing.T) {
	client := NewOpenAIClient("test-key")
	raw := marshalChatParamsForTest(t, client)

	if strings.Contains(raw, "enable_thinking") || strings.Contains(raw, `"thinking"`) {
		t.Fatalf("openai params = %s, want no provider-specific thinking fields", raw)
	}
}

func marshalChatParamsForTest(t *testing.T, client *Client) string {
	t.Helper()
	params := client.chatCompletionParams(&ChatRequest{Message: "测试"}, client.resolveModel(""))
	raw, err := json.Marshal(params)
	if err != nil {
		t.Fatalf("marshal chat params: %v", err)
	}
	return string(raw)
}
