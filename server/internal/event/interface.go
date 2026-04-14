package event

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/yohamta/donburi"
)

// Event is the single write entry for all game state mutations.
type Event interface {
	Apply(world donburi.World, state *domain.GameState)
	Kind() string
	String() string
}
