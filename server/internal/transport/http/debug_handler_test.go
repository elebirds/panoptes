package http

import (
	"bytes"
	"context"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"testing"
	"time"

	"github.com/elebirds/panoptes/internal/auth"
	"github.com/elebirds/panoptes/internal/config"
	"github.com/elebirds/panoptes/internal/debug"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/game"
	"github.com/elebirds/panoptes/internal/game/scenario"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	cmddispatch "github.com/elebirds/panoptes/internal/transport/dispatch"
	"github.com/golang-jwt/jwt/v5"
	"github.com/yohamta/donburi"
	"google.golang.org/protobuf/encoding/protojson"
)

func TestNewServerDoesNotRegisterDevGameRoutesWhenDevModeFalse(t *testing.T) {
	server := NewServer(
		auth.NewService(&stubUserStore{}, "test-secret", 3600),
		"test-secret",
		nil,
		nil,
		false,
		NewDebugHandler(game.NewGameRoomRegistry(), debug.NewGameSyncRecorder(), debug.NewCommandResultRecorder()),
	)

	req := httptest.NewRequest(http.MethodGet, "/api/dev/game/state", nil)
	resp := httptest.NewRecorder()
	server.Handler().ServeHTTP(resp, req)

	if resp.Code != http.StatusNotFound {
		t.Fatalf("status = %d, want 404", resp.Code)
	}
}

func TestDebugHandlerGetStateReturnsStructuredSummary(t *testing.T) {
	fixture := newDebugHTTPFixture(t)

	req := httptest.NewRequest(http.MethodGet, "/api/dev/game/state", nil)
	req.Header.Set("Authorization", "Bearer "+fixture.token)
	resp := httptest.NewRecorder()
	fixture.server.Handler().ServeHTTP(resp, req)

	if resp.Code != http.StatusOK {
		t.Fatalf("status = %d, want 200", resp.Code)
	}

	var body debug.StateSummary
	if err := json.Unmarshal(resp.Body.Bytes(), &body); err != nil {
		t.Fatalf("json.Unmarshal() error = %v", err)
	}
	if body.Turn != 1 || body.Phase != domain.PhasePlanning.String() {
		t.Fatalf("state summary = %#v", body)
	}
	if body.Players["player-1"].Username != "alice" {
		t.Fatalf("player summary = %#v", body.Players["player-1"])
	}
}

func TestDebugHandlerCommandQueuesPlanningCommand(t *testing.T) {
	fixture := newDebugHTTPFixture(t)

	req := httptest.NewRequest(http.MethodPost, "/api/dev/game/command", bytes.NewBufferString(`{
		"request_id":"req-research",
		"planning":{"setResearchTarget":{"technologyId":"agri_unlock_farm"}}
	}`))
	req.Header.Set("Authorization", "Bearer "+fixture.token)
	resp := httptest.NewRecorder()
	fixture.server.Handler().ServeHTTP(resp, req)

	if resp.Code != http.StatusOK {
		t.Fatalf("status = %d, want 200 body=%s", resp.Code, resp.Body.String())
	}

	var body struct {
		RequestID  string             `json:"request_id"`
		State      debug.StateSummary `json:"state"`
		ResultType string             `json:"result_type"`
		Result     json.RawMessage    `json:"result"`
	}
	if err := json.Unmarshal(resp.Body.Bytes(), &body); err != nil {
		t.Fatalf("json.Unmarshal() error = %v", err)
	}
	if body.RequestID != "req-research" {
		t.Fatalf("request_id = %q, want req-research", body.RequestID)
	}
	if body.ResultType != "MsgResearchResult" {
		t.Fatalf("result_type = %q, want MsgResearchResult", body.ResultType)
	}

	var result pb.MsgResearchResult
	if err := protojson.Unmarshal(body.Result, &result); err != nil {
		t.Fatalf("protojson.Unmarshal() error = %v", err)
	}
	if !result.GetSuccess() || result.GetTechnologyId() != "agri_unlock_farm" {
		t.Fatalf("research result success=%v technology_id=%q error_code=%q", result.GetSuccess(), result.GetTechnologyId(), result.GetErrorCode())
	}

	if got := fixture.room.State().TurnRuntime.Planning.PendingResearchTarget("player-1"); got != "agri_unlock_farm" {
		t.Fatalf("pending research target = %q, want agri_unlock_farm", got)
	}
}

func TestDebugHandlerCommandReturnsConflictForRejectedPlanningCommand(t *testing.T) {
	fixture := newDebugHTTPFixture(t)

	req := httptest.NewRequest(http.MethodPost, "/api/dev/game/command", bytes.NewBufferString(`{
		"request_id":"req-invalid-research",
		"planning":{"setResearchTarget":{"technologyId":"missing-tech"}}
	}`))
	req.Header.Set("Authorization", "Bearer "+fixture.token)
	resp := httptest.NewRecorder()
	fixture.server.Handler().ServeHTTP(resp, req)

	if resp.Code != http.StatusConflict {
		t.Fatalf("status = %d, want 409 body=%s", resp.Code, resp.Body.String())
	}

	var body struct {
		RequestID  string             `json:"request_id"`
		State      debug.StateSummary `json:"state"`
		ResultType string             `json:"result_type"`
		Result     json.RawMessage    `json:"result"`
	}
	if err := json.Unmarshal(resp.Body.Bytes(), &body); err != nil {
		t.Fatalf("json.Unmarshal() error = %v", err)
	}
	if body.ResultType != "MsgResearchResult" {
		t.Fatalf("result_type = %q, want MsgResearchResult", body.ResultType)
	}

	var result pb.MsgResearchResult
	if err := protojson.Unmarshal(body.Result, &result); err != nil {
		t.Fatalf("protojson.Unmarshal() error = %v", err)
	}
	if result.GetSuccess() {
		t.Fatalf("research result should fail: success=%v technology_id=%q error_code=%q", result.GetSuccess(), result.GetTechnologyId(), result.GetErrorCode())
	}
	if result.GetErrorCode() != "invalid_target" {
		t.Fatalf("error_code = %q, want invalid_target", result.GetErrorCode())
	}
	if got := fixture.room.State().TurnRuntime.Planning.PendingResearchTarget("player-1"); got != "" {
		t.Fatalf("pending research target = %q, want empty", got)
	}
}

func TestDebugHandlerSubmitAdvancesSinglePlayerRoom(t *testing.T) {
	fixture := newDebugHTTPFixture(t)

	req := httptest.NewRequest(http.MethodPost, "/api/dev/game/submit", bytes.NewBufferString(`{}`))
	req.Header.Set("Authorization", "Bearer "+fixture.token)
	resp := httptest.NewRecorder()
	fixture.server.Handler().ServeHTTP(resp, req)

	if resp.Code != http.StatusOK {
		t.Fatalf("status = %d, want 200 body=%s", resp.Code, resp.Body.String())
	}

	waitForDebugHTTP(t, 3*time.Second, func() bool {
		msg := fixture.recorder.LatestGameSync(fixture.room.ID, "player-1")
		return msg != nil && msg.GetTurn() == 1
	})
}

func TestDebugHandlerStepTurnReturnsLatestGameSync(t *testing.T) {
	fixture := newDebugHTTPFixture(t)

	req := httptest.NewRequest(http.MethodPost, "/api/dev/game/step-turn", bytes.NewBufferString(`{}`))
	req.Header.Set("Authorization", "Bearer "+fixture.token)
	resp := httptest.NewRecorder()
	fixture.server.Handler().ServeHTTP(resp, req)

	if resp.Code != http.StatusOK {
		t.Fatalf("status = %d, want 200 body=%s", resp.Code, resp.Body.String())
	}

	var body struct {
		GameSync json.RawMessage    `json:"game_sync"`
		State    debug.StateSummary `json:"state"`
	}
	if err := json.Unmarshal(resp.Body.Bytes(), &body); err != nil {
		t.Fatalf("json.Unmarshal() error = %v", err)
	}
	var gameSync pb.MsgGameSync
	if err := protojson.Unmarshal(body.GameSync, &gameSync); err != nil {
		t.Fatalf("protojson.Unmarshal(game_sync) error = %v", err)
	}
	if gameSync.GetTurn() != 1 {
		t.Fatalf("game sync = %#v", &gameSync)
	}
	if gameSync.GetPhase() != domain.PhaseResolving.String() || gameSync.GetNextPhase() != domain.PhaseTurnReport.String() {
		t.Fatalf("game sync phase=%q next_phase=%q, want resolving -> turn_report", gameSync.GetPhase(), gameSync.GetNextPhase())
	}
	if body.State.Turn != 1 || body.State.Phase != domain.PhaseResolving.String() {
		t.Fatalf("state summary = %#v", body.State)
	}
}

func TestDebugHandlerGetGameSyncReturnsRecorderGameSync(t *testing.T) {
	fixture := newDebugHTTPFixture(t)

	stepReq := httptest.NewRequest(http.MethodPost, "/api/dev/game/step-turn", bytes.NewBufferString(`{}`))
	stepReq.Header.Set("Authorization", "Bearer "+fixture.token)
	stepResp := httptest.NewRecorder()
	fixture.server.Handler().ServeHTTP(stepResp, stepReq)
	if stepResp.Code != http.StatusOK {
		t.Fatalf("step status = %d, want 200 body=%s", stepResp.Code, stepResp.Body.String())
	}

	req := httptest.NewRequest(http.MethodGet, "/api/dev/game/sync", nil)
	req.Header.Set("Authorization", "Bearer "+fixture.token)
	resp := httptest.NewRecorder()
	fixture.server.Handler().ServeHTTP(resp, req)

	if resp.Code != http.StatusOK {
		t.Fatalf("status = %d, want 200 body=%s", resp.Code, resp.Body.String())
	}

	var body struct {
		GameSync json.RawMessage `json:"game_sync"`
	}
	if err := json.Unmarshal(resp.Body.Bytes(), &body); err != nil {
		t.Fatalf("json.Unmarshal() error = %v", err)
	}
	var gameSync pb.MsgGameSync
	if err := protojson.Unmarshal(body.GameSync, &gameSync); err != nil {
		t.Fatalf("protojson.Unmarshal(game_sync) error = %v", err)
	}
	if gameSync.GetTurn() != 1 {
		t.Fatalf("game sync = %#v", &gameSync)
	}
}

func TestDebugHandlerVisionTogglesFullMapAndPushesPlanningRefresh(t *testing.T) {
	fixture := newDebugFogHTTPFixture(t)

	offReq := httptest.NewRequest(http.MethodPost, "/api/dev/game/vision", bytes.NewBufferString(`{
		"full_map": false
	}`))
	offReq.Header.Set("Authorization", "Bearer "+fixture.token)
	offResp := httptest.NewRecorder()
	fixture.server.Handler().ServeHTTP(offResp, offReq)

	if offResp.Code != http.StatusOK {
		t.Fatalf("off status = %d, want 200 body=%s", offResp.Code, offResp.Body.String())
	}

	var offBody struct {
		ParticipantID    string `json:"participant_id"`
		FullMap          bool   `json:"full_map"`
		VisibleNodeCount int    `json:"visible_node_count"`
		TotalNodeCount   int    `json:"total_node_count"`
		Refreshed        bool   `json:"refreshed"`
	}
	if err := json.Unmarshal(offResp.Body.Bytes(), &offBody); err != nil {
		t.Fatalf("json.Unmarshal(off) error = %v", err)
	}
	if offBody.ParticipantID != "player-1" {
		t.Fatalf("participant_id = %q, want player-1", offBody.ParticipantID)
	}
	if offBody.FullMap {
		t.Fatalf("full_map = true, want false")
	}
	if offBody.VisibleNodeCount >= offBody.TotalNodeCount {
		t.Fatalf("visible nodes without full map = %d, total = %d, want visible < total", offBody.VisibleNodeCount, offBody.TotalNodeCount)
	}

	beforeMessages := len(fixture.transport.Snapshot("player-1"))

	onReq := httptest.NewRequest(http.MethodPost, "/api/dev/game/vision", bytes.NewBufferString(`{
		"full_map": true
	}`))
	onReq.Header.Set("Authorization", "Bearer "+fixture.token)
	onResp := httptest.NewRecorder()
	fixture.server.Handler().ServeHTTP(onResp, onReq)

	if onResp.Code != http.StatusOK {
		t.Fatalf("on status = %d, want 200 body=%s", onResp.Code, onResp.Body.String())
	}

	var onBody struct {
		ParticipantID    string `json:"participant_id"`
		FullMap          bool   `json:"full_map"`
		VisibleNodeCount int    `json:"visible_node_count"`
		TotalNodeCount   int    `json:"total_node_count"`
		Refreshed        bool   `json:"refreshed"`
	}
	if err := json.Unmarshal(onResp.Body.Bytes(), &onBody); err != nil {
		t.Fatalf("json.Unmarshal(on) error = %v", err)
	}
	if !onBody.FullMap {
		t.Fatalf("full_map = false, want true")
	}
	if onBody.VisibleNodeCount != onBody.TotalNodeCount {
		t.Fatalf("visible nodes with full map = %d, total = %d, want equal", onBody.VisibleNodeCount, onBody.TotalNodeCount)
	}
	if !onBody.Refreshed {
		t.Fatalf("refreshed = false, want true")
	}

	afterMessages := fixture.transport.Snapshot("player-1")
	if len(afterMessages) <= beforeMessages {
		t.Fatalf("message count = %d, want > %d after planning refresh", len(afterMessages), beforeMessages)
	}
	if _, ok := afterMessages[len(afterMessages)-1].(*pb.MsgPlanningStart); !ok {
		t.Fatalf("last message = %T, want *pb.MsgPlanningStart", afterMessages[len(afterMessages)-1])
	}
}

type debugHTTPFixture struct {
	server          *Server
	room            *game.GameRoom
	recorder        *debug.GameSyncRecorder
	commandRecorder *debug.CommandResultRecorder
	transport       *debug.CaptureTransport
	token           string
}

func newDebugHTTPFixture(t *testing.T) *debugHTTPFixture {
	t.Helper()

	def, err := scenario.ResearchUnlockBuild()
	if err != nil {
		t.Fatalf("ResearchUnlockBuild() error = %v", err)
	}
	staticdata.SetDefault(def.Catalog)

	recorder := debug.NewGameSyncRecorder()
	commandRecorder := debug.NewCommandResultRecorder()
	previousRegistry := game.Registry
	game.Registry = game.NewGameRoomRegistry()
	t.Cleanup(func() {
		game.Registry = previousRegistry
		game.SetDebugHooks(game.DebugHooks{})
	})
	game.SetDebugHooks(game.DebugHooks{
		DumpStateSummary:      debug.DumpGameStateSummary,
		RecordGameSync:        recorder.RecordGameSync,
		RecordGameOver:        recorder.RecordGameOver,
		RecordOutgoingMessage: commandRecorder.RecordOutgoingMessage,
	})

	transport := debug.NewCaptureTransport()
	room := game.NewPreparedRoom(
		def.State.GameID,
		[]game.ParticipantSpec{game.NewHumanParticipantSpec("player-1", "alice")},
		transport,
		&config.Config{DevMode: true, MapID: def.State.Map.ID},
		def.State,
	)
	game.Registry.Register(room)
	room.Start()
	if err := room.HandleGameCommand(cmddispatch.InboundContext{
		PlayerID:  "player-1",
		RequestID: "bootstrap-sync-player-1",
	}, &pb.GameCommand{
		Body: &pb.GameCommand_StaticCatalogSyncRequest{
			StaticCatalogSyncRequest: &pb.MsgStaticCatalogSyncRequest{
				BundleHash: staticdata.Default().BundleHash(),
			},
		},
	}); err != nil {
		t.Fatalf("HandleGameCommand(sync) error = %v", err)
	}

	waitForDebugHTTP(t, 2*time.Second, func() bool {
		return room.State() != nil && room.State().Phase == domain.PhasePlanning.String() && room.State().Turn == 1
	})

	return &debugHTTPFixture{
		server: NewServer(
			auth.NewService(&stubUserStore{}, "test-secret", 3600),
			"test-secret",
			nil,
			nil,
			true,
			NewDebugHandler(game.Registry, recorder, commandRecorder),
		),
		room:            room,
		recorder:        recorder,
		commandRecorder: commandRecorder,
		transport:       transport,
		token:           mustDebugToken(t, "test-secret", "player-1"),
	}
}

func newDebugFogHTTPFixture(t *testing.T) *debugHTTPFixture {
	t.Helper()

	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			SafeZoneRadius:             2,
			CityCoreMaxHP:              100,
			BaseResearchOutputPerTurn:  1,
			BaseIndustryOutputPerTurn:  2,
			FacilityTakeoverTurns:      2,
			InitialCityTerritoryRadius: 1,
		},
		Units: []staticdata.UnitDefinition{
			{ID: "infantry", Class: "melee", MaxHP: 30, Attack: 10, AttackRange: 1, MoveRange: 2, VisionRange: 1, TrainCost: staticdata.ResourceAmounts{}, Upkeep: staticdata.ResourceAmounts{}},
		},
	}))

	world := donburi.NewWorld()
	mapData := &domain.MapData{
		ID:           "debug-fog",
		Width:        4,
		Height:       1,
		PlayerSpawns: map[string]domain.Position{"player-1": {Q: 0, R: 0}, "bot-1": {Q: 3, R: 0}},
		NodeIndex:    map[string]donburi.Entity{},
	}
	_ = createDebugHTTPNode(world, mapData, "N0", 0, 0)
	_ = createDebugHTTPNode(world, mapData, "N1", 1, 0)
	_ = createDebugHTTPNode(world, mapData, "N2", 2, 0)
	_ = createDebugHTTPNode(world, mapData, "N3", 3, 0)

	state := domain.NewGameState("debug-fog-game", []string{"player-1", "bot-1"}, []string{"alice", "bot"}, mapData)
	state.World = world
	state.Turn = 1
	state.Phase = domain.PhasePlanning.String()

	allyEntry := world.Entry(ecs.CreateUnit(world, "infantry", "player-1", domain.Position{Q: 0, R: 0}))
	enemyEntry := world.Entry(ecs.CreateUnit(world, "infantry", "bot-1", domain.Position{Q: 3, R: 0}))
	ecs.UnitStatsC.Get(allyEntry).ID = "ally-1"
	ecs.UnitStatsC.Get(enemyEntry).ID = "enemy-1"

	recorder := debug.NewGameSyncRecorder()
	commandRecorder := debug.NewCommandResultRecorder()
	previousRegistry := game.Registry
	game.Registry = game.NewGameRoomRegistry()
	t.Cleanup(func() {
		game.Registry = previousRegistry
		game.SetDebugHooks(game.DebugHooks{})
	})
	game.SetDebugHooks(game.DebugHooks{
		DumpStateSummary:      debug.DumpGameStateSummary,
		RecordGameSync:        recorder.RecordGameSync,
		RecordGameOver:        recorder.RecordGameOver,
		RecordOutgoingMessage: commandRecorder.RecordOutgoingMessage,
	})

	transport := debug.NewCaptureTransport()
	room := game.NewPreparedRoom(
		state.GameID,
		[]game.ParticipantSpec{
			game.NewHumanParticipantSpec("player-1", "alice"),
			game.NewBotParticipantSpec("bot-1", "bot"),
		},
		transport,
		&config.Config{DevMode: true, MapID: state.Map.ID},
		state,
	)
	game.Registry.Register(room)
	room.Start()
	if err := room.HandleGameCommand(cmddispatch.InboundContext{
		PlayerID:  "player-1",
		RequestID: "bootstrap-sync-player-1",
	}, &pb.GameCommand{
		Body: &pb.GameCommand_StaticCatalogSyncRequest{
			StaticCatalogSyncRequest: &pb.MsgStaticCatalogSyncRequest{
				BundleHash: staticdata.Default().BundleHash(),
			},
		},
	}); err != nil {
		t.Fatalf("HandleGameCommand(sync) error = %v", err)
	}

	waitForDebugHTTP(t, 2*time.Second, func() bool {
		return room.State() != nil && room.State().Phase == domain.PhasePlanning.String() && room.State().Turn == 1
	})

	return &debugHTTPFixture{
		server: NewServer(
			auth.NewService(&stubUserStore{}, "test-secret", 3600),
			"test-secret",
			nil,
			nil,
			true,
			NewDebugHandler(game.Registry, recorder, commandRecorder),
		),
		room:            room,
		recorder:        recorder,
		commandRecorder: commandRecorder,
		transport:       transport,
		token:           mustDebugToken(t, "test-secret", "player-1"),
	}
}

func waitForDebugHTTP(t *testing.T, timeout time.Duration, cond func() bool) {
	t.Helper()

	deadline := time.Now().Add(timeout)
	for time.Now().Before(deadline) {
		if cond() {
			return
		}
		time.Sleep(20 * time.Millisecond)
	}
	t.Fatalf("condition not met within %s", timeout)
}

func mustDebugToken(t *testing.T, secret string, playerID string) string {
	t.Helper()

	token := jwt.NewWithClaims(jwt.SigningMethodHS256, jwt.MapClaims{
		"player_id": playerID,
		"username":  "alice",
		"exp":       time.Now().Add(time.Hour).Unix(),
	})
	signed, err := token.SignedString([]byte(secret))
	if err != nil {
		t.Fatalf("SignedString() error = %v", err)
	}
	return signed
}

type stubUserStore struct{}

func (s *stubUserStore) Create(_ context.Context, _ *auth.User) error { return nil }

func (s *stubUserStore) GetByUsername(_ context.Context, username string) (*auth.User, error) {
	return &auth.User{ID: "player-1", Username: username}, nil
}

func (s *stubUserStore) GetByID(_ context.Context, id string) (*auth.User, error) {
	return &auth.User{ID: id, Username: "alice"}, nil
}

func createDebugHTTPNode(world donburi.World, mapData *domain.MapData, nodeID string, x int, y int) *donburi.Entry {
	entity := ecs.CreateNode(world, ecs.MapNode{ID: nodeID, Q: x, R: y, Terrain: "plain"})
	mapData.NodeIndex[nodeID] = entity
	return world.Entry(entity)
}
