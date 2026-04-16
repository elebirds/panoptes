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
	BuildOrders         []BuildOrder
	RecipeSelections    []RecipeSelectionOrder
	MinisterBuilds      []BuildOrder
	MinisterMoves       []MoveOrder
	MinisterDirectives  map[string]string
	PendingPolicies     map[string]Policy
	PendingResearch     map[string]string
	PendingInstitutions map[string][]string
	WarDirectives       map[string][]WarZoneDirective
	UnitOrders          map[string]UnitDirective
}

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

// ActiveModifierEffects 只读取“已正式解锁”的科技/政策修正。
//
// 科技研究在当前语义下是回合末完成、下一回合生效，因此这里不暴露任何本回合
// 尚未 Apply 的临时解锁状态。
func (s *GameState) ActiveModifierEffects(playerID string) []staticdata.ModifierEffect {
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
	for _, policyID := range playerState.Institutions.ActivePolicyIDs {
		if policy, ok := staticdata.Default().GetPolicy(policyID); ok {
			effects = append(effects, policy.ModifierEffects...)
		}
	}
	if s.World != nil {
		nodeQuery.Each(s.World, func(entry *donburi.Entry) {
			if entry == nil || !entry.HasComponent(BuildingC) {
				return
			}
			building := BuildingC.Get(entry)
			if building.Owner != playerID {
				return
			}
			if !BuildingOperationalAtTurn(entry, s.Turn) {
				return
			}
			cfg, ok := staticdata.Default().GetBuilding(string(building.Type))
			if !ok || len(cfg.ModifierEffects) == 0 {
				return
			}
			effects = append(effects, cfg.ModifierEffects...)
		})
	}
	return effects
}

// ApplyFloatModifier 实现统一的 percent -> flat -> multiplier 聚合顺序。
//
// 所有 trigger/key 型修正都通过这里读时计算，而不是把最终值预写回 ECS 或静态表。
func (s *GameState) ApplyFloatModifier(playerID string, trigger string, targetID string, modifierKey string, base float64) float64 {
	flat := 0.0
	percent := 0.0
	multiplier := 1.0
	for _, effect := range s.ActiveModifierEffects(playerID) {
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
	value := ((base * (1 + percent)) + flat) * multiplier
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

func (s *GameState) ApplyPointModifiers(playerID string, trigger string, targetID string, base PointBag) PointBag {
	if base == nil {
		return nil
	}
	out := NewPointBag()
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

func (s *GameState) IsPolicyActive(playerID string, policyID string) bool {
	if s == nil || policyID == "" {
		return false
	}
	playerState, ok := s.Players[playerID]
	if !ok || playerState == nil {
		return false
	}
	if string(playerState.Policy) == policyID {
		return true
	}
	for _, activeID := range playerState.Institutions.ActivePolicyIDs {
		if activeID == policyID {
			return true
		}
	}
	return false
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
