package building

import (
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/staticdata"
)

// CapturedLifecycleForBuilding 是城市陷落后城内建筑命运的唯一规则真相。
// producer 不应再提前生成一批 building_ruined 事件，真正命运统一在 Apply 阶段复用这里的规则。
func CapturedLifecycleForBuilding(cfg staticdata.BuildingDefinition) (string, string) {
	if HasAnyTag(cfg.Tags, "defense", "governance") {
		return domain.BuildingStatusRuined, "city_captured"
	}
	return domain.BuildingStatusDisabled, "pending_activation"
}

func HasAnyTag(tags []string, expected ...string) bool {
	for _, tag := range tags {
		current := strings.ToLower(strings.TrimSpace(tag))
		for _, want := range expected {
			if current == strings.ToLower(strings.TrimSpace(want)) {
				return true
			}
		}
	}
	return false
}
