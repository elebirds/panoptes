package session

import (
	"context"
	"encoding/json"
	"strings"
	"sync"
	"testing"
	"time"

	"github.com/elebirds/panoptes/internal/config"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/engine/maploader"
	ministerengine "github.com/elebirds/panoptes/internal/engine/minister"
	"github.com/elebirds/panoptes/internal/game/ai"
	"github.com/elebirds/panoptes/internal/game/participant"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/llm"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
	"google.golang.org/protobuf/proto"
)

type capturePlayer struct {
	playerID string
	username string
	sent     []proto.Message
}

func (p *capturePlayer) PlayerID() string { return p.playerID }

func (p *capturePlayer) Username() string { return p.username }

func (p *capturePlayer) IsBot() bool { return false }

func (p *capturePlayer) Send(_ context.Context, msg proto.Message) error {
	p.sent = append(p.sent, msg)
	return nil
}

type captureBotPlayer struct {
	playerID string
	username string
}

func (p *captureBotPlayer) PlayerID() string { return p.playerID }

func (p *captureBotPlayer) Username() string { return p.username }

func (p *captureBotPlayer) IsBot() bool { return true }

func (p *captureBotPlayer) Send(context.Context, proto.Message) error { return nil }

type captureTransport struct {
	humans map[string]*capturePlayer
}

func (t *captureTransport) Send(_ context.Context, participantID string, msg proto.Message) error {
	if t == nil || t.humans == nil {
		return nil
	}
	player := t.humans[participantID]
	if player == nil {
		return nil
	}
	player.sent = append(player.sent, msg)
	return nil
}

func (t *captureTransport) Broadcast(context.Context, string, proto.Message) error { return nil }

func (t *captureTransport) Stream(context.Context, string, <-chan proto.Message) error { return nil }

type scriptedLLMClient struct {
	mu       sync.Mutex
	chunks   []string
	err      error
	gate     <-chan struct{}
	requests []llm.CompletionRequest
}

func (c *scriptedLLMClient) Stream(ctx context.Context, req llm.CompletionRequest) (<-chan string, error) {
	c.mu.Lock()
	c.requests = append(c.requests, req)
	err := c.err
	chunks := append([]string(nil), c.chunks...)
	gate := c.gate
	c.mu.Unlock()
	if err != nil {
		return nil, err
	}
	out := make(chan string, len(chunks))
	go func() {
		defer close(out)
		if gate != nil {
			select {
			case <-gate:
			case <-ctx.Done():
				return
			}
		}
		for _, chunk := range chunks {
			select {
			case <-ctx.Done():
				return
			case out <- chunk:
			}
		}
	}()
	return out, nil
}

func newTestRuntime(id string, humans []*capturePlayer, bots []*captureBotPlayer) *Runtime {
	transport := &captureTransport{humans: make(map[string]*capturePlayer, len(humans))}
	bindings := make([]ParticipantBinding, 0, len(humans)+len(bots))
	for _, human := range humans {
		if human == nil {
			continue
		}
		transport.humans[human.playerID] = human
		bindings = append(bindings, ParticipantBinding{
			Participant: participant.Participant{
				ID:       human.playerID,
				Username: human.username,
				Kind:     participant.KindHuman,
			},
			Controller: HumanController{},
		})
	}
	for _, bot := range bots {
		if bot == nil {
			continue
		}
		bindings = append(bindings, ParticipantBinding{
			Participant: participant.Participant{
				ID:       bot.playerID,
				Username: bot.username,
				Kind:     participant.KindBot,
			},
			Controller: NewAutonomousController(ai.RuleBotProvider{}),
		})
	}
	return NewRuntime(id, bindings, transport, nil)
}

func TestRuntimeBootstrapDuringPlanningSendsPlanningStartWithSnapshotAndCurrentTokens(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Manifest: staticdata.Manifest{DefaultMapID: "default"},
		Rules:    staticdata.Rules{TurnTimeLimitPlanning: 30, TokensPerTurn: 3},
		Technologies: []staticdata.TechnologyDefinition{
			{ID: "agrarian_foundations", Name: "Agrarian Foundations", ResearchCost: 2},
		},
		Policies: []staticdata.PolicyDefinition{
			{ID: "reorganization", Name: "Reorganization", Layer: "national"},
			{ID: "expansion", Name: "Expansion", Layer: "national"},
		},
		Ministers: []staticdata.Minister{
			{ID: "m002", Name: "沈衡", Role: "domestic", Ability: 7, Personality: "steady"},
		},
		Maps: []staticdata.MapCatalogEntry{
			{ID: "default", Name: "Default", Width: 2, Height: 2},
		},
	}, &staticdata.MapRuntimeBundle{
		ID:     "default",
		Name:   "Default",
		Width:  2,
		Height: 2,
		Nodes: []staticdata.MapRuntimeNode{
			{ID: "A1", X: 0, Y: 0, Terrain: "plain"},
		},
	}))

	player := &capturePlayer{playerID: "player-1", username: "alice"}
	runtime := newTestRuntime("game-1", []*capturePlayer{player}, nil)
	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	state.Turn = 4
	state.Phase = domain.PhasePlanning.String()
	state.Players["player-1"].TokensLeft = 1
	state.Players["player-1"].Policy = domain.Policy("reorganization")

	if err := runtime.InitializePrepared(state); err != nil {
		t.Fatalf("InitializePrepared() error = %v", err)
	}

	if err := runtime.HandleStaticCatalogSyncRequest(context.Background(), "player-1", &pb.MsgStaticCatalogSyncRequest{
		BundleHash: staticdata.Default().BundleHash(),
	}); err != nil {
		t.Fatalf("HandleStaticCatalogSyncRequest() error = %v", err)
	}

	if len(player.sent) != 5 {
		t.Fatalf("send count = %d, want 5", len(player.sent))
	}

	if got := player.sent[0].ProtoReflect().Descriptor().Name(); got != "MsgStaticCatalogManifest" {
		t.Fatalf("message[0] = %s, want MsgStaticCatalogManifest", got)
	}

	if got := player.sent[1].ProtoReflect().Descriptor().Name(); got != "MsgStaticCatalogSyncComplete" {
		t.Fatalf("message[1] = %s, want MsgStaticCatalogSyncComplete", got)
	}

	configMsg := player.sent[2]
	if got := configMsg.ProtoReflect().Descriptor().Name(); got != "MsgConfigBatchJson" {
		t.Fatalf("message[2] = %s, want MsgConfigBatchJson", got)
	}
	configsField := configMsg.ProtoReflect().Descriptor().Fields().ByName("configs")
	if configsField == nil {
		t.Fatalf("MsgConfigBatchJson.configs field missing")
	}
	configs := configMsg.ProtoReflect().Get(configsField).List()
	if configs.Len() != 1 {
		t.Fatalf("configs len = %d, want 1", configs.Len())
	}
	entry := configs.Get(0).Message()
	keyField := entry.Descriptor().Fields().ByName("key")
	jsonField := entry.Descriptor().Fields().ByName("json")
	if keyField == nil || jsonField == nil {
		t.Fatalf("config entry fields missing")
	}
	if got := entry.Get(keyField).String(); got != "mapconfig" {
		t.Fatalf("config key = %q, want mapconfig", got)
	}
	var payload struct {
		ID     string `json:"id"`
		Width  int    `json:"width"`
		Height int    `json:"height"`
		Nodes  []struct {
			ID string `json:"id"`
		} `json:"nodes"`
	}
	if err := json.Unmarshal([]byte(entry.Get(jsonField).String()), &payload); err != nil {
		t.Fatalf("unmarshal mapconfig json: %v", err)
	}
	if payload.ID != "default" {
		t.Fatalf("mapconfig.id = %q, want default", payload.ID)
	}
	if len(payload.Nodes) != 1 || payload.Nodes[0].ID != "A1" {
		t.Fatalf("mapconfig nodes = %+v, want A1", payload.Nodes)
	}

	gameInit, ok := player.sent[3].(*pb.MsgGameInit)
	if !ok {
		t.Fatalf("message[3] type = %T, want MsgGameInit", player.sent[3])
	}
	if got := gameInit.ProtoReflect().Descriptor().Name(); got != "MsgGameInit" {
		t.Fatalf("message[3] = %s, want MsgGameInit", got)
	}
	if len(gameInit.GetMinisters()) != 1 || gameInit.GetMinisters()[0].GetRole() != "domestic" {
		t.Fatalf("game init ministers = %#v, want domestic roster", gameInit.GetMinisters())
	}

	start, ok := player.sent[4].(*pb.MsgPlanningStart)
	if !ok {
		t.Fatalf("message type = %T, want MsgPlanningStart", player.sent[4])
	}
	if got := start.GetTokens(); got != 1 {
		t.Fatalf("tokens = %d, want 1", got)
	}
	if got := start.GetTurn(); got != 4 {
		t.Fatalf("turn = %d, want 4", got)
	}
	if start.GetMyPlayer() == nil {
		t.Fatalf("my_player is nil")
	}
	if start.GetSnapshot() == nil {
		t.Fatalf("snapshot is nil")
	}
	if len(start.GetMinisterDrafts()) == 0 {
		t.Fatalf("planning start should include minister drafts")
	}
	if len(start.GetSnapshot().GetMinisterDrafts()) == 0 {
		t.Fatalf("planning snapshot should include minister drafts")
	}
	var ministerPayload struct {
		DraftID      string `json:"draft_id"`
		MinisterRole string `json:"minister_role"`
		Status       string `json:"status"`
	}
	if err := json.Unmarshal([]byte(start.GetMinisterDrafts()[0].GetJsonPayload()), &ministerPayload); err != nil {
		t.Fatalf("unmarshal planning start minister draft payload: %v", err)
	}
	if ministerPayload.DraftID == "" || ministerPayload.MinisterRole != "domestic" || ministerPayload.Status != "pending" {
		t.Fatalf("planning start minister payload = %+v, want domestic pending draft", ministerPayload)
	}
	if len(start.GetPlanningStartEvents()) != 0 {
		t.Fatalf("planning_start_events len = %d, want 0 without pending activations", len(start.GetPlanningStartEvents()))
	}
	for idx, msg := range player.sent {
		if _, ok := msg.(*pb.MsgGameChatSync); ok {
			t.Fatalf("message[%d] unexpectedly sends MsgGameChatSync in current MVP", idx)
		}
	}
}

func TestRuntimeBootstrapOutsidePlanningDoesNotSendPlanningStart(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Manifest: staticdata.Manifest{DefaultMapID: "default"},
		Rules:    staticdata.Rules{TurnTimeLimitPlanning: 30, TokensPerTurn: 3},
		Maps: []staticdata.MapCatalogEntry{
			{ID: "default", Name: "Default", Width: 1, Height: 1},
		},
	}, &staticdata.MapRuntimeBundle{
		ID:     "default",
		Name:   "Default",
		Width:  1,
		Height: 1,
		Nodes: []staticdata.MapRuntimeNode{
			{ID: "A1", X: 0, Y: 0, Terrain: "plain"},
		},
	}))

	player := &capturePlayer{playerID: "player-1", username: "alice"}
	runtime := newTestRuntime("game-1", []*capturePlayer{player}, nil)
	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	state.Turn = 4
	state.Phase = domain.PhaseResolving.String()

	if err := runtime.InitializePrepared(state); err != nil {
		t.Fatalf("InitializePrepared() error = %v", err)
	}

	if err := runtime.HandleStaticCatalogSyncRequest(context.Background(), "player-1", &pb.MsgStaticCatalogSyncRequest{
		BundleHash: staticdata.Default().BundleHash(),
	}); err != nil {
		t.Fatalf("HandleStaticCatalogSyncRequest() error = %v", err)
	}

	if len(player.sent) != 4 {
		t.Fatalf("send count = %d, want 4", len(player.sent))
	}
	if got := player.sent[1].ProtoReflect().Descriptor().Name(); got != "MsgStaticCatalogSyncComplete" {
		t.Fatalf("message[1] = %s, want MsgStaticCatalogSyncComplete", got)
	}
	if got := player.sent[2].ProtoReflect().Descriptor().Name(); got != "MsgConfigBatchJson" {
		t.Fatalf("message[2] = %s, want MsgConfigBatchJson", got)
	}
	if _, ok := player.sent[len(player.sent)-1].(*pb.MsgPlanningStart); ok {
		t.Fatalf("unexpected planning start in resolving bootstrap")
	}
}

func TestRuntimeInitializePreparedSendsManifestOnlyUntilCatalogSyncRequest(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Manifest: staticdata.Manifest{
			DefaultMapID: "default",
		},
		Rules: staticdata.Rules{TurnTimeLimitPlanning: 30, TokensPerTurn: 3},
		Maps: []staticdata.MapCatalogEntry{
			{ID: "default", Name: "Default", Width: 1, Height: 1},
		},
	}, &staticdata.MapRuntimeBundle{
		ID:     "default",
		Name:   "Default",
		Width:  1,
		Height: 1,
		Nodes: []staticdata.MapRuntimeNode{
			{ID: "A1", X: 0, Y: 0, Terrain: "plain"},
		},
	}))

	player := &capturePlayer{playerID: "player-1", username: "alice"}
	runtime := newTestRuntime("game-1", []*capturePlayer{player}, nil)
	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	state.Phase = domain.PhaseResolving.String()

	if err := runtime.InitializePrepared(state); err != nil {
		t.Fatalf("InitializePrepared() error = %v", err)
	}

	if len(player.sent) != 1 {
		t.Fatalf("send count = %d, want 1", len(player.sent))
	}
	if got := player.sent[0].ProtoReflect().Descriptor().Name(); got != "MsgStaticCatalogManifest" {
		t.Fatalf("message[0] = %s, want MsgStaticCatalogManifest", got)
	}
}

func TestRuntimeCatalogSyncRequestWithMatchingHashSendsSyncCompleteThenBootstrapRemainder(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Manifest: staticdata.Manifest{
			DefaultMapID: "default",
		},
		Rules: staticdata.Rules{TurnTimeLimitPlanning: 30, TokensPerTurn: 3},
		Maps: []staticdata.MapCatalogEntry{
			{ID: "default", Name: "Default", Width: 1, Height: 1},
		},
	}, &staticdata.MapRuntimeBundle{
		ID:     "default",
		Name:   "Default",
		Width:  1,
		Height: 1,
		Nodes: []staticdata.MapRuntimeNode{
			{ID: "A1", X: 0, Y: 0, Terrain: "plain"},
		},
	}))

	player := &capturePlayer{playerID: "player-1", username: "alice"}
	runtime := newTestRuntime("game-1", []*capturePlayer{player}, nil)
	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	state.Phase = domain.PhaseResolving.String()

	if err := runtime.InitializePrepared(state); err != nil {
		t.Fatalf("InitializePrepared() error = %v", err)
	}

	if err := runtime.HandleStaticCatalogSyncRequest(context.Background(), "player-1", &pb.MsgStaticCatalogSyncRequest{
		BundleHash: staticdata.Default().BundleHash(),
	}); err != nil {
		t.Fatalf("HandleStaticCatalogSyncRequest() error = %v", err)
	}

	if len(player.sent) != 4 {
		t.Fatalf("send count = %d, want 4", len(player.sent))
	}
	if got := player.sent[1].ProtoReflect().Descriptor().Name(); got != "MsgStaticCatalogSyncComplete" {
		t.Fatalf("message[1] = %s, want MsgStaticCatalogSyncComplete", got)
	}
	if got := player.sent[2].ProtoReflect().Descriptor().Name(); got != "MsgConfigBatchJson" {
		t.Fatalf("message[2] = %s, want MsgConfigBatchJson", got)
	}
	if got := player.sent[3].ProtoReflect().Descriptor().Name(); got != "MsgGameInit" {
		t.Fatalf("message[3] = %s, want MsgGameInit", got)
	}
}

func TestPreparePlanningStartStateIfNeededRunsOnlyOncePerTurn(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TokensPerTurn:             3,
			BaseResearchOutputPerTurn: 1,
		},
		Technologies: []staticdata.TechnologyDefinition{
			{
				ID:           "agrarian_foundations",
				ResearchCost: 2,
				ExplicitEffects: []staticdata.ExplicitEffect{
					{Type: "unlock_building", TargetID: "farm"},
				},
			},
			{
				ID:           "masonry",
				ResearchCost: 2,
				ExplicitEffects: []staticdata.ExplicitEffect{
					{Type: "unlock_building", TargetID: "wall"},
				},
			},
		},
	}))

	runtime := NewRuntime("game-1", nil, nil, nil)
	runtime.state = domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	runtime.state.Phase = domain.PhasePlanning.String()
	runtime.state.Turn = 3
	runtime.state.Players["player-1"].Research.MarkTechnologyCompleted("agrarian_foundations", 2)

	runtime.PreparePlanningStartStateIfNeeded()
	if got := runtime.planningStartPreparedTurn; got != 3 {
		t.Fatalf("planningStartPreparedTurn = %d, want 3", got)
	}
	if !runtime.state.IsBuildingUnlocked("player-1", "farm") {
		t.Fatalf("farm should unlock on first planning-start prepare")
	}
	firstResult := runtime.PlanningStartResult()
	if firstResult == nil {
		t.Fatalf("planning start result should be cached after first prepare")
	}
	if len(firstResult.Events) == 0 {
		t.Fatalf("planning start result should include activation events")
	}

	runtime.state.Players["player-1"].Research.MarkTechnologyCompleted("masonry", 2)
	runtime.PreparePlanningStartStateIfNeeded()
	if runtime.state.IsBuildingUnlocked("player-1", "wall") {
		t.Fatalf("second prepare in same turn should be skipped")
	}
	if runtime.PlanningStartResult() != firstResult {
		t.Fatalf("planning start result should be reused within the same turn")
	}

	runtime.state.Turn = 4
	runtime.PreparePlanningStartStateIfNeeded()
	if !runtime.state.IsBuildingUnlocked("player-1", "wall") {
		t.Fatalf("prepare after turn advance should run again")
	}
	if runtime.PlanningStartResult() == firstResult {
		t.Fatalf("planning start result should refresh after turn advance")
	}
}

func TestPreparePlanningStartStateIfNeededAppliesPreparedMinisterDrafts(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TokensPerTurn:             3,
			BaseResearchOutputPerTurn: 1,
		},
		Technologies: []staticdata.TechnologyDefinition{
			{ID: "agrarian_foundations", Name: "Agrarian Foundations", ResearchCost: 2},
		},
		Policies: []staticdata.PolicyDefinition{
			{ID: "reorganization", Name: "Reorganization", Layer: "national"},
			{ID: "expansion", Name: "Expansion", Layer: "national"},
		},
	}))

	player := &capturePlayer{playerID: "player-1", username: "alice"}
	runtime := newTestRuntime("game-1", []*capturePlayer{player}, nil)
	runtime.state = domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	runtime.state.Phase = domain.PhasePlanning.String()
	runtime.state.Turn = 3
	runtime.state.Players["player-1"].Policy = domain.Policy("reorganization")

	runtime.PrepareMinisterDraftCacheForTurn(3)
	clear(runtime.state.TurnRuntime.Planning.MinisterDrafts)
	runtime.PreparePlanningStartStateIfNeeded()

	drafts := runtime.state.TurnRuntime.Planning.MinisterDraftsForPlayer("player-1")
	if len(drafts) == 0 {
		t.Fatalf("planning start should apply prepared minister drafts")
	}
	if drafts[0].MinisterRole != "domestic" || drafts[0].Turn != 3 {
		t.Fatalf("minister drafts = %#v, want domestic turn 3 draft", drafts)
	}
}

func TestPrepareMinisterDraftCacheForTurnPolishesDraftsWithMinisterEngine(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{TokensPerTurn: 3, BaseResearchOutputPerTurn: 1},
		Technologies: []staticdata.TechnologyDefinition{
			{ID: "agrarian_foundations", Name: "Agrarian Foundations", ResearchCost: 2},
		},
		Ministers: []staticdata.Minister{
			{ID: "m002", Name: "沈衡", Role: "domestic", Ability: 7, Personality: "steady", PersonalityDesc: "稳健审慎", Loyalty: 8, Ambition: 4},
		},
	}))

	player := &capturePlayer{playerID: "player-1", username: "alice"}
	runtime := newTestRuntime("game-1", []*capturePlayer{player}, nil)
	runtime.state = domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	runtime.state.Turn = 3
	runtime.state.Phase = domain.PhaseResolving.String()

	llmClient := &scriptedLLMClient{
		chunks: []string{`{"title":"稳住粮秣","summary":"先把农业基础稳住。","rationale":"当前局势更适合补基础产能。","risk_note":"会延后更激进的路线。"}`},
	}
	engine := ministerengine.NewMinisterEngine(llmClient)
	engine.SetEnabledRoles([]string{"domestic"})
	runtime.SetMinisterEngine(engine)

	runtime.PrepareMinisterDraftCacheForTurn(3)

	deadline := time.Now().Add(2 * time.Second)
	for {
		runtime.preparedMinisterDraftsMu.RLock()
		drafts := append([]domain.MinisterDraft(nil), runtime.preparedMinisterDrafts[3]["player-1"]...)
		runtime.preparedMinisterDraftsMu.RUnlock()
		if len(drafts) == 1 && drafts[0].Source == domain.MinisterDraftSourceRuleLLM {
			if drafts[0].Title != "稳住粮秣" || drafts[0].Summary != "先把农业基础稳住。" {
				t.Fatalf("polished draft = %#v, want LLM text applied", drafts[0])
			}
			return
		}
		if time.Now().After(deadline) {
			t.Fatalf("drafts were not polished in time, got %#v", drafts)
		}
		time.Sleep(10 * time.Millisecond)
	}
}

func TestPrepareMinisterDraftCacheForTurnDropsLatePolishAfterPlanningStartPrepared(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{TokensPerTurn: 3, BaseResearchOutputPerTurn: 1},
		Technologies: []staticdata.TechnologyDefinition{
			{ID: "agrarian_foundations", Name: "Agrarian Foundations", ResearchCost: 2},
		},
		Ministers: []staticdata.Minister{
			{ID: "m002", Name: "沈衡", Role: "domestic", Ability: 7, Personality: "steady", PersonalityDesc: "稳健审慎", Loyalty: 8, Ambition: 4},
		},
	}))

	player := &capturePlayer{playerID: "player-1", username: "alice"}
	runtime := newTestRuntime("game-1", []*capturePlayer{player}, nil)
	runtime.state = domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	runtime.state.Turn = 4
	runtime.state.Phase = domain.PhasePlanning.String()

	release := make(chan struct{})
	llmClient := &scriptedLLMClient{
		chunks: []string{`{"title":"晚到建议","summary":"不该覆盖已开回合卡片。","rationale":"测试晚到丢弃。","risk_note":"无。"}`},
		gate:   release,
	}
	engine := ministerengine.NewMinisterEngine(llmClient)
	engine.SetEnabledRoles([]string{"domestic"})
	runtime.SetMinisterEngine(engine)

	runtime.PrepareMinisterDraftCacheForTurn(4)
	runtime.PreparePlanningStartStateIfNeeded()
	close(release)
	time.Sleep(50 * time.Millisecond)

	runtime.preparedMinisterDraftsMu.RLock()
	drafts := append([]domain.MinisterDraft(nil), runtime.preparedMinisterDrafts[4]["player-1"]...)
	runtime.preparedMinisterDraftsMu.RUnlock()
	if len(drafts) != 1 {
		t.Fatalf("draft count = %d, want 1", len(drafts))
	}
	if drafts[0].Source != domain.MinisterDraftSourceRuleOnly {
		t.Fatalf("draft source = %q, want rule_only for late polish", drafts[0].Source)
	}
	if drafts[0].Title == "晚到建议" {
		t.Fatalf("late LLM polish should be dropped, got %#v", drafts[0])
	}
}

func TestRuntimeBootstrapPlanningStartIncludesProjectedActivationEvents(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{TurnTimeLimitPlanning: 30, TokensPerTurn: 3},
		Technologies: []staticdata.TechnologyDefinition{
			{
				ID:           "agrarian_foundations",
				ResearchCost: 2,
				ExplicitEffects: []staticdata.ExplicitEffect{
					{Type: "unlock_building", TargetID: "farm"},
				},
			},
		},
	}))

	player := &capturePlayer{playerID: "player-1", username: "alice"}
	runtime := newTestRuntime("game-1", []*capturePlayer{player}, nil)
	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	state.Turn = 4
	state.Phase = domain.PhasePlanning.String()
	state.Players["player-1"].Research.MarkTechnologyCompleted("agrarian_foundations", 3)

	if err := runtime.InitializePrepared(state); err != nil {
		t.Fatalf("InitializePrepared() error = %v", err)
	}
	if err := runtime.HandleStaticCatalogSyncRequest(context.Background(), "player-1", &pb.MsgStaticCatalogSyncRequest{
		BundleHash: staticdata.Default().BundleHash(),
	}); err != nil {
		t.Fatalf("HandleStaticCatalogSyncRequest() error = %v", err)
	}

	start, ok := player.sent[len(player.sent)-1].(*pb.MsgPlanningStart)
	if !ok {
		t.Fatalf("message type = %T, want MsgPlanningStart", player.sent[len(player.sent)-1])
	}
	if len(start.GetPlanningStartEvents()) != 1 {
		t.Fatalf("planning_start_events len = %d, want 1", len(start.GetPlanningStartEvents()))
	}
	if got := start.GetPlanningStartEvents()[0].GetKind(); got != "technology_activated" {
		t.Fatalf("planning_start_events[0].type = %q, want technology_activated", got)
	}
}

func TestRuntimeInitializePreparedDevModeKeepsBuildingAndRecipeLocked(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Buildings: []staticdata.BuildingDefinition{
			{ID: "barracks"},
		},
		Recipes: []staticdata.RecipeDefinition{
			{ID: "barracks_infantry"},
		},
		Technologies: []staticdata.TechnologyDefinition{
			{
				ID: "military_foundation",
				ExplicitEffects: []staticdata.ExplicitEffect{
					{Type: "unlock_building", TargetID: "barracks"},
					{Type: "unlock_recipe", TargetID: "barracks_infantry"},
				},
			},
		},
	}))

	runtime := NewRuntime("game-dev", nil, nil, &config.Config{DevMode: true})
	state := domain.NewGameState("game-dev", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	state.World = donburi.NewWorld()

	if err := runtime.InitializePrepared(state); err != nil {
		t.Fatalf("InitializePrepared() error = %v", err)
	}

	if runtime.state.IsBuildingUnlocked("player-1", "barracks") {
		t.Fatalf("building barracks should remain locked in dev mode")
	}
	if runtime.state.IsRecipeUnlocked("player-1", "barracks_infantry") {
		t.Fatalf("recipe barracks_infantry should remain locked in dev mode")
	}
}

func TestRuntimeInitializePreparedNonDevModeKeepsBuildingAndRecipeLocked(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Buildings: []staticdata.BuildingDefinition{
			{ID: "barracks"},
		},
		Recipes: []staticdata.RecipeDefinition{
			{ID: "barracks_infantry"},
		},
		Technologies: []staticdata.TechnologyDefinition{
			{
				ID: "military_foundation",
				ExplicitEffects: []staticdata.ExplicitEffect{
					{Type: "unlock_building", TargetID: "barracks"},
					{Type: "unlock_recipe", TargetID: "barracks_infantry"},
				},
			},
		},
	}))

	runtime := NewRuntime("game-prod", nil, nil, &config.Config{DevMode: false})
	state := domain.NewGameState("game-prod", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	state.World = donburi.NewWorld()

	if err := runtime.InitializePrepared(state); err != nil {
		t.Fatalf("InitializePrepared() error = %v", err)
	}

	if runtime.state.IsBuildingUnlocked("player-1", "barracks") {
		t.Fatalf("building barracks should remain locked when dev mode is disabled")
	}
	if runtime.state.IsRecipeUnlocked("player-1", "barracks_infantry") {
		t.Fatalf("recipe barracks_infantry should remain locked when dev mode is disabled")
	}
}

func TestRuntimeInitializeBootstrapsCapitalOnProceduralSpawn(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Manifest: staticdata.Manifest{
			SchemaVersion:  "2026-04-15",
			ContentVersion: "runtime-bootstrap-test",
			BundleHash:     "runtime-bootstrap-test",
			DefaultLocale:  "zh-CN",
			DefaultMapID:   "runtime_bootstrap",
		},
		Rules: staticdata.Rules{
			TurnTimeLimitPlanning:      30,
			TokensPerTurn:              3,
			CityCoreMaxHP:              100,
			SafeZoneRadius:             3,
			FacilityTakeoverTurns:      2,
			BaseResearchOutputPerTurn:  1,
			BaseIndustryOutputPerTurn:  2,
			MinimumCityDistance:        2,
			InitialCityTerritoryRadius: 1,
		},
		Buildings: []staticdata.BuildingDefinition{
			{
				ID:            "city_core",
				PlacementKind: "city_foundation_center",
				BuildingScope: "city_core",
				MaxHP:         100,
				TakeoverMode:  "disabled",
			},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
	}, &staticdata.MapRuntimeBundle{
		ID:     "runtime_bootstrap",
		Name:   "runtime_bootstrap",
		Width:  8,
		Height: 8,
		Nodes:  runtimeBootstrapNodes(8, 8),
	}))

	player := &capturePlayer{playerID: "player-1", username: "alice"}
	runtime := newTestRuntime("game-1", []*capturePlayer{player}, nil)

	if err := runtime.Initialize(); err != nil {
		t.Fatalf("Initialize() error = %v", err)
	}

	state := runtime.State()
	if state == nil {
		t.Fatalf("state is nil")
	}
	playerState := state.Players["player-1"]
	if playerState == nil {
		t.Fatalf("player-1 missing")
	}
	if playerState.CapitalCityID == "" {
		t.Fatalf("CapitalCityID should be initialized")
	}
	cityState := playerState.Cities[playerState.CapitalCityID]
	if cityState == nil {
		t.Fatalf("capital city state missing for %q", playerState.CapitalCityID)
	}
	spawnPos, ok := state.Map.PlayerSpawns["player-1"]
	if !ok {
		t.Fatalf("player spawn missing")
	}
	entry, ok := domain.GetNodeAt(state.World, spawnPos)
	if !ok {
		t.Fatalf("spawn node missing")
	}
	if !entry.HasComponent(ecs.BuildingC) {
		t.Fatalf("spawn node should have a city_core")
	}
	building := ecs.BuildingC.Get(entry)
	if got := string(building.Type); got != "city_core" {
		t.Fatalf("spawn building type = %q, want city_core", got)
	}
	if got := building.Owner; got != "player-1" {
		t.Fatalf("city_core owner = %q, want player-1", got)
	}
}

func TestRuntimeInitializeSpawnsInitialInfantryButNotSettler(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Manifest: staticdata.Manifest{
			SchemaVersion:  "2026-04-15",
			ContentVersion: "runtime-bootstrap-test",
			BundleHash:     "runtime-bootstrap-test",
			DefaultLocale:  "zh-CN",
			DefaultMapID:   "runtime_bootstrap",
		},
		Rules: staticdata.Rules{
			TurnTimeLimitPlanning:      30,
			TokensPerTurn:              3,
			CityCoreMaxHP:              100,
			SafeZoneRadius:             3,
			FacilityTakeoverTurns:      2,
			BaseResearchOutputPerTurn:  1,
			BaseIndustryOutputPerTurn:  2,
			MinimumCityDistance:        2,
			InitialCityTerritoryRadius: 1,
		},
		Buildings: []staticdata.BuildingDefinition{
			{
				ID:            "city_core",
				PlacementKind: "city_foundation_center",
				BuildingScope: "city_core",
				MaxHP:         100,
				TakeoverMode:  "disabled",
			},
		},
		Units: []staticdata.UnitDefinition{
			{
				ID:          "settler",
				Class:       "civilian",
				MaxHP:       12,
				Attack:      0,
				AttackRange: 0,
				MoveRange:   2,
				VisionRange: 2,
				Flags:       staticdata.UnitFlags{CanCapture: true},
			},
			{
				ID:          "infantry",
				Class:       "melee",
				MaxHP:       30,
				Attack:      10,
				AttackRange: 1,
				MoveRange:   2,
				VisionRange: 3,
				Flags:       staticdata.UnitFlags{CanCapture: true, CanAttackStructures: true},
			},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
			{ID: "forest", Passable: true, Buildable: true},
			{ID: "mountain", Passable: false, Buildable: false},
			{ID: "river", Passable: false, Buildable: false},
			{ID: "snow", Passable: true, Buildable: true},
		},
	}, &staticdata.MapRuntimeBundle{
		ID:     "runtime_bootstrap",
		Name:   "runtime_bootstrap",
		Width:  8,
		Height: 8,
		Nodes:  runtimeBootstrapNodes(8, 8),
	}))

	player := &capturePlayer{playerID: "player-1", username: "alice"}
	runtime := newTestRuntime("game-1", []*capturePlayer{player}, nil)

	if err := runtime.Initialize(); err != nil {
		t.Fatalf("Initialize() error = %v", err)
	}

	state := runtime.State()
	playerState := state.Players["player-1"]
	if playerState == nil {
		t.Fatalf("player-1 missing")
	}
	if playerState.CapitalCityID == "" {
		t.Fatalf("CapitalCityID should be initialized")
	}
	if _, ok := findOwnedUnitEntryByType(state.World, "player-1", "settler"); ok {
		t.Fatalf("player-1 should not receive an initial settler")
	}
	if _, ok := findOwnedUnitEntryByType(state.World, "player-1", "infantry"); !ok {
		t.Fatalf("player-1 should receive an initial infantry")
	}
	if got := countOwnedUnitsByType(state.World, "player-1", "infantry"); got != 1 {
		t.Fatalf("player-1 infantry count = %d, want 1", got)
	}
}

func TestBootstrapStartingPlayersPlacesInitialInfantryAdjacentToCapitalWhenAvailable(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Manifest: staticdata.Manifest{DefaultMapID: "runtime_bootstrap"},
		Rules: staticdata.Rules{
			TokensPerTurn:              3,
			CityCoreMaxHP:              100,
			BaseResearchOutputPerTurn:  1,
			BaseIndustryOutputPerTurn:  2,
			InitialCityTerritoryRadius: 1,
		},
		Buildings: []staticdata.BuildingDefinition{
			{
				ID:            "city_core",
				PlacementKind: "city_foundation_center",
				BuildingScope: "city_core",
				MaxHP:         100,
				TakeoverMode:  "disabled",
			},
		},
		Units: []staticdata.UnitDefinition{
			{
				ID:          "infantry",
				Class:       "melee",
				MaxHP:       30,
				Attack:      10,
				AttackRange: 1,
				MoveRange:   2,
				VisionRange: 3,
				Flags:       staticdata.UnitFlags{CanCapture: true, CanAttackStructures: true},
			},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
	}))

	spawnPos := domain.Position{Q: 1, R: 1}
	state := newBootstrapStateForSinglePlayer(6, 6, spawnPos)
	player := &capturePlayer{playerID: "player-1", username: "alice"}
	runtime := newTestRuntime("game-1", []*capturePlayer{player}, nil)
	runtime.state = state

	if err := runtime.bootstrapStartingPlayers(); err != nil {
		t.Fatalf("bootstrapStartingPlayers() error = %v", err)
	}

	if got := countOwnedUnitsByType(state.World, "player-1", "infantry"); got != 1 {
		t.Fatalf("player-1 infantry count = %d, want 1", got)
	}
	unitEntry, ok := findOwnedUnitEntryByType(state.World, "player-1", "infantry")
	if !ok {
		t.Fatalf("player-1 infantry missing")
	}
	gotPos := ecs.PositionC.Get(unitEntry)
	if gotPos.Q != 2 || gotPos.R != 1 {
		t.Fatalf("initial infantry position = (%d,%d), want first axial neighbor", gotPos.Q, gotPos.R)
	}
}

func TestBootstrapStartingPlayersSkipsInfantryWhenNoSafeSpawnExists(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Manifest: staticdata.Manifest{DefaultMapID: "runtime_bootstrap"},
		Rules: staticdata.Rules{
			TokensPerTurn:              3,
			CityCoreMaxHP:              100,
			BaseResearchOutputPerTurn:  1,
			BaseIndustryOutputPerTurn:  2,
			InitialCityTerritoryRadius: 1,
		},
		Buildings: []staticdata.BuildingDefinition{
			{
				ID:            "city_core",
				PlacementKind: "city_foundation_center",
				BuildingScope: "city_core",
				MaxHP:         100,
				TakeoverMode:  "disabled",
			},
			{
				ID:            "farm",
				PlacementKind: "city_territory",
				BuildingScope: "out_of_city",
				MaxHP:         60,
				TakeoverMode:  "delayed",
			},
		},
		Units: []staticdata.UnitDefinition{
			{
				ID:          "infantry",
				Class:       "melee",
				MaxHP:       30,
				Attack:      10,
				AttackRange: 1,
				MoveRange:   2,
				VisionRange: 3,
				Flags:       staticdata.UnitFlags{CanCapture: true, CanAttackStructures: true},
			},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
			{ID: "mountain", Passable: false, Buildable: false},
		},
	}))

	spawnPos := domain.Position{Q: 1, R: 1}
	state := newBootstrapStateForSinglePlayer(3, 3, spawnPos)
	setNodeTerrain(t, state, domain.Position{Q: 2, R: 1}, "mountain")
	setNodeTerrain(t, state, domain.Position{Q: 2, R: 0}, "mountain")
	setNodeTerrain(t, state, domain.Position{Q: 1, R: 0}, "mountain")
	ecs.CreateUnit(state.World, "infantry", "player-2", domain.Position{Q: 0, R: 1})
	setNodeTerrain(t, state, domain.Position{Q: 0, R: 2}, "mountain")
	downEntry := mustGetNodeAt(t, state, domain.Position{Q: 1, R: 2})
	ecs.CreateBuilding(state.World, "farm", "player-2", "", downEntry)

	player := &capturePlayer{playerID: "player-1", username: "alice"}
	runtime := newTestRuntime("game-1", []*capturePlayer{player}, nil)
	runtime.state = state

	if err := runtime.bootstrapStartingPlayers(); err != nil {
		t.Fatalf("bootstrapStartingPlayers() error = %v", err)
	}

	if got := countOwnedUnitsByType(state.World, "player-1", "infantry"); got != 0 {
		t.Fatalf("player-1 infantry count = %d, want 0 when no safe spawn exists", got)
	}
}

func TestRuntimeInitializeIncludesBotParticipantsInAuthoritativeState(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Manifest: staticdata.Manifest{
			SchemaVersion:  "2026-04-15",
			ContentVersion: "runtime-bootstrap-test",
			BundleHash:     "runtime-bootstrap-test",
			DefaultLocale:  "zh-CN",
			DefaultMapID:   "runtime_bootstrap",
		},
		Rules: staticdata.Rules{
			TurnTimeLimitPlanning:      30,
			TokensPerTurn:              3,
			CityCoreMaxHP:              100,
			SafeZoneRadius:             3,
			FacilityTakeoverTurns:      2,
			BaseResearchOutputPerTurn:  1,
			BaseIndustryOutputPerTurn:  2,
			MinimumCityDistance:        2,
			InitialCityTerritoryRadius: 1,
		},
		Buildings: []staticdata.BuildingDefinition{
			{
				ID:            "city_core",
				PlacementKind: "city_foundation_center",
				BuildingScope: "city_core",
				MaxHP:         100,
				TakeoverMode:  "disabled",
			},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
			{ID: "forest", Passable: true, Buildable: true},
			{ID: "mountain", Passable: false, Buildable: false},
			{ID: "river", Passable: false, Buildable: false},
			{ID: "snow", Passable: true, Buildable: true},
		},
	}, &staticdata.MapRuntimeBundle{
		ID:     "runtime_bootstrap",
		Name:   "runtime_bootstrap",
		Width:  8,
		Height: 8,
		Nodes:  runtimeBootstrapNodes(8, 8),
	}))

	human := &capturePlayer{playerID: "player-1", username: "alice"}
	bot := &captureBotPlayer{playerID: "bot-1", username: "Bot"}
	runtime := newTestRuntime("game-1", []*capturePlayer{human}, []*captureBotPlayer{bot})
	if err := runtime.Initialize(); err != nil {
		t.Fatalf("Initialize() error = %v", err)
	}

	state := runtime.State()
	if state == nil {
		t.Fatalf("state is nil")
	}

	for _, playerID := range []string{"player-1", "bot-1"} {
		playerState := state.Players[playerID]
		if playerState == nil {
			t.Fatalf("%s missing from authoritative state", playerID)
		}
		if playerState.CapitalCityID == "" {
			t.Fatalf("%s CapitalCityID should be initialized", playerID)
		}
		if _, ok := state.Map.PlayerSpawns[playerID]; !ok {
			t.Fatalf("%s spawn missing", playerID)
		}
	}
}

func TestRuntimeInitializeSupportsBotOnlyParticipants(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Manifest: staticdata.Manifest{
			SchemaVersion:  "2026-04-15",
			ContentVersion: "runtime-bootstrap-test",
			BundleHash:     "runtime-bootstrap-test",
			DefaultLocale:  "zh-CN",
			DefaultMapID:   "runtime_bootstrap",
		},
		Rules: staticdata.Rules{
			TurnTimeLimitPlanning:      30,
			TokensPerTurn:              3,
			CityCoreMaxHP:              100,
			SafeZoneRadius:             3,
			FacilityTakeoverTurns:      2,
			BaseResearchOutputPerTurn:  1,
			BaseIndustryOutputPerTurn:  2,
			MinimumCityDistance:        2,
			InitialCityTerritoryRadius: 1,
		},
		Buildings: []staticdata.BuildingDefinition{
			{
				ID:            "city_core",
				PlacementKind: "city_foundation_center",
				BuildingScope: "city_core",
				MaxHP:         100,
				TakeoverMode:  "disabled",
			},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
			{ID: "forest", Passable: true, Buildable: true},
			{ID: "mountain", Passable: false, Buildable: false},
			{ID: "river", Passable: false, Buildable: false},
			{ID: "snow", Passable: true, Buildable: true},
		},
	}, &staticdata.MapRuntimeBundle{
		ID:     "runtime_bootstrap",
		Name:   "runtime_bootstrap",
		Width:  8,
		Height: 8,
		Nodes:  runtimeBootstrapNodes(8, 8),
	}))

	runtime := newTestRuntime("game-1", nil, []*captureBotPlayer{
		{playerID: "bot-1", username: "Bot"},
	})
	if err := runtime.Initialize(); err != nil {
		t.Fatalf("Initialize() error = %v", err)
	}

	state := runtime.State()
	if state == nil {
		t.Fatalf("state is nil")
	}
	botState := state.Players["bot-1"]
	if botState == nil {
		t.Fatalf("bot-1 missing from authoritative state")
	}
	if botState.CapitalCityID == "" {
		t.Fatalf("bot-1 CapitalCityID should be initialized")
	}
	if _, ok := state.Map.PlayerSpawns["bot-1"]; !ok {
		t.Fatalf("bot-1 spawn missing")
	}
}

func TestBootstrapStartingPlayersFailsWhenPlayerSpawnMissing(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Manifest: staticdata.Manifest{
			SchemaVersion:  "2026-04-15",
			ContentVersion: "runtime-bootstrap-test",
			BundleHash:     "runtime-bootstrap-test",
			DefaultLocale:  "zh-CN",
			DefaultMapID:   "runtime_bootstrap",
		},
		Rules: staticdata.Rules{
			TurnTimeLimitPlanning:      30,
			TokensPerTurn:              3,
			CityCoreMaxHP:              100,
			SafeZoneRadius:             3,
			FacilityTakeoverTurns:      2,
			BaseResearchOutputPerTurn:  1,
			BaseIndustryOutputPerTurn:  2,
			MinimumCityDistance:        2,
			InitialCityTerritoryRadius: 1,
		},
		Buildings: []staticdata.BuildingDefinition{
			{
				ID:            "city_core",
				PlacementKind: "city_foundation_center",
				BuildingScope: "city_core",
				MaxHP:         100,
				TakeoverMode:  "disabled",
			},
		},
		Units: []staticdata.UnitDefinition{
			{
				ID:          "settler",
				Class:       "civilian",
				MaxHP:       12,
				Attack:      0,
				AttackRange: 0,
				MoveRange:   2,
				VisionRange: 2,
				Flags:       staticdata.UnitFlags{CanCapture: true},
			},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
	}, &staticdata.MapRuntimeBundle{
		ID:     "runtime_bootstrap",
		Name:   "runtime_bootstrap",
		Width:  8,
		Height: 8,
		Nodes:  runtimeBootstrapNodes(8, 8),
	}))

	catalog := staticdata.Default()
	baseMap, err := maploader.LoadMap(catalog, catalog.DefaultMapID())
	if err != nil {
		t.Fatalf("LoadMap() error = %v", err)
	}

	world := donburi.NewWorld()
	runtimeMap := maploader.GenerateProceduralMap(baseMap, 1, 1)
	if runtimeMap == nil {
		t.Fatalf("GenerateProceduralMap() returned nil")
	}

	mapData := maploader.InitWorldFromMap(world, runtimeMap, []string{"player-1"})
	delete(mapData.PlayerSpawns, "player-1")

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, mapData)
	state.World = world

	player := &capturePlayer{playerID: "player-1", username: "alice"}
	runtime := newTestRuntime("game-1", []*capturePlayer{player}, nil)
	runtime.state = state

	err = runtime.bootstrapStartingPlayers()
	if err == nil {
		t.Fatalf("bootstrapStartingPlayers() error = nil, want missing spawn failure")
	}
	if !strings.Contains(err.Error(), "capital city missing") {
		t.Fatalf("bootstrapStartingPlayers() error = %q, want capital city missing", err)
	}
}

func runtimeBootstrapNodes(width int, height int) []staticdata.MapRuntimeNode {
	nodes := make([]staticdata.MapRuntimeNode, 0, width*height)
	for y := 0; y < height; y++ {
		for x := 0; x < width; x++ {
			nodes = append(nodes, staticdata.MapRuntimeNode{
				ID:      string(rune('A'+x)) + string(rune('1'+y)),
				X:       x,
				Y:       y,
				Terrain: "plain",
			})
		}
	}
	return nodes
}

func findOwnedUnitEntryByType(world donburi.World, owner string, unitType string) (*donburi.Entry, bool) {
	var found *donburi.Entry
	ecs.AllUnits(world).Each(world, func(entry *donburi.Entry) {
		if found != nil || entry == nil {
			return
		}
		stats := ecs.UnitStatsC.Get(entry)
		if stats.Faction == owner && string(stats.Type) == unitType {
			found = entry
		}
	})
	return found, found != nil
}

func countOwnedUnitsByType(world donburi.World, owner string, unitType string) int {
	count := 0
	ecs.AllUnits(world).Each(world, func(entry *donburi.Entry) {
		if entry == nil {
			return
		}
		stats := ecs.UnitStatsC.Get(entry)
		if stats.Faction == owner && string(stats.Type) == unitType {
			count++
		}
	})
	return count
}

func newBootstrapStateForSinglePlayer(width int, height int, spawnPos domain.Position) *domain.GameState {
	world := donburi.NewWorld()
	mapData := &domain.MapData{
		ID:           "runtime_bootstrap",
		Width:        width,
		Height:       height,
		SpawnPoints:  map[int]domain.Position{0: spawnPos},
		PlayerSpawns: map[string]domain.Position{"player-1": spawnPos},
		NamedNodes:   map[string]string{},
		NodeIndex:    map[string]donburi.Entity{},
	}
	for _, node := range runtimeBootstrapNodes(width, height) {
		entity := ecs.CreateNode(world, ecs.MapNode{
			ID:      node.ID,
			Q:       node.X,
			R:       node.Y,
			Terrain: node.Terrain,
		})
		mapData.NodeIndex[node.ID] = entity
	}

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, mapData)
	state.World = world
	return state
}

func mustGetNodeAt(t *testing.T, state *domain.GameState, pos domain.Position) *donburi.Entry {
	t.Helper()
	entry, ok := domain.GetNodeAt(state.World, pos)
	if !ok || entry == nil {
		t.Fatalf("node at (%d,%d) missing", pos.Q, pos.R)
	}
	return entry
}

func setNodeTerrain(t *testing.T, state *domain.GameState, pos domain.Position, terrain string) {
	t.Helper()
	entry := mustGetNodeAt(t, state, pos)
	node := ecs.NodeC.Get(entry)
	node.Terrain = domain.Terrain(terrain)
}
