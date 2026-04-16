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
	"github.com/elebirds/panoptes/internal/game"
	"github.com/elebirds/panoptes/internal/game/scenario"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	cmddispatch "github.com/elebirds/panoptes/internal/transport/dispatch"
	"github.com/golang-jwt/jwt/v5"
	"google.golang.org/protobuf/encoding/protojson"
)

func TestNewServerDoesNotRegisterDevGameRoutesWhenDevModeFalse(t *testing.T) {
	server := NewServer(
		auth.NewService(&stubUserStore{}, "test-secret", 3600),
		"test-secret",
		nil,
		nil,
		false,
		NewDebugHandler(game.NewGameRoomRegistry(), debug.NewSettlementRecorder(), debug.NewCommandResultRecorder()),
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
		t.Fatalf("research result = %#v", result)
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
		t.Fatalf("research result should fail: %#v", result)
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
		msg := fixture.recorder.LatestSettlement(fixture.room.ID, "player-1")
		return msg != nil && msg.GetTurn() == 1
	})
}

func TestDebugHandlerStepTurnReturnsLatestSettlement(t *testing.T) {
	fixture := newDebugHTTPFixture(t)

	req := httptest.NewRequest(http.MethodPost, "/api/dev/game/step-turn", bytes.NewBufferString(`{}`))
	req.Header.Set("Authorization", "Bearer "+fixture.token)
	resp := httptest.NewRecorder()
	fixture.server.Handler().ServeHTTP(resp, req)

	if resp.Code != http.StatusOK {
		t.Fatalf("status = %d, want 200 body=%s", resp.Code, resp.Body.String())
	}

	var body struct {
		TurnSettlement *pb.MsgTurnSettlement `json:"turn_settlement"`
		State          debug.StateSummary    `json:"state"`
	}
	if err := json.Unmarshal(resp.Body.Bytes(), &body); err != nil {
		t.Fatalf("json.Unmarshal() error = %v", err)
	}
	if body.TurnSettlement == nil || body.TurnSettlement.GetTurn() != 1 {
		t.Fatalf("turn settlement = %#v", body.TurnSettlement)
	}
	if body.State.Turn != 2 || body.State.Phase != domain.PhasePlanning.String() {
		t.Fatalf("state summary = %#v", body.State)
	}
}

func TestDebugHandlerGetSettlementReturnsRecorderPayload(t *testing.T) {
	fixture := newDebugHTTPFixture(t)

	stepReq := httptest.NewRequest(http.MethodPost, "/api/dev/game/step-turn", bytes.NewBufferString(`{}`))
	stepReq.Header.Set("Authorization", "Bearer "+fixture.token)
	stepResp := httptest.NewRecorder()
	fixture.server.Handler().ServeHTTP(stepResp, stepReq)
	if stepResp.Code != http.StatusOK {
		t.Fatalf("step status = %d, want 200 body=%s", stepResp.Code, stepResp.Body.String())
	}

	req := httptest.NewRequest(http.MethodGet, "/api/dev/game/settlement", nil)
	req.Header.Set("Authorization", "Bearer "+fixture.token)
	resp := httptest.NewRecorder()
	fixture.server.Handler().ServeHTTP(resp, req)

	if resp.Code != http.StatusOK {
		t.Fatalf("status = %d, want 200 body=%s", resp.Code, resp.Body.String())
	}

	var body struct {
		TurnSettlement *pb.MsgTurnSettlement `json:"turn_settlement"`
	}
	if err := json.Unmarshal(resp.Body.Bytes(), &body); err != nil {
		t.Fatalf("json.Unmarshal() error = %v", err)
	}
	if body.TurnSettlement == nil || body.TurnSettlement.GetTurn() != 1 {
		t.Fatalf("turn settlement = %#v", body.TurnSettlement)
	}
}

type debugHTTPFixture struct {
	server          *Server
	room            *game.GameRoom
	recorder        *debug.SettlementRecorder
	commandRecorder *debug.CommandResultRecorder
	token           string
}

func newDebugHTTPFixture(t *testing.T) *debugHTTPFixture {
	t.Helper()

	def, err := scenario.ResearchUnlockBuild()
	if err != nil {
		t.Fatalf("ResearchUnlockBuild() error = %v", err)
	}
	staticdata.SetDefault(def.Catalog)

	recorder := debug.NewSettlementRecorder()
	commandRecorder := debug.NewCommandResultRecorder()
	previousRegistry := game.Registry
	game.Registry = game.NewGameRoomRegistry()
	t.Cleanup(func() {
		game.Registry = previousRegistry
		game.SetDebugHooks(game.DebugHooks{})
	})
	game.SetDebugHooks(game.DebugHooks{
		DumpStateSummary:      debug.DumpGameStateSummary,
		RecordSettlement:      recorder.RecordSettlement,
		RecordGameOver:        recorder.RecordGameOver,
		RecordOutgoingMessage: commandRecorder.RecordOutgoingMessage,
	})

	transport := debug.NewCaptureTransport()
	room := game.NewPreparedRoom(
		def.State.GameID,
		[]game.Player{game.NewHumanPlayer("player-1", "alice", transport)},
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
