package projection

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/event"
	gameresolution "github.com/elebirds/panoptes/internal/game/resolution"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
)

func TestProjectGameSyncIncludesTypedDomainEventEnvelope(t *testing.T) {
	collector := gameresolution.NewCollector()
	collector.AppendDeferred(gameresolution.ChannelPlanning, event.ResearchTargetChangedEvent{
		PlayerID:     "player-1",
		TechnologyID: "agrarian_foundations",
	})

	msg := ProjectGameSync(nil, "player-1", 3, domain.PhaseResolving.String(), domain.PhasePlanning.String(), collector)
	if msg.GetTurn() != 3 {
		t.Fatalf("turn = %d, want 3", msg.GetTurn())
	}
	if msg.GetPhase() != domain.PhaseResolving.String() {
		t.Fatalf("phase = %q, want resolving", msg.GetPhase())
	}
	if msg.GetNextPhase() != domain.PhasePlanning.String() {
		t.Fatalf("next_phase = %q, want planning", msg.GetNextPhase())
	}
	if len(msg.GetEvents()) != 1 {
		t.Fatalf("event count = %d, want 1", len(msg.GetEvents()))
	}
	envelope := msg.GetEvents()[0]
	if envelope.GetChannel() != string(gameresolution.ChannelPlanning) {
		t.Fatalf("channel = %q, want planning", envelope.GetChannel())
	}
	if envelope.GetResearchTargetChanged().GetTechnologyId() != "agrarian_foundations" {
		t.Fatalf("typed research target event = %#v", envelope.GetEvent())
	}
}

func TestProjectGameSyncIncludesTypedUnitMovedCoordinates(t *testing.T) {
	collector := gameresolution.NewCollector()
	collector.AppendDeferred(gameresolution.ChannelUnit, event.UnitMovedEvent{
		UnitID: "unit-1",
		From:   domain.Position{Q: 1, R: 2},
		To:     domain.Position{Q: 3, R: 4},
	})

	msg := ProjectGameSync(nil, "player-1", 3, domain.PhaseResolving.String(), domain.PhasePlanning.String(), collector)
	moved := msg.GetEvents()[0].GetUnitMoved()
	if moved == nil {
		t.Fatalf("typed unit_moved event is nil")
	}
	if moved.GetUnitId() != "unit-1" || moved.GetFromQ() != 1 || moved.GetFromR() != 2 || moved.GetToQ() != 3 || moved.GetToR() != 4 {
		t.Fatalf("typed unit_moved = %#v", moved)
	}
}

func TestV2CommandBatchProtoShape(t *testing.T) {
	batch := &pb.MsgGameCommandBatch{
		Commands: []*pb.CommandEnvelope{{
			CommandId:     "cmd-1",
			ParticipantId: "player-1",
			Source:        "human",
			Turn:          3,
			Body: &pb.CommandEnvelope_SetResearchTarget{
				SetResearchTarget: &pb.MsgSetResearchTarget{TechnologyId: "agrarian_foundations"},
			},
		}},
	}
	if got := batch.GetCommands()[0].GetSetResearchTarget().GetTechnologyId(); got != "agrarian_foundations" {
		t.Fatalf("technology_id = %q", got)
	}
}
