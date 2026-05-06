package app

import (
	"reflect"
	"testing"

	"github.com/elebirds/panoptes/internal/config"
	"github.com/elebirds/panoptes/internal/llm/chatmodule"
)

func TestBuildMinisterChatClientUsesConfiguredQwenModel(t *testing.T) {
	client := buildMinisterChatClient(&config.Config{
		MinisterLLMEnabled:  true,
		MinisterLLMProvider: "qwen",
		MinisterLLMModel:    "qwen-max-latest",
		QwenAPIKey:          "test-qwen-key",
	})
	if client == nil {
		t.Fatalf("buildMinisterChatClient() = nil, want qwen client")
	}

	impl, ok := client.(*chatmodule.Client)
	if !ok {
		t.Fatalf("client type = %T, want *chatmodule.Client", client)
	}

	defaultModel := reflect.ValueOf(impl).Elem().FieldByName("cfg").FieldByName("defaultModel").String()
	if defaultModel != "qwen-max-latest" {
		t.Fatalf("defaultModel = %q, want qwen-max-latest", defaultModel)
	}
}

func TestBuildMinisterChatClientDisabledReturnsNil(t *testing.T) {
	if client := buildMinisterChatClient(&config.Config{
		MinisterLLMEnabled: false,
		QwenAPIKey:         "test-qwen-key",
	}); client != nil {
		t.Fatalf("buildMinisterChatClient() = %T, want nil when disabled", client)
	}
}

func TestParseMinisterEnabledRoles(t *testing.T) {
	got := parseMinisterEnabledRoles(" domestic, Military,domestic ,, ")
	want := []string{"domestic", "military"}
	if !reflect.DeepEqual(got, want) {
		t.Fatalf("parseMinisterEnabledRoles() = %#v, want %#v", got, want)
	}
}

func TestParseMinisterEnabledRolesDefaultsWhenBlank(t *testing.T) {
	got := parseMinisterEnabledRoles("  ")
	want := []string{"domestic", "military"}
	if !reflect.DeepEqual(got, want) {
		t.Fatalf("parseMinisterEnabledRoles() = %#v, want %#v", got, want)
	}
}
