package turn

import (
	"context"
	"testing"
	"time"

	"github.com/elebirds/panoptes/internal/config"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/game/ai"
	gameorders "github.com/elebirds/panoptes/internal/game/orders"
	"github.com/elebirds/panoptes/internal/game/participant"
	"github.com/elebirds/panoptes/internal/game/planning"
	gamesession "github.com/elebirds/panoptes/internal/game/session"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
	"google.golang.org/protobuf/proto"
)

func TestCoordinatorStopsPromptlyWhenRuntimeCanceledDuringPlanning(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{TurnTimeLimitPlanning: 10, TokensPerTurn: 3},
	}))

	runtime := gamesession.NewRuntime("game-1", []gamesession.ParticipantBinding{
		{
			Participant: participant.Participant{ID: "player-1", Username: "alice", Kind: participant.KindHuman},
			Controller:  gamesession.HumanController{},
		},
	}, nil, &config.Config{})
	runtime.SetState(domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"}))
	runtime.State().Phase = domain.PhasePlanning.String()
	runtime.State().Turn = 1

	host := &stubCoordinatorHost{runtime: runtime}
	coordinator := NewCoordinator(runtime, host)

	done := make(chan struct{})
	go func() {
		coordinator.Start()
		close(done)
	}()

	time.Sleep(100 * time.Millisecond)
	runtime.Cancel()

	select {
	case <-done:
	case <-time.After(300 * time.Millisecond):
		t.Fatalf("coordinator should exit promptly after runtime.Cancel() during planning")
	}
}

func TestCoordinatorTriggersAutonomousControllerAndCountsBotSubmission(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{TurnTimeLimitPlanning: 10, TokensPerTurn: 3},
	}))

	provider := &stubPlanningProvider{
		intents: []planning.Intent{planning.SubmitTurnIntent{}},
	}
	runtime := gamesession.NewRuntime("game-1", []gamesession.ParticipantBinding{
		{
			Participant: participant.Participant{ID: "bot-1", Username: "bot", Kind: participant.KindBot},
			Controller:  gamesession.NewAutonomousController(provider),
		},
	}, nil, &config.Config{})
	runtime.SetState(domain.NewGameState("game-1", []string{"bot-1"}, []string{"bot"}, &domain.MapData{ID: "default"}))
	runtime.State().Phase = domain.PhasePlanning.String()
	runtime.State().Turn = 1

	host := &stubCoordinatorHost{runtime: runtime}
	coordinator := NewCoordinator(runtime, host)
	host.submit = coordinator.Submit
	host.runTurnResolution = func() {
		host.runTurnResolutionCalls++
		host.runtime.State().IsOver = true
	}

	done := make(chan struct{})
	go func() {
		coordinator.Start()
		close(done)
	}()

	select {
	case <-done:
	case <-time.After(500 * time.Millisecond):
		t.Fatalf("coordinator should finish after bot submit and one resolution tick")
	}

	if provider.calls != 1 {
		t.Fatalf("provider calls = %d, want 1", provider.calls)
	}
	if len(host.submissions) != 1 || host.submissions[0] != "bot-1" {
		t.Fatalf("submissions = %#v, want [bot-1]", host.submissions)
	}
	if host.runTurnResolutionCalls != 1 {
		t.Fatalf("runTurnResolutionCalls = %d, want 1", host.runTurnResolutionCalls)
	}
}

type stubPlanningProvider struct {
	intents []planning.Intent
	calls   int
}

func (p *stubPlanningProvider) BuildPlanningIntents(context.Context, ai.Request) ([]planning.Intent, error) {
	p.calls++
	return append([]planning.Intent(nil), p.intents...), nil
}

type stubCoordinatorHost struct {
	runtime                *gamesession.Runtime
	submit                 func(string)
	submissions            []string
	runTurnResolution      func()
	runTurnResolutionCalls int
}

func (h *stubCoordinatorHost) State() *domain.GameState { return h.runtime.State() }
func (h *stubCoordinatorHost) Submit(playerID string) {
	h.submissions = append(h.submissions, playerID)
	if h.submit != nil {
		h.submit(playerID)
	}
}
func (h *stubCoordinatorHost) SendToPlayer(context.Context, string, proto.Message) error {
	return nil
}
func (h *stubCoordinatorHost) IsDevMode() bool                                    { return false }
func (h *stubCoordinatorHost) QueueBuildOrder(domain.BuildOrder)                  {}
func (h *stubCoordinatorHost) QueueRecipeSelection(domain.RecipeSelectionOrder)   {}
func (h *stubCoordinatorHost) SetInstitutionLoadout(string, []string)             {}
func (h *stubCoordinatorHost) SetMinisterDirective(string, string)                {}
func (h *stubCoordinatorHost) SetWarDirectives(string, []domain.WarZoneDirective) {}
func (h *stubCoordinatorHost) SetUnitOrder(gameorders.UnitOrder)                  {}
func (h *stubCoordinatorHost) CancelUnitOrder(string, string)                     {}
func (h *stubCoordinatorHost) SendPlanningSnapshot(context.Context, string) error { return nil }
func (h *stubCoordinatorHost) BuildNodeViewForPlayer(string, string) *pb.NodeView { return nil }
func (h *stubCoordinatorHost) NodeByID(string) (*donburi.Entry, bool)             { return nil, false }
func (h *stubCoordinatorHost) RunTurnResolution() {
	if h.runTurnResolution != nil {
		h.runTurnResolution()
	}
}
func (h *stubCoordinatorHost) ShouldStopAfterResolution() bool { return false }
func (h *stubCoordinatorHost) HandleDraw()                     { h.runtime.State().IsOver = true }
func (h *stubCoordinatorHost) CheckGameOver()                  {}
