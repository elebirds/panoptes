package building

import (
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/yohamta/donburi"
)

func SetBinding(entry *donburi.Entry, scope string, cityID string, serviceCityID string) {
	if entry == nil {
		return
	}
	if !entry.HasComponent(domain.BuildingBindingC) {
		entry.AddComponent(domain.BuildingBindingC)
	}
	binding := domain.BuildingBindingComp{
		Scope:         domain.NormalizeBuildingScope(scope),
		CityID:        strings.TrimSpace(cityID),
		ServiceCityID: strings.TrimSpace(serviceCityID),
	}
	if binding.ServiceCityID == "" {
		binding.ServiceCityID = binding.CityID
	}
	domain.BuildingBindingC.SetValue(entry, binding)
}

// Binding 是建筑隶属关系的唯一真相读取入口。
// 后续无论是查询、事件 Apply 还是生命周期阶段，都不再拼接多份 CityID 镜像。
func Binding(entry *donburi.Entry) (domain.BuildingBindingComp, bool) {
	if entry == nil || !entry.HasComponent(domain.BuildingBindingC) {
		return domain.BuildingBindingComp{}, false
	}
	return *domain.BuildingBindingC.Get(entry), true
}

func ResolveCityID(entry *donburi.Entry) string {
	binding, ok := Binding(entry)
	if !ok {
		return ""
	}
	return strings.TrimSpace(binding.CityID)
}

func ResolveServiceCityID(entry *donburi.Entry) string {
	binding, ok := Binding(entry)
	if !ok {
		return ""
	}
	if serviceCityID := strings.TrimSpace(binding.ServiceCityID); serviceCityID != "" {
		return serviceCityID
	}
	return strings.TrimSpace(binding.CityID)
}

func Scope(entry *donburi.Entry) string {
	binding, ok := Binding(entry)
	if !ok {
		if entry != nil && entry.HasComponent(domain.BuildingC) && strings.EqualFold(string(domain.BuildingC.Get(entry).Type), domain.BuildingScopeCityCore) {
			return domain.BuildingScopeCityCore
		}
		return ""
	}
	return domain.NormalizeBuildingScope(binding.Scope)
}

func Rebind(entry *donburi.Entry, cityID string, serviceCityID string) {
	if entry == nil || !entry.HasComponent(domain.BuildingC) {
		return
	}
	SetBinding(entry, Scope(entry), cityID, serviceCityID)
}

func IsCityCore(entry *donburi.Entry) bool {
	if entry == nil || !entry.HasComponent(domain.BuildingC) {
		return false
	}
	if Scope(entry) == domain.BuildingScopeCityCore {
		return true
	}
	return strings.EqualFold(string(domain.BuildingC.Get(entry).Type), domain.BuildingScopeCityCore)
}
