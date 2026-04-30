// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 声明事件模型包的职责与边界。

// Package event defines authoritative state-change events and report-only
// domain facts.
//
// Events are the audit boundary for backend rule decisions: systems may inspect
// state and emit Event values, but authoritative writes happen when Apply runs.
// Report-only events keep a stable Kind and payload contract while leaving
// Apply as a no-op. Internal state-only events may be collected for server audit
// while being filtered from client projections.
package event
