package resolution

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/event"
)

func TestBuildPlanningCommitEventsBuildsPolicyResearchAndInstitutionEvents(t *testing.T) {
	t.Parallel()

	state := &domain.GameState{
		Turn: 7,
		Players: map[string]*domain.PlayerState{
			"player-1": {
				PlayerID: "player-1",
				Policy:   domain.Policy("reorganization"),
				Research: domain.ResearchState{CurrentTargetTechnologyID: "mining"},
				Institutions: domain.InstitutionState{
					PendingPolicyIDs:      []string{"old_charter"},
					PendingActivationTurn: 7,
				},
			},
		},
	}
	state.TurnRuntime.Planning.EnsureDraftMaps()
	state.TurnRuntime.Planning.SetPendingPolicy("player-1", domain.PolicyExpansion)
	state.TurnRuntime.Planning.SetPendingResearchTarget("player-1", "agrarian_foundations")
	state.TurnRuntime.Planning.SetPendingInstitutionLoadout("player-1", []string{"academy_charter"})

	events := BuildPlanningCommitEvents(state)

	policyEvent := findEvent[event.PolicyChangedEvent](events)
	if policyEvent == nil {
		t.Fatalf("missing PolicyChangedEvent in %#v", events)
	}
	if policyEvent.PlayerID != "player-1" || policyEvent.OldPolicy != "reorganization" || policyEvent.NewPolicy != "expansion" {
		t.Fatalf("policy event = %#v, want player-1 reorganization->expansion", *policyEvent)
	}

	researchEvent := findEvent[event.ResearchTargetChangedEvent](events)
	if researchEvent == nil {
		t.Fatalf("missing ResearchTargetChangedEvent in %#v", events)
	}
	if researchEvent.PlayerID != "player-1" || researchEvent.TechnologyID != "agrarian_foundations" {
		t.Fatalf("research event = %#v, want player-1 agrarian_foundations", *researchEvent)
	}

	institutionEvent := findEvent[event.InstitutionLoadoutChangedEvent](events)
	if institutionEvent == nil {
		t.Fatalf("missing InstitutionLoadoutChangedEvent in %#v", events)
	}
	if institutionEvent.PlayerID != "player-1" || institutionEvent.ActivationTurn != 8 {
		t.Fatalf("institution event = %#v, want player-1 activation turn 8", *institutionEvent)
	}
	if len(institutionEvent.PolicyIDs) != 1 || institutionEvent.PolicyIDs[0] != "academy_charter" {
		t.Fatalf("institution policy ids = %#v, want [academy_charter]", institutionEvent.PolicyIDs)
	}
}

func TestBuildPlanningCommitEventsSkipsUnchangedAndMissingPlayers(t *testing.T) {
	t.Parallel()

	state := &domain.GameState{
		Turn: 3,
		Players: map[string]*domain.PlayerState{
			"player-1": {
				PlayerID: "player-1",
				Policy:   domain.PolicyExpansion,
				Research: domain.ResearchState{CurrentTargetTechnologyID: "agrarian_foundations"},
				Institutions: domain.InstitutionState{
					PendingPolicyIDs:      []string{"academy_charter"},
					PendingActivationTurn: 4,
				},
			},
		},
	}
	state.TurnRuntime.Planning.EnsureDraftMaps()
	state.TurnRuntime.Planning.SetPendingPolicy("player-1", domain.PolicyExpansion)
	state.TurnRuntime.Planning.SetPendingPolicy("missing-player", domain.PolicyExpansion)
	state.TurnRuntime.Planning.SetPendingResearchTarget("player-1", "agrarian_foundations")
	state.TurnRuntime.Planning.SetPendingResearchTarget("missing-player", "agrarian_foundations")
	state.TurnRuntime.Planning.SetPendingInstitutionLoadout("player-1", []string{"academy_charter"})

	events := BuildPlanningCommitEvents(state)

	if len(events) != 0 {
		t.Fatalf("events = %#v, want none", events)
	}
}

func findEvent[T event.Event](events []event.Event) *T {
	for _, evt := range events {
		if typed, ok := evt.(T); ok {
			return &typed
		}
	}
	return nil
}
