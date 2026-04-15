package game

import (
	"sync"

	"github.com/elebirds/panoptes/internal/domain"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
)

type DebugHooks struct {
	DumpStateSummary func(state *domain.GameState)
	RecordSettlement func(roomID string, playerID string, msg *pb.MsgTurnSettlement)
	RecordGameOver   func(roomID string, msg *pb.MsgGameOver)
}

var (
	debugHooksMu sync.RWMutex
	debugHooks   DebugHooks
)

func SetDebugHooks(hooks DebugHooks) {
	debugHooksMu.Lock()
	defer debugHooksMu.Unlock()
	debugHooks = hooks
}

func currentDebugHooks() DebugHooks {
	debugHooksMu.RLock()
	defer debugHooksMu.RUnlock()
	return debugHooks
}
