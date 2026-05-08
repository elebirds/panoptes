package economy

import "testing"

func TestNewRunnerExposesCanonicalStageOrder(t *testing.T) {
	runner := NewRunner()

	stages := runner.Stages()
	if len(stages) != 7 {
		t.Fatalf("stage count = %d, want 7", len(stages))
	}

	want := []string{
		"budget",
		"research_progress",
		"research_completion",
		"demolish",
		"build",
		"recipe_selection",
		"recipe_progress",
	}
	for idx, stage := range stages {
		if got := stage.Name(); got != want[idx] {
			t.Fatalf("stage[%d] = %q, want %q", idx, got, want[idx])
		}
	}
}
