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
    }
}
