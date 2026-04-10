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
	m.mu.Lock()
	defer m.mu.Unlock()
	m.Entries = append(m.Entries, entry)
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
	if len(m.Entries) == 0 {
		return "(暂无历史记忆)"
	}
	var b strings.Builder
	for _, e := range m.Entries {
		b.WriteString(fmt.Sprintf("- T%d [%s] %s | outcome=%s | player=%s\n", e.Turn, e.Type, e.Content, e.Outcome, e.PlayerResp))
	}
	return strings.TrimSpace(b.String())
}
