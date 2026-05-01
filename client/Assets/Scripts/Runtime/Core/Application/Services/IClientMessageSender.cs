using Google.Protobuf;

namespace Panoptes.Core.Application.Services
{
    public interface IClientMessageSender
    {
        bool Send(IMessage message);
    }
}
