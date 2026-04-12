package phase

import (
	"errors"

	"github.com/elebirds/panoptes/internal/domain"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"google.golang.org/protobuf/encoding/protojson"
)

type CombatPhase struct {
	submitted  map[string]bool
	directives map[string][]WarZoneDirective
	submitCh   chan string
}

func (p *CombatPhase) Name() string { return "combat" }

func (p *CombatPhase) Enter(room Room) {
	p.submitted = map[string]bool{}
	p.directives = map[string][]WarZoneDirective{}
	p.submitCh = make(chan string, 16)
	room.NotifyTurn("combat")
}

func (p *CombatPhase) HandleMessage(room Room, playerID string, msgType string, payload []byte) error {
	state := room.State()
	playerState, ok := state.Players[playerID]
	if !ok {
		return errors.New("player not found")
	}

	switch msgType {
	case "MsgSetWarZone":
		msg := &pb.MsgSetWarZone{}
		if err := protojson.Unmarshal(payload, msg); err != nil {
			return err
		}
		updated := false
		for _, zone := range playerState.WarZones {
			if zone.ID == msg.GetZoneId() {
				zone.Name = msg.GetName()
				zone.NodeIDs = msg.GetNodeIds()
				updated = true
				break
			}
		}
		if !updated {
			playerState.WarZones = append(playerState.WarZones, &domain.WarZone{ID: msg.GetZoneId(), Name: msg.GetName(), NodeIDs: msg.GetNodeIds()})
		}
		return nil

	case "MsgWarZoneDirective":
		msg := &pb.MsgWarZoneDirective{}
		if err := protojson.Unmarshal(payload, msg); err != nil {
			return err
		}
		d := WarZoneDirective{ZoneID: msg.GetZoneId(), Directive: msg.GetDirective(), TargetNode: msg.GetTargetNode()}
		p.directives[playerID] = append(p.directives[playerID], d)
		room.SetWarDirectives(playerID, p.directives[playerID])
		return nil

	case "MsgTokenVetoCombat":
		msg := &pb.MsgTokenVetoCombat{}
		if err := protojson.Unmarshal(payload, msg); err != nil {
			_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "veto", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "invalid_request"})
			return err
		}
		if playerState.TokensLeft <= 0 {
			_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "veto", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "no_tokens_left"})
			return nil
		}
		room.SetVetoUnit(playerID, msg.GetUnitId())
		playerState.TokensLeft--
		_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: true, Action: "veto", TokensLeft: int32(playerState.TokensLeft)})
		return nil

	case "MsgTokenMicro":
		msg := &pb.MsgTokenMicro{}
		if err := protojson.Unmarshal(payload, msg); err != nil {
			_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "micro", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "invalid_request"})
			return err
		}
		if playerState.TokensLeft <= 0 {
			_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "micro", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "no_tokens_left"})
			return nil
		}
		room.SetMicroOrder(playerID, msg.GetUnitId(), msg.GetTargetNode())
		playerState.TokensLeft--
		_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: true, Action: "micro", TokensLeft: int32(playerState.TokensLeft)})
		return nil

	case "MsgCombatOrder":
		msg := &pb.MsgCombatOrder{}
		if err := protojson.Unmarshal(payload, msg); err != nil {
			return err
		}
		room.SetCombatOrder(domain.CombatOrder{
			PlayerID:     playerID,
			UnitID:       msg.GetUnitId(),
			Action:       domain.CombatAction(msg.GetAction()),
			TargetNodeID: msg.GetTargetNodeId(),
			TargetUnitID: msg.GetTargetUnitId(),
		})
		return nil

	case "MsgSubmitCombat":
		room.Submit(playerID)
		p.submitted[playerID] = true
		return nil
	}

	return nil
}

func (p *CombatPhase) Timeout(room Room) {
	room.Submit("timeout")
}
