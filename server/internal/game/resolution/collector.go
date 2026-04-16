package resolution

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/yohamta/donburi"
)

type Channel string

const (
	ChannelPlanning Channel = "planning"
	ChannelUnit     Channel = "unit"
	ChannelMap      Channel = "map"
	ChannelEconomy  Channel = "economy"
)

type Collector struct {
	events map[Channel][]event.Event
}

func NewCollector() *Collector {
	return &Collector{
		events: map[Channel][]event.Event{
			ChannelPlanning: {},
			ChannelUnit:     {},
			ChannelMap:      {},
			ChannelEconomy:  {},
		},
	}
}

func (c *Collector) ApplyNow(channel Channel, world donburi.World, state *domain.GameState, events ...event.Event) {
	if c == nil {
		return
	}
	for _, evt := range events {
		if evt == nil {
			continue
		}
		c.events[channel] = append(c.events[channel], evt)
		evt.Apply(world, state)
	}
}

func (c *Collector) AppendDeferred(channel Channel, events ...event.Event) {
	if c == nil {
		return
	}
	for _, evt := range events {
		if evt == nil {
			continue
		}
		c.events[channel] = append(c.events[channel], evt)
	}
}

func (c *Collector) Events(channel Channel) []event.Event {
	if c == nil {
		return nil
	}
	return append([]event.Event(nil), c.events[channel]...)
}
