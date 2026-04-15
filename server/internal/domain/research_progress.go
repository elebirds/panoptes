package domain

import "sort"

func (r *ResearchState) EnsureProgressMaps() {
	if r == nil {
		return
	}
	if r.ProgressByTechnology == nil {
		r.ProgressByTechnology = make(map[string]int)
	}
	if r.CompletedTechnologyTurns == nil {
		r.CompletedTechnologyTurns = make(map[string]int)
	}
	if r.ActiveTechnologyTurns == nil {
		r.ActiveTechnologyTurns = make(map[string]int)
	}
	if r.UnlockedTechnologies == nil {
		r.UnlockedTechnologies = make(map[string]struct{})
	}
	if r.UnlockedBuildings == nil {
		r.UnlockedBuildings = make(map[string]struct{})
	}
	if r.UnlockedRecipes == nil {
		r.UnlockedRecipes = make(map[string]struct{})
	}
	if r.UnlockedPolicyCandidates == nil {
		r.UnlockedPolicyCandidates = make(map[string]struct{})
	}
}

func (r *ResearchState) ProgressForTechnology(technologyID string) int {
	if r == nil || technologyID == "" {
		return 0
	}
	r.EnsureProgressMaps()
	if amount, ok := r.ProgressByTechnology[technologyID]; ok {
		return amount
	}
	if technologyID == r.CurrentTargetTechnologyID {
		return r.CurrentProgress
	}
	return 0
}

func (r *ResearchState) SetProgress(technologyID string, amount int) {
	if r == nil || technologyID == "" {
		return
	}
	r.EnsureProgressMaps()
	if amount < 0 {
		amount = 0
	}
	r.ProgressByTechnology[technologyID] = amount
	if technologyID == r.CurrentTargetTechnologyID {
		r.CurrentProgress = amount
	}
}

func (r *ResearchState) CurrentTargetProgress() int {
	if r == nil {
		return 0
	}
	return r.ProgressForTechnology(r.CurrentTargetTechnologyID)
}

func (r *ResearchState) SetCurrentTarget(technologyID string) {
	if r == nil {
		return
	}
	r.EnsureProgressMaps()
	previousTarget := r.CurrentTargetTechnologyID
	if previousTarget != "" {
		if _, ok := r.ProgressByTechnology[previousTarget]; !ok && r.CurrentProgress > 0 {
			r.ProgressByTechnology[previousTarget] = r.CurrentProgress
		}
	}
	legacyProgress := r.CurrentProgress
	r.CurrentTargetTechnologyID = technologyID
	if technologyID == "" {
		r.CurrentProgress = 0
		return
	}
	if _, ok := r.ProgressByTechnology[technologyID]; !ok && previousTarget == "" && legacyProgress > 0 {
		r.ProgressByTechnology[technologyID] = legacyProgress
	}
	r.CurrentProgress = r.ProgressForTechnology(technologyID)
}

func (r *ResearchState) HasCompletedTechnology(id string) bool {
	if r == nil || id == "" {
		return false
	}
	r.EnsureProgressMaps()
	_, ok := r.CompletedTechnologyTurns[id]
	return ok
}

func (r *ResearchState) MarkTechnologyCompleted(id string, turn int) {
	if r == nil || id == "" {
		return
	}
	r.EnsureProgressMaps()
	r.CompletedTechnologyTurns[id] = turn
}

func (r *ResearchState) MarkTechnologyActive(id string, turn int) {
	if r == nil || id == "" {
		return
	}
	r.EnsureProgressMaps()
	r.ActiveTechnologyTurns[id] = turn
	r.UnlockedTechnologies[id] = struct{}{}
}

func (r *ResearchState) CompletedTechnologyIDs() []string {
	if r == nil {
		return nil
	}
	r.EnsureProgressMaps()
	ids := make([]string, 0, len(r.CompletedTechnologyTurns))
	for technologyID := range r.CompletedTechnologyTurns {
		ids = append(ids, technologyID)
	}
	sort.Strings(ids)
	return ids
}

func (r *ResearchState) ActiveTechnologyIDs() []string {
	if r == nil {
		return nil
	}
	r.EnsureProgressMaps()
	ids := make([]string, 0, len(r.ActiveTechnologyTurns))
	for technologyID := range r.ActiveTechnologyTurns {
		ids = append(ids, technologyID)
	}
	sort.Strings(ids)
	return ids
}

func (r *ResearchState) PendingActivationTechnologyIDs() []string {
	if r == nil {
		return nil
	}
	r.EnsureProgressMaps()
	ids := make([]string, 0)
	for technologyID := range r.CompletedTechnologyTurns {
		if _, ok := r.ActiveTechnologyTurns[technologyID]; ok {
			continue
		}
		ids = append(ids, technologyID)
	}
	sort.Strings(ids)
	return ids
}

func (r *ResearchState) StoredProgressTechnologyIDs() []string {
	if r == nil {
		return nil
	}
	r.EnsureProgressMaps()
	ids := make([]string, 0, len(r.ProgressByTechnology))
	for technologyID, amount := range r.ProgressByTechnology {
		if technologyID == "" || amount <= 0 {
			continue
		}
		ids = append(ids, technologyID)
	}
	sort.Strings(ids)
	return ids
}

func (r *ResearchState) RebuildActiveUnlocks(activeTechnologyIDs []string, resolver func(technologyID string)) {
	if r == nil {
		return
	}
	r.EnsureProgressMaps()
	clear(r.UnlockedTechnologies)
	clear(r.UnlockedBuildings)
	clear(r.UnlockedRecipes)
	clear(r.UnlockedPolicyCandidates)
	for _, technologyID := range activeTechnologyIDs {
		if technologyID == "" {
			continue
		}
		r.UnlockedTechnologies[technologyID] = struct{}{}
		if resolver != nil {
			resolver(technologyID)
		}
	}
}
