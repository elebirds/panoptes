package skills

import (
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/staticdata"
)

type ActivationResult struct {
	SkillCardID      string
	EffectKey        string
	ActiveTurn       int
	ExpiresAfterTurn int
}

type Handler interface {
	EffectKey() string
	Activate(state *domain.GameState, playerID string, ministerRole string, card staticdata.MinisterSkillCard) (ActivationResult, bool)
}

var defaultRegistry = NewRegistry(
	Stargazing{},
)

type Registry struct {
	handlers map[string]Handler
}

func NewRegistry(handlers ...Handler) *Registry {
	registry := &Registry{handlers: make(map[string]Handler, len(handlers))}
	for _, handler := range handlers {
		if handler == nil {
			continue
		}
		key := strings.TrimSpace(handler.EffectKey())
		if key == "" {
			continue
		}
		registry.handlers[key] = handler
	}
	return registry
}

func Activate(state *domain.GameState, playerID string, ministerRole string, skillCardID string) (ActivationResult, bool) {
	return defaultRegistry.Activate(state, playerID, ministerRole, skillCardID, staticdata.Default())
}

func (r *Registry) Activate(state *domain.GameState, playerID string, ministerRole string, skillCardID string, catalog *staticdata.Catalog) (ActivationResult, bool) {
	if r == nil || state == nil || catalog == nil {
		return ActivationResult{}, false
	}
	player := state.Players[strings.TrimSpace(playerID)]
	if player == nil || !player.HasMinisterSkillCard(ministerRole, skillCardID) {
		return ActivationResult{}, false
	}
	card, ok := catalog.GetMinisterSkillCard(strings.TrimSpace(skillCardID))
	if !ok {
		return ActivationResult{}, false
	}
	handler, ok := r.handlers[strings.TrimSpace(card.EffectKey)]
	if !ok {
		return ActivationResult{}, false
	}
	return handler.Activate(state, playerID, ministerRole, card)
}
