package debug

import (
	"fmt"
	"log/slog"
	"sort"
	"strings"

	"github.com/elebirds/panoptes/internal/building"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/yohamta/donburi"
)

type StateSummary struct {
	Turn       int                        `json:"turn"`
	Phase      string                     `json:"phase"`
	IsOver     bool                       `json:"is_over"`
	WinnerID   string                     `json:"winner_id,omitempty"`
	OverReason string                     `json:"over_reason,omitempty"`
	Players    map[string]PlayerSummary   `json:"players"`
	Buildings  map[string]BuildingSummary `json:"buildings"`
	Units      map[string]UnitSummary     `json:"units"`
}

type PlayerSummary struct {
	Username                           string         `json:"username"`
	Resources                          map[string]int `json:"resources"`
	TokensLeft                         int            `json:"tokens_left"`
	CapitalCityCoreHP                  int            `json:"capital_city_core_hp"`
	CurrentResearchProgress            int            `json:"current_research_progress"`
	CurrentTargetTechnologyID          string         `json:"current_target_technology_id,omitempty"`
	CompletedTechnologyIDs             []string       `json:"completed_technology_ids"`
	ActiveTechnologyIDs                []string       `json:"active_technology_ids"`
	PendingActivationTechnologyIDs     []string       `json:"pending_activation_technology_ids"`
	SavedResearchProgress              map[string]int `json:"saved_research_progress"`
	UnlockedBuildingIDs                []string       `json:"unlocked_building_ids"`
	UnlockedRecipeIDs                  []string       `json:"unlocked_recipe_ids"`
	InstitutionSlotCount               int            `json:"institution_slot_count"`
	InstitutionCandidateInstitutionIDs []string       `json:"institution_candidate_institution_ids"`
	InstitutionActiveInstitutionIDs    []string       `json:"institution_active_institution_ids"`
	InstitutionPendingInstitutionIDs   []string       `json:"institution_pending_institution_ids"`
	InstitutionPendingActivationTurn   int            `json:"institution_pending_activation_turn"`
}

type BuildingSummary struct {
	NodeID         string `json:"node_id"`
	Type           string `json:"type"`
	Owner          string `json:"owner"`
	CityID         string `json:"city_id,omitempty"`
	HP             int    `json:"hp"`
	MaxHP          int    `json:"max_hp"`
	Disabled       bool   `json:"disabled"`
	DisabledReason string `json:"disabled_reason,omitempty"`
}

type UnitSummary struct {
	UnitID string `json:"unit_id"`
	Type   string `json:"type"`
	Owner  string `json:"owner"`
	NodeID string `json:"node_id,omitempty"`
	HP     int    `json:"hp"`
	MaxHP  int    `json:"max_hp"`
}

func BuildStateSummary(state *domain.GameState) StateSummary {
	summary := StateSummary{
		Players:   make(map[string]PlayerSummary),
		Buildings: make(map[string]BuildingSummary),
		Units:     make(map[string]UnitSummary),
	}
	if state == nil {
		return summary
	}

	summary.Turn = state.Turn
	summary.Phase = state.Phase
	summary.IsOver = state.IsOver
	summary.WinnerID = state.WinnerID
	summary.OverReason = state.OverReason

	playerIDs := make([]string, 0, len(state.Players))
	for playerID := range state.Players {
		playerIDs = append(playerIDs, playerID)
	}
	sort.Strings(playerIDs)
	for _, playerID := range playerIDs {
		player := state.Players[playerID]
		if player == nil {
			continue
		}
		summary.Players[playerID] = PlayerSummary{
			Username:                           player.Username,
			Resources:                          resourceMap(player.Resources),
			TokensLeft:                         player.TokensLeft,
			CapitalCityCoreHP:                  player.CapitalCityCoreHP,
			CurrentResearchProgress:            player.Research.CurrentTargetProgress(),
			CurrentTargetTechnologyID:          player.Research.CurrentTargetTechnologyID,
			CompletedTechnologyIDs:             player.Research.CompletedTechnologyIDs(),
			ActiveTechnologyIDs:                player.Research.ActiveTechnologyIDs(),
			PendingActivationTechnologyIDs:     player.Research.PendingActivationTechnologyIDs(),
			SavedResearchProgress:              savedResearchProgress(player.Research),
			UnlockedBuildingIDs:                sortedSetKeys(player.Research.UnlockedBuildings),
			UnlockedRecipeIDs:                  sortedSetKeys(player.Research.UnlockedRecipes),
			InstitutionSlotCount:               player.Institutions.SlotCount,
			InstitutionCandidateInstitutionIDs: player.Institutions.CandidateIDs(),
			InstitutionActiveInstitutionIDs:    append([]string(nil), player.Institutions.ActiveInstitutionIDs...),
			InstitutionPendingInstitutionIDs:   append([]string(nil), player.Institutions.PendingInstitutionIDs...),
			InstitutionPendingActivationTurn:   player.Institutions.PendingActivationTurn,
		}
	}

	if state.World == nil {
		return summary
	}

	ecs.NodesWithBuilding(state.World).Each(state.World, func(entry *donburi.Entry) {
		if entry == nil {
			return
		}
		node := ecs.NodeC.Get(entry)
		buildingComp := ecs.BuildingC.Get(entry)
		summary.Buildings[node.ID] = BuildingSummary{
			NodeID: node.ID,
			Type:   string(buildingComp.Type),
			Owner:  buildingComp.Owner,
			CityID: building.ResolveCityID(entry),
			HP:     buildingComp.HP,
			MaxHP:  buildingComp.MaxHP,
		}
		status, reason := domain.BuildingLifecycleStateAtTurn(entry, state.Turn)
		current := summary.Buildings[node.ID]
		current.Disabled = buildingSummaryDisabled(status)
		if current.Disabled {
			current.DisabledReason = reason
		} else {
			current.DisabledReason = ""
		}
		summary.Buildings[node.ID] = current
	})

	ecs.AllUnits(state.World).Each(state.World, func(entry *donburi.Entry) {
		if entry == nil {
			return
		}
		stats := ecs.UnitStatsC.Get(entry)
		pos := ecs.PositionC.Get(entry)
		summary.Units[stats.ID] = UnitSummary{
			UnitID: stats.ID,
			Type:   string(stats.Type),
			Owner:  stats.Faction,
			NodeID: nodeIDAt(state.World, domain.Position{Q: pos.Q, R: pos.R}),
			HP:     stats.HP,
			MaxHP:  stats.MaxHP,
		}
	})

	return summary
}

func buildingSummaryDisabled(status string) bool {
	switch status {
	case domain.BuildingStatusDisabled,
		domain.BuildingStatusContested,
		domain.BuildingStatusTakeover,
		domain.BuildingStatusRuined:
		return true
	default:
		return false
	}
}

func DumpGameStateSummary(state *domain.GameState) {
	summary := BuildStateSummary(state)
	if len(summary.Players) == 0 && len(summary.Buildings) == 0 && len(summary.Units) == 0 && summary.Turn == 0 && summary.Phase == "" {
		return
	}
	slog.Debug("游戏状态摘要",
		"turn", summary.Turn,
		"phase", summary.Phase,
		"is_over", summary.IsOver,
		"winner_id", summary.WinnerID,
		"players", formatPlayerSummary(summary.Players),
		"building_count", len(summary.Buildings),
		"unit_count", len(summary.Units),
	)
}

func formatPlayerSummary(players map[string]PlayerSummary) string {
	if len(players) == 0 {
		return ""
	}

	ids := make([]string, 0, len(players))
	for playerID := range players {
		ids = append(ids, playerID)
	}
	sort.Strings(ids)

	parts := make([]string, 0, len(ids))
	for _, playerID := range ids {
		player := players[playerID]
		parts = append(parts, fmt.Sprintf(
			"%s: resources={%s} tokens=%d city_core_hp=%d research_progress=%d target=%s active_techs=%v institutions=%v",
			playerID,
			formatResources(player.Resources),
			player.TokensLeft,
			player.CapitalCityCoreHP,
			player.CurrentResearchProgress,
			player.CurrentTargetTechnologyID,
			player.ActiveTechnologyIDs,
			player.InstitutionActiveInstitutionIDs,
		))
	}
	return strings.Join(parts, " | ")
}

func formatResources(resources map[string]int) string {
	if len(resources) == 0 {
		return ""
	}
	keys := make([]string, 0, len(resources))
	for key := range resources {
		keys = append(keys, key)
	}
	sort.Strings(keys)
	parts := make([]string, 0, len(keys))
	for _, key := range keys {
		parts = append(parts, fmt.Sprintf("%s:%d", key, resources[key]))
	}
	return strings.Join(parts, " ")
}

func sortedSetKeys[K ~string](set map[K]struct{}) []string {
	if len(set) == 0 {
		return nil
	}
	keys := make([]string, 0, len(set))
	for key := range set {
		keys = append(keys, string(key))
	}
	sort.Strings(keys)
	return keys
}

func resourceMap(bag domain.ResourceBag) map[string]int {
	if len(bag) == 0 {
		return map[string]int{}
	}
	out := make(map[string]int, len(bag))
	for key, amount := range bag {
		out[string(key)] = amount
	}
	return out
}

func savedResearchProgress(research domain.ResearchState) map[string]int {
	ids := research.StoredProgressTechnologyIDs()
	if len(ids) == 0 {
		return map[string]int{}
	}
	out := make(map[string]int, len(ids))
	for _, technologyID := range ids {
		out[technologyID] = research.ProgressForTechnology(technologyID)
	}
	return out
}

func nodeIDAt(world donburi.World, pos domain.Position) string {
	if world == nil {
		return ""
	}
	entry, ok := domain.GetNodeAt(world, pos)
	if !ok || entry == nil {
		return ""
	}
	return ecs.NodeC.Get(entry).ID
}
