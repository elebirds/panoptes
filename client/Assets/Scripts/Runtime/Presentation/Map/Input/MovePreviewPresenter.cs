/*************************************************
 * Project: Panoptes
 * File: MovePreviewPresenter.cs
 * Author: Panoptes Team
 * Date: 2026-04-29
 * Description: Presentation text resolver for server-backed move path previews.
 *************************************************/

using Panoptes.Core.Domain;

namespace Panoptes.Presentation.Map
{
    public static class MovePreviewPresenter
    {
        public static string ResolveErrorMessage(PathPreviewDto preview, string targetNodeId)
        {
            if (preview == null)
            {
                return "等待服务器确认路径预览。";
            }

            return preview.ErrorCode switch
            {
                "invalid_target" => $"目标节点 {targetNodeId} 当前无法抵达",
                "unit_not_found" => "该单位当前不可用",
                "invalid_directive" => "当前动作不支持该目标",
                _ => "等待服务器确认路径预览。"
            };
        }
    }
}
