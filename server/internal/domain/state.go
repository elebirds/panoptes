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

type TurnRuntime struct {
	Planning  PlanningInputs
	Resolving ResolvingState
}

type PlanningInputs struct {
	BuildOrders        []BuildOrder
	RecipeSelections   []RecipeSelectionOrder
	MinisterBuilds     []BuildOrder
	MinisterMoves      []MoveOrder
	MinisterDirectives map[string]string
	PendingPolicies    map[string]Policy
	PendingResearch    map[string]string
	WarDirectives      map[string][]WarZoneDirective
	UnitOrders         map[string]UnitDirective
}

type ResolvingState struct {
	UnitOrders    map[string]UnitResolutionOrder
	ActiveMarches map[string]ActiveMarch
	PendingMoves  []PendingMove
	Conflicts     []Conflict
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
	Research          ResearchState
	Policy            Policy
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
	UnlockedTechnologies      map[string]struct{}
	UnlockedBuildings         map[string]struct{}
	UnlockedRecipes           map[string]struct{}
}

func NewResearchState(starting int, income int, cap int) ResearchState {
	return ResearchState{
		CurrentTargetTechnologyID: "",
		CurrentProgress:           starting,
		OutputPerTurn:             income,
		ProgressCap:               cap,
		UnlockedTechnologies:      make(map[string]struct{}),
		UnlockedBuildings:         make(map[string]struct{}),
		UnlockedRecipes:           make(map[string]struct{}),
	}
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

// technologyEffects 只读取“已正式解锁”的科技效果。
//
// 科技研究在当前语义下是回合末完成、下一回合生效，因此这里不再暴露任何本回合
// 尚未 Apply 的临时解锁状态。
func (s *GameState) modifierEffects(playerID string) []staticdata.ModifierEffect {
	if s == nil {
		return nil
	}
	playerState, ok := s.Players[playerID]
	if !ok || playerState == nil {
		return nil
	}
	technologyIDs := make(map[string]struct{}, len(playerState.Research.UnlockedTechnologies))
	for technologyID := range playerState.Research.UnlockedTechnologies {
		technologyIDs[technologyID] = struct{}{}
	}
	effects := make([]staticdata.ModifierEffect, 0)
	for technologyID := range technologyIDs {
		technology, ok := staticdata.Default().GetTechnology(technologyID)
		if !ok {
			continue
		}
		effects = append(effects, technology.ModifierEffects...)
	}
	if playerState.Policy != "" {
		if policy, ok := staticdata.Default().GetPolicy(string(playerState.Policy)); ok {
			effects = append(effects, policy.ModifierEffects...)
		}
	}
	return effects
}

// ApplyFloatModifier 实现统一的 flat -> percent -> multiplier 聚合顺序。
//
// 所有 trigger/key 型修正都通过这里读时计算，而不是把最终值预写回 ECS 或静态表。
func (s *GameState) ApplyFloatModifier(playerID string, trigger string, targetID string, modifierKey string, base float64) float64 {
	value := base
	flat := 0.0
	percent := 0.0
	multiplier := 1.0
	for _, effect := range s.modifierEffects(playerID) {
		if effect.Trigger != trigger {
			continue
		}
		if effect.TargetID != "" && effect.TargetID != targetID {
			continue
		}
		if effect.ResourceKey != "" && effect.ResourceKey != modifierKey {
			continue
		}
		if effect.PointKey != "" && effect.PointKey != modifierKey {
			continue
		}
		switch effect.ModifierType {
		case "flat":
			flat += effect.Value
		case "percent":
			percent += effect.Value
		case "multiplier":
			multiplier *= effect.Value
		}
	}
	value = (value + flat) * (1 + percent) * multiplier
	if value < 0 {
		value = 0
	}
	return value
}

func (s *GameState) ApplyScalarModifier(playerID string, trigger string, targetID string, resourceKey string, base int) int {
	return int(math.Round(s.ApplyFloatModifier(playerID, trigger, targetID, resourceKey, float64(base))))
}

func (s *GameState) ApplyResourceModifiers(playerID string, trigger string, targetID string, base ResourceBag) ResourceBag {
	if base == nil {
		return nil
	}
	out := NewResourceBag()
	for _, key := range base.Keys() {
		out.Set(key, s.ApplyScalarModifier(playerID, trigger, targetID, string(key), base.Get(key)))
	}
	return out
}

// 研究推进数值也走同一套 modifier 入口，避免研究系统和展示层再各自复制一份
// “研究产出增益”逻辑。
func (s *GameState) EffectiveResearchOutput(playerID string) int {
	if s == nil {
		return 0
	}
	playerState, ok := s.Players[playerID]
	if !ok || playerState == nil {
		return 0
	}
	base := playerState.Research.OutputPerTurn
	if base <= 0 {
		base = staticdata.Default().Rules().BaseResearchOutputPerTurn
	}
	return s.ApplyScalarModifier(playerID, string(staticdata.ModifierTriggerPointOutput), "", "research_output", base)
}

func (s *GameState) EffectiveIndustryOutput(playerID string) int {
	if s == nil {
		return 0
	}
	if _, ok := s.Players[playerID]; !ok {
		return 0
	}
	return s.ApplyScalarModifier(playerID, string(staticdata.ModifierTriggerPointOutput), "", "industry_output", staticdata.Default().Rules().BaseIndustryOutputPerTurn)
}

func (s *GameState) EffectiveResearchCap(playerID string) int {
	if s == nil {
		return 0
	}
	playerState, ok := s.Players[playerID]
	if !ok || playerState == nil {
		return 0
	}
	if playerState.Research.ProgressCap > 0 {
		return playerState.Research.ProgressCap
	}
	return math.MaxInt / 4
}

func (s *GameState) HasTechnologyUnlocked(playerID string, technologyID string) bool {
	if s == nil || technologyID == "" {
		return false
	}
	playerState, ok := s.Players[playerID]
	if !ok || playerState == nil {
		return false
	}
	return playerState.Research.HasTechnology(technologyID)
}

func (s *GameState) IsBuildingUnlocked(playerID string, buildingID string) bool {
	if s == nil || buildingID == "" {
		return false
	}
	playerState, ok := s.Players[playerID]
	if !ok || playerState == nil {
		return false
	}
	return playerState.Research.HasBuilding(buildingID)
}

func (s *GameState) IsRecipeUnlocked(playerID string, recipeID string) bool {
	if s == nil || recipeID == "" {
		return false
	}
	playerState, ok := s.Players[playerID]
	if !ok || playerState == nil {
		return false
	}
	return playerState.Research.HasRecipe(recipeID)
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
		GameID:    gameID,
		Turn:      1,
		Phase:     PhasePlanning.String(),
		World:     donburi.NewWorld(),
		Map:       mapData,
		Players:   make(map[string]*PlayerState, len(playerIDs)),
		NodeIndex: make(map[string]donburi.Entity),
		TurnRuntime: TurnRuntime{
			Planning: PlanningInputs{
				MinisterDirectives: make(map[string]string),
				PendingPolicies:    make(map[string]Policy),
				PendingResearch:    make(map[string]string),
				WarDirectives:      make(map[string][]WarZoneDirective),
				UnitOrders:         make(map[string]UnitDirective),
			},
			Resolving: ResolvingState{
				UnitOrders:    make(map[string]UnitResolutionOrder),
				ActiveMarches: make(map[string]ActiveMarch),
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
			PlayerID: playerID,
			Username: username,
			Resources: func() ResourceBag {
				bag := NewResourceBag()
				bag[ResourceIndustryOutput] = rules.BaseIndustryOutputPerTurn
				return bag
			}(),
			Cities:            make(map[string]*CityState),
			Research:          NewResearchState(0, rules.BaseResearchOutputPerTurn, math.MaxInt/4),
			TokensLeft:        rules.TokensPerTurn,
			CapitalCityCoreHP: rules.CityCoreMaxHP,
			WarZones:          []*WarZone{},
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
