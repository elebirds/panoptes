package session

import (
	"context"
	"encoding/json"
	"strings"
	"testing"
	"time"

	"github.com/elebirds/panoptes/internal/config"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/engine/maploader"
	"github.com/elebirds/panoptes/internal/game/ai"
	"github.com/elebirds/panoptes/internal/game/participant"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
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
	if len(gameInit.GetMinisters()) != 5 {
		t.Fatalf("game init ministers = %#v, want normalized five-role roster", gameInit.GetMinisters())
	}
	wantRoles := map[string]bool{"domestic": false, "works": false, "defense": false, "command": false, "frontier": false}
	for _, minister := range gameInit.GetMinisters() {
		wantRoles[minister.GetRole()] = true
	}
	for role, seen := range wantRoles {
		if !seen {
			t.Fatalf("game init ministers = %#v, missing %q role", gameInit.GetMinisters(), role)
		}
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
	if got := len(start.GetMinisterDrafts()); got != 0 {
		t.Fatalf("planning start minister drafts = %d, want 0 before LLM selection", got)
	}
	if got := len(start.GetSnapshot().GetMinisterDrafts()); got != 0 {
		t.Fatalf("planning snapshot minister drafts = %d, want 0 before LLM selection", got)
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

func TestRuntimeTurnReportLifecycleWaitsForAllHumanAcks(t *testing.T) {
	runtime := NewRuntime("room-1", []ParticipantBinding{
		{
			Participant: participant.Participant{ID: "player-1", Username: "alice", Kind: participant.KindHuman},
			Controller:  HumanController{},
		},
		{
			Participant: participant.Participant{ID: "player-2", Username: "bob", Kind: participant.KindHuman},
			Controller:  HumanController{},
		},
		{
			Participant: participant.Participant{ID: "bot-1", Username: "bot", Kind: participant.KindBot},
			Controller:  NewAutonomousController(ai.RuleBotProvider{}),
		},
	}, nil, &config.Config{TurnReportTimeoutMs: 50})
	state := domain.NewGameState("room-1", []string{"player-1", "player-2", "bot-1"}, []string{"alice", "bob", "bot"}, &domain.MapData{ID: "default"})
	runtime.SetState(state)

	runtime.BeginTurnReport(7)
	if runtime.AcknowledgeTurnReport("player-1", 8) {
		t.Fatalf("ack should reject mismatched turn")
	}
	if runtime.AcknowledgeTurnReport("unknown", 7) {
		t.Fatalf("ack should reject unknown player")
	}
	if runtime.AcknowledgeTurnReport("bot-1", 7) {
		t.Fatalf("ack should ignore bot participants")
	}
	if runtime.WaitTurnReport(context.Background(), 20*time.Millisecond) {
		t.Fatalf("wait should time out before all human acknowledgements")
	}
	if !runtime.AcknowledgeTurnReport("player-1", 7) {
		t.Fatalf("player-1 ack should be accepted")
	}
	if runtime.WaitTurnReport(context.Background(), 20*time.Millisecond) {
		t.Fatalf("wait should still block until the last human ack arrives")
	}
	if !runtime.AcknowledgeTurnReport("player-2", 7) {
		t.Fatalf("player-2 ack should be accepted")
	}
	if !runtime.WaitTurnReport(context.Background(), 100*time.Millisecond) {
		t.Fatalf("wait should complete after all human acknowledgements")
	}

	runtime.FinishTurnReport(7)
	if runtime.AcknowledgeTurnReport("player-1", 7) {
		t.Fatalf("ack should be ignored after the report gate finishes")
	}
}

func TestRuntimeTurnReportTimeoutFallsBackToConfigOrDefault(t *testing.T) {
	runtime := NewRuntime("room-1", nil, nil, &config.Config{TurnReportTimeoutMs: 2750})
	if got := runtime.TurnReportTimeout(); got != 2750*time.Millisecond {
		t.Fatalf("TurnReportTimeout() = %v, want 2750ms", got)
	}

	if got := NewRuntime("room-2", nil, nil, nil).TurnReportTimeout(); got != 10*time.Second {
		t.Fatalf("TurnReportTimeout() default = %v, want 10s", got)
	}
}

func TestRuntimeClearMandateModesResetsDirectCommandAuthority(t *testing.T) {
	runtime := NewRuntime("room-1", nil, nil, &config.Config{})
	runtime.SetPlayerMandateMode("player-1", true)
	if !runtime.IsPlayerInMandateMode("player-1") {
		t.Fatalf("mandate mode not enabled")
	}

	runtime.ClearMandateModes()

	if runtime.IsPlayerInMandateMode("player-1") {
		t.Fatalf("mandate mode still enabled after clear")
	}
}

func TestRuntimeMinisterStrongModeReadsConfig(t *testing.T) {
	runtime := NewRuntime("room-1", nil, nil, &config.Config{MinisterLLMParticipationMode: " strong "})
	if !runtime.IsMinisterStrongMode() {
		t.Fatalf("minister strong mode not enabled")
	}

	runtime = NewRuntime("room-1", nil, nil, &config.Config{MinisterLLMParticipationMode: "weak"})
	if runtime.IsMinisterStrongMode() {
		t.Fatalf("minister strong mode enabled for weak mode")
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

func TestPreparePlanningStartStateIfNeededKeepsMinisterCandidatesHidden(t *testing.T) {
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
	if len(drafts) != 0 {
		t.Fatalf("planning start exposed hidden minister candidates as drafts: %#v", drafts)
	}
	candidates := runtime.preparedMinisterDraftsForPlayer(3, "player-1")
	if len(candidates) == 0 {
		t.Fatalf("planning start should prepare hidden minister candidates")
	}
	if summary := runtime.BuildMinisterActionCandidateSummary("player-1", "domestic"); !strings.Contains(summary, "candidate_id=domestic:") {
		t.Fatalf("candidate summary = %q, want hidden domestic candidate", summary)
	}
}

func TestPrepareMinisterDraftCacheForTurnKeepsRuleOnlyCandidates(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{TokensPerTurn: 3, BaseResearchOutputPerTurn: 1},
		Technologies: []staticdata.TechnologyDefinition{
			{ID: "agrarian_foundations", Name: "Agrarian Foundations", ResearchCost: 2},
		},
	}))

	player := &capturePlayer{playerID: "player-1", username: "alice"}
	runtime := newTestRuntime("game-1", []*capturePlayer{player}, nil)
	runtime.state = domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	runtime.state.Turn = 4
	runtime.state.Phase = domain.PhasePlanning.String()

	runtime.PrepareMinisterDraftCacheForTurn(4)

	runtime.preparedMinisterDraftsMu.RLock()
	drafts := append([]domain.MinisterDraft(nil), runtime.preparedMinisterDrafts[4]["player-1"]...)
	runtime.preparedMinisterDraftsMu.RUnlock()
	if len(drafts) != 1 {
		t.Fatalf("draft count = %d, want 1", len(drafts))
	}
	if drafts[0].Source != domain.MinisterDraftSourceRuleOnly {
		t.Fatalf("draft source = %q, want rule_only", drafts[0].Source)
	}
	if got := len(runtime.state.TurnRuntime.Planning.MinisterDraftsForPlayer("player-1")); got != 0 {
		t.Fatalf("visible minister drafts = %d, want 0 before LLM selects a candidate", got)
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

func TestRuntimeInitializePreparedDevModeLeavesTechnologiesLocked(t *testing.T) {
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

	if runtime.state.HasTechnologyUnlocked("player-1", "military_foundation") {
		t.Fatalf("technology military_foundation should stay locked in dev mode")
	}
	if runtime.state.IsBuildingUnlocked("player-1", "barracks") {
		t.Fatalf("building barracks should stay locked in dev mode")
	}
	if runtime.state.IsRecipeUnlocked("player-1", "barracks_infantry") {
		t.Fatalf("recipe barracks_infantry should stay locked in dev mode")
	}
}

func TestRuntimeInitializePreparedNonDevModeLeavesTechnologiesLocked(t *testing.T) {
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

	if runtime.state.HasTechnologyUnlocked("player-1", "military_foundation") {
		t.Fatalf("technology military_foundation should stay locked when dev mode is disabled")
	}
	if runtime.state.IsBuildingUnlocked("player-1", "barracks") {
		t.Fatalf("building barracks should stay locked when dev mode is disabled")
	}
	if runtime.state.IsRecipeUnlocked("player-1", "barracks_infantry") {
		t.Fatalf("recipe barracks_infantry should stay locked when dev mode is disabled")
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
