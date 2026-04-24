package building

import (
	"strings"

	"github.com/elebirds/panoptes/internal/algo/geometry"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
	"github.com/yohamta/donburi/filter"
)

var nodesWithBuildingQuery = donburi.NewQuery(filter.Contains(domain.PositionC, domain.NodeC, domain.BuildingC))

func ResolveCityContext(state *domain.GameState, playerID string, cityID string) (*donburi.Entry, *domain.CityState, string) {
	if state == nil {
		return nil, nil, "invalid_target"
	}
	cityID = strings.TrimSpace(cityID)
	if cityID == "" {
		return nil, nil, "invalid_request"
	}
	cityEntry, ok := state.GetNode(cityID)
	if !ok || !cityEntry.HasComponent(domain.BuildingC) {
		return nil, nil, "invalid_target"
	}
	building := domain.BuildingC.Get(cityEntry)
	if !strings.EqualFold(string(building.Type), domain.BuildingScopeCityCore) {
		return nil, nil, "invalid_target"
	}
	node := domain.NodeC.Get(cityEntry)
	player := normalizeToken(playerID)
	if normalizeToken(building.Owner) != player && normalizeToken(node.Owner) != player && normalizeToken(node.TerritoryOwner) != player {
		return nil, nil, "unauthorized"
	}
	ownerState, cityState := state.FindCityState(cityID)
	if ownerState == nil || cityState == nil {
		return cityEntry, &domain.CityState{
			CityID:              cityID,
			CoreNodeID:          cityID,
			OwnerID:             playerID,
			TerritoryBaseRadius: staticdata.Default().Rules().InitialCityTerritoryRadius,
		}, ""
	}
	if normalizeToken(ownerState.PlayerID) != player {
		return nil, nil, "unauthorized"
	}
	return cityEntry, cityState, ""
}

func ValidatePlacement(state *domain.GameState, nodeEntry *donburi.Entry, playerID string, cfg staticdata.BuildingDefinition, cityID string) string {
	if state == nil || nodeEntry == nil {
		return "invalid_target"
	}
	scope := domain.NormalizeBuildingScope(cfg.BuildingScope)
	if scope == domain.BuildingScopeCityCore {
		return "invalid_directive"
	}
	cityID = strings.TrimSpace(cityID)
	if cityID == "" {
		return "invalid_request"
	}
	cityEntry, cityState, errCode := ResolveCityContext(state, playerID, cityID)
	if errCode != "" {
		return errCode
	}
	if !domain.IsCityOnline(state, cityState) {
		return "invalid_directive"
	}
	if errCode := canPlaceAtStatic(state, nodeEntry, playerID, cfg); errCode != "" {
		return errCode
	}
	if scope != domain.BuildingScopeInCity {
		return ""
	}
	radius := staticdata.Default().Rules().InitialCityTerritoryRadius
	if radius <= 0 {
		radius = 1
	}
	targetPos := domain.PositionC.Get(nodeEntry)
	cityPos := domain.PositionC.Get(cityEntry)
	target := domain.Position{Q: targetPos.Q, R: targetPos.R}
	city := domain.Position{Q: cityPos.Q, R: cityPos.R}
	if target.DistanceTo(city) <= radius {
		return ""
	}
	return "outside_territory"
}

// canPlaceAtStatic 只判断“节点本身是否满足放置条件”。
// 城市上下文、城市是否在线、城内建筑半径这些更高层约束由 ValidatePlacement 统一包裹。
func canPlaceAtStatic(state *domain.GameState, entry *donburi.Entry, playerID string, cfg staticdata.BuildingDefinition) string {
	if entry == nil {
		return "invalid_target"
	}
	node := domain.NodeC.Get(entry)
	terrainID := normalizeToken(string(node.Terrain))
	if terrainID != "" {
		if terrain, ok := staticdata.Default().GetTerrain(terrainID); ok && !terrain.Buildable {
			return "terrain_not_buildable"
		}
	}
	switch normalizeToken(cfg.PlacementKind) {
	case "city_territory":
		player := normalizeToken(playerID)
		pos := domain.PositionC.Get(entry)
		if normalizeToken(node.TerritoryOwner) != player && normalizeToken(node.Owner) != player && !domain.IsInSafeZone(state, domain.Position{Q: pos.Q, R: pos.R}, playerID) {
			return "outside_territory"
		}
	case "resource_node":
		// 资源点规则使用新的混合口径：
		// 己方辖区 / 安全区内可直接开发；
		// 前线资源点要求当回合己方单位独占驻守；
		// 多方混战或敌方独占时都不能开发。
		if !node.IsResource {
			return "resource_only_required"
		}
		required := normalizeToken(cfg.RequiredResourceType)
		if required != "" && normalizeToken(node.ResourceType) != required {
			return "resource_type_mismatch"
		}
		if nodeControlledByPlayer(state, entry, playerID) {
			return ""
		}
		controller, contested := ExclusiveController(state, entry)
		if contested || controller != normalizeToken(playerID) {
			return "outside_territory"
		}
	}
	return ""
}

func TerritoryFootprint(state *domain.GameState, centerEntry *donburi.Entry) ([]*donburi.Entry, []string, string) {
	if state == nil || state.World == nil || centerEntry == nil {
		return nil, nil, "invalid_target"
	}
	centerPos := domain.PositionC.Get(centerEntry)
	center := domain.Position{Q: centerPos.Q, R: centerPos.R}
	positions := append([]domain.Position{center}, center.Neighbors()...)
	entries := make([]*donburi.Entry, 0, len(positions))
	ids := make([]string, 0, len(positions))
	for _, pos := range positions {
		entry, ok := domain.GetNodeAt(state.World, pos)
		if !ok {
			return nil, nil, "territory_out_of_bounds"
		}
		entries = append(entries, entry)
		ids = append(ids, domain.NodeC.Get(entry).ID)
	}
	return entries, ids, ""
}

func CanFoundCityAt(state *domain.GameState, centerEntry *donburi.Entry) (bool, string) {
	footprintEntries, _, reason := TerritoryFootprint(state, centerEntry)
	if reason != "" {
		return false, reason
	}
	if centerEntry == nil {
		return false, "invalid_target"
	}
	centerNodeID := strings.TrimSpace(domain.NodeC.Get(centerEntry).ID)
	for _, entry := range footprintEntries {
		if entry == nil {
			continue
		}
		node := domain.NodeC.Get(entry)
		if node.IsResource {
			return false, "territory_blocked"
		}
		if !entry.HasComponent(domain.BuildingC) {
			continue
		}
		building := domain.BuildingC.Get(entry)
		if strings.TrimSpace(node.ID) == centerNodeID && strings.EqualFold(string(building.Type), domain.BuildingScopeCityCore) {
			continue
		}
		return false, "territory_blocked"
	}
	centerPos := domain.PositionC.Get(centerEntry)
	minimumDistance := staticdata.Default().Rules().MinimumCityDistance
	if minimumDistance > 0 {
		nodesWithBuildingQuery.Each(state.World, func(entry *donburi.Entry) {
			if entry == nil || !entry.HasComponent(domain.BuildingC) {
				return
			}
			building := domain.BuildingC.Get(entry)
			if !strings.EqualFold(string(building.Type), domain.BuildingScopeCityCore) {
				return
			}
			node := domain.NodeC.Get(entry)
			if strings.TrimSpace(node.ID) == centerNodeID {
				return
			}
			pos := domain.PositionC.Get(entry)
			if geometry.AxialDistance(domain.Position{Q: centerPos.Q, R: centerPos.R}, domain.Position{Q: pos.Q, R: pos.R}) < minimumDistance {
				reason = "minimum_city_distance"
			}
		})
		if reason != "" {
			return false, reason
		}
	}
	return true, ""
}

func nodeControlledByPlayer(state *domain.GameState, entry *donburi.Entry, playerID string) bool {
	if entry == nil {
		return false
	}
	node := domain.NodeC.Get(entry)
	player := normalizeToken(playerID)
	if normalizeToken(node.Owner) == player || normalizeToken(node.TerritoryOwner) == player {
		return true
	}
	if state != nil {
		pos := domain.PositionC.Get(entry)
		if domain.IsInSafeZone(state, domain.Position{Q: pos.Q, R: pos.R}, playerID) {
			return true
		}
	}
	return false
}

func ExclusiveController(state *domain.GameState, entry *donburi.Entry) (string, bool) {
	if state == nil || state.World == nil || entry == nil {
		return "", false
	}
	pos := domain.PositionC.Get(entry)
	unitsByFaction := domain.UnitsByFactionAtNode(state.World, domain.Position{Q: pos.Q, R: pos.R})
	if len(unitsByFaction) == 0 {
		return "", false
	}
	controller := ""
	for faction := range unitsByFaction {
		current := normalizeToken(faction)
		if controller == "" {
			controller = current
			continue
		}
		if controller != current {
			return "", true
		}
	}
	return controller, false
}

func normalizeToken(value string) string {
	return strings.ToLower(strings.TrimSpace(value))
}
