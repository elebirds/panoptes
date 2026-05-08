package turn

import (
	"context"
	"strings"
	"testing"
	"time"

	"github.com/elebirds/panoptes/internal/config"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	ministerengine "github.com/elebirds/panoptes/internal/engine/minister"
	"github.com/elebirds/panoptes/internal/game/ai"
	gameorders "github.com/elebirds/panoptes/internal/game/orders"
	"github.com/elebirds/panoptes/internal/game/participant"
	"github.com/elebirds/panoptes/internal/game/planning"
	gamequery "github.com/elebirds/panoptes/internal/game/query"
	gamesession "github.com/elebirds/panoptes/internal/game/session"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/llm"
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

func TestCoordinatorBeginPlanningTriggersDomesticMinisterReports(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{TurnTimeLimitPlanning: 10, TokensPerTurn: 3},
		Ministers: []staticdata.Minister{
			{ID: "m001", Name: "李猛", Role: "military", Ability: 8, Personality: "aggressive", PersonalityDesc: "果敢激进"},
			{ID: "m002", Name: "沈衡", Role: "domestic", Ability: 7, Personality: "steady", PersonalityDesc: "稳健审慎"},
		},
	}))

	transport := &coordinatorCaptureTransport{messages: make(map[string][]proto.Message)}
	runtime := gamesession.NewRuntime("game-1", []gamesession.ParticipantBinding{
		{
			Participant: participant.Participant{ID: "player-1", Username: "alice", Kind: participant.KindHuman},
			Controller:  gamesession.HumanController{},
		},
	}, transport, &config.Config{})
	runtime.SetState(domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"}))
	runtime.State().Phase = domain.PhasePlanning.String()
	runtime.State().Turn = 1

	engine := ministerengine.NewMinisterEngine(&coordinatorScriptedLLMClient{
		chunks: []string{`{"report":"内政建议保持稳健扩张。","metrics":[{"label":"粮食","value":"紧张","trend":"stable","confidence":"medium","is_delayed":false}],"actions":[],"action_id":"rep-1"}`},
	})
	engine.SetEnabledRoles([]string{"domestic"})
	runtime.SetMinisterEngine(engine)

	host := &stubCoordinatorHost{runtime: runtime}
	coordinator := NewCoordinator(runtime, host)
	coordinator.beginPlanning(context.Background(), false)
	runtime.GenerateMinisterReports(context.Background())

	deadline := time.Now().Add(2 * time.Second)
	for {
		msgs := transport.messages["player-1"]
		if hasMinisterReport(msgs, "domestic") && hasMinisterMetrics(msgs, "domestic") {
			if hasMinisterReport(msgs, "military") || hasMinisterMetrics(msgs, "military") {
				t.Fatalf("unexpected non-domestic minister messages: %#v", msgs)
			}
			return
		}
		if time.Now().After(deadline) {
			t.Fatalf("minister report messages not observed, got %#v", msgs)
		}
		time.Sleep(10 * time.Millisecond)
	}
}

func TestCoordinatorBeginPlanningExposesHumanMinisterDefaultDraftsBeforeNotify(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TurnTimeLimitPlanning:      10,
			TokensPerTurn:              3,
			SafeZoneRadius:             2,
			CityCoreMaxHP:              100,
			BaseResearchOutputPerTurn:  1,
			BaseIndustryOutputPerTurn:  2,
			FacilityTakeoverTurns:      2,
			InitialCityTerritoryRadius: 1,
		},
		Technologies: []staticdata.TechnologyDefinition{
			{ID: "agrarian_foundations", Name: "Agrarian Foundations", ResearchCost: 3},
		},
		Policies: []staticdata.PolicyDefinition{
			{ID: "reorganization", Name: "Reorganization", Layer: "national"},
			{ID: "expansion", Name: "Expansion", Layer: "national"},
		},
		Units: []staticdata.UnitDefinition{
			{ID: "infantry", Class: "melee", MaxHP: 30, Attack: 10, AttackRange: 1, MoveRange: 2, VisionRange: 2},
		},
		Buildings: []staticdata.BuildingDefinition{
			{ID: "city_core", Name: "City Core", PlacementKind: "city_foundation_center", BuildingScope: "city_core", MaxHP: 100, TakeoverMode: "disabled"},
			{ID: "farm", Name: "Farm", PlacementKind: "resource_node", BuildingScope: "out_of_city", RequiredResourceType: "food", MaxHP: 60, TakeoverMode: "delayed"},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
	}))

	transport := &coordinatorCaptureTransport{messages: make(map[string][]proto.Message)}
	runtime := gamesession.NewRuntime("game-1", []gamesession.ParticipantBinding{
		{
			Participant: participant.Participant{ID: "player-1", Username: "alice", Kind: participant.KindHuman},
			Controller:  gamesession.HumanController{},
		},
	}, transport, &config.Config{DevMode: true})
	runtime.SetState(newMinisterDefaultCoordinatorState(t))
	runtime.State().Phase = domain.PhasePlanning.String()
	runtime.State().Turn = 1

	host := &stubCoordinatorHost{runtime: runtime}
	coordinator := NewCoordinator(runtime, host)
	runtime.PreparePlanningStartStateIfNeeded()
	coordinator.beginPlanning(context.Background(), true)

	if got := runtime.State().TurnRuntime.Planning.PendingResearchTarget("player-1"); got != "" {
		t.Fatalf("pending research = %q, want empty before minister approval", got)
	}
	if got := runtime.State().TurnRuntime.Planning.PendingPolicy("player-1"); got != domain.Policy("") {
		t.Fatalf("pending policy = %q, want empty before minister approval", got)
	}
	if len(runtime.State().TurnRuntime.Planning.BuildOrders) != 0 {
		t.Fatalf("build orders = %#v, want none before minister approval", runtime.State().TurnRuntime.Planning.BuildOrders)
	}

	msgs := transport.messages["player-1"]
	start, ok := msgs[len(msgs)-1].(*pb.MsgPlanningStart)
	if !ok {
		t.Fatalf("last message = %T, want MsgPlanningStart", msgs[len(msgs)-1])
	}
	if start.GetSnapshot().GetPlannedResearchTargetTechnologyId() != "" ||
		start.GetSnapshot().GetPlannedNationalPolicyId() != "" {
		t.Fatalf("planning snapshot = %#v, want no applied minister defaults", start.GetSnapshot())
	}
	if got := len(start.GetMinisterDrafts()); got != 0 {
		t.Fatalf("planning start minister drafts = %d, want 0 before LLM selection", got)
	}
	if summary := runtime.BuildMinisterActionCandidateSummary("player-1", "domestic"); !strings.Contains(summary, "candidate_id=") {
		t.Fatalf("candidate summary = %q, want hidden minister candidates", summary)
	}
}

func newMinisterDefaultCoordinatorState(t *testing.T) *domain.GameState {
	t.Helper()
	world := donburi.NewWorld()
	nodeIndex := map[string]donburi.Entity{
		"N0": ecs.CreateNode(world, ecs.MapNode{ID: "N0", Q: 0, R: 0, Terrain: "plain"}),
		"N1": ecs.CreateNode(world, ecs.MapNode{ID: "N1", Q: 1, R: 0, Terrain: "plain", IsResourcePoint: true, ResourceType: "food"}),
		"N2": ecs.CreateNode(world, ecs.MapNode{ID: "N2", Q: 2, R: 0, Terrain: "plain"}),
	}
	state := domain.NewGameState("minister-default-test", []string{"player-1"}, []string{"alice"}, &domain.MapData{
		ID:        "minister-default-test",
		Width:     3,
		Height:    1,
		NodeIndex: nodeIndex,
	})
	state.World = world
	state.NodeIndex = nodeIndex
	for _, nodeID := range []string{"N0", "N1"} {
		entry := world.Entry(nodeIndex[nodeID])
		node := ecs.NodeC.Get(entry)
		node.Owner = "player-1"
		node.TerritoryOwner = "player-1"
	}
	state.EnsureCityState("player-1", "N0")
	state.Players["player-1"].CapitalCityID = "N0"
	state.Players["player-1"].Policy = domain.Policy("reorganization")
	state.Players["player-1"].Research.UnlockBuilding("farm")
	state.Players["player-1"].Resources.Set(domain.ResourceFood, 3)
	ecs.CreateBuilding(world, "city_core", "player-1", "N0", world.Entry(nodeIndex["N0"]))
	unitEntry := world.Entry(ecs.CreateUnit(world, "infantry", "player-1", domain.Position{Q: 0, R: 0}))
	ecs.UnitStatsC.Get(unitEntry).ID = "infantry-1"
	return state
}

type coordinatorCaptureTransport struct {
	messages map[string][]proto.Message
}

func (t *coordinatorCaptureTransport) Send(_ context.Context, playerID string, msg proto.Message) error {
	t.messages[playerID] = append(t.messages[playerID], msg)
	return nil
}

func (t *coordinatorCaptureTransport) Broadcast(context.Context, string, proto.Message) error {
	return nil
}

func (t *coordinatorCaptureTransport) Stream(context.Context, string, <-chan proto.Message) error {
	return nil
}

type coordinatorScriptedLLMClient struct {
	chunks []string
	err    error
}

func (c *coordinatorScriptedLLMClient) Stream(ctx context.Context, _ llm.CompletionRequest) (<-chan string, error) {
	if c.err != nil {
		return nil, c.err
	}
	out := make(chan string, len(c.chunks))
	go func() {
		defer close(out)
		for _, chunk := range c.chunks {
			select {
			case <-ctx.Done():
				return
			case out <- chunk:
			}
		}
	}()
	return out, nil
}

func hasMinisterReport(msgs []proto.Message, role string) bool {
	for _, msg := range msgs {
		report, ok := msg.(*pb.MsgMinisterReportChunk)
		if ok && report.GetMinisterRole() == role {
			return true
		}
	}
	return false
}

func hasMinisterMetrics(msgs []proto.Message, role string) bool {
	for _, msg := range msgs {
		metrics, ok := msg.(*pb.MsgMinisterMetrics)
		if ok && metrics.GetMinisterRole() == role {
			return true
		}
	}
	return false
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
func (h *stubCoordinatorHost) Participant(participantID string) (participant.Participant, bool) {
	if h == nil || h.runtime == nil {
		return participant.Participant{}, false
	}
	return h.runtime.Participant(participantID)
}
func (h *stubCoordinatorHost) Submit(playerID string) {
	h.submissions = append(h.submissions, playerID)
	if h.submit != nil {
		h.submit(playerID)
	}
}
func (h *stubCoordinatorHost) SendToPlayer(context.Context, string, proto.Message) error {
	return nil
}
func (h *stubCoordinatorHost) Broadcast(context.Context, proto.Message) {}
func (h *stubCoordinatorHost) NextChatSequence() int64                  { return 1 }
func (h *stubCoordinatorHost) IsDevMode() bool {
	return h != nil && h.runtime != nil && h.runtime.IsDevMode()
}
func (h *stubCoordinatorHost) IsMinisterStrongMode() bool {
	return h != nil && h.runtime != nil && h.runtime.IsMinisterStrongMode()
}
func (h *stubCoordinatorHost) IsPlayerInMandateMode(playerID string) bool {
	return h != nil && h.runtime != nil && h.runtime.IsPlayerInMandateMode(playerID)
}
func (h *stubCoordinatorHost) QueueBuildOrder(order domain.BuildOrder) {
	if h.State() != nil {
		h.State().TurnRuntime.Planning.UpsertBuildOrder(order)
	}
}
func (h *stubCoordinatorHost) QueueDemolishOrder(order domain.DemolishOrder) {
	if h.State() != nil {
		h.State().TurnRuntime.Planning.UpsertDemolishOrder(order)
	}
}
func (h *stubCoordinatorHost) QueueRecipeSelection(order domain.RecipeSelectionOrder) {
	if h.State() != nil {
		h.State().TurnRuntime.Planning.UpsertRecipeSelection(order)
	}
}
func (h *stubCoordinatorHost) CancelRecipeSelection(playerID string, nodeID string) {
	if h.State() != nil {
		h.State().TurnRuntime.Planning.RemoveRecipeSelection(playerID, nodeID)
	}
}
func (h *stubCoordinatorHost) SetInstitutionLoadout(playerID string, institutionIDs []string) {
	if h.State() != nil {
		h.State().TurnRuntime.Planning.SetPendingInstitutionLoadout(playerID, institutionIDs)
	}
}
func (h *stubCoordinatorHost) SetMinisterDirective(string, string)                {}
func (h *stubCoordinatorHost) SetWarDirectives(string, []domain.WarZoneDirective) {}
func (h *stubCoordinatorHost) SetUnitOrder(order gameorders.UnitOrder) {
	gameorders.ApplyPlanningUnitOrder(h.State(), order, gameorders.RoutePreviewCallbacks{})
}
func (h *stubCoordinatorHost) CancelUnitOrder(playerID string, unitID string) {
	gameorders.CancelPlanningUnitOrder(h.State(), playerID, unitID)
}
func (h *stubCoordinatorHost) SetPlayerMandateMode(playerID string, enabled bool) {
	if h != nil && h.runtime != nil {
		h.runtime.SetPlayerMandateMode(playerID, enabled)
	}
}
func (h *stubCoordinatorHost) SendPlanningSnapshot(context.Context, string) error { return nil }
func (h *stubCoordinatorHost) BuildNodeViewForPlayer(nodeID string, viewerID string) *pb.NodeView {
	entry, ok := h.State().GetNode(nodeID)
	if !ok {
		return nil
	}
	return gamequery.BuildNodeView(h.State(), entry, viewerID)
}
func (h *stubCoordinatorHost) NodeByID(nodeID string) (*donburi.Entry, bool) {
	return h.State().GetNode(nodeID)
}
func (h *stubCoordinatorHost) RunTurnResolution() {
	if h.runTurnResolution != nil {
		h.runTurnResolution()
	}
}
func (h *stubCoordinatorHost) BroadcastTurnReport()            {}
func (h *stubCoordinatorHost) ShouldStopAfterResolution() bool { return false }
func (h *stubCoordinatorHost) HandleDraw()                     { h.runtime.State().IsOver = true }
func (h *stubCoordinatorHost) CheckGameOver()                  {}
