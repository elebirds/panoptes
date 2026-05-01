/*************************************************
 * Project: Panoptes
 * File: MessageSender.cs
 * Author: Panoptes Team
 * Date: 2026-04-04
 * Description: Typed Transport V2 send helper.
 *************************************************/

using System;
using Google.Protobuf;
using UnityEngine;

namespace Panoptes.Core.Infrastructure.Network
{
    public static class MessageSender
    {
        public static event Action<string, IMessage> OnSendIntercepted;

        public static void Send(IMessage message)
        {
            if (message == null)
            {
                Debug.LogWarning("[MessageSender] Send ignored: message is null.");
                return;
            }

            PublishSendIntercepted(message);

            var network = NetworkManager.Instance;
            if (network == null)
            {
                Debug.LogWarning($"[MessageSender] Send ignored: NetworkManager.Instance is null for {message.Descriptor.Name}.");
                return;
            }

            network.Send(message);
        }

        public static void Send<T>(T message) where T : IMessage<T>
        {
            Send((IMessage)message);
        }

        internal static void PublishSendIntercepted(IMessage message)
        {
            if (message != null)
            {
                OnSendIntercepted?.Invoke(message.Descriptor.Name, message);
            }
        }
    }
}
