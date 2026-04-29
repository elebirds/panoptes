package projection

import (
	"fmt"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/event"
	gamequery "github.com/elebirds/panoptes/internal/game/query"
	gameresolution "github.com/elebirds/panoptes/internal/game/resolution"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
)

func ProjectGameSync(
	state *domain.GameState,
	playerID string,
	turn int32,
	phase string,
	nextPhase string,
	collector *gameresolution.Collector,
) *pb.MsgGameSync {
	return ProjectGameSyncFromObservation(state, gamequery.NewObservationStore().BuildObservation(state, playerID), turn, phase, nextPhase, collector)
}

func ProjectGameSyncFromObservation(
	state *domain.GameState,
	observation *gamequery.ObservationSnapshot,
	turn int32,
	phase string,
	nextPhase string,
	collector *gameresolution.Collector,
) *pb.MsgGameSync {
	msg := &pb.MsgGameSync{
		Turn:      turn,
		Phase:     phase,
		NextPhase: nextPhase,
		Events:    DomainEventEnvelopes(collector, turn, phase),
	}
	if state == nil {
		return msg
	}

	playerID := ""
	if observation != nil {
		playerID = observation.ViewerID
		msg.Nodes = observation.Nodes
		msg.Units = observation.Units
		msg.MyPlayer = observation.MyPlayer
	}
	if msg.MyPlayer == nil {
		msg.MyPlayer = gamequery.BuildPlayerView(state, playerID)
	}
	if msg.Nodes == nil {
		msg.Nodes = gamequery.BuildNodeViews(state, playerID)
	}
	if msg.Units == nil {
		msg.Units = gamequery.BuildUnitViews(state)
	}
	if phase == domain.PhasePlanning.String() {
		msg.Snapshot = gamequery.BuildPlanningSnapshot(state, playerID)
		msg.MinisterProposals = gamequery.BuildMinisterProposalViews(state, playerID)
	}
	return msg
}

func DomainEventEnvelopes(collector *gameresolution.Collector, turn int32, phase string) []*pb.DomainEventEnvelope {
	if collector == nil {
		collector = gameresolution.NewCollector()
	}
	channels := []gameresolution.Channel{
		gameresolution.ChannelPlanning,
		gameresolution.ChannelUnit,
		gameresolution.ChannelMap,
		gameresolution.ChannelEconomy,
	}
	out := make([]*pb.DomainEventEnvelope, 0)
	for _, channel := range channels {
		for idx, evt := range collector.Events(channel) {
			if evt == nil {
				continue
			}
			out = append(out, domainEventEnvelope(evt, channel, turn, phase, idx))
		}
	}
	return out
}

func domainEventEnvelope(evt event.Event, channel gameresolution.Channel, turn int32, phase string, idx int) *pb.DomainEventEnvelope {
	kind, data := EventPayloadFromEvent(evt)
	envelope := &pb.DomainEventEnvelope{
		EventId: fmt.Sprintf("%d:%s:%d:%s", turn, channel, idx, evt.Kind()),
		Turn:    turn,
		Phase:   phase,
		Channel: string(channel),
		Source:  "server",
		Kind:    kind,
		Data:    data,
	}
	switch e := evt.(type) {
	case event.ResearchTargetChangedEvent:
		envelope.Event = &pb.DomainEventEnvelope_ResearchTargetChanged{
			ResearchTargetChanged: &pb.DomainResearchTargetChangedEvent{
				PlayerId:     e.PlayerID,
				TechnologyId: e.TechnologyID,
			},
		}
	case event.PolicyChangedEvent:
		envelope.Event = &pb.DomainEventEnvelope_PolicyChanged{
			PolicyChanged: &pb.DomainPolicyChangedEvent{
				PlayerId:  e.PlayerID,
				OldPolicy: e.OldPolicy,
				NewPolicy: e.NewPolicy,
			},
		}
	case event.TechnologyCompletedEvent:
		envelope.Event = &pb.DomainEventEnvelope_TechnologyCompleted{
			TechnologyCompleted: &pb.DomainTechnologyCompletedEvent{
				PlayerId:     e.PlayerID,
				TechnologyId: e.TechnologyID,
			},
		}
	case event.TechnologyActivatedEvent:
		envelope.Event = &pb.DomainEventEnvelope_TechnologyActivated{
			TechnologyActivated: &pb.DomainTechnologyActivatedEvent{
				PlayerId:     e.PlayerID,
				TechnologyId: e.TechnologyID,
			},
		}
	case event.UnitMovedEvent:
		envelope.Event = &pb.DomainEventEnvelope_UnitMoved{
			UnitMoved: &pb.DomainUnitMovedEvent{
				UnitId: e.UnitID,
			},
		}
	case event.CityFoundedEvent:
		envelope.Event = &pb.DomainEventEnvelope_CityFounded{
			CityFounded: &pb.DomainCityFoundedEvent{
				PlayerId:     e.PlayerID,
				UnitId:       e.UnitID,
				CityId:       e.CityID,
				CenterNodeId: e.CenterNodeID,
			},
		}
	case event.BuildingBuiltEvent:
		envelope.Event = &pb.DomainEventEnvelope_BuildingBuilt{
			BuildingBuilt: &pb.DomainBuildingBuiltEvent{
				NodeId:         e.NodeID,
				BuildingTypeId: e.BuildingType,
				Owner:          e.Owner,
				CityId:         e.CityID,
			},
		}
	}
	return envelope
}
