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
