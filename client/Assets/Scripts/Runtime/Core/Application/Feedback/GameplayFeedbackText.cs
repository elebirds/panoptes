using System;

namespace Panoptes.Core.Application.Feedback
{
    public static class GameplayFeedbackText
    {
        public static string ResolveMessage(string serverMessage, string code)
        {
            var server = (serverMessage ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(server))
            {
                return server;
            }

            var fallback = FallbackFromCode(code);
            if (!string.IsNullOrWhiteSpace(fallback))
            {
                return fallback;
            }

            return string.IsNullOrWhiteSpace(code) ? "未知错误" : code.Trim();
        }

        public static string FallbackFromCode(string code)
        {
            switch ((code ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "invalid_request":
                    return "请求格式错误";
                case "invalid_credentials":
                    return "用户名或密码错误";
                case "user_exists":
                    return "用户名已被占用";
                case "internal_error":
                    return "服务器错误，请稍后重试";
                case "unauthorized":
                    return "请重新登录";
                case "phase_mismatch":
                    return "当前阶段不支持此操作";
                case "game_not_found":
                    return "当前对局不存在";
                case "invalid_target":
                    return "目标无效";
                case "invalid_directive":
                    return "当前不能执行这条指令";
                case "building_exists":
                    return "该位置已经有建筑";
                case "no_tokens_left":
                    return "本回合不能继续执行该操作";
                case "building_technology_locked":
                    return "该建筑的科技未解锁";
                case "recipe_technology_locked":
                    return "该配方的科技未解锁";
                case "insufficient_resources":
                    return "资源不足";
                case "insufficient_points":
                    return "建造点数不足";
                case "outside_territory":
                    return "目标不在你的有效辖区内";
                case "terrain_not_buildable":
                    return "该地形不能建造";
                case "resource_only_required":
                    return "该建筑只能建在资源点上";
                case "resource_type_mismatch":
                    return "资源点类型不匹配";
                case "building_disabled":
                    return "建筑当前停摆，本回合不会生产";
                case "invalid_recipe_selection":
                    return "当前配方无效或未解锁";
                case "enemy_control":
                    return "建筑处于敌方控制下";
                case "multiple_controllers":
                    return "节点处于多方争夺中";
                case "pending_activation":
                    return "建筑仍在启用中";
                default:
                    return string.Empty;
            }
        }
    }
}
