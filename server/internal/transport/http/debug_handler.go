package http

import (
	"encoding/json"
	"errors"
	"net/http"
	"time"

	"github.com/elebirds/panoptes/internal/debug"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/game"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	coretransport "github.com/elebirds/panoptes/internal/transport"
	cmddispatch "github.com/elebirds/panoptes/internal/transport/dispatch"
	"google.golang.org/protobuf/encoding/protojson"
)

type DebugHandler struct {
	rooms         coretransport.GameRoomRegistry
	recorder      *debug.SettlementRecorder
	unmarshalOpts protojson.UnmarshalOptions
}

type debugCommandRequest struct {
	RequestID string          `json:"request_id"`
	Planning  json.RawMessage `json:"planning"`
}

type debugCommandResponse struct {
	RequestID string             `json:"request_id,omitempty"`
	State     debug.StateSummary `json:"state"`
}

type debugSubmitResponse struct {
	Turn  int                `json:"turn"`
	Phase string             `json:"phase"`
	State debug.StateSummary `json:"state"`
}

type debugSettlementResponse struct {
	TurnSettlement *pb.MsgTurnSettlement `json:"turn_settlement,omitempty"`
	GameOver       *pb.MsgGameOver       `json:"game_over,omitempty"`
}

type debugStepTurnResponse struct {
	TurnSettlement *pb.MsgTurnSettlement `json:"turn_settlement,omitempty"`
	GameOver       *pb.MsgGameOver       `json:"game_over,omitempty"`
	State          debug.StateSummary    `json:"state"`
}

func NewDebugHandler(rooms coretransport.GameRoomRegistry, recorder *debug.SettlementRecorder) *DebugHandler {
	return &DebugHandler{
		rooms:         rooms,
		recorder:      recorder,
		unmarshalOpts: protojson.UnmarshalOptions{DiscardUnknown: true},
	}
}

func (h *DebugHandler) GetState(w http.ResponseWriter, r *http.Request) {
	_, room, ok := h.lookupRoom(r)
	if !ok {
		writeError(w, http.StatusNotFound, "game_not_found")
		return
	}
	writeJSON(w, http.StatusOK, debug.BuildStateSummary(room.State()))
}

func (h *DebugHandler) GetSettlement(w http.ResponseWriter, r *http.Request) {
	playerID, room, ok := h.lookupRoom(r)
	if !ok {
		writeError(w, http.StatusNotFound, "game_not_found")
		return
	}
	writeJSON(w, http.StatusOK, debugSettlementResponse{
		TurnSettlement: h.latestSettlement(room.ID, playerID),
		GameOver:       h.latestGameOver(room.ID),
	})
}

func (h *DebugHandler) Command(w http.ResponseWriter, r *http.Request) {
	playerID, room, ok := h.lookupRoom(r)
	if !ok {
		writeError(w, http.StatusNotFound, "game_not_found")
		return
	}

	var req debugCommandRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		writeError(w, http.StatusBadRequest, "invalid_request")
		return
	}
	if len(req.Planning) == 0 {
		writeError(w, http.StatusBadRequest, "invalid_request")
		return
	}

	cmd := &pb.PlanningCommand{}
	if err := h.unmarshalOpts.Unmarshal(req.Planning, cmd); err != nil || cmd.GetBody() == nil {
		writeError(w, http.StatusBadRequest, "invalid_request")
		return
	}

	err := room.HandleGameCommand(cmddispatch.InboundContext{
		PlayerID:  playerID,
		RequestID: req.RequestID,
	}, &pb.GameCommand{
		Body: &pb.GameCommand_Planning{
			Planning: cmd,
		},
	})
	if err != nil {
		writeCommandError(w, err)
		return
	}

	writeJSON(w, http.StatusOK, debugCommandResponse{
		RequestID: req.RequestID,
		State:     debug.BuildStateSummary(room.State()),
	})
}

func (h *DebugHandler) Submit(w http.ResponseWriter, r *http.Request) {
	playerID, room, ok := h.lookupRoom(r)
	if !ok {
		writeError(w, http.StatusNotFound, "game_not_found")
		return
	}
	if state := room.State(); state == nil || state.Phase != domain.PhasePlanning.String() {
		writeError(w, http.StatusConflict, "phase_mismatch")
		return
	}

	room.Submit(playerID)
	writeJSON(w, http.StatusOK, debugSubmitResponse{
		Turn:  room.State().Turn,
		Phase: room.State().Phase,
		State: debug.BuildStateSummary(room.State()),
	})
}

func (h *DebugHandler) StepTurn(w http.ResponseWriter, r *http.Request) {
	playerID, room, ok := h.lookupRoom(r)
	if !ok {
		writeError(w, http.StatusNotFound, "game_not_found")
		return
	}
	state := room.State()
	if state == nil || state.Phase != domain.PhasePlanning.String() {
		writeError(w, http.StatusConflict, "phase_mismatch")
		return
	}
	if h.recorder == nil {
		writeError(w, http.StatusInternalServerError, "internal_error")
		return
	}

	currentTurn := state.Turn
	for _, currentPlayerID := range room.PlayerIDs() {
		room.Submit(currentPlayerID)
	}

	resp, err := h.waitForTurn(room, playerID, currentTurn, 3*time.Second)
	if err != nil {
		writeError(w, http.StatusInternalServerError, "internal_error")
		return
	}
	writeJSON(w, http.StatusOK, resp)
}

func (h *DebugHandler) waitForTurn(room *game.GameRoom, playerID string, turn int, timeout time.Duration) (*debugStepTurnResponse, error) {
	deadline := time.Now().Add(timeout)
	for time.Now().Before(deadline) {
		settlement := h.latestSettlement(room.ID, playerID)
		if settlement == nil || int(settlement.GetTurn()) != turn {
			time.Sleep(20 * time.Millisecond)
			continue
		}

		resp := &debugStepTurnResponse{
			TurnSettlement: settlement,
			State:          debug.BuildStateSummary(room.State()),
		}
		if resp.State.IsOver {
			resp.GameOver = h.latestGameOver(room.ID)
			if resp.GameOver == nil {
				time.Sleep(20 * time.Millisecond)
				continue
			}
		}
		return resp, nil
	}
	return nil, errors.New("turn settlement timeout")
}

func (h *DebugHandler) lookupRoom(r *http.Request) (string, *game.GameRoom, bool) {
	if h == nil || h.rooms == nil {
		return "", nil, false
	}
	playerID, _ := r.Context().Value(PlayerIDKey).(string)
	if playerID == "" {
		return "", nil, false
	}

	room, ok := h.rooms.GetRoomByPlayerID(playerID)
	if !ok {
		return "", nil, false
	}
	gameRoom, ok := room.(*game.GameRoom)
	if !ok || gameRoom == nil {
		return "", nil, false
	}
	return playerID, gameRoom, true
}

func (h *DebugHandler) latestSettlement(roomID string, playerID string) *pb.MsgTurnSettlement {
	if h == nil || h.recorder == nil {
		return nil
	}
	return h.recorder.LatestSettlement(roomID, playerID)
}

func (h *DebugHandler) latestGameOver(roomID string) *pb.MsgGameOver {
	if h == nil || h.recorder == nil {
		return nil
	}
	return h.recorder.LatestGameOver(roomID)
}

func writeCommandError(w http.ResponseWriter, err error) {
	if errors.Is(err, game.ErrPhaseMismatch) {
		writeError(w, http.StatusConflict, "phase_mismatch")
		return
	}

	problem, ok := cmddispatch.AsProblem(err)
	if !ok || problem == nil {
		writeError(w, http.StatusInternalServerError, "internal_error")
		return
	}

	switch problem.GetCode() {
	case "invalid_request":
		writeError(w, http.StatusBadRequest, problem.GetCode())
	case "unauthorized", "auth_failed", "invalid_credentials":
		writeError(w, http.StatusUnauthorized, problem.GetCode())
	case "game_not_found", "room_not_found", "user_not_found", "unit_not_found":
		writeError(w, http.StatusNotFound, problem.GetCode())
	case "internal_error":
		writeError(w, http.StatusInternalServerError, problem.GetCode())
	default:
		writeError(w, http.StatusConflict, problem.GetCode())
	}
}
