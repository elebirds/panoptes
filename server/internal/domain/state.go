package domain

import (
	"sort"

	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

type GameState struct {
	GameID     string
	Turn       int
	Phase      string
	IsOver     bool
	WinnerID   string
	OverReason string
	Narrative  string
	World      donburi.World
	Map        *MapData
	Players    map[string]*PlayerState
	NodeIndex  map[string]donburi.Entity

	PendingBuilds       []BuildOrder
	MinisterBuildOrders []BuildOrder
	MinisterMoveOrders  []MoveOrder
	PendingCombatOrders map[string]CombatOrder
	PendingMoves        []PendingMove
	PendingConflicts    []Conflict
}

type PlayerState struct {
	PlayerID     string
	Username     string
	Resources    ResourceBag
	Castles      map[string]*CastleState
	Policy       Policy
	TokensLeft   int
	MainCastleHP int
	WarZones     []*WarZone
}

type CastleState struct {
	CastleID  string
	NodeID    string
	OwnerID   string
	Resources ResourceBag
}

type WarZone struct {
	ID        string
	Name      string
	NodeIDs   []string
	Directive string
	Target    string
}

type MapData struct {
	ID           string
	Width        int
	Height       int
	SpawnPoints  map[int]Position
	PlayerSpawns map[string]Position
	NamedNodes   map[string]string
	NodeIndex    map[string]donburi.Entity
}

type BuildOrder struct {
	PlayerID     string
	NodeID       string
	BuildingType string
	CastleID     string
}

type MoveOrder struct {
	PlayerID string
	UnitID   string
	Target   Position
}

type Conflict struct {
	UnitAID      string
	UnitBID      string
	Location     Position
	ConflictType string
	TimeStep     int
	MaxSpeed     int
}

type PendingMove struct {
	UnitID    string
	Faction   string
	Speed     int
	Path      []Position
	Timestamp int
}

func NewGameState(gameID string, playerIDs []string, usernames []string, mapData *MapData) *GameState {
	state := &GameState{
		GameID:              gameID,
		Turn:                1,
		Phase:               PhaseDomesticPlanning.String(),
		World:               donburi.NewWorld(),
		Map:                 mapData,
		Players:             make(map[string]*PlayerState, len(playerIDs)),
		NodeIndex:           make(map[string]donburi.Entity),
		PendingCombatOrders: make(map[string]CombatOrder),
	}

	if mapData != nil && mapData.NodeIndex != nil {
		for nodeID, entity := range mapData.NodeIndex {
			state.NodeIndex[nodeID] = entity
		}
	}

	rules := staticdata.Default().Rules()
	for idx, playerID := range playerIDs {
		username := playerID
		if idx < len(usernames) && usernames[idx] != "" {
			username = usernames[idx]
		}
		state.Players[playerID] = &PlayerState{
			PlayerID: playerID,
			Username: username,
			Resources: func() ResourceBag {
				bag := NewResourceBag()
				bag[ResourceBuildPoints] = rules.BuildPointsPerTurn
				return bag
			}(),
			Castles:      make(map[string]*CastleState),
			TokensLeft:   rules.TokensPerTurn,
			MainCastleHP: rules.CastleBaseHP,
			WarZones:     []*WarZone{},
		}
	}

	return state
}

func (s *GameState) GetNode(nodeID string) (*donburi.Entry, bool) {
	if s == nil || s.World == nil {
		return nil, false
	}
	entity, ok := s.NodeIndex[nodeID]
	if !ok || !s.World.Valid(entity) {
		return nil, false
	}
	return s.World.Entry(entity), true
}

func (s *GameState) EnsureCastleState(playerID string, castleID string) *CastleState {
	if s == nil {
		return nil
	}
	if castleID == "" {
		return nil
	}

	playerState, ok := s.Players[playerID]
	if !ok || playerState == nil {
		return nil
	}

	if playerState.Castles == nil {
		playerState.Castles = make(map[string]*CastleState)
	}

	castle, ok := playerState.Castles[castleID]
	if ok && castle != nil {
		if castle.NodeID == "" {
			castle.NodeID = castleID
		}
		if castle.OwnerID == "" {
			castle.OwnerID = playerID
		}
		if castle.Resources == nil {
			castle.Resources = NewResourceBag()
		}
		return castle
	}

	castle = &CastleState{
		CastleID:  castleID,
		NodeID:    castleID,
		OwnerID:   playerID,
		Resources: NewResourceBag(),
	}
	playerState.Castles[castleID] = castle
	return castle
}

// PrimaryCastleState returns a stable fallback castle for player-scoped
// operations that still need落到某个城堡上。
//
// 当前实现按 castleID 的字典序选择主城堡，目的是在“没有显式 castleID”
// 的旧逻辑里维持可预测行为，避免不同运行时因为 map 遍历顺序不同而把资源
// 加到不同城堡。
func (s *GameState) PrimaryCastleState(playerID string) *CastleState {
	if s == nil {
		return nil
	}
	playerState, ok := s.Players[playerID]
	if !ok || playerState == nil || len(playerState.Castles) == 0 {
		return nil
	}

	ids := make([]string, 0, len(playerState.Castles))
	for castleID := range playerState.Castles {
		if castleID != "" {
			ids = append(ids, castleID)
		}
	}
	if len(ids) == 0 {
		return nil
	}
	sort.Strings(ids)
	return playerState.Castles[ids[0]]
}

// CastleResources returns the resource bag for a specific castle.
//
// 当调用方没有传 castleID 时，这里退回到主城堡资源；如果玩家当前还没有
// 城堡资源结构，则继续兼容旧的 player.Resources。
func (s *GameState) CastleResources(playerID string, castleID string) ResourceBag {
	if s == nil {
		return nil
	}
	playerState, ok := s.Players[playerID]
	if !ok || playerState == nil {
		return nil
	}
	if castleID != "" {
		if castle := s.EnsureCastleState(playerID, castleID); castle != nil {
			return castle.Resources
		}
		return nil
	}
	if castle := s.PrimaryCastleState(playerID); castle != nil {
		return castle.Resources
	}
	return playerState.Resources
}

// TotalCastleResources aggregates all castle resource bags into a single view.
//
// 这层聚合主要服务于两类场景：
// 1. 仍然只认识 player.Resources 的旧协议/旧客户端。
// 2. 没有明确 castleID 的消耗逻辑，需要先判断玩家总池是否足够。
func (s *GameState) TotalCastleResources(playerID string) ResourceBag {
	if s == nil {
		return nil
	}
	playerState, ok := s.Players[playerID]
	if !ok || playerState == nil {
		return nil
	}
	if len(playerState.Castles) == 0 {
		return playerState.Resources.Clone()
	}

	total := NewResourceBag()
	for _, castle := range playerState.Castles {
		if castle == nil || castle.Resources == nil {
			continue
		}
		total = total.Add(castle.Resources)
	}
	return total
}

// SyncPlayerResourcesFromCastles mirrors all castle resources back into
// player.Resources.
//
// 目前 player.Resources 不再是唯一真实来源，而更像“聚合视图”。
// 每次城堡资源发生结算后，都需要同步这里，保证仍依赖 PlayerView.Resources
// 的消息与 UI 不会和城堡看板显示脱节。
func (s *GameState) SyncPlayerResourcesFromCastles(playerID string) {
	if s == nil {
		return
	}
	playerState, ok := s.Players[playerID]
	if !ok || playerState == nil {
		return
	}
	if len(playerState.Castles) == 0 {
		if playerState.Resources == nil {
			playerState.Resources = NewResourceBag()
		}
		return
	}
	playerState.Resources = s.TotalCastleResources(playerID)
}

// CanAffordFromCastle checks affordability against the castle-scoped pool.
//
// 有 castleID 时严格校验指定城堡；没有 castleID 时，退回到玩家所有城堡
// 的聚合资源池，用于兼容道路、战斗补给等尚未绑定具体城堡的行为。
func (s *GameState) CanAffordFromCastle(playerID string, castleID string, cost ResourceBag) bool {
	if cost == nil || cost.IsZero() {
		return true
	}
	if castleID != "" {
		resources := s.CastleResources(playerID, castleID)
		return resources != nil && resources.CanAfford(cost)
	}
	total := s.TotalCastleResources(playerID)
	return total != nil && total.CanAfford(cost)
}

// ConsumeResources subtracts resources from the castle-scoped model.
//
// 规则如下：
// 1. 有 castleID 时，只从该城堡扣费。
// 2. 没有 castleID 时，先校验玩家总城堡资源是否足够，再按稳定顺序从多个城堡
//    分摊扣除，避免 nondeterministic 的 map 遍历影响结果。
// 3. 每次扣费完成后，同步刷新 player.Resources 聚合视图。
func (s *GameState) ConsumeResources(playerID string, castleID string, cost ResourceBag) bool {
	if s == nil || cost == nil || cost.IsZero() {
		return true
	}
	playerState, ok := s.Players[playerID]
	if !ok || playerState == nil {
		return false
	}

	if castleID != "" {
		castle := s.EnsureCastleState(playerID, castleID)
		if castle == nil || !castle.Resources.CanAfford(cost) {
			return false
		}
		castle.Resources = castle.Resources.Sub(cost)
		s.SyncPlayerResourcesFromCastles(playerID)
		return true
	}

	total := s.TotalCastleResources(playerID)
	if total == nil || !total.CanAfford(cost) {
		return false
	}
	if len(playerState.Castles) == 0 {
		playerState.Resources = playerState.Resources.Sub(cost)
		return true
	}

	ids := make([]string, 0, len(playerState.Castles))
	for id := range playerState.Castles {
		if id != "" {
			ids = append(ids, id)
		}
	}
	sort.Strings(ids)
	for _, key := range cost.Keys() {
		remaining := cost.Get(key)
		for _, id := range ids {
			if remaining <= 0 {
				break
			}
			castle := playerState.Castles[id]
			if castle == nil || castle.Resources == nil {
				continue
			}
			available := castle.Resources.Get(key)
			if available <= 0 {
				continue
			}
			consume := available
			if consume > remaining {
				consume = remaining
			}
			castle.Resources.AddAmount(key, -consume)
			remaining -= consume
		}
	}
	s.SyncPlayerResourcesFromCastles(playerID)
	return true
}

// AddResourceToCastle adds delta to a castle resource pool and keeps the
// player-level aggregate in sync.
//
// 没有显式 castleID 时，这里优先回落到主城堡，用来承接尚未完成“明确归属”
// 改造的产出逻辑；如果玩家甚至还没有城堡结构，则继续兼容旧的 player.Resources。
func (s *GameState) AddResourceToCastle(playerID string, castleID string, key ResourceKey, delta int) {
	if s == nil || delta == 0 {
		return
	}
	playerState, ok := s.Players[playerID]
	if !ok || playerState == nil {
		return
	}
	if castleID != "" {
		castle := s.EnsureCastleState(playerID, castleID)
		if castle == nil {
			return
		}
		castle.Resources.AddAmount(key, delta)
		s.SyncPlayerResourcesFromCastles(playerID)
		return
	}
	if castle := s.PrimaryCastleState(playerID); castle != nil {
		castle.Resources.AddAmount(key, delta)
		s.SyncPlayerResourcesFromCastles(playerID)
		return
	}
	playerState.Resources.AddAmount(key, delta)
}

// RechargeBuildPoints replenishes build points per castle instead of once per
// player.
//
// 这样客户端城堡资源看板里的 build_points 才能反映“每座城堡自己的建造点”，
// 而不是玩家共享的一份总值。
func (s *GameState) RechargeBuildPoints(playerID string, amount int, maxVal int) {
	if s == nil || amount == 0 {
		return
	}
	playerState, ok := s.Players[playerID]
	if !ok || playerState == nil {
		return
	}
	if len(playerState.Castles) == 0 {
		next := playerState.Resources.Get(ResourceBuildPoints) + amount
		if next > maxVal {
			next = maxVal
		}
		playerState.Resources.Set(ResourceBuildPoints, next)
		return
	}
	for _, castle := range playerState.Castles {
		if castle == nil {
			continue
		}
		next := castle.Resources.Get(ResourceBuildPoints) + amount
		if next > maxVal {
			next = maxVal
		}
		castle.Resources.Set(ResourceBuildPoints, next)
	}
	s.SyncPlayerResourcesFromCastles(playerID)
}
