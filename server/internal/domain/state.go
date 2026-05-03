// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 定义领域模型的状态模型。

package domain

import (
	"math"

	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

type GameState struct {
	Meta        GameMeta
	Clock       TurnClock
	Outcome     GameOutcome
	WorldState  WorldState
	PlayerStore PlayerStore
	Runtime     RuntimeState

	GameID      string
	Turn        int
	Phase       string
	IsOver      bool
	WinnerID    string
	OverReason  string
	Narrative   string
	World       donburi.World
	Map         *MapData
	Players     map[string]*PlayerState
	NodeIndex   map[string]donburi.Entity
	TurnRuntime TurnRuntime
}

// TurnRuntime holds server-authoritative runtime inputs and caches that should
// not be confused with durable world/player truth on GameState.
type TurnRuntime struct {
	Planning  PlanningInputs
	Resolving ResolvingState
}

// PlanningInputs stores current-turn drafts accepted during planning. These
// inputs become durable state only after a resolving lock-in event or stage.
type PlanningInputs struct {
	BuildOrders         []BuildOrder
	RecipeSelections    []RecipeSelectionOrder
	MinisterBuilds      []BuildOrder
	MinisterMoves       []MoveOrder
	MinisterDrafts      map[string][]MinisterDraft
	MinisterDirectives  map[string]string
	PendingPolicies     map[string]Policy
	PendingResearch     map[string]string
	PendingInstitutions map[string][]string
	WarDirectives       map[string][]WarZoneDirective
	UnitOrders          map[string]UnitDirective
}

// ResolvingState stores frozen inputs and per-resolving caches. ActiveMarches
// intentionally spans turns as a command cache; unit position remains ECS truth.
type ResolvingState struct {
	UnitOrders    map[string]UnitResolutionOrder
	ActiveMarches map[string]ActiveMarch
	PointBudgets  map[string]PointBag
}

type WarZoneDirective struct {
	ZoneID     string
	Directive  string
	TargetNode string
}

type UnitDirective struct {
	PlayerID        string
	UnitID          string
	Action          string
	TargetNodeID    string
	TargetUnitID    string
	SecondaryNodeID string
	Params          map[string]string
	PathNodeIDs     []string
}

type PlayerState struct {
	PlayerID          string
	Username          string
	Resources         ResourceBag
	Cities            map[string]*CityState
	CapitalCityID     string
	Research          ResearchState
	Policy            Policy
	Institutions      InstitutionState
	TokensLeft        int
	CapitalCityCoreHP int
	WarZones          []*WarZone
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
	CityID       string
}

type RecipeSelectionOrder struct {
	PlayerID string
	NodeID   string
	RecipeID string
}

type ResearchState struct {
	CurrentTargetTechnologyID string
	CurrentProgress           int
	OutputPerTurn             int
	ProgressCap               int
	ProgressByTechnology      map[string]int
	CompletedTechnologyTurns  map[string]int
	ActiveTechnologyTurns     map[string]int
	UnlockedTechnologies      map[string]struct{}
	UnlockedBuildings         map[string]struct{}
	UnlockedRecipes           map[string]struct{}
	UnlockedPolicyCandidates  map[string]struct{}
}

func NewResearchState(starting int, income int, cap int) ResearchState {
	state := ResearchState{
		CurrentTargetTechnologyID: "",
		CurrentProgress:           starting,
		OutputPerTurn:             income,
		ProgressCap:               cap,
		ProgressByTechnology:      make(map[string]int),
		CompletedTechnologyTurns:  make(map[string]int),
		ActiveTechnologyTurns:     make(map[string]int),
		UnlockedTechnologies:      make(map[string]struct{}),
		UnlockedBuildings:         make(map[string]struct{}),
		UnlockedRecipes:           make(map[string]struct{}),
		UnlockedPolicyCandidates:  make(map[string]struct{}),
	}
	if starting > 0 {
		state.ProgressByTechnology[""] = starting
	}
	return state
}

func (r *ResearchState) HasTechnology(id string) bool {
	if r == nil {
		return false
	}
	_, ok := r.UnlockedTechnologies[id]
	return ok
}

func (r *ResearchState) HasBuilding(id string) bool {
	if r == nil {
		return false
	}
	_, ok := r.UnlockedBuildings[id]
	return ok
}

func (r *ResearchState) HasRecipe(id string) bool {
	if r == nil {
		return false
	}
	_, ok := r.UnlockedRecipes[id]
	return ok
}

func (r *ResearchState) UnlockTechnology(id string) {
	if r == nil || id == "" {
		return
	}
	r.EnsureProgressMaps()
	r.CompletedTechnologyTurns[id] = 0
	r.ActiveTechnologyTurns[id] = 0
	r.UnlockedTechnologies[id] = struct{}{}
}

func (r *ResearchState) UnlockBuilding(id string) {
	if r == nil || id == "" {
		return
	}
	r.UnlockedBuildings[id] = struct{}{}
}

func (r *ResearchState) UnlockRecipe(id string) {
	if r == nil || id == "" {
		return
	}
	r.UnlockedRecipes[id] = struct{}{}
}

func (r *ResearchState) UnlockPolicyCandidate(id string) {
	if r == nil || id == "" {
		return
	}
	r.UnlockedPolicyCandidates[id] = struct{}{}
}

type InstitutionState struct {
	SlotCount             int
	CandidatePolicyIDs    map[string]struct{}
	ActivePolicyIDs       []string
	PendingPolicyIDs      []string
	PendingActivationTurn int
}

func NewInstitutionState() InstitutionState {
	return InstitutionState{
		CandidatePolicyIDs: make(map[string]struct{}),
		ActivePolicyIDs:    []string{},
		PendingPolicyIDs:   []string{},
	}
}

type MoveOrder struct {
	PlayerID string
	UnitID   string
	Target   Position
}

func NewGameState(gameID string, playerIDs []string, usernames []string, mapData *MapData) *GameState {
	state := &GameState{
		GameID:    gameID,
		Turn:      1,
		Phase:     PhasePlanning.String(),
		World:     donburi.NewWorld(),
		Map:       mapData,
		Players:   make(map[string]*PlayerState, len(playerIDs)),
		NodeIndex: make(map[string]donburi.Entity),
		TurnRuntime: TurnRuntime{
			Planning: PlanningInputs{
				MinisterDrafts:      make(map[string][]MinisterDraft),
				MinisterDirectives:  make(map[string]string),
				PendingPolicies:     make(map[string]Policy),
				PendingResearch:     make(map[string]string),
				PendingInstitutions: make(map[string][]string),
				WarDirectives:       make(map[string][]WarZoneDirective),
				UnitOrders:          make(map[string]UnitDirective),
			},
			Resolving: ResolvingState{
				UnitOrders:    make(map[string]UnitResolutionOrder),
				ActiveMarches: make(map[string]ActiveMarch),
				PointBudgets:  make(map[string]PointBag),
			},
		},
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
			PlayerID:          playerID,
			Username:          username,
			Resources:         NewResourceBag(),
			Cities:            make(map[string]*CityState),
			Research:          NewResearchState(0, rules.BaseResearchOutputPerTurn, math.MaxInt/4),
			Institutions:      NewInstitutionState(),
			TokensLeft:        rules.TokensPerTurn,
			CapitalCityCoreHP: rules.CityCoreMaxHP,
			WarZones:          []*WarZone{},
		}
		state.TurnRuntime.Resolving.PointBudgets[playerID] = NewPointBag()
	}

	state.RefreshStructuredModel()
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
