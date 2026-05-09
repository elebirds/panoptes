package domain

// ClearPostResolutionScratch clears per-turn planning/resolving scratch data
// after resolving has broadcast its events. Durable active marches and point
// budget containers are preserved for the next planning cycle.
func (r *TurnRuntime) ClearPostResolutionScratch() {
	if r == nil {
		return
	}
	clear(r.Resolving.UnitOrders)
	r.Planning.BuildOrders = r.Planning.BuildOrders[:0]
	r.Planning.DemolishOrders = r.Planning.DemolishOrders[:0]
	r.Planning.RecipeSelections = r.Planning.RecipeSelections[:0]
	r.Planning.MinisterBuilds = r.Planning.MinisterBuilds[:0]
	r.Planning.MinisterMoves = r.Planning.MinisterMoves[:0]
	clear(r.Planning.MinisterDrafts)
	clear(r.Planning.UnitOrders)
	clear(r.Planning.MinisterDirectives)
	clear(r.Planning.PendingPolicies)
	clear(r.Planning.PendingResearch)
	clear(r.Planning.PendingInstitutions)
	clear(r.Planning.WarDirectives)
}
