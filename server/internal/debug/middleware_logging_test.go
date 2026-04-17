package debug

import (
	"bytes"
	"encoding/json"
	"log/slog"
	"strings"
	"testing"
)

func TestMessageLoggerLogIncomingStillEmitsRawTransportLog(t *testing.T) {
	logger := NewMessageLogger(true)

	buffer := &bytes.Buffer{}
	previous := slog.Default()
	slog.SetDefault(slog.New(slog.NewJSONHandler(buffer, &slog.HandlerOptions{Level: slog.LevelDebug})))
	t.Cleanup(func() {
		slog.SetDefault(previous)
	})

	logger.LogIncoming("player-1", "PlanningCommand", `{"submitTurn":{}}`)

	line := strings.TrimSpace(buffer.String())
	if line == "" {
		t.Fatalf("expected raw transport debug log, got empty output")
	}
	record := make(map[string]any)
	if err := json.Unmarshal([]byte(line), &record); err != nil {
		t.Fatalf("unmarshal log: %v", err)
	}
	if record["msg"] != "← 收到消息" {
		t.Fatalf("msg = %#v, want raw transport message log", record["msg"])
	}
	if record["type"] != "PlanningCommand" {
		t.Fatalf("type = %#v, want PlanningCommand", record["type"])
	}
	if record["raw"] != `{"submitTurn":{}}` {
		t.Fatalf("raw = %#v, want raw payload", record["raw"])
	}
}
