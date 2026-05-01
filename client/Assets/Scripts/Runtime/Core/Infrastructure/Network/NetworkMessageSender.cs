using Google.Protobuf;
using Panoptes.Core.Application.Services;
using UnityEngine;

namespace Panoptes.Core.Infrastructure.Network
{
    public sealed class NetworkMessageSender : IClientMessageSender
    {
        private readonly NetworkManager _networkManager;

        public NetworkMessageSender(NetworkManager networkManager)
        {
            _networkManager = networkManager;
        }

        public bool Send(IMessage message)
        {
            if (message == null)
            {
                Debug.LogWarning("[NetworkMessageSender] Send ignored: message is null.");
                return false;
            }

            if (_networkManager == null)
            {
                Debug.LogWarning($"[NetworkMessageSender] Send ignored: NetworkManager is missing for {message.Descriptor.Name}.");
                return false;
            }

            MessageSender.PublishSendIntercepted(message);
            _networkManager.Send(message);
            return true;
        }
    }
}
