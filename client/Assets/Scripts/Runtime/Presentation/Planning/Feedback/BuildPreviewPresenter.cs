/*************************************************
 * Project: Panoptes
 * File: BuildPreviewPresenter.cs
 * Author: Panoptes Team
 * Date: 2026-04-29
 * Description: Planning feedback text resolver for server-backed build preview results.
 *************************************************/

using Panoptes.Core.Application.Feedback;
using Panoptes.Core.Domain;

namespace Panoptes.Presentation.Planning.Feedback
{
    /// <summary>
    /// Converts authoritative build preview DTO feedback into user-facing presentation text.
    /// </summary>
    public static class BuildPreviewPresenter
    {
        public static string ResolveMessage(BuildPreviewDto preview)
        {
            return preview == null ? "检查中" : GameplayFeedbackText.ResolveMessage(preview.Message, preview.ErrorCode);
        }
    }
}
