package phase

import (
	"errors"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/event"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	"google.golang.org/protobuf/encoding/protojson"
)

type DomesticPhase struct {
	submitted map[string]bool
	submitCh  chan string
}

func (p *DomesticPhase) Name() string { return "domestic" }

func (p *DomesticPhase) Enter(room Room) {
	p.submitted = map[string]bool{}
	p.submitCh = make(chan string, 16)
	state := room.State()
	rules := staticdata.Default().Rules()
	for _, player := range state.Players {
		player.TokensLeft = rules.TokensPerTurn
	}
	room.NotifyTurn("domestic")
}

func (p *DomesticPhase) HandleMessage(room Room, playerID string, msgType string, payload []byte) error {
	state := room.State()
	playerState, ok := state.Players[playerID]
	if !ok {
		return errors.New("player not found")
	}

	switch msgType {
	case "MsgSetPolicy":
		msg := &pb.MsgSetPolicy{}
		if err := protojson.Unmarshal(payload, msg); err != nil {
			return err
		}
		event.PolicyChangedEvent{PlayerID: playerID, OldPolicy: string(playerState.Policy), NewPolicy: msg.GetPolicy()}.Apply(state.World, state)
		_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: true, Action: "set_policy", TokensLeft: int32(playerState.TokensLeft)})
		return nil

	case "MsgTokenBuild":
		msg := &pb.MsgTokenBuild{}
		if err := protojson.Unmarshal(payload, msg); err != nil {
			_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "build", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "invalid_request"})
			return err
		}
		if playerState.TokensLeft <= 0 {
			_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "build", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "no_tokens_left"})
			return nil
		}
		nodeEntry, ok := room.NodeByID(msg.GetNodeId())
		if !ok {
			_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "build", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "invalid_target"})
			return nil
		}
		pos := ecs.PositionC.Get(nodeEntry)
		if !domain.IsInSafeZone(state, domain.Position{X: pos.X, Y: pos.Y}, playerID) {
			_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "build", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "outside_safe_zone"})
			return nil
		}
		if nodeEntry.HasComponent(ecs.BuildingC) {
			_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "build", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "building_exists"})
			return nil
		}
		cfg, ok := staticdata.Default().GetBuilding(msg.GetBuildingType())
		if !ok {
			_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "build", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "invalid_target"})
			return nil
		}
		cost := toResourceBag(cfg.BuildCost)
		if !playerState.Resources.CanAfford(cost) {
			_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "build", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "insufficient_resources"})
			return nil
		}
		room.QueueBuildOrder(domain.BuildOrder{PlayerID: playerID, NodeID: msg.GetNodeId(), BuildingType: msg.GetBuildingType()})
		playerState.TokensLeft--
		_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: true, Action: "build", TokensLeft: int32(playerState.TokensLeft)})
		return nil

	case "MsgTokenExpandTerritory":
		return p.handleExpandTerritory(room, state, playerID, playerState, payload)

	case "MsgTokenReveal":
		msg := &pb.MsgTokenReveal{}
		if err := protojson.Unmarshal(payload, msg); err != nil {
			_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "reveal", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "invalid_request"})
			return err
		}
		if playerState.TokensLeft <= 0 {
			_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "reveal", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "no_tokens_left"})
			return nil
		}
		nodeView := room.BuildNodeViewForPlayer(msg.GetNodeId(), playerID)
		if nodeView == nil {
			_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "reveal", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "invalid_target"})
			return nil
		}
		playerState.TokensLeft--
		_ = room.SendToPlayer(playerID, &pb.MsgRevealResult{NodeId: msg.GetNodeId(), TrueState: nodeView, TokensLeft: int32(playerState.TokensLeft)})
		return nil

	case "MsgMinisterDirective":
		msg := &pb.MsgMinisterDirective{}
		if err := protojson.Unmarshal(payload, msg); err != nil {
			return err
		}
		room.SetMinisterDirective(playerID, msg.GetContent())
		return nil

	case "MsgSubmitDomestic":
		room.Submit(playerID)
		p.submitted[playerID] = true
		return nil
	}

	return nil
}

func (p *DomesticPhase) Timeout(room Room) {
	room.Submit("timeout")
}

func toResourceBag(amounts map[string]int) domain.ResourceBag {
	bag := domain.NewResourceBag()
	for key, value := range amounts {
		bag.Set(domain.ResourceKey(key), value)
	}
	return bag
}
