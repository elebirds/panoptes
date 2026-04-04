/*************************************************
 * Project: Panoptes
 * File: MessageSender.cs
 * Author: Panoptes Team
 * Date: 2026-04-04
 * Description: Envelope send helper placeholder.
 *************************************************/


namespace Panoptes.Runtime.Network
{
    // 静态工具类，所有发送操作走这里
    public static class MessageSender
    {
        public static void Send<T>(T message) where T : IMessage<T>
        {
            NetworkManager.Instance.Send(message);
        }
    }
}
