package domain

import "sort"

func (s *InstitutionState) EnsureMaps() {
	if s == nil {
		return
	}
	if s.CandidatePolicyIDs == nil {
		s.CandidatePolicyIDs = make(map[string]struct{})
	}
	if s.ActivePolicyIDs == nil {
		s.ActivePolicyIDs = []string{}
	}
	if s.PendingPolicyIDs == nil {
		s.PendingPolicyIDs = []string{}
	}
}

func (s *InstitutionState) HasCandidate(policyID string) bool {
	if s == nil || policyID == "" {
		return false
	}
	s.EnsureMaps()
	_, ok := s.CandidatePolicyIDs[policyID]
	return ok
}

func (s *InstitutionState) UnlockCandidate(policyID string) {
	if s == nil || policyID == "" {
		return
	}
	s.EnsureMaps()
	s.CandidatePolicyIDs[policyID] = struct{}{}
}

func (s *InstitutionState) CandidateIDs() []string {
	if s == nil {
		return nil
	}
	s.EnsureMaps()
	ids := make([]string, 0, len(s.CandidatePolicyIDs))
	for policyID := range s.CandidatePolicyIDs {
		ids = append(ids, policyID)
	}
	sort.Strings(ids)
	return ids
}

func NormalizePolicyIDList(ids []string) []string {
	if len(ids) == 0 {
		return nil
	}
	seen := make(map[string]struct{}, len(ids))
	out := make([]string, 0, len(ids))
	for _, id := range ids {
		if id == "" {
			continue
		}
		if _, ok := seen[id]; ok {
			continue
		}
		seen[id] = struct{}{}
		out = append(out, id)
	}
	sort.Strings(out)
	return out
}
