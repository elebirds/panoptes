package event

import (
	"github.com/elebirds/panoptes/internal/domain"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/yohamta/donburi"
)

// Event is the single write entry for all game state mutations.
type Event interface {
	Apply(world donburi.World, state *domain.GameState)
	ClientPayload() *pb.CombatEvent
	String() string
}
