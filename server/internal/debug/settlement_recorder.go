package debug

import (
	"sync"

	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"google.golang.org/protobuf/proto"
)

type SettlementRecorder struct {
	mu        sync.RWMutex
	gameSyncs map[string]map[string]*pb.MsgGameSync
	gameOvers map[string]*pb.MsgGameOver
}

func NewSettlementRecorder() *SettlementRecorder {
	return &SettlementRecorder{
		gameSyncs: make(map[string]map[string]*pb.MsgGameSync),
		gameOvers: make(map[string]*pb.MsgGameOver),
	}
}

func (r *SettlementRecorder) RecordGameSync(roomID string, playerID string, msg *pb.MsgGameSync) {
	if r == nil || roomID == "" || playerID == "" || msg == nil {
		return
	}

	r.mu.Lock()
	defer r.mu.Unlock()

	if r.gameSyncs[roomID] == nil {
		r.gameSyncs[roomID] = make(map[string]*pb.MsgGameSync)
	}
	r.gameSyncs[roomID][playerID] = proto.Clone(msg).(*pb.MsgGameSync)
}

func (r *SettlementRecorder) LatestGameSync(roomID string, playerID string) *pb.MsgGameSync {
	if r == nil || roomID == "" || playerID == "" {
		return nil
	}

	r.mu.RLock()
	defer r.mu.RUnlock()

	msg := r.gameSyncs[roomID][playerID]
	if msg == nil {
		return nil
	}
	return proto.Clone(msg).(*pb.MsgGameSync)
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
