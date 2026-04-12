package domain

import (
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
	Policy       Policy
	TokensLeft   int
	MainCastleHP int
	WarZones     []*WarZone
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
		Phase:               "domestic",
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
