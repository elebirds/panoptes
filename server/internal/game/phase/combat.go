package phase

import (
	"errors"

	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/engine/combat"
	"github.com/elebirds/panoptes/internal/domain"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/yohamta/donburi"
	"google.golang.org/protobuf/encoding/protojson"
)

type CombatPhase struct {
	submitted  map[string]bool
	directives map[string][]WarZoneDirective
	submitCh   chan string
}

func (p *CombatPhase) Name() string { return domain.PhaseCombatPlanning.String() }

func (p *CombatPhase) Enter(room Room) {
	p.submitted = map[string]bool{}
	p.directives = map[string][]WarZoneDirective{}
	p.submitCh = make(chan string, 16)
	room.NotifyTurn(domain.PhaseCombatPlanning.String())
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
		if snapshotSender, ok := room.(interface {
			SendCombatOrdersSnapshot(playerID string) error
		}); ok {
			_ = snapshotSender.SendCombatOrdersSnapshot(playerID)
		}
		return nil

	case "MsgCombatPathPreviewRequest":
		msg := &pb.MsgCombatPathPreviewRequest{}
		if err := protojson.Unmarshal(payload, msg); err != nil {
			return err
		}
		_ = room.SendToPlayer(playerID, buildCombatPathPreviewResponse(room.State(), playerID, msg))
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

func buildCombatPathPreviewResponse(state *domain.GameState, playerID string, msg *pb.MsgCombatPathPreviewRequest) *pb.MsgCombatPathPreviewResponse {
	resp := &pb.MsgCombatPathPreviewResponse{
		RequestId:    msg.GetRequestId(),
		UnitId:       msg.GetUnitId(),
		Action:       msg.GetAction(),
		TargetNodeId: msg.GetTargetNodeId(),
		Valid:        false,
	}
	if state == nil || msg == nil {
		resp.ErrorCode = "invalid_request"
		return resp
	}
	if domain.CombatAction(msg.GetAction()) != domain.CombatActionMove {
		resp.ErrorCode = "invalid_directive"
		return resp
	}
	entry, ok := findPreviewUnit(state, msg.GetUnitId(), playerID)
	if !ok {
		resp.ErrorCode = "unit_not_found"
		return resp
	}
	if _, ok := state.GetNode(msg.GetTargetNodeId()); !ok {
		resp.ErrorCode = "invalid_target"
		return resp
	}

	planner := combat.NewWeightedRoutePlanner(combat.DefaultTerrainCostPolicy{})
	preview, ok := planner.BuildPreview(state.World, state, ecs.UnitStatsC.Get(entry).ID, msg.GetTargetNodeId())
	if !ok {
		resp.ErrorCode = "invalid_target"
		return resp
	}

	resp.Valid = true
	resp.PathNodeIds = append(resp.PathNodeIds, preview.PathNodeIDs...)
	resp.FirstTurnNodeId = preview.FirstTurnNodeID
	resp.TotalTurns = int32(preview.TotalTurns)
	for _, stop := range preview.TurnStops {
		resp.TurnStops = append(resp.TurnStops, &pb.MarchTurnStop{
			TurnIndex: int32(stop.TurnIndex),
			NodeId:    stop.NodeID,
		})
	}
	return resp
}

func findPreviewUnit(state *domain.GameState, unitID string, ownerID string) (*donburi.Entry, bool) {
	if state == nil || state.World == nil {
		return nil, false
	}
	var found *donburi.Entry
	ecs.AllUnits(state.World).Each(state.World, func(entry *donburi.Entry) {
		if found != nil {
			return
		}
		stats := ecs.UnitStatsC.Get(entry)
		if stats.ID == unitID && stats.Faction == ownerID {
			found = entry
		}
	})
	return found, found != nil
}
