/*************************************************
 * Project: Panoptes
 * File: MessageSender.cs
 * Author: Panoptes Team
 * Date: 2026-04-04
 * Description: Envelope send helper placeholder.
 *************************************************/


using Google.Protobuf;

namespace Panoptes.Runtime.Network
{
    public static class MessageSender
    {
        public static void Send<T>(T message) where T : IMessage<T>
        {
            NetworkManager.Instance.Send(message);
        }
    }
}
