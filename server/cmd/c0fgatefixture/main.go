// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Description: Generates deterministic C0f backend-to-Unity protocol fixtures through real HTTP/WebSocket paths.

package main

import (
	"bytes"
	"context"
	"encoding/json"
	"errors"
	"flag"
	"fmt"
	"net/http"
	"net/http/httptest"
	"net/url"
	"os"
	"path/filepath"
	"sort"
	"strings"
	"sync"
	"time"

	"github.com/elebirds/panoptes/internal/auth"
	"github.com/elebirds/panoptes/internal/config"
	"github.com/elebirds/panoptes/internal/game"
	"github.com/elebirds/panoptes/internal/game/scenario"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/lobby"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/elebirds/panoptes/internal/transport/codec"
	httptransport "github.com/elebirds/panoptes/internal/transport/http"
	"github.com/elebirds/panoptes/internal/transport/inbound"
	wstransport "github.com/elebirds/panoptes/internal/transport/websocket"
	"github.com/gorilla/websocket"
	"google.golang.org/protobuf/proto"
	"google.golang.org/protobuf/reflect/protoreflect"
)

const (
	defaultOutputPath = "../client/Assets/Scripts/Tests/EditMode/Fixtures/C0f/server_frames.jsonl"
	fixturePlayerID   = "player-1"
	fixtureUsername   = "alice"
	fixturePassword   = "panoptes-c0f"
	fixtureSecret     = "c0f-fixture-secret"
)

func main() {
	outputPath := flag.String("out", defaultOutputPath, "output JSONL fixture path")
	flag.Parse()

	if err := run(*outputPath); err != nil {
		fmt.Fprintf(os.Stderr, "c0f fixture generation failed: %v\n", err)
		os.Exit(1)
	}
}

func run(outputPath string) error {
	ctx, cancel := context.WithTimeout(context.Background(), 20*time.Second)
	defer cancel()

	def, err := scenario.ResearchUnlockBuild()
	if err != nil {
		return fmt.Errorf("build scenario: %w", err)
	}
	staticdata.SetDefault(def.Catalog)

	previousRegistry := game.Registry
	game.Registry = game.NewGameRoomRegistry()
	defer func() {
		game.Registry = previousRegistry
	}()

	srv, err := newFixtureServer(ctx, def)
	if err != nil {
		return err
	}
	defer srv.Close()

	if err := register(ctx, srv.URL); err != nil {
		return err
	}
	token, err := login(ctx, srv.URL)
	if err != nil {
		return err
	}

	recorder := newFrameRecorder()
	conn, err := dialWebSocket(ctx, srv.URL, token)
	if err != nil {
		return err
	}
	defer conn.Close()
	go recorder.read(ctx, conn)

	if _, err := recorder.waitFor(ctx, "MsgClientRuntimeConfig", nil); err != nil {
		return err
	}

	if err := sendLobby(conn, "c0f-create-room", &pb.LobbyCommand{
		Body: &pb.LobbyCommand_CreateRoom{CreateRoom: &pb.MsgCreateRoom{
			Name:       "C0f Gate",
			MaxPlayers: 2,
		}},
	}); err != nil {
		return err
	}
	if _, err := recorder.waitFor(ctx, "MsgRoomCreated", nil); err != nil {
		return err
	}
	if _, err := recorder.waitFor(ctx, "MsgRoomState", func(frame *pb.ServerFrame) bool {
		return frame.GetLobby().GetRoomState().GetStatus() == string(lobby.RoomStatusWaiting)
	}); err != nil {
		return err
	}

	if err := sendLobby(conn, "c0f-ready-up", &pb.LobbyCommand{
		Body: &pb.LobbyCommand_ReadyUp{ReadyUp: &pb.MsgReadyUp{}},
	}); err != nil {
		return err
	}
	if _, err := recorder.waitFor(ctx, "MsgRoomState", func(frame *pb.ServerFrame) bool {
		return frame.GetLobby().GetRoomState().GetStatus() == string(lobby.RoomStatusReady)
	}); err != nil {
		return err
	}

	if err := sendLobby(conn, "c0f-start-game", &pb.LobbyCommand{
		Body: &pb.LobbyCommand_StartGame{StartGame: &pb.MsgStartGame{}},
	}); err != nil {
		return err
	}
	if _, err := recorder.waitFor(ctx, "MsgGameStarting", nil); err != nil {
		return err
	}
	if _, err := recorder.waitFor(ctx, "MsgStaticCatalogManifest", nil); err != nil {
		return err
	}
	if err := waitForRegisteredGame(ctx, fixturePlayerID); err != nil {
		return err
	}

	if err := sendGame(conn, "c0f-static-sync", &pb.GameCommand{
		Body: &pb.GameCommand_StaticCatalogSyncRequest{StaticCatalogSyncRequest: &pb.MsgStaticCatalogSyncRequest{
			ForceFullSync: true,
		}},
	}); err != nil {
		return err
	}
	if _, err := recorder.waitFor(ctx, "MsgStaticCatalogSyncComplete", nil); err != nil {
		return err
	}
	if _, err := recorder.waitFor(ctx, "MsgGameInit", nil); err != nil {
		return err
	}
	if _, err := recorder.waitFor(ctx, "MsgPlanningStart", func(frame *pb.ServerFrame) bool {
		return frame.GetGame().GetPlanningStart().GetTurn() == 1
	}); err != nil {
		return err
	}

	if err := sendGame(conn, "c0f-research", &pb.GameCommand{
		Body: &pb.GameCommand_Planning{Planning: &pb.PlanningCommand{
			Body: &pb.PlanningCommand_SetResearchTarget{SetResearchTarget: &pb.MsgSetResearchTarget{
				TechnologyId: "agri_unlock_farm",
			}},
		}},
	}); err != nil {
		return err
	}
	if _, err := recorder.waitFor(ctx, "MsgResearchResult", func(frame *pb.ServerFrame) bool {
		result := frame.GetGame().GetResearchResult()
		return result.GetSuccess() && result.GetTechnologyId() == "agri_unlock_farm"
	}); err != nil {
		return err
	}

	if err := sendGame(conn, "c0f-submit-turn", &pb.GameCommand{
		Body: &pb.GameCommand_Planning{Planning: &pb.PlanningCommand{
			Body: &pb.PlanningCommand_SubmitTurn{SubmitTurn: &pb.MsgSubmitTurn{}},
		}},
	}); err != nil {
		return err
	}
	if _, err := recorder.waitFor(ctx, "MsgGameSync", func(frame *pb.ServerFrame) bool {
		return frame.GetGame().GetGameSync().GetTurn() == 1
	}); err != nil {
		return err
	}
	if _, err := recorder.waitFor(ctx, "MsgPlanningStart", func(frame *pb.ServerFrame) bool {
		return frame.GetGame().GetPlanningStart().GetTurn() == 2
	}); err != nil {
		return err
	}

	return writeFixture(outputPath, recorder.recordedLines())
}

func newFixtureServer(ctx context.Context, def *scenario.Definition) (*httptest.Server, error) {
	userStore := newMemoryUserStore()
	authSvc := auth.NewService(userStore, fixtureSecret, 3600)

	wsHub := wstransport.NewHub(fixtureSecret)
	gameTransport := wstransport.NewTransport(wsHub)
	lobbySvc := lobby.NewService(newMemoryLobbyStore(), gameTransport, authSvc, 2, true)
	lobbySvc.SetGameStartCallback(func(room *lobby.Room) {
		participants := make([]game.ParticipantSpec, 0, len(room.Players))
		for _, player := range room.Players {
			if player.IsBot {
				participants = append(participants, game.NewBotParticipantSpec(player.PlayerID, player.Username))
				continue
			}
			participants = append(participants, game.NewHumanParticipantSpec(player.PlayerID, player.Username))
		}

		gameRoom := game.NewPreparedRoom(
			def.State.GameID,
			participants,
			gameTransport,
			&config.Config{DevMode: true, MapID: def.State.Map.ID},
			def.State,
		)
		go gameRoom.Start()
	})
	wsHub.SetDispatcher(&inbound.Dispatcher{
		Lobby: lobby.NewCommandHandler(lobbySvc),
		Game:  game.NewRegistryCommandHandler(game.Registry),
	})
	wsHub.SetLeaveRoomFunc(func(ctx context.Context, playerID string) error {
		if err := lobbySvc.HandleDisconnect(ctx, playerID); err != nil {
			return err
		}
		game.Registry.HandleParticipantDisconnect(playerID)
		return nil
	})
	wsHub.SetConnectFunc(func(ctx context.Context, playerID string) error {
		return gameTransport.Send(ctx, playerID, &pb.MsgClientRuntimeConfig{DevMode: true})
	})
	go wsHub.Run(ctx)

	httpServer := httptransport.NewServer(authSvc, fixtureSecret, nil, nil, true, nil)
	mux := http.NewServeMux()
	mux.Handle("/api/", httpServer.Handler())
	mux.HandleFunc("/ws", wsHub.ServeWS)
	return httptest.NewServer(mux), nil
}

func register(ctx context.Context, baseURL string) error {
	var response struct {
		PlayerID string `json:"player_id"`
	}
	if err := postJSON(ctx, baseURL+"/api/register", map[string]string{
		"username": fixtureUsername,
		"password": fixturePassword,
	}, &response); err != nil {
		return fmt.Errorf("register: %w", err)
	}
	if response.PlayerID != fixturePlayerID {
		return fmt.Errorf("register player_id = %q, want %q", response.PlayerID, fixturePlayerID)
	}
	return nil
}

func login(ctx context.Context, baseURL string) (string, error) {
	var response struct {
		Token    string `json:"token"`
		PlayerID string `json:"player_id"`
	}
	if err := postJSON(ctx, baseURL+"/api/login", map[string]string{
		"username": fixtureUsername,
		"password": fixturePassword,
	}, &response); err != nil {
		return "", fmt.Errorf("login: %w", err)
	}
	if response.PlayerID != fixturePlayerID {
		return "", fmt.Errorf("login player_id = %q, want %q", response.PlayerID, fixturePlayerID)
	}
	if response.Token == "" {
		return "", errors.New("login returned empty token")
	}
	return response.Token, nil
}

func postJSON(ctx context.Context, url string, request any, response any) error {
	body, err := json.Marshal(request)
	if err != nil {
		return err
	}
	httpReq, err := http.NewRequestWithContext(ctx, http.MethodPost, url, bytes.NewReader(body))
	if err != nil {
		return err
	}
	httpReq.Header.Set("Content-Type", "application/json")
	httpResp, err := http.DefaultClient.Do(httpReq)
	if err != nil {
		return err
	}
	defer httpResp.Body.Close()
	if httpResp.StatusCode < 200 || httpResp.StatusCode >= 300 {
		return fmt.Errorf("unexpected HTTP status %s", httpResp.Status)
	}
	return json.NewDecoder(httpResp.Body).Decode(response)
}

func dialWebSocket(ctx context.Context, baseURL string, token string) (*websocket.Conn, error) {
	parsed, err := url.Parse(baseURL)
	if err != nil {
		return nil, err
	}
	parsed.Scheme = strings.Replace(parsed.Scheme, "http", "ws", 1)
	parsed.Path = "/ws"
	q := parsed.Query()
	q.Set("token", token)
	parsed.RawQuery = q.Encode()

	conn, _, err := websocket.DefaultDialer.DialContext(ctx, parsed.String(), nil)
	return conn, err
}

func sendLobby(conn *websocket.Conn, requestID string, cmd *pb.LobbyCommand) error {
	return sendClientFrame(conn, &pb.ClientFrame{
		Meta:   &pb.CommandMeta{RequestId: requestID, TraceId: "c0f"},
		Target: &pb.ClientFrame_Lobby{Lobby: cmd},
	})
}

func sendGame(conn *websocket.Conn, requestID string, cmd *pb.GameCommand) error {
	return sendClientFrame(conn, &pb.ClientFrame{
		Meta:   &pb.CommandMeta{RequestId: requestID, TraceId: "c0f"},
		Target: &pb.ClientFrame_Game{Game: cmd},
	})
}

func sendClientFrame(conn *websocket.Conn, frame *pb.ClientFrame) error {
	data, err := codec.EncodeClientFrame(frame)
	if err != nil {
		return err
	}
	return conn.WriteMessage(websocket.TextMessage, data)
}

func waitForRegisteredGame(ctx context.Context, playerID string) error {
	ticker := time.NewTicker(10 * time.Millisecond)
	defer ticker.Stop()
	for {
		if _, ok := game.Registry.GetRoomByParticipantID(playerID); ok {
			return nil
		}
		select {
		case <-ctx.Done():
			return fmt.Errorf("wait for game registry: %w", ctx.Err())
		case <-ticker.C:
		}
	}
}

type frameRecorder struct {
	mu     sync.Mutex
	frames []*pb.ServerFrame
	lines  []string
	ch     chan *pb.ServerFrame
	errs   chan error
}

func newFrameRecorder() *frameRecorder {
	return &frameRecorder{
		ch:   make(chan *pb.ServerFrame, 64),
		errs: make(chan error, 1),
	}
}

func (r *frameRecorder) read(ctx context.Context, conn *websocket.Conn) {
	for {
		_, raw, err := conn.ReadMessage()
		if err != nil {
			select {
			case <-ctx.Done():
				return
			default:
			}
			select {
			case r.errs <- err:
			default:
			}
			return
		}
		frame, err := codec.DecodeServerFrame(raw)
		if err != nil {
			select {
			case r.errs <- err:
			default:
			}
			return
		}
		normalizeFrame(frame)
		line, err := codec.EncodeServerFrame(frame)
		if err != nil {
			select {
			case r.errs <- err:
			default:
			}
			return
		}
		r.mu.Lock()
		r.frames = append(r.frames, frame)
		r.lines = append(r.lines, string(line))
		r.mu.Unlock()
		select {
		case r.ch <- frame:
		case <-ctx.Done():
			return
		}
	}
}

func (r *frameRecorder) waitFor(ctx context.Context, wantName string, predicate func(*pb.ServerFrame) bool) (*pb.ServerFrame, error) {
	for {
		select {
		case <-ctx.Done():
			return nil, fmt.Errorf("wait for %s: %w", wantName, ctx.Err())
		case err := <-r.errs:
			return nil, fmt.Errorf("read websocket: %w", err)
		case frame := <-r.ch:
			name := serverFrameMessageName(frame)
			if name == "Problem" {
				return nil, fmt.Errorf("received problem while waiting for %s: %v", wantName, frame.GetProblem())
			}
			if name != wantName {
				continue
			}
			if predicate == nil || predicate(frame) {
				return frame, nil
			}
		}
	}
}

func (r *frameRecorder) recordedLines() []string {
	r.mu.Lock()
	defer r.mu.Unlock()
	return append([]string(nil), r.lines...)
}

func normalizeFrame(frame *pb.ServerFrame) {
	if frame == nil {
		return
	}
	if frame.Meta != nil {
		frame.Meta.ServerUnixMillis = 1
		if frame.Meta.GameSessionId == "" && hasGameEvent(frame) {
			frame.Meta.GameSessionId = "research_unlock_build"
		}
	}
	if created := frame.GetLobby().GetRoomCreated(); created != nil {
		created.RoomId = "c0f-room"
		created.RoomCode = "C0F001"
	}
	if state := frame.GetLobby().GetRoomState(); state != nil {
		state.RoomId = "c0f-room"
		state.RoomCode = "C0F001"
	}
}

func hasGameEvent(frame *pb.ServerFrame) bool {
	return frame != nil && frame.GetGame() != nil
}

func serverFrameMessageName(frame *pb.ServerFrame) string {
	if frame == nil {
		return "unknown"
	}
	return deepestMessageName(frame.ProtoReflect())
}

func deepestMessageName(msg protoreflect.Message) string {
	if !msg.IsValid() {
		return "unknown"
	}
	descriptor := msg.Descriptor()
	for i := 0; i < descriptor.Oneofs().Len(); i++ {
		oneof := descriptor.Oneofs().Get(i)
		if oneof.IsSynthetic() {
			continue
		}
		field := msg.WhichOneof(oneof)
		if field == nil || field.Kind() != protoreflect.MessageKind {
			continue
		}
		nested := msg.Get(field).Message()
		if nested.IsValid() {
			return deepestMessageName(nested)
		}
	}
	return string(descriptor.Name())
}

func writeFixture(outputPath string, lines []string) error {
	if len(lines) == 0 {
		return errors.New("no server frames recorded")
	}
	if err := os.MkdirAll(filepath.Dir(outputPath), 0o755); err != nil {
		return err
	}
	return os.WriteFile(outputPath, []byte(strings.Join(lines, "\n")+"\n"), 0o644)
}

type memoryUserStore struct {
	mu         sync.RWMutex
	byID       map[string]*auth.User
	byUsername map[string]*auth.User
}

func newMemoryUserStore() *memoryUserStore {
	return &memoryUserStore{
		byID:       make(map[string]*auth.User),
		byUsername: make(map[string]*auth.User),
	}
}

func (s *memoryUserStore) Create(_ context.Context, user *auth.User) error {
	s.mu.Lock()
	defer s.mu.Unlock()
	if _, ok := s.byUsername[user.Username]; ok {
		return auth.ErrUserExists
	}
	if user.Username == fixtureUsername {
		user.ID = fixturePlayerID
	}
	cloned := cloneUser(user)
	s.byID[cloned.ID] = cloned
	s.byUsername[cloned.Username] = cloned
	return nil
}

func (s *memoryUserStore) GetByUsername(_ context.Context, username string) (*auth.User, error) {
	s.mu.RLock()
	defer s.mu.RUnlock()
	user, ok := s.byUsername[username]
	if !ok {
		return nil, auth.ErrUserNotFound
	}
	return cloneUser(user), nil
}

func (s *memoryUserStore) GetByID(_ context.Context, id string) (*auth.User, error) {
	s.mu.RLock()
	defer s.mu.RUnlock()
	user, ok := s.byID[id]
	if !ok {
		return nil, auth.ErrUserNotFound
	}
	return cloneUser(user), nil
}

func cloneUser(user *auth.User) *auth.User {
	if user == nil {
		return nil
	}
	cloned := *user
	return &cloned
}

type memoryLobbyStore struct {
	mu    sync.RWMutex
	rooms map[string]*lobby.Room
}

func newMemoryLobbyStore() *memoryLobbyStore {
	return &memoryLobbyStore{rooms: make(map[string]*lobby.Room)}
}

func (s *memoryLobbyStore) CreateRoom(_ context.Context, room *lobby.Room) error {
	s.mu.Lock()
	defer s.mu.Unlock()
	s.rooms[room.ID] = cloneLobbyRoom(room)
	return nil
}

func (s *memoryLobbyStore) GetRoom(_ context.Context, roomID string) (*lobby.Room, error) {
	s.mu.RLock()
	defer s.mu.RUnlock()
	room, ok := s.rooms[roomID]
	if !ok {
		return nil, lobby.ErrRoomNotFound
	}
	return cloneLobbyRoom(room), nil
}

func (s *memoryLobbyStore) GetRoomByCode(_ context.Context, code string) (*lobby.Room, error) {
	s.mu.RLock()
	defer s.mu.RUnlock()
	for _, room := range s.rooms {
		if room.Code == code {
			return cloneLobbyRoom(room), nil
		}
	}
	return nil, lobby.ErrRoomNotFound
}

func (s *memoryLobbyStore) UpdateRoom(_ context.Context, room *lobby.Room) error {
	s.mu.Lock()
	defer s.mu.Unlock()
	if _, ok := s.rooms[room.ID]; !ok {
		return lobby.ErrRoomNotFound
	}
	s.rooms[room.ID] = cloneLobbyRoom(room)
	return nil
}

func (s *memoryLobbyStore) DeleteRoom(_ context.Context, roomID string) error {
	s.mu.Lock()
	defer s.mu.Unlock()
	if _, ok := s.rooms[roomID]; !ok {
		return lobby.ErrRoomNotFound
	}
	delete(s.rooms, roomID)
	return nil
}

func (s *memoryLobbyStore) GetRoomByPlayerID(_ context.Context, playerID string) (*lobby.Room, error) {
	s.mu.RLock()
	defer s.mu.RUnlock()
	ids := make([]string, 0, len(s.rooms))
	for id := range s.rooms {
		ids = append(ids, id)
	}
	sort.Strings(ids)
	for _, id := range ids {
		room := s.rooms[id]
		for _, player := range room.Players {
			if player.PlayerID == playerID {
				return cloneLobbyRoom(room), nil
			}
		}
	}
	return nil, lobby.ErrRoomNotFound
}

func cloneLobbyRoom(room *lobby.Room) *lobby.Room {
	if room == nil {
		return nil
	}
	cloned := *room
	cloned.Players = make([]*lobby.RoomPlayer, 0, len(room.Players))
	for _, player := range room.Players {
		playerCopy := *player
		cloned.Players = append(cloned.Players, &playerCopy)
	}
	return &cloned
}

var _ auth.UserStore = (*memoryUserStore)(nil)
var _ lobby.LobbyStore = (*memoryLobbyStore)(nil)
var _ proto.Message = (*pb.ServerFrame)(nil)
