namespace Panoptes.Core.Application.Feedback
{
    public interface ILoadingOverlayPresenter
    {
        void Show(string message);
        void Hide();
    }
}
