namespace Panoptes.Presentation.Binders
{
    public interface IBinder<in TViewModel>
    {
        void Bind(TViewModel viewModel);
        void Unbind();
    }
}
