using System.Collections.Generic;
using Google.Protobuf;
using NUnit.Framework;
using Panoptes.Core.Application.Intents;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Domain;
using Panoptes.Protocol.V1;

namespace Panoptes.Tests.EditMode.Core
{
    public sealed class CommandServiceMigrationTests
    {
        [TearDown]
        public void TearDown()
        {
            ActionLock.Release();
        }

        [Test]
        public void GameIntentService_ShouldSendResearchAndChatThroughInjectedSender()
        {
            var sender = new RecordingSender();
            var service = new GameIntentService(sender);

            Assert.That(service.SetResearchTarget("agrarian_foundations"), Is.True);
            Assert.That(service.SendChatEmote(GameChatEmoteKind.Thinking), Is.True);

            Assert.That(sender.Messages[0], Is.TypeOf<MsgSetResearchTarget>());
            Assert.That(((MsgSetResearchTarget)sender.Messages[0]).TechnologyId, Is.EqualTo("agrarian_foundations"));
            Assert.That(sender.Messages[1], Is.TypeOf<MsgSendGameChat>());
            Assert.That(((MsgSendGameChat)sender.Messages[1]).Payload.Emote, Is.EqualTo(ChatEmote.Thinking));
        }

        [Test]
        public void MinisterCommandService_ShouldSendDomesticDirectivePayload()
        {
            var sender = new RecordingSender();
            var service = new MinisterCommandService(sender);

            Assert.That(service.AcceptDraft("draft-research-1"), Is.True);
            Assert.That(service.RejectDraft("draft-policy-1"), Is.True);

            var accepted = sender.Messages[0] as MsgSetMinisterDirective;
            var rejected = sender.Messages[1] as MsgSetMinisterDirective;
            Assert.That(accepted, Is.Not.Null);
            Assert.That(accepted!.MinisterRole, Is.EqualTo("domestic"));
            StringAssert.Contains("\"directive_type\":\"accept\"", accepted.Content);
            StringAssert.Contains("\"draft_id\":\"draft-research-1\"", accepted.Content);
            Assert.That(rejected, Is.Not.Null);
            Assert.That(rejected!.MinisterRole, Is.EqualTo("domestic"));
            StringAssert.Contains("\"directive_type\":\"reject\"", rejected.Content);
            StringAssert.Contains("\"draft_id\":\"draft-policy-1\"", rejected.Content);
        }

        [Test]
        public void LegacyGameIntents_ShouldBeRemoved()
        {
            var intentsType = System.Type.GetType("Panoptes.Core.Application.Intents.GameIntents, Panoptes.Core");
            Assert.That(intentsType, Is.Null, "静态 GameIntents 兼容壳应被移除，命令必须通过注入服务发送。");
        }

        private sealed class RecordingSender : IClientMessageSender
        {
            public readonly List<IMessage> Messages = new();

            public bool Send(IMessage message)
            {
                Messages.Add(message);
                return true;
            }
        }
    }
}
