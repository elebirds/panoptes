using R3;

namespace Panoptes.Presentation.ViewModels
{
    public interface IViewModel<TState>
    {
        TState Current { get; }
        Observable<TState> State { get; }
    }
}
