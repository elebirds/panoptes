package debug

import (
	"sync"

	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"google.golang.org/protobuf/proto"
)

type SettlementRecorder struct {
	mu          sync.RWMutex
	settlements map[string]map[string]*pb.MsgTurnSettlement
	gameOvers   map[string]*pb.MsgGameOver
}

func NewSettlementRecorder() *SettlementRecorder {
	return &SettlementRecorder{
		settlements: make(map[string]map[string]*pb.MsgTurnSettlement),
		gameOvers:   make(map[string]*pb.MsgGameOver),
	}
}

func (r *SettlementRecorder) RecordSettlement(roomID string, playerID string, msg *pb.MsgTurnSettlement) {
	if r == nil || roomID == "" || playerID == "" || msg == nil {
		return
	}

	r.mu.Lock()
	defer r.mu.Unlock()

	if r.settlements[roomID] == nil {
		r.settlements[roomID] = make(map[string]*pb.MsgTurnSettlement)
	}
	r.settlements[roomID][playerID] = proto.Clone(msg).(*pb.MsgTurnSettlement)
}

func (r *SettlementRecorder) LatestSettlement(roomID string, playerID string) *pb.MsgTurnSettlement {
	if r == nil || roomID == "" || playerID == "" {
		return nil
	}

	r.mu.RLock()
	defer r.mu.RUnlock()

	msg := r.settlements[roomID][playerID]
	if msg == nil {
		return nil
	}
	return proto.Clone(msg).(*pb.MsgTurnSettlement)
}

func (r *SettlementRecorder) RecordGameOver(roomID string, msg *pb.MsgGameOver) {
	if r == nil || roomID == "" || msg == nil {
		return
	}

	r.mu.Lock()
	defer r.mu.Unlock()
	r.gameOvers[roomID] = proto.Clone(msg).(*pb.MsgGameOver)
}

func (r *SettlementRecorder) LatestGameOver(roomID string) *pb.MsgGameOver {
	if r == nil || roomID == "" {
		return nil
	}

	r.mu.RLock()
	defer r.mu.RUnlock()

	msg := r.gameOvers[roomID]
	if msg == nil {
		return nil
	}
	return proto.Clone(msg).(*pb.MsgGameOver)
}
