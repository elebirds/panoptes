// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现部长引擎的部长上下文记忆。

package minister

import (
	"fmt"
	"strings"
	"sync"
)

type MinisterMemory struct {
	PlayerID string
	Role     string
	Entries  []MemoryEntry
	Favor    int // 好感度：0-100
	mu       sync.RWMutex
}

type MemoryEntry struct {
	Turn       int
	Type       string
	Content    string
	Outcome    string
	PlayerResp string
}

func (m *MinisterMemory) Add(entry MemoryEntry) {
	if m == nil {
		return
	}
	m.mu.Lock()
	defer m.mu.Unlock()
	m.Entries = append(m.Entries, entry)
	m.applyFeedbackLocked(entry)
	if len(m.Entries) <= 5 {
		return
	}
	m.Entries = append([]MemoryEntry(nil), m.Entries[len(m.Entries)-5:]...)
}

func (m *MinisterMemory) Recent(n int) []MemoryEntry {
	if m == nil {
		return nil
	}
	m.mu.RLock()
	defer m.mu.RUnlock()
	if n <= 0 || len(m.Entries) == 0 {
		return nil
	}
	if n > len(m.Entries) {
		n = len(m.Entries)
	}
	out := make([]MemoryEntry, n)
	copy(out, m.Entries[len(m.Entries)-n:])
	return out
}

func (m *MinisterMemory) ToPromptString() string {
	if m == nil {
		return "(暂无历史记忆)"
	}
	m.mu.RLock()
	defer m.mu.RUnlock()
	var b strings.Builder
	b.WriteString(fmt.Sprintf("favor=%d/100\n", m.Favor))
	if hint := behaviorHintForFavor(m.Favor); hint != "" {
		b.WriteString("behavior_hint=")
		b.WriteString(hint)
		b.WriteByte('\n')
	}
	if len(m.Entries) == 0 {
		return strings.TrimSpace(b.String())
	}
	for _, e := range m.Entries {
		b.WriteString(fmt.Sprintf("- T%d [%s] %s | outcome=%s | player=%s\n", e.Turn, e.Type, e.Content, e.Outcome, e.PlayerResp))
	}
	return strings.TrimSpace(b.String())
}

// ChangeFavor 改变好感度
func (m *MinisterMemory) ChangeFavor(delta int) {
	if m == nil {
		return
	}
	m.mu.Lock()
	defer m.mu.Unlock()
	m.setFavorLocked(m.Favor + delta)
}

// GetFavor 获取好感度
func (m *MinisterMemory) GetFavor() int {
	if m == nil {
		return 50 // 默认好感度
	}
	m.mu.RLock()
	defer m.mu.RUnlock()
	return m.Favor
}

func (m *MinisterMemory) applyFeedbackLocked(entry MemoryEntry) {
	switch strings.TrimSpace(entry.PlayerResp) {
	case "accepted":
		m.setFavorLocked(m.Favor + 5)
	case "rejected":
		m.setFavorLocked(m.Favor - 8)
	case "stale":
		m.setFavorLocked(m.Favor - 3)
	}
}

func (m *MinisterMemory) setFavorLocked(value int) {
	if value < 0 {
		value = 0
	}
	if value > 100 {
		value = 100
	}
	m.Favor = value
}

func behaviorHintForFavor(favor int) string {
	switch {
	case favor < 35:
		return "玩家近期多次否决你的方案；建议更保守、更强调风险，并避免擅自夸大成果。"
	case favor > 70:
		return "玩家近期认可你的方案；可以更主动地提出清晰主张，但仍需说明风险。"
	default:
		return "维持审慎语气，根据观察报告给出有限但明确的判断。"
	}
}
