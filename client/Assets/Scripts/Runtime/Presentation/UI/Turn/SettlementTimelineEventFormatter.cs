using Panoptes.Core.Application.Feedback;
using Panoptes.Core.Domain;

namespace Panoptes.Presentation.UI.Turn
{
    public static class SettlementTimelineEventFormatter
    {
        public static bool TryDescribeEvent(TurnEventDto evt, out string description)
        {
            description = string.Empty;
            if (evt == null)
            {
                return false;
            }

            var nodeId = ReadData(evt, "node_id");
            var buildingType = ReadData(evt, "building_type_id", "building_type");
            var recipeId = ReadData(evt, "recipe_id");
            var reasonMessage = ResolveReasonMessage(evt.ReasonMessage, ReadData(evt, "reason"));
            var blockedReasonMessage = ResolveReasonMessage(evt.BlockedReasonMessage, ReadData(evt, "blocked_reason"));

            switch (evt.Type)
            {
                case "unit_moved":
                    description = $"单位 {Fallback(evt.UnitId, "未知单位")} 移动到 ({evt.ToQ},{evt.ToR})。";
                    return true;
                case "unit_damaged":
                    description = $"单位 {Fallback(evt.UnitId, "未知单位")} 受到 {ResolveDamageText(evt)}。";
                    return true;
                case "unit_died":
                    description = $"单位 {Fallback(evt.UnitId, "未知单位")} 阵亡。";
                    return true;
                case "building_damaged":
                    description = $"节点 {Fallback(nodeId, Fallback(evt.NodeId, "未知节点"))} 的建筑受损。";
                    return true;
                case "city_core_damaged":
                    description = $"节点 {Fallback(nodeId, Fallback(evt.NodeId, "城市核心"))} 的城市核心受损。";
                    return true;
                case "city_core_destroyed":
                    description = $"节点 {Fallback(nodeId, Fallback(evt.NodeId, "城市核心"))} 的城市核心被摧毁。";
                    return true;
                case "city_founded":
                    description = string.IsNullOrWhiteSpace(nodeId)
                        ? "建立了新城市。"
                        : $"节点 {nodeId} 建立了新城市。";
                    return true;
                case "building_built":
                    description = $"节点 {Fallback(nodeId, "未知节点")} 完成了 {Fallback(buildingType, "建筑")}。";
                    return true;
                case "building_demolished":
                    description = string.IsNullOrWhiteSpace(reasonMessage)
                        ? $"节点 {Fallback(nodeId, "未知节点")} 的 {Fallback(buildingType, "建筑")} 已拆除。"
                        : $"节点 {Fallback(nodeId, "未知节点")} 的 {Fallback(buildingType, "建筑")} 已拆除：{reasonMessage}";
                    return true;
                case "building_skipped":
                    description = $"节点 {Fallback(nodeId, "未知节点")} 的 {Fallback(buildingType, "建筑")} 未能建造：{Fallback(reasonMessage, "当前不能建造。")}";
                    return true;
                case "building_demolish_skipped":
                    description = $"节点 {Fallback(nodeId, "未知节点")} 的 {Fallback(buildingType, "建筑")} 未能拆除：{Fallback(reasonMessage, "当前不能拆除。")}";
                    return true;
                case "recipe_skipped":
                    description = $"节点 {Fallback(nodeId, "未知节点")} 的配方 {Fallback(recipeId, "当前配方")} 未推进：{Fallback(reasonMessage, "本回合未生产。")}";
                    return true;
                case "recipe_progressed":
                    if (!string.IsNullOrWhiteSpace(blockedReasonMessage))
                    {
                        description = $"节点 {Fallback(nodeId, "未知节点")} 的配方 {Fallback(recipeId, "当前配方")} 被阻塞：{blockedReasonMessage}";
                        return true;
                    }

                    var amount = ReadData(evt, "amount", "progress", "base_progress");
                    description = string.IsNullOrWhiteSpace(amount)
                        ? $"节点 {Fallback(nodeId, "未知节点")} 的配方 {Fallback(recipeId, "当前配方")} 已推进。"
                        : $"节点 {Fallback(nodeId, "未知节点")} 的配方 {Fallback(recipeId, "当前配方")} 推进了 {amount} 点。";
                    return true;
                case "building_status_changed":
                    var status = ReadData(evt, "status");
                    description = string.IsNullOrWhiteSpace(reasonMessage)
                        ? $"节点 {Fallback(nodeId, "未知节点")} 的建筑状态变为 {Fallback(status, "未知状态")}。"
                        : $"节点 {Fallback(nodeId, "未知节点")} 的建筑状态变为 {Fallback(status, "未知状态")}：{reasonMessage}";
                    return true;
                case "facility_takeover_progressed":
                    var progress = ReadData(evt, "takeover_progress");
                    var required = ReadData(evt, "takeover_required");
                    if (!string.IsNullOrWhiteSpace(progress) && !string.IsNullOrWhiteSpace(required))
                    {
                        description = $"节点 {Fallback(nodeId, "未知节点")} 正在被接管（{progress}/{required}）：{Fallback(reasonMessage, "当前无法正常运作。")}";
                        return true;
                    }

                    description = $"节点 {Fallback(nodeId, "未知节点")} 正在被接管：{Fallback(reasonMessage, "当前无法正常运作。")}";
                    return true;
                default:
                    return false;
            }
        }

        private static string ResolveDamageText(TurnEventDto evt)
        {
            var damage = evt.Damage > 0 ? evt.Damage.ToString() : ReadData(evt, "damage");
            return string.IsNullOrWhiteSpace(damage) ? "伤害" : $"{damage} 点伤害";
        }

        private static string ReadData(TurnEventDto evt, params string[] keys)
        {
            if (evt?.Data == null || keys == null)
            {
                return string.Empty;
            }

            for (var i = 0; i < keys.Length; i++)
            {
                var key = keys[i];
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (evt.Data.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        private static string Fallback(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static string ResolveReasonMessage(string serverMessage, string code)
        {
            if (!string.IsNullOrWhiteSpace(serverMessage))
            {
                return serverMessage.Trim();
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                return string.Empty;
            }

            return GameplayFeedbackText.ResolveMessage(string.Empty, code);
        }
    }
}
