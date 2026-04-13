package phase

import (
	"encoding/json"
	"errors"
	"strings"

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

type tokenBuildPayload struct {
	CastleID      string `json:"castle_id"`
	CastleId      string `json:"castleId"`
	NodeID        string `json:"node_id"`
	NodeId        string `json:"nodeId"`
	BuildingType  string `json:"building_type"`
	BuildingType2 string `json:"buildingType"`
}

func (p *DomesticPhase) Name() string { return domain.PhaseDomesticPlanning.String() }

func (p *DomesticPhase) Enter(room Room) {
	p.submitted = map[string]bool{}
	p.submitCh = make(chan string, 16)
	state := room.State()
	rules := staticdata.Default().Rules()
	for _, player := range state.Players {
		player.TokensLeft = rules.TokensPerTurn
	}
	room.NotifyTurn(domain.PhaseDomesticPlanning.String())
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
		req := &tokenBuildPayload{}
		if err := json.Unmarshal(payload, req); err != nil {
			_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "build", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "invalid_request"})
			return err
		}
		return p.handleBuildRequest(room, playerID, playerState, firstNonEmpty(req.NodeID, req.NodeId), firstNonEmpty(req.BuildingType, req.BuildingType2), firstNonEmpty(req.CastleID, req.CastleId))

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

func (p *DomesticPhase) handleBuildRequest(room Room, playerID string, playerState *domain.PlayerState, nodeID string, buildingType string, castleID string) error {
	if playerState == nil {
		return errors.New("player not found")
	}

	nodeID = strings.TrimSpace(nodeID)
	buildingType = strings.TrimSpace(buildingType)
	castleID = strings.TrimSpace(castleID)

	if playerState.TokensLeft <= 0 {
		_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "build", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "no_tokens_left"})
		return nil
	}

	nodeEntry, ok := room.NodeByID(nodeID)
	if !ok {
		_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "build", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "invalid_target"})
		return nil
	}
	if nodeEntry.HasComponent(ecs.BuildingC) {
		_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "build", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "building_exists"})
		return nil
	}

	cfg, ok := staticdata.Default().GetBuilding(buildingType)
	if !ok {
		_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "build", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "invalid_target"})
		return nil
	}

	if castleID != "" {
		if errCode := validateCastleContext(room, playerID, castleID); errCode != "" {
			_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "build", TokensLeft: int32(playerState.TokensLeft), ErrorCode: errCode})
			return nil
		}
	}

	nodeComp := ecs.NodeC.Get(nodeEntry)
	if errCode := validateBuildPlacement(nodeComp, cfg, playerID); errCode != "" {
		_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{
			Success:    false,
			Action:     "build",
			TokensLeft: int32(playerState.TokensLeft),
			ErrorCode:  errCode,
		})
		return nil
	}

	cost := toResourceBag(cfg.BuildCost)
	// 建造资源校验改为按 castleID 对应资源池判断。
	// 这样客户端看到的城堡资源看板，和“这个城堡当前还能不能继续建造”
	// 使用的是同一套结算口径。
	if !room.State().CanAffordFromCastle(playerID, castleID, cost) && !room.IsDevMode() {
		_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "build", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "insufficient_resources"})
		return nil
	}

	room.QueueBuildOrder(domain.BuildOrder{PlayerID: playerID, NodeID: nodeID, BuildingType: buildingType, CastleID: castleID})
	playerState.TokensLeft--
	_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: true, Action: "build", TokensLeft: int32(playerState.TokensLeft)})
	return nil
}

func validateCastleContext(room Room, playerID string, castleID string) string {
	castleID = strings.TrimSpace(castleID)
	if castleID == "" {
		return "invalid_request"
	}

	castleEntry, ok := room.NodeByID(castleID)
	if !ok || !castleEntry.HasComponent(ecs.BuildingC) {
		return "invalid_target"
	}

	building := ecs.BuildingC.Get(castleEntry)
	if normalizeToken(string(building.Type)) != "castle" {
		return "invalid_target"
	}

	node := ecs.NodeC.Get(castleEntry)
	player := normalizeToken(playerID)
	if normalizeToken(building.Owner) != player && normalizeToken(node.Owner) != player && normalizeToken(node.TerritoryOwner) != player {
		return "unauthorized"
	}

	return ""
}

func firstNonEmpty(values ...string) string {
	for _, value := range values {
		if strings.TrimSpace(value) != "" {
			return strings.TrimSpace(value)
		}
	}
	return ""
}

func validateBuildPlacement(node *ecs.NodeComp, cfg staticdata.BuildingDefinition, playerID string) string {
	if node == nil {
		return "invalid_target"
	}

	terrainID := normalizeToken(string(node.Terrain))
	if terrainID != "" {
		if terrain, ok := staticdata.Default().GetTerrain(terrainID); ok && !terrain.Buildable {
			return "terrain_not_buildable"
		}
	}

	rule := normalizeToken(cfg.PlacementRule)
	switch rule {
	case "city_only":
		player := normalizeToken(playerID)
		territoryOwner := normalizeToken(node.TerritoryOwner)
		owner := normalizeToken(node.Owner)
		if territoryOwner != player && owner != player {
			return "outside_territory"
		}
	case "resource_only":
		if !node.IsResource {
			return "resource_only_required"
		}
		required := normalizeToken(cfg.RequiredResourceType)
		if required != "" && normalizeToken(node.ResourceType) != required {
			return "resource_type_mismatch"
		}
	}

	return ""
}
