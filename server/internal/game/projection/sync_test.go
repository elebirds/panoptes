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

func TestDomainEventEnvelopeTypedProjectionContract(t *testing.T) {
	t.Parallel()

	cases := []struct {
		name      string
		channel   gameresolution.Channel
		ev        event.Event
		typedName string
	}{
		{name: "research target", channel: gameresolution.ChannelPlanning, ev: event.ResearchTargetChangedEvent{PlayerID: "player-1", TechnologyID: "agrarian_foundations"}, typedName: "research_target_changed"},
		{name: "policy", channel: gameresolution.ChannelPlanning, ev: event.PolicyChangedEvent{PlayerID: "player-1", OldPolicy: "balanced", NewPolicy: "expansion"}, typedName: "policy_changed"},
		{name: "technology completed", channel: gameresolution.ChannelEconomy, ev: event.TechnologyCompletedEvent{PlayerID: "player-1", TechnologyID: "agrarian_foundations"}, typedName: "technology_completed"},
		{name: "technology activated", channel: gameresolution.ChannelPlanning, ev: event.TechnologyActivatedEvent{PlayerID: "player-1", TechnologyID: "agrarian_foundations"}, typedName: "technology_activated"},
		{name: "unit moved", channel: gameresolution.ChannelUnit, ev: event.UnitMovedEvent{UnitID: "unit-1", From: domain.Position{Q: 1, R: 2}, To: domain.Position{Q: 3, R: 4}}, typedName: "unit_moved"},
		{name: "city founded", channel: gameresolution.ChannelMap, ev: event.CityFoundedEvent{PlayerID: "player-1", UnitID: "settler-1", CityID: "city-1", CenterNodeID: "C3"}, typedName: "city_founded"},
		{name: "building built", channel: gameresolution.ChannelEconomy, ev: event.BuildingBuiltEvent{NodeID: "C4", BuildingType: "farm", Owner: "player-1", CityID: "city-1"}, typedName: "building_built"},
	}

	for _, tc := range cases {
		tc := tc
		t.Run(tc.name, func(t *testing.T) {
			t.Parallel()
			envelope := domainEventEnvelope(tc.ev, tc.channel, 3, domain.PhaseResolving.String(), 0)
			if envelope.GetKind() != tc.ev.Kind() {
				t.Fatalf("kind = %q, want %q", envelope.GetKind(), tc.ev.Kind())
			}
			if got := typedProjectionName(envelope); got != tc.typedName {
				t.Fatalf("typed projection = %q, want %q", got, tc.typedName)
			}
		})
	}
}

func typedProjectionName(envelope *pb.DomainEventEnvelope) string {
	switch envelope.GetEvent().(type) {
	case *pb.DomainEventEnvelope_ResearchTargetChanged:
		return "research_target_changed"
	case *pb.DomainEventEnvelope_PolicyChanged:
		return "policy_changed"
	case *pb.DomainEventEnvelope_TechnologyCompleted:
		return "technology_completed"
	case *pb.DomainEventEnvelope_TechnologyActivated:
		return "technology_activated"
	case *pb.DomainEventEnvelope_UnitMoved:
		return "unit_moved"
	case *pb.DomainEventEnvelope_CityFounded:
		return "city_founded"
	case *pb.DomainEventEnvelope_BuildingBuilt:
		return "building_built"
	default:
		return ""
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
