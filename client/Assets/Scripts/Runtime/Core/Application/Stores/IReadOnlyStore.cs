using R3;

namespace Panoptes.Core.Application.Stores
{
    public interface IReadOnlyStore<TState>
    {
        TState Snapshot { get; }
        Observable<TState> State { get; }
    }
}
