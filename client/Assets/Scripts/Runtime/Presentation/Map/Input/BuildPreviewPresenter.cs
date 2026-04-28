/*************************************************
 * Project: Panoptes
 * File: BuildPreviewPresenter.cs
 * Author: Panoptes Team
 * Date: 2026-04-29
 * Description: Presentation text resolver for server-backed build preview feedback.
 *************************************************/

using Panoptes.Core.Application.Feedback;
using Panoptes.Core.Domain;

namespace Panoptes.Presentation.Map
{
    public static class BuildPreviewPresenter
    {
        public static string ResolveMessage(BuildPreviewDto preview)
        {
            return preview == null ? "检查中" : GameplayFeedbackText.ResolveMessage(preview.Message, preview.ErrorCode);
        }
    }
}
