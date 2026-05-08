package debug

import (
	"sync"

	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"google.golang.org/protobuf/proto"
)

type RecordedCommandResult struct {
	Type    string
	Message proto.Message
	Meta    *pb.EventMeta
}

type CommandResultRecorder struct {
	mu      sync.RWMutex
	results map[string]map[string][]RecordedCommandResult
}

func NewCommandResultRecorder() *CommandResultRecorder {
	return &CommandResultRecorder{
		results: make(map[string]map[string][]RecordedCommandResult),
	}
}

func (r *CommandResultRecorder) RecordOutgoingMessage(roomID string, playerID string, msg proto.Message, meta *pb.EventMeta) {
	if r == nil || roomID == "" || playerID == "" || msg == nil {
		return
	}
	msgType, priority := classifyCommandResultMessage(msg)
	if msgType == "" || priority <= 0 {
		return
	}

	r.mu.Lock()
	defer r.mu.Unlock()

	if r.results[roomID] == nil {
		r.results[roomID] = make(map[string][]RecordedCommandResult)
	}
	entries := append(r.results[roomID][playerID], RecordedCommandResult{
		Type:    msgType,
		Message: proto.Clone(msg),
		Meta:    cloneEventMeta(meta),
	})
	if len(entries) > 64 {
		entries = append([]RecordedCommandResult(nil), entries[len(entries)-64:]...)
	}
	r.results[roomID][playerID] = entries
}

func (r *CommandResultRecorder) LatestCommandResult(roomID string, playerID string, requestID string) *RecordedCommandResult {
	if r == nil || roomID == "" || playerID == "" {
		return nil
	}

	r.mu.RLock()
	defer r.mu.RUnlock()

	entries := r.results[roomID][playerID]
	if len(entries) == 0 {
		return nil
	}

	var best *RecordedCommandResult
	bestPriority := -1
	for idx := len(entries) - 1; idx >= 0; idx-- {
		entry := entries[idx]
		if requestID != "" && (entry.Meta == nil || entry.Meta.GetRequestId() != requestID) {
			continue
		}
		_, priority := classifyCommandResultMessage(entry.Message)
		if priority <= 0 {
			continue
		}
		if best == nil || priority > bestPriority {
			copy := cloneRecordedCommandResult(entry)
			best = &copy
			bestPriority = priority
		}
		if priority >= 2 {
			break
		}
	}
	return best
}

func cloneRecordedCommandResult(entry RecordedCommandResult) RecordedCommandResult {
	return RecordedCommandResult{
		Type:    entry.Type,
		Message: proto.Clone(entry.Message),
		Meta:    cloneEventMeta(entry.Meta),
	}
}

func cloneEventMeta(meta *pb.EventMeta) *pb.EventMeta {
	if meta == nil {
		return nil
	}
	return proto.Clone(meta).(*pb.EventMeta)
}

func classifyCommandResultMessage(msg proto.Message) (string, int) {
	switch msg.(type) {
	case *pb.MsgResearchResult:
		return "MsgResearchResult", 2
	case *pb.MsgSetPolicyResult:
		return "MsgSetPolicyResult", 2
	case *pb.MsgSetInstitutionLoadoutResult:
		return "MsgSetInstitutionLoadoutResult", 2
	case *pb.MsgIssueUnitOrderResult:
		return "MsgIssueUnitOrderResult", 2
	case *pb.MsgSetBuildingRecipeResult:
		return "MsgSetBuildingRecipeResult", 2
	case *pb.MsgBuildStructureResult:
		return "MsgBuildStructureResult", 2
	case *pb.MsgPlanningPathPreviewResponse:
		return "MsgPlanningPathPreviewResponse", 1
	case *pb.MsgRevealResult:
		return "MsgRevealResult", 1
	case *pb.MsgTokenResult:
		return "MsgTokenResult", 1
	case *pb.MsgMandateResult:
		return "MsgMandateResult", 1
	default:
		return "", 0
	}
}
