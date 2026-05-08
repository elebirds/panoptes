package domain

import "sort"

func (s *InstitutionState) EnsureMaps() {
	if s == nil {
		return
	}
	if s.CandidateInstitutionIDs == nil {
		s.CandidateInstitutionIDs = make(map[string]struct{})
	}
	if s.ActiveInstitutionIDs == nil {
		s.ActiveInstitutionIDs = []string{}
	}
	if s.PendingInstitutionIDs == nil {
		s.PendingInstitutionIDs = []string{}
	}
}

func (s *InstitutionState) HasCandidate(institutionID string) bool {
	if s == nil || institutionID == "" {
		return false
	}
	s.EnsureMaps()
	_, ok := s.CandidateInstitutionIDs[institutionID]
	return ok
}

func (s *InstitutionState) UnlockCandidate(institutionID string) {
	if s == nil || institutionID == "" {
		return
	}
	s.EnsureMaps()
	s.CandidateInstitutionIDs[institutionID] = struct{}{}
}

func (s *InstitutionState) CandidateIDs() []string {
	if s == nil {
		return nil
	}
	s.EnsureMaps()
	ids := make([]string, 0, len(s.CandidateInstitutionIDs))
	for institutionID := range s.CandidateInstitutionIDs {
		ids = append(ids, institutionID)
	}
	sort.Strings(ids)
	return ids
}

func NormalizeIDList(ids []string) []string {
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

func NormalizePolicyIDList(ids []string) []string {
	return NormalizeIDList(ids)
}

func NormalizeInstitutionIDList(ids []string) []string {
	return NormalizeIDList(ids)
}
