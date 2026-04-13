package phase

import (
	"encoding/json"
	"fmt"
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

type tokenExpandTerritoryPayload struct {
	UnitID       string `json:"unit_id"`
	UnitId       string `json:"unitId"`
	CenterNodeID string `json:"center_node_id"`
	CenterNodeId string `json:"centerNodeId"`
	ForceCenter  bool   `json:"force_center"`
}

// ApplyTerritoryExpansion applies a queued territory deployment order.
// It reuses the same validation and world mutation path as MsgTokenExpandTerritory.
func ApplyTerritoryExpansion(room Room, playerID, unitID, centerNodeID string) error {
	if room == nil {
		return fmt.Errorf("room is nil")
	}

	state := room.State()
	if state == nil {
		return fmt.Errorf("state is nil")
	}

	playerState, ok := state.Players[playerID]
	if !ok || playerState == nil {
		return fmt.Errorf("player not found: %s", playerID)
	}

	centerNodeID = strings.TrimSpace(centerNodeID)

	payload, err := json.Marshal(&tokenExpandTerritoryPayload{
		UnitID:       unitID,
		CenterNodeID: centerNodeID,
		ForceCenter:  centerNodeID != "",
	})
	if err != nil {
		return err
	}

	return (&DomesticPhase{}).handleExpandTerritory(room, state, playerID, playerState, payload)
}

func (p *DomesticPhase) handleExpandTerritory(room Room, state *domain.GameState, playerID string, playerState *domain.PlayerState, payload []byte) error {
	req := &tokenExpandTerritoryPayload{}
	if err := json.Unmarshal(payload, req); err != nil {
		_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "expand_territory", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "invalid_request"})
		return err
	}

	unitID := strings.TrimSpace(req.UnitID)
	if unitID == "" {
		unitID = strings.TrimSpace(req.UnitId)
	}
	if unitID == "" {
		_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "expand_territory", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "invalid_request"})
		return nil
	}

	unitEntry, ok := findUnitEntryByID(state.World, unitID)
	if !ok {
		_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "expand_territory", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "unit_not_found"})
		return nil
	}

	stats := ecs.UnitStatsC.Get(unitEntry)
	if stats.Faction != playerID {
		_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "expand_territory", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "unit_not_owned"})
		return nil
	}
	if !isTerritoryExpansionUnit(stats.Type) {
		_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "expand_territory", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "invalid_unit_type"})
		return nil
	}

	unitPos := ecs.PositionC.Get(unitEntry)
	unitNodeEntry, ok := domain.GetNodeAt(state.World, domain.Position{X: unitPos.X, Y: unitPos.Y})
	if !ok {
		_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "expand_territory", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "invalid_target"})
		return nil
	}
	centerEntry := unitNodeEntry
	centerNodeID := ecs.NodeC.Get(unitNodeEntry).ID

	requestedCenterNodeID := strings.TrimSpace(req.CenterNodeID)
	if requestedCenterNodeID == "" {
		requestedCenterNodeID = strings.TrimSpace(req.CenterNodeId)
	}
	if requestedCenterNodeID != "" && requestedCenterNodeID != centerNodeID {
		requestedCenterEntry, found := room.NodeByID(requestedCenterNodeID)
		if !found {
			_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "expand_territory", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "invalid_target"})
			return nil
		}
		if !req.ForceCenter {
			requestedPos := ecs.PositionC.Get(requestedCenterEntry)
			dist := abs(requestedPos.X-unitPos.X) + abs(requestedPos.Y-unitPos.Y)
			if dist > 1 {
				_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "expand_territory", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "center_too_far"})
				return nil
			}
		}
		centerEntry = requestedCenterEntry
		centerNodeID = requestedCenterNodeID
	}

	footprintEntries, footprintIDs, err := collectTerritory3x3(state.World, centerEntry)
	if err != nil {
		_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "expand_territory", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "territory_out_of_bounds"})
		return nil
	}

	for _, entry := range footprintEntries {
		if entry == nil {
			continue
		}

		nodeComp := ecs.NodeC.Get(entry)
		if nodeComp.IsResource {
			_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "expand_territory", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "territory_blocked"})
			return nil
		}

		if !entry.HasComponent(ecs.BuildingC) {
			continue
		}
		building := ecs.BuildingC.Get(entry)
		buildingType := normalizeToken(string(building.Type))
		nodeID := ecs.NodeC.Get(entry).ID
		if nodeID == centerNodeID && buildingType == "castle" {
			continue
		}
		_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "expand_territory", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "territory_blocked"})
		return nil
	}

	barracksEntry, barracksNodeID := pickBarracksNode(centerEntry, footprintEntries)
	if barracksEntry == nil || barracksNodeID == "" {
		_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "expand_territory", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "no_barracks_slot"})
		return nil
	}

	for _, entry := range footprintEntries {
		node := ecs.NodeC.Get(entry)
		node.TerritoryOwner = playerID
		// Keep owner aligned for backward-compatible clients that do not consume territory_owner.
		node.Owner = playerID
	}

	setBuildingOnNode(centerEntry, "castle", playerID, centerNodeID)
	setBuildingOnNode(barracksEntry, "barracks", playerID, centerNodeID)
	state.EnsureCastleState(playerID, centerNodeID)

	centerPos := ecs.PositionC.Get(centerEntry)
	world := state.World
	world.Remove(unitEntry.Entity())

	_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: true, Action: "expand_territory", TokensLeft: int32(playerState.TokensLeft)})

	for viewerID, viewerState := range state.Players {
		for _, nodeID := range footprintIDs {
			view := room.BuildNodeViewForPlayer(nodeID, viewerID)
			if view == nil {
				continue
			}
			_ = room.SendToPlayer(viewerID, &pb.MsgRevealResult{
				NodeId:    nodeID,
				TrueState: view,
				TokensLeft: func() int32 {
					if viewerState == nil {
						return 0
					}
					return int32(viewerState.TokensLeft)
				}(),
			})
		}
	}

	removeEvent := &pb.MsgCombatSettlement{
		Events: []*pb.CombatEvent{
			{
				Type: "unit_died",
				Data: &pb.CombatEvent_UnitDied{
					UnitDied: &pb.UnitDiedEvent{
						UnitId:   unitID,
						KillerId: "territory_expand",
						Pos:      &pb.Position{X: int32(centerPos.X), Y: int32(centerPos.Y)},
					},
				},
			},
		},
	}
	for viewerID := range state.Players {
		_ = room.SendToPlayer(viewerID, removeEvent)
	}

	return nil
}

func collectTerritory3x3(world donburi.World, centerEntry *donburi.Entry) ([]*donburi.Entry, []string, error) {
	if centerEntry == nil {
		return nil, nil, fmt.Errorf("center entry is nil")
	}

	centerPos := ecs.PositionC.Get(centerEntry)
	entries := make([]*donburi.Entry, 0, 9)
	ids := make([]string, 0, 9)
	for dy := -1; dy <= 1; dy++ {
		for dx := -1; dx <= 1; dx++ {
			pos := domain.Position{X: centerPos.X + dx, Y: centerPos.Y + dy}
			entry, ok := domain.GetNodeAt(world, pos)
			if !ok {
				return nil, nil, fmt.Errorf("node out of map (%d,%d)", pos.X, pos.Y)
			}
			entries = append(entries, entry)
			ids = append(ids, ecs.NodeC.Get(entry).ID)
		}
	}

	return entries, ids, nil
}

func pickBarracksNode(centerEntry *donburi.Entry, footprintEntries []*donburi.Entry) (*donburi.Entry, string) {
	if centerEntry == nil {
		return nil, ""
	}

	centerPos := ecs.PositionC.Get(centerEntry)
	byPos := make(map[domain.Position]*donburi.Entry, len(footprintEntries))
	for _, entry := range footprintEntries {
		if entry == nil {
			continue
		}
		pos := ecs.PositionC.Get(entry)
		byPos[domain.Position{X: pos.X, Y: pos.Y}] = entry
	}

	candidates := []domain.Position{
		{X: centerPos.X + 1, Y: centerPos.Y},
		{X: centerPos.X - 1, Y: centerPos.Y},
		{X: centerPos.X, Y: centerPos.Y + 1},
		{X: centerPos.X, Y: centerPos.Y - 1},
	}
	for _, candidate := range candidates {
		entry, ok := byPos[candidate]
		if !ok || entry == nil {
			continue
		}
		return entry, ecs.NodeC.Get(entry).ID
	}

	for _, entry := range footprintEntries {
		if entry == nil || entry == centerEntry {
			continue
		}
		return entry, ecs.NodeC.Get(entry).ID
	}

	return nil, ""
}

func setBuildingOnNode(nodeEntry *donburi.Entry, buildingType, owner string, castleID string) {
	if nodeEntry == nil {
		return
	}

	maxHP, wallLevel, towers := resolveBuildingTemplate(buildingType)
	comp := domain.BuildingComp{
		Type:      domain.BuildingType(buildingType),
		HP:        maxHP,
		MaxHP:     maxHP,
		WallLevel: wallLevel,
		Owner:     owner,
		CastleID:  castleID,
		Towers:    towers,
	}

	if !nodeEntry.HasComponent(ecs.BuildingC) {
		nodeEntry.AddComponent(ecs.BuildingC)
	}
	ecs.BuildingC.SetValue(nodeEntry, comp)

	node := ecs.NodeC.Get(nodeEntry)
	node.Owner = owner
}

func resolveBuildingTemplate(buildingType string) (maxHP int, wallLevel int, towers int) {
	maxHP = 100

	catalog := staticdata.Default()
	if catalog != nil {
		if cfg, ok := catalog.GetBuilding(buildingType); ok {
			maxHP = cfg.Combat.MaxHP
			wallLevel = cfg.Combat.WallLevel
			towers = cfg.Combat.Towers
			return
		}
		if normalizeToken(buildingType) == "castle" {
			maxHP = catalog.Rules().CastleBaseHP
			return
		}
	}

	switch normalizeToken(buildingType) {
	case "barracks":
		return 120, 0, 0
	case "castle":
		return 100, 0, 0
	default:
		return maxHP, 0, 0
	}
}

func findUnitEntryByID(world donburi.World, unitID string) (*donburi.Entry, bool) {
	var found *donburi.Entry
	ecs.AllUnits(world).Each(world, func(entry *donburi.Entry) {
		if found != nil || entry == nil {
			return
		}
		stats := ecs.UnitStatsC.Get(entry)
		if stats.ID == unitID {
			found = entry
		}
	})
	return found, found != nil
}

func isTerritoryExpansionUnit(unitType domain.UnitType) bool {
	switch normalizeToken(string(unitType)) {
	case "settler", "pioneer", "expander", "engineer":
		return true
	default:
		return false
	}
}

func normalizeToken(value string) string {
	return strings.ToLower(strings.TrimSpace(value))
}

func abs(value int) int {
	if value < 0 {
		return -value
	}
	return value
}
