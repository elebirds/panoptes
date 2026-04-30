// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-05-01 00:00:00 +0800
// Description: Builds reported information metadata from observed snapshots.

package query

import (
	"strings"

	pb "github.com/elebirds/panoptes/internal/gen/proto"
)

const (
	ReportingModeClear          = "clear"
	ReportingModeStandard       = "standard"
	ReportingModeHighDistortion = "high_distortion"
)

func (s *ObservationStore) SetReportingMode(viewerID string, mode string) {
	if s == nil {
		return
	}
	viewerID = strings.TrimSpace(viewerID)
	if viewerID == "" {
		return
	}
	mode = NormalizeReportingMode(mode)
	s.mu.Lock()
	defer s.mu.Unlock()
	if s.reportingModes == nil {
		s.reportingModes = make(map[string]string)
	}
	if mode == ReportingModeStandard {
		delete(s.reportingModes, viewerID)
		return
	}
	s.reportingModes[viewerID] = mode
}

func (s *ObservationStore) ReportingMode(viewerID string) string {
	if s == nil {
		return ReportingModeStandard
	}
	viewerID = strings.TrimSpace(viewerID)
	if viewerID == "" {
		return ReportingModeStandard
	}
	s.mu.Lock()
	defer s.mu.Unlock()
	return NormalizeReportingMode(s.reportingModes[viewerID])
}

func NormalizeReportingMode(mode string) string {
	switch strings.TrimSpace(mode) {
	case ReportingModeClear:
		return ReportingModeClear
	case ReportingModeHighDistortion:
		return ReportingModeHighDistortion
	default:
		return ReportingModeStandard
	}
}

func BuildInformationReport(observation *ObservationSnapshot) *pb.InformationReportView {
	if observation == nil {
		return &pb.InformationReportView{Mode: ReportingModeStandard, Confidence: "unknown"}
	}
	mode := NormalizeReportingMode(observation.ReportingMode)
	if observation.DirectInspection {
		mode = ReportingModeClear
	}
	unknownNodes := 0
	for _, node := range observation.Nodes {
		if node == nil {
			continue
		}
		if !node.GetIsCurrentlyVisible() && !node.GetIsMemory() {
			unknownNodes++
		}
	}
	report := &pb.InformationReportView{
		Mode:             mode,
		VisibleNodeCount: int32(len(observation.VisibleNodes)),
		MemoryNodeCount:  int32(len(observation.MemoryNodes)),
		UnknownNodeCount: int32(unknownNodes),
		VisibleUnitCount: int32(len(observation.Units)),
		MemoryUnitCount:  int32(len(observation.MemoryUnits)),
		DirectInspection: observation.DirectInspection,
	}
	switch mode {
	case ReportingModeClear:
		report.Confidence = "clear"
		report.Notes = []string{"direct_inspection"}
	case ReportingModeHighDistortion:
		report.Confidence = "low"
		report.DelayedCount = int32(len(observation.MemoryNodes) + len(observation.MemoryUnits))
		report.OmittedCount = int32(unknownNodes + len(observation.MemoryNodes)/2)
		report.MisreadCount = int32((len(observation.VisibleNodes)+len(observation.Units))/4 + len(observation.MemoryUnits))
		report.Notes = []string{"high_distortion", "memory_may_lag", "visible_details_may_be_misread"}
	default:
		report.Confidence = "medium"
		report.DelayedCount = int32(len(observation.MemoryNodes) + len(observation.MemoryUnits))
		report.OmittedCount = int32(unknownNodes)
		if report.DelayedCount > 0 {
			report.Notes = append(report.Notes, "memory_may_lag")
		}
		if report.OmittedCount > 0 {
			report.Notes = append(report.Notes, "unknown_areas_omitted")
		}
	}
	return report
}
