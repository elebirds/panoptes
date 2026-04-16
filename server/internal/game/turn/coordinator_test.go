package turn

import (
	"context"
	"testing"
	"time"

	"github.com/elebirds/panoptes/internal/config"
	"github.com/elebirds/panoptes/internal/domain"
	gameorders "github.com/elebirds/panoptes/internal/game/orders"
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

	runtime := gamesession.NewRuntime("game-1", []gamesession.Player{
		&stubSessionPlayer{playerID: "player-1", username: "alice"},
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

type stubSessionPlayer struct {
	playerID string
	username string
}

func (p *stubSessionPlayer) PlayerID() string { return p.playerID }
func (p *stubSessionPlayer) Username() string { return p.username }
func (p *stubSessionPlayer) IsBot() bool { return false }
func (p *stubSessionPlayer) Send(context.Context, proto.Message) error { return nil }

type stubCoordinatorHost struct {
	runtime *gamesession.Runtime
}

func (h *stubCoordinatorHost) State() *domain.GameState { return h.runtime.State() }
func (h *stubCoordinatorHost) Submit(string)            {}
func (h *stubCoordinatorHost) SendToPlayer(context.Context, string, proto.Message) error {
	return nil
}
func (h *stubCoordinatorHost) IsDevMode() bool { return false }
func (h *stubCoordinatorHost) QueueBuildOrder(domain.BuildOrder) {}
func (h *stubCoordinatorHost) QueueRecipeSelection(domain.RecipeSelectionOrder) {}
func (h *stubCoordinatorHost) SetInstitutionLoadout(string, []string) {}
func (h *stubCoordinatorHost) SetMinisterDirective(string, string) {}
func (h *stubCoordinatorHost) SetWarDirectives(string, []domain.WarZoneDirective) {}
func (h *stubCoordinatorHost) SetUnitOrder(gameorders.UnitOrder) {}
func (h *stubCoordinatorHost) CancelUnitOrder(string, string) {}
func (h *stubCoordinatorHost) SendPlanningSnapshot(context.Context, string) error { return nil }
func (h *stubCoordinatorHost) BuildNodeViewForPlayer(string, string) *pb.NodeView { return nil }
func (h *stubCoordinatorHost) NodeByID(string) (*donburi.Entry, bool)             { return nil, false }
func (h *stubCoordinatorHost) PlayerIDs() []string                                { return []string{"player-1"} }
func (h *stubCoordinatorHost) NotifyTurn(string)                                  {}
func (h *stubCoordinatorHost) RunTurnResolution()                                 {}
func (h *stubCoordinatorHost) ShouldStopAfterResolution() bool                    { return false }
func (h *stubCoordinatorHost) HandleDraw()                                        { h.runtime.State().IsOver = true }
func (h *stubCoordinatorHost) CheckGameOver()                                     {}
