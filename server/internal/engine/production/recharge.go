package production

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

type RechargeSystem struct{}

func (s *RechargeSystem) Run(world donburi.World, state *domain.GameState) []event.Event {
	rules := staticdata.Default().Rules()
	events := make([]event.Event, 0, len(state.Players))
	for playerID := range state.Players {
		events = append(events, event.BuildPointsRechargedEvent{PlayerID: playerID, Amount: rules.BuildPointsPerTurn})
		events = append(events, event.TechPointsRechargedEvent{PlayerID: playerID, Amount: state.EffectiveTechPointIncome(playerID)})
	}
	return events
}
