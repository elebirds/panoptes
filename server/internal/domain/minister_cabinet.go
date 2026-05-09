package domain

import (
	"fmt"
	"hash/fnv"
	"math/rand"
	"strings"

	"github.com/elebirds/panoptes/internal/ministerroles"
	"github.com/elebirds/panoptes/internal/staticdata"
)

func (s *GameState) InitializeMinisterRoster() {
	if s == nil {
		return
	}
	for playerID, playerState := range s.Players {
		if playerState == nil {
			continue
		}
		playerState.ensureMinisterMaps()
		if len(playerState.MinisterRoster) == 0 {
			playerState.MinisterRoster = buildInitialMinisterRoster()
		}
		if len(playerState.MinisterCandidates) == 0 {
			playerState.refreshMinisterCandidates(s.GameID, playerID, s.Turn)
		}
	}
}

func (s *GameState) RefreshMinisterCandidates() {
	if s == nil {
		return
	}
	for playerID, playerState := range s.Players {
		if playerState == nil {
			continue
		}
		playerState.refreshMinisterCandidates(s.GameID, playerID, s.Turn)
	}
}

func (s *GameState) RefreshMinisterCandidatesForPlayer(playerID string) {
	if s == nil {
		return
	}
	playerState := s.Players[playerID]
	if playerState == nil {
		return
	}
	playerState.refreshMinisterCandidates(s.GameID, playerID, s.Turn)
}

func (p *PlayerState) ensureMinisterMaps() {
	if p == nil {
		return
	}
	if p.MinisterRoster == nil {
		p.MinisterRoster = make(map[string]staticdata.Minister)
	}
	if p.MinisterCandidates == nil {
		p.MinisterCandidates = make(map[string]staticdata.Minister)
	}
}

func (p *PlayerState) MinisterForRole(role string) (staticdata.Minister, bool) {
	if p == nil {
		return staticdata.Minister{}, false
	}
	role = ministerroles.Canonical(role)
	if role == "" || p.MinisterRoster == nil {
		return staticdata.Minister{}, false
	}
	minister, ok := p.MinisterRoster[role]
	return minister, ok && strings.TrimSpace(minister.ID) != ""
}

func (p *PlayerState) MinisterCandidateForRole(role string) (staticdata.Minister, bool) {
	if p == nil {
		return staticdata.Minister{}, false
	}
	role = ministerroles.Canonical(role)
	if role == "" || p.MinisterCandidates == nil {
		return staticdata.Minister{}, false
	}
	minister, ok := p.MinisterCandidates[role]
	return minister, ok && strings.TrimSpace(minister.ID) != ""
}

func (p *PlayerState) SetMinisterForRole(role string, minister staticdata.Minister) {
	if p == nil {
		return
	}
	p.ensureMinisterMaps()
	role = ministerroles.Canonical(role)
	if role == "" {
		return
	}
	minister.Role = role
	if strings.TrimSpace(minister.IconKey) == "" {
		minister.IconKey = role
	}
	p.MinisterRoster[role] = minister
}

func (p *PlayerState) ClearMinisterForRole(role string) {
	if p == nil || p.MinisterRoster == nil {
		return
	}
	role = ministerroles.Canonical(role)
	if role == "" {
		return
	}
	delete(p.MinisterRoster, role)
}

func (p *PlayerState) SetMinisterCandidate(role string, minister staticdata.Minister) {
	if p == nil {
		return
	}
	p.ensureMinisterMaps()
	role = ministerroles.Canonical(role)
	if role == "" {
		return
	}
	minister.Role = role
	if strings.TrimSpace(minister.IconKey) == "" {
		minister.IconKey = role
	}
	p.MinisterCandidates[role] = minister
}

func (p *PlayerState) refreshMinisterCandidates(gameID string, playerID string, turn int) {
	if p == nil {
		return
	}
	p.ensureMinisterMaps()
	catalog := staticdata.Default()
	if catalog == nil {
		p.MinisterCandidates = make(map[string]staticdata.Minister)
		return
	}
	p.MinisterCandidateCycle++
	seed := ministerCandidateSeed(gameID, playerID, turn, p.MinisterCandidateCycle)
	rng := rand.New(rand.NewSource(seed))
	next := make(map[string]staticdata.Minister, len(ministerroles.OrderedRoles()))
	for _, role := range ministerroles.OrderedRoles() {
		candidate, ok := buildMinisterCandidateForRole(catalog.Ministers(), p.MinisterRoster[role], role, rng, gameID, playerID, turn, p.MinisterCandidateCycle)
		if !ok {
			continue
		}
		next[role] = candidate
	}
	p.MinisterCandidates = next
}

func buildInitialMinisterRoster() map[string]staticdata.Minister {
	catalog := staticdata.Default()
	if catalog == nil {
		return defaultMinisterRoster()
	}
	ministers := ministerroles.NormalizeMinisters(catalog.Ministers())
	if len(ministers) == 0 {
		return defaultMinisterRoster()
	}
	roster := make(map[string]staticdata.Minister, len(ministerroles.OrderedRoles()))
	for _, minister := range ministers {
		role := ministerroles.Canonical(minister.Role)
		if role == "" {
			continue
		}
		minister.Role = role
		if strings.TrimSpace(minister.IconKey) == "" {
			minister.IconKey = role
		}
		roster[role] = minister
	}
	for _, role := range ministerroles.OrderedRoles() {
		if _, ok := roster[role]; !ok {
			roster[role] = ministerroles.DefaultMinister(role)
		}
	}
	return roster
}

func defaultMinisterRoster() map[string]staticdata.Minister {
	roster := make(map[string]staticdata.Minister, len(ministerroles.OrderedRoles()))
	for _, role := range ministerroles.OrderedRoles() {
		roster[role] = ministerroles.DefaultMinister(role)
	}
	return roster
}

func buildMinisterCandidateForRole(pool []staticdata.Minister, current staticdata.Minister, role string, rng *rand.Rand, gameID string, playerID string, turn int, cycle int) (staticdata.Minister, bool) {
	role = ministerroles.Canonical(role)
	if role == "" || rng == nil {
		return staticdata.Minister{}, false
	}
	choices := make([]staticdata.Minister, 0)
	for _, minister := range pool {
		if ministerroles.Canonical(minister.Role) != role {
			continue
		}
		if strings.TrimSpace(current.ID) != "" && strings.EqualFold(strings.TrimSpace(minister.ID), strings.TrimSpace(current.ID)) {
			continue
		}
		choices = append(choices, minister)
	}
	if len(choices) == 0 {
		choices = append(choices, ministerroles.DefaultMinister(role))
	}
	chosen := choices[rng.Intn(len(choices))]
	minister := mutateMinisterCandidate(chosen, role, rng)
	minister.ID = candidateMinisterID(gameID, playerID, role, turn, cycle, chosen.ID)
	minister.Role = role
	if strings.TrimSpace(current.Name) != "" && strings.EqualFold(strings.TrimSpace(minister.Name), strings.TrimSpace(current.Name)) {
		minister.Name = generatedMinisterCandidateName(minister.ID, role)
	}
	if strings.TrimSpace(minister.IconKey) == "" {
		minister.IconKey = role
	}
	return minister, true
}

func mutateMinisterCandidate(base staticdata.Minister, role string, rng *rand.Rand) staticdata.Minister {
	minister := base
	minister.Role = ministerroles.Canonical(role)
	if strings.TrimSpace(minister.IconKey) == "" {
		minister.IconKey = minister.Role
	}
	minister.Ability = clampInt(minister.Ability+rng.Intn(3)-1, 1, 10)
	minister.Loyalty = clampInt(minister.Loyalty+rng.Intn(5)-2, 0, 10)
	minister.Ambition = clampInt(minister.Ambition+rng.Intn(5)-2, 0, 10)
	minister.Cautiousness = clampInt(minister.Cautiousness+rng.Intn(19)-9, 0, 100)
	minister.Decisiveness = clampInt(minister.Decisiveness+rng.Intn(19)-9, 0, 100)
	minister.LoyaltyTendency = clampInt(minister.LoyaltyTendency+rng.Intn(19)-9, 0, 100)
	minister.AmbitionStyle = clampInt(minister.AmbitionStyle+rng.Intn(19)-9, 0, 100)
	applyMinisterRoleBias(&minister, role)
	return minister
}

func applyMinisterRoleBias(minister *staticdata.Minister, role string) {
	if minister == nil {
		return
	}
	switch ministerroles.Canonical(role) {
	case ministerroles.Domestic:
		minister.Cautiousness = clampInt(minister.Cautiousness+6, 0, 100)
		minister.Loyalty = clampInt(minister.Loyalty+1, 0, 10)
	case ministerroles.Works:
		minister.Ability = clampInt(minister.Ability+1, 1, 10)
		minister.Decisiveness = clampInt(minister.Decisiveness+3, 0, 100)
	case ministerroles.Defense:
		minister.Decisiveness = clampInt(minister.Decisiveness+7, 0, 100)
		minister.Cautiousness = clampInt(minister.Cautiousness-3, 0, 100)
	case ministerroles.Command:
		minister.Decisiveness = clampInt(minister.Decisiveness+9, 0, 100)
		minister.Ambition = clampInt(minister.Ambition+1, 0, 10)
	case ministerroles.Frontier:
		minister.Ambition = clampInt(minister.Ambition+2, 0, 10)
		minister.AmbitionStyle = clampInt(minister.AmbitionStyle+5, 0, 100)
	}
}

func candidateMinisterID(gameID string, playerID string, role string, turn int, cycle int, sourceID string) string {
	cleanSourceID := strings.TrimSpace(sourceID)
	if cleanSourceID == "" {
		cleanSourceID = "generated"
	}
	return fmt.Sprintf("cand:%s:%s:%s:%d:%d:%s", sanitizeMinisterIDPart(gameID), sanitizeMinisterIDPart(playerID), ministerroles.Canonical(role), turn, cycle, sanitizeMinisterIDPart(cleanSourceID))
}

func generatedMinisterCandidateName(candidateID string, role string) string {
	surnames := []string{"顾", "陆", "崔", "裴", "薛", "郑", "卢", "范", "谢", "姚"}
	givenNames := []string{"承远", "景行", "仲明", "怀谨", "子衡", "元修", "敬初", "伯昭", "廷肃", "文澜"}
	h := fnv.New32a()
	_, _ = h.Write([]byte(strings.TrimSpace(candidateID) + "|" + ministerroles.Canonical(role)))
	hash := h.Sum32()
	return surnames[int(hash%uint32(len(surnames)))] + givenNames[int((hash/uint32(len(surnames)))%uint32(len(givenNames)))]
}

func ministerCandidateSeed(gameID string, playerID string, turn int, cycle int) int64 {
	h := fnv.New64a()
	_, _ = h.Write([]byte(strings.Join([]string{
		strings.TrimSpace(gameID),
		strings.TrimSpace(playerID),
		fmt.Sprint(turn),
		fmt.Sprint(cycle),
	}, "|")))
	return int64(h.Sum64())
}

func sanitizeMinisterIDPart(value string) string {
	value = strings.TrimSpace(value)
	if value == "" {
		return "unknown"
	}
	replacer := strings.NewReplacer(" ", "_", ":", "_", ",", "_", "/", "_", "\\", "_")
	return replacer.Replace(value)
}

func clampInt(value int, minValue int, maxValue int) int {
	if value < minValue {
		return minValue
	}
	if value > maxValue {
		return maxValue
	}
	return value
}
