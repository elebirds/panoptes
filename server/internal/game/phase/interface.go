package phase

import (
	"github.com/elebirds/panoptes/internal/domain"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/yohamta/donburi"
	"google.golang.org/protobuf/proto"
)

type Room interface {
	State() *domain.GameState
	NotifyTurn(phase string)
	Submit(playerID string)
	SendToPlayer(playerID string, msg proto.Message) error
	IsDevMode() bool
	QueueBuildOrder(order domain.BuildOrder)
	SetMinisterDirective(playerID string, directive string)
	SetWarDirectives(playerID string, directives []WarZoneDirective)
	SetVetoUnit(playerID string, unitID string)
	SetMicroOrder(playerID string, unitID string, targetNode string)
	SetCombatOrder(order domain.CombatOrder)
	BuildNodeViewForPlayer(nodeID string, viewerID string) *pb.NodeView
	NodeByID(nodeID string) (*donburi.Entry, bool)
}

type Phase interface {
	Name() string
	Enter(room Room)
	HandleMessage(room Room, playerID string, msgType string, payload []byte) error
	Timeout(room Room)
}

type WarZoneDirective struct {
	ZoneID     string
	Directive  string
	TargetNode string
}
