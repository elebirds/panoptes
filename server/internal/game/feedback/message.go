package feedback

import (
	"fmt"
	"strings"
)

func RuntimeReasonMessage(reason string) string {
	switch strings.TrimSpace(reason) {
	case "":
		return ""
	case "building_disabled":
		return "建筑当前停摆，本回合不会生产。"
	case "invalid_recipe_selection":
		return "当前配方无效或未解锁，本回合不会生产。"
	case "enemy_control":
		return "建筑正处于敌方控制下，当前无法正常运作。"
	case "multiple_controllers":
		return "该节点处于多方争夺中，当前无法正常运作。"
	case "pending_activation":
		return "建筑仍在启用中，本回合还不会生效。"
	case "insufficient_resources":
		return "生产所需资源不足，本回合无法推进。"
	case "insufficient_points":
		return "生产所需点数不足，本回合无法推进。"
	case "outside_territory":
		return "建筑已脱离你的有效辖区，当前无法正常运作。"
	default:
		return fmt.Sprintf("当前无法运行：%s。", strings.TrimSpace(reason))
	}
}

func BuildReasonMessage(reason string) string {
	switch strings.TrimSpace(reason) {
	case "invalid_request":
		return "建造指令不完整，请重新选择建筑、节点和所属城市。"
	case "invalid_target":
		return "目标节点或所属城市无效，无法提交建造。"
	case "unauthorized":
		return "所属城市不归你控制，无法以它作为建造归属。"
	case "invalid_directive":
		return "当前不能提交这条建造指令。"
	case "building_exists":
		return "该节点已经有建筑，不能重复建造。"
	case "outside_territory":
		return "该节点不在你的有效辖区内，当前不能建造。"
	case "terrain_not_buildable":
		return "该地形不能建造这类建筑。"
	case "resource_only_required":
		return "这类建筑只能建在资源点上。"
	case "resource_type_mismatch":
		return "该资源点类型与建筑要求不匹配。"
	case "insufficient_resources":
		return "资源不足，无法完成这条建造。"
	case "insufficient_points":
		return "工业点数不足，无法完成这条建造。"
	default:
		return ""
	}
}

func RecipeReasonMessage(reason string) string {
	switch strings.TrimSpace(reason) {
	case "invalid_request":
		return "配方设置指令不完整，请重新选择建筑和配方。"
	case "invalid_target":
		return "目标建筑或配方无效，无法设置配方。"
	case "unauthorized":
		return "这座建筑不归你控制，无法设置配方。"
	case "invalid_directive":
		return "当前不能设置这个配方。"
	default:
		return RuntimeReasonMessage(reason)
	}
}
