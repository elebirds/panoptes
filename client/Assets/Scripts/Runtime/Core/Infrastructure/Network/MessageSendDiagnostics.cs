using System;
using Google.Protobuf;

namespace Panoptes.Core.Infrastructure.Network
{
    public static class MessageSendDiagnostics
    {
        public static event Action<string, IMessage> OnSendIntercepted;

        internal static void PublishSendIntercepted(IMessage message)
        {
            if (message != null)
            {
                OnSendIntercepted?.Invoke(message.Descriptor.Name, message);
            }
        }
    }
}
