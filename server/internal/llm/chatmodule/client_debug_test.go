package chatmodule

import "testing"

func TestDebugJSONFieldStringExtractsProviderExtraField(t *testing.T) {
	got := debugJSONFieldString(`{"content":"","reasoning_content":"先推理，再输出正文。"}`, "reasoning_content")
	if got != "先推理，再输出正文。" {
		t.Fatalf("debugJSONFieldString() = %q", got)
	}
}

func TestDebugPreviewTextTrimsAndEscapesNewlines(t *testing.T) {
	got := debugPreviewText("  第一行\n第二行  ")
	if got != `第一行\n第二行` {
		t.Fatalf("debugPreviewText() = %q", got)
	}
}
