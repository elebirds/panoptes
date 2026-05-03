using System.Threading;
using Cysharp.Threading.Tasks;

namespace Panoptes.Core.Application.Services
{
    public interface ICommandService<in TCommand>
    {
        UniTask SubmitAsync(TCommand command, CancellationToken cancellationToken = default);
    }
}
