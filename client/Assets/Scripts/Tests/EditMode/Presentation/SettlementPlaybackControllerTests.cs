using System.Collections.Generic;
using System.Reflection;
using Google.Protobuf;
using NUnit.Framework;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using Panoptes.Protocol.V1;
using Panoptes.Presentation.Map;
using UnityEngine;

namespace Panoptes.Tests.EditMode.Presentation
{
    public sealed class SettlementPlaybackControllerTests
    {
        [Test]
        public void TurnReportGate_ShouldAcknowledgeWhenPlaybackIsAlreadyComplete()
        {
            var sender = new CaptureMessageSender();
            var turnStore = new TurnStore();
            var settlementStore = new SettlementStore();
            using var intentService = new GameIntentService(sender, turnStore, null);
            var controller = CreateController(settlementStore, turnStore, intentService);

            try
            {
                Assert.That(sender.Messages, Is.Empty);

                PublishTurnState(turnStore, new TurnState(
                    turn: 7,
                    phase: GamePhases.Resolving,
                    timeoutSeconds: 0,
                    nextPhase: GamePhases.TurnReport));
                PublishEmptySettlement(settlementStore);
                PublishTurnState(turnStore, new TurnState(
                    turn: 7,
                    phase: GamePhases.TurnReport,
                    timeoutSeconds: 5,
                    nextPhase: GamePhases.Planning));

                Assert.That(sender.Messages, Has.Count.EqualTo(1));
                var ack = sender.Messages[0] as MsgAcknowledgeTurnReport;
                Assert.That(ack, Is.Not.Null);
                Assert.That(ack.Turn, Is.EqualTo(7));
            }
            finally
            {
                if (controller != null)
                {
                    Object.DestroyImmediate(controller.gameObject);
                }
            }
        }

        private static SettlementPlaybackController CreateController(
            SettlementStore settlementStore,
            TurnStore turnStore,
            GameIntentService intentService)
        {
            var gameObject = new GameObject("SettlementPlaybackControllerTest");
            gameObject.SetActive(false);
            var controller = gameObject.AddComponent<SettlementPlaybackController>();

            var construct = typeof(SettlementPlaybackController).GetMethod(
                "Construct",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(construct, Is.Not.Null);
            construct.Invoke(controller, new object[] { settlementStore, turnStore, intentService, null, null, null });

            gameObject.SetActive(true);
            return controller;
        }

        private static void PublishEmptySettlement(SettlementStore settlementStore)
        {
            var replace = typeof(SettlementStore).GetMethod(
                "Replace",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(replace, Is.Not.Null);
            replace.Invoke(settlementStore, new object[] { null });
        }

        private static void PublishTurnState(TurnStore turnStore, TurnState state)
        {
            var replace = typeof(TurnStore).GetMethod(
                "Replace",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(replace, Is.Not.Null);
            replace.Invoke(turnStore, new object[] { state });
        }

        private sealed class CaptureMessageSender : IClientMessageSender
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
