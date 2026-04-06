package domain

import (
	"github.com/elebirds/panoptes/internal/config"
	"github.com/yohamta/donburi"
)

type GameState struct {
	GameID    string
	Turn      int
	Phase     string
	World     donburi.World
	Map       *MapData
	Players   map[string]*PlayerState
	NodeIndex map[string]donburi.Entity
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
	ID          string
	Width       int
	Height      int
	SpawnPoints map[int]Position
	NamedNodes  map[string]string
	NodeIndex   map[string]donburi.Entity
}

func NewGameState(gameID string, playerIDs []string, usernames []string, mapData *MapData) *GameState {
	state := &GameState{
		GameID:    gameID,
		Turn:      1,
		Phase:     "domestic",
		World:     donburi.NewWorld(),
		Map:       mapData,
		Players:   make(map[string]*PlayerState, len(playerIDs)),
		NodeIndex: make(map[string]donburi.Entity),
	}

	if mapData != nil && mapData.NodeIndex != nil {
		for nodeID, entity := range mapData.NodeIndex {
			state.NodeIndex[nodeID] = entity
		}
	}

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
				bag[ResourceBuildPoints] = config.Data.Rules.BuildPointsPerTurn
				return bag
			}(),
			TokensLeft:   config.Data.Rules.TokensPerTurn,
			MainCastleHP: config.Data.Rules.CastleBaseHP,
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
