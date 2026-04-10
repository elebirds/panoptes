/*************************************************
 * Project: Panoptes
 * File: MessageSender.cs
 * Author: Panoptes Team
 * Date: 2026-04-04
 * Description: Envelope send helper placeholder.
 *************************************************/


using System;
using Google.Protobuf;
using UnityEngine;

namespace Panoptes.Core.Infrastructure.Network
{
    public static class MessageSender
    {
        public static event Action<string, IMessage> OnSendIntercepted;

        public static void Send<T>(T message) where T : IMessage<T>
        {
            if (message == null)
            {
                Debug.LogWarning("[MessageSender] Send ignored: message is null.");
                return;
            }

            OnSendIntercepted?.Invoke(typeof(T).Name, message);

            var network = NetworkManager.Instance;
            if (network == null)
            {
                Debug.LogWarning($"[MessageSender] Send ignored: NetworkManager.Instance is null for {typeof(T).Name}.");
                return;
            }

            network.Send(message);
        }
    }
}
