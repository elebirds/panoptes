using System;
using System.Reflection;
using Google.Protobuf;
using NUnit.Framework;
using Panoptes.Core.Application.Intents;
using Panoptes.Core.Infrastructure.Network;
using Panoptes.Protocol.V1;

namespace Panoptes.Tests.EditMode.Core
{
    public sealed class GameIntentsResearchTests
    {
        [TearDown]
        public void TearDown()
        {
            ActionLock.Release();
        }

        [Test]
        public void SetResearchTarget_ShouldSendMsgSetResearchTarget()
        {
            ActionLock.Release();

            var intentsType = Type.GetType("Panoptes.Core.Application.Intents.GameIntents, Panoptes.Core");
            Assert.That(intentsType, Is.Not.Null, "缺少 GameIntents。");

            var method = intentsType!.GetMethod("SetResearchTarget", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "GameIntents 必须提供 SetResearchTarget(string)。");

            IMessage capturedMessage = null;
            void OnSend(string _, IMessage message)
            {
                capturedMessage = message;
            }

            MessageSender.OnSendIntercepted += OnSend;
            try
            {
                method!.Invoke(null, new object[] { "agrarian_foundations" });
            }
            finally
            {
                MessageSender.OnSendIntercepted -= OnSend;
            }

            Assert.That(capturedMessage, Is.TypeOf<MsgSetResearchTarget>());
            Assert.That(((MsgSetResearchTarget)capturedMessage!).TechnologyId, Is.EqualTo("agrarian_foundations"));
        }

        [Test]
        public void SendChatEmote_ShouldSendMsgSendGameChat()
        {
            ActionLock.Release();

            var intentsType = Type.GetType("Panoptes.Core.Application.Intents.GameIntents, Panoptes.Core");
            Assert.That(intentsType, Is.Not.Null, "缺少 GameIntents。");

            var emoteType = Type.GetType("Panoptes.Core.Domain.GameChatEmoteKind, Panoptes.Core");
            Assert.That(emoteType, Is.Not.Null, "缺少 GameChatEmoteKind。");

            var method = intentsType!.GetMethod("SendChatEmote", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "GameIntents 必须提供 SendChatEmote(GameChatEmoteKind)。");

            var emote = Enum.Parse(emoteType!, "Thinking");

            IMessage capturedMessage = null;
            void OnSend(string _, IMessage message)
            {
                capturedMessage = message;
            }

            MessageSender.OnSendIntercepted += OnSend;
            try
            {
                method!.Invoke(null, new[] { emote });
            }
            finally
            {
                MessageSender.OnSendIntercepted -= OnSend;
            }

            Assert.That(capturedMessage, Is.TypeOf<MsgSendGameChat>());
            var sendChat = (MsgSendGameChat)capturedMessage!;
            Assert.That(sendChat.Payload, Is.Not.Null);
            Assert.That(sendChat.Payload.HasEmote, Is.True);
            Assert.That(sendChat.Payload.Emote, Is.EqualTo(ChatEmote.Thinking));
        }
    }
}
