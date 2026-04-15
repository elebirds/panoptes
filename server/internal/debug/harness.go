package debug

import (
	"fmt"
	"time"

	"github.com/elebirds/panoptes/internal/config"
	"github.com/elebirds/panoptes/internal/game"
	"github.com/elebirds/panoptes/internal/game/scenario"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	cmddispatch "github.com/elebirds/panoptes/internal/transport/dispatch"
	"google.golang.org/protobuf/proto"
)

type Harness struct {
	definition *scenario.Definition
	transport  *CaptureTransport
	room       *game.GameRoom
}

type TurnRecord struct {
	Turn          int
	PlanningStart *pb.MsgPlanningStart
	Settlement    *pb.MsgTurnSettlement
	GameOver      *pb.MsgGameOver
	Summary       StateSummary
}

func NewHarness(def *scenario.Definition) (*Harness, error) {
	if def == nil {
		return nil, fmt.Errorf("scenario definition is nil")
	}
	if def.Catalog == nil {
		return nil, fmt.Errorf("scenario catalog is nil")
	}
	if def.State == nil {
		return nil, fmt.Errorf("scenario state is nil")
	}
	if len(def.PlayerIDs) == 0 {
		return nil, fmt.Errorf("scenario players are empty")
	}

	transport := NewCaptureTransport()
	players := make([]game.Player, 0, len(def.PlayerIDs))
	for idx, playerID := range def.PlayerIDs {
		username := playerID
		if idx < len(def.Usernames) && def.Usernames[idx] != "" {
			username = def.Usernames[idx]
		}
		players = append(players, game.NewHumanPlayer(playerID, username, transport))
	}

	room := game.NewPreparedRoom(
		def.State.GameID,
		players,
		transport,
		&config.Config{DevMode: true, MapID: def.State.Map.ID},
		def.State,
	)

	return &Harness{
		definition: def,
		transport:  transport,
		room:       room,
	}, nil
}

func (h *Harness) Start() error {
	if h == nil || h.room == nil || h.definition == nil {
		return fmt.Errorf("harness is not initialized")
	}
	if h.definition.Catalog != nil {
		staticdata.SetDefault(h.definition.Catalog)
	}
	h.room.Start()
	return nil
}

func (h *Harness) InjectPlanningCommand(playerID string, requestID string, cmd *pb.PlanningCommand) error {
	if h == nil || h.room == nil {
		return fmt.Errorf("harness room is nil")
	}
	if cmd == nil {
		return fmt.Errorf("planning command is nil")
	}
	return h.room.HandleGameCommand(cmddispatch.InboundContext{
		PlayerID:  playerID,
		RequestID: requestID,
	}, &pb.GameCommand{
		Body: &pb.GameCommand_Planning{
			Planning: cmd,
		},
	})
}

func (h *Harness) SubmitTurn(playerID string) error {
	return h.InjectPlanningCommand(playerID, "submit-"+playerID, &pb.PlanningCommand{
		Body: &pb.PlanningCommand_SubmitTurn{
			SubmitTurn: &pb.MsgSubmitTurn{},
		},
	})
}

func (h *Harness) WaitPlanningStart(playerID string, turn int, timeout time.Duration) (*pb.MsgPlanningStart, error) {
	deadline := time.Now().Add(timeout)
	for time.Now().Before(deadline) {
		if msg := latestMessage[*pb.MsgPlanningStart](h.transport.Snapshot(playerID), func(msg *pb.MsgPlanningStart) bool {
			return int(msg.GetTurn()) == turn
		}); msg != nil {
			return msg, nil
		}
		time.Sleep(20 * time.Millisecond)
	}
	return nil, fmt.Errorf("planning start turn=%d not received within %s", turn, timeout)
}

func (h *Harness) WaitSettlement(playerID string, turn int, timeout time.Duration) (*TurnRecord, error) {
	deadline := time.Now().Add(timeout)
	for time.Now().Before(deadline) {
		msgs := h.transport.Snapshot(playerID)
		settlement := latestMessage[*pb.MsgTurnSettlement](msgs, func(msg *pb.MsgTurnSettlement) bool {
			return int(msg.GetTurn()) == turn
		})
		if settlement == nil {
			time.Sleep(20 * time.Millisecond)
			continue
		}

		record := &TurnRecord{
			Turn:          turn,
			PlanningStart: latestMessage[*pb.MsgPlanningStart](msgs, func(msg *pb.MsgPlanningStart) bool { return int(msg.GetTurn()) == turn }),
			Settlement:    settlement,
			Summary:       BuildStateSummary(h.room.State()),
		}
		if record.Summary.IsOver {
			record.GameOver = latestMessage[*pb.MsgGameOver](msgs, func(msg *pb.MsgGameOver) bool { return true })
			if record.GameOver == nil {
				time.Sleep(20 * time.Millisecond)
				continue
			}
		}
		return record, nil
	}
	return nil, fmt.Errorf("settlement turn=%d not received within %s", turn, timeout)
}

func (h *Harness) Messages(playerID string) []proto.Message {
	if h == nil || h.transport == nil {
		return nil
	}
	return h.transport.Snapshot(playerID)
}

func latestMessage[T proto.Message](msgs []proto.Message, predicate func(T) bool) T {
	var zero T
	for i := len(msgs) - 1; i >= 0; i-- {
		typed, ok := msgs[i].(T)
		if !ok {
			continue
		}
		if predicate != nil && !predicate(typed) {
			continue
		}
		return typed
	}
	return zero
}
