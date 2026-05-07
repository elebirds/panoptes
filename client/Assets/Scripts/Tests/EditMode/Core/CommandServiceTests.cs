using System.Collections.Generic;
using Google.Protobuf;
using NUnit.Framework;
using Panoptes.Core.Application.Intents;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using Panoptes.Protocol.V1;

namespace Panoptes.Tests.EditMode.Core
{
    public sealed class CommandServiceTests
    {
        [TearDown]
        public void TearDown()
        {
            ActionLock.Release();
        }

        [Test]
        public void GameIntentService_ShouldSendResearchChatAndSubmitCommands()
        {
            var sender = new FakeMessageSender();
            var service = new GameIntentService(sender);

            Assert.That(service.SetResearchTarget("agrarian_foundations"), Is.True);
            Assert.That(sender.Last, Is.TypeOf<MsgSetResearchTarget>());
            Assert.That(((MsgSetResearchTarget)sender.Last).TechnologyId, Is.EqualTo("agrarian_foundations"));

            Assert.That(service.SendChatEmote(GameChatEmoteKind.Thinking), Is.True);
            Assert.That(sender.Last, Is.TypeOf<MsgSendGameChat>());
            Assert.That(((MsgSendGameChat)sender.Last).Payload.Emote, Is.EqualTo(ChatEmote.Thinking));

            Assert.That(service.SubmitTurn(), Is.True);
            Assert.That(ActionLock.IsLocked, Is.True);
            Assert.That(sender.Last, Is.TypeOf<MsgSubmitTurn>());
        }

        [Test]
        public void PlanningIntentService_ShouldSendPlanningCommands()
        {
            var sender = new FakeMessageSender();
            var service = new PlanningIntentService(sender);

            Assert.That(service.PreviewMove("req-1", "unit-1", "node-b"), Is.True);
            Assert.That(sender.Last, Is.TypeOf<MsgPlanningPathPreviewRequest>());
            var movePreview = (MsgPlanningPathPreviewRequest)sender.Last;
            Assert.That(movePreview.RequestId, Is.EqualTo("req-1"));
            Assert.That(movePreview.UnitId, Is.EqualTo("unit-1"));
            Assert.That(movePreview.Action, Is.EqualTo("move"));
            Assert.That(movePreview.TargetNodeId, Is.EqualTo("node-b"));

            Assert.That(service.AttackUnit("unit-1", "unit-2", "node-c"), Is.True);
            Assert.That(sender.Last, Is.TypeOf<MsgIssueUnitOrder>());
            var attack = (MsgIssueUnitOrder)sender.Last;
            Assert.That(attack.UnitId, Is.EqualTo("unit-1"));
            Assert.That(attack.Action, Is.EqualTo("attack"));
            Assert.That(attack.TargetUnitId, Is.EqualTo("unit-2"));
            Assert.That(attack.SecondaryNodeId, Is.EqualTo("node-c"));

            Assert.That(service.ChargeUnit("unit-1", "node-d", "unit-3"), Is.True);
            Assert.That(sender.Last, Is.TypeOf<MsgIssueUnitOrder>());
            var charge = (MsgIssueUnitOrder)sender.Last;
            Assert.That(charge.UnitId, Is.EqualTo("unit-1"));
            Assert.That(charge.Action, Is.EqualTo("charge"));
            Assert.That(charge.TargetNodeId, Is.EqualTo("node-d"));
            Assert.That(charge.TargetUnitId, Is.EqualTo("unit-3"));

            Assert.That(service.SetBuildingRecipe("city-core", "grain_rations"), Is.True);
            Assert.That(sender.Last, Is.TypeOf<MsgSetBuildingRecipe>());
            var recipe = (MsgSetBuildingRecipe)sender.Last;
            Assert.That(recipe.NodeId, Is.EqualTo("city-core"));
            Assert.That(recipe.RecipeId, Is.EqualTo("grain_rations"));
        }

        [Test]
        public void MinisterCommandService_ShouldSendDirectivePayloads()
        {
            var sender = new FakeMessageSender();
            var service = new MinisterCommandService(sender);

            Assert.That(service.AcceptDraft("draft-research-1"), Is.True);
            Assert.That(sender.Last, Is.TypeOf<MsgSetMinisterDirective>());
            var accept = (MsgSetMinisterDirective)sender.Last;
            Assert.That(accept.MinisterRole, Is.EqualTo("domestic"));
            StringAssert.Contains("\"directive_type\":\"accept\"", accept.Content);
            StringAssert.Contains("\"draft_id\":\"draft-research-1\"", accept.Content);

            Assert.That(service.RejectDraft("draft-policy-1"), Is.True);
            var reject = (MsgSetMinisterDirective)sender.Last;
            StringAssert.Contains("\"directive_type\":\"reject\"", reject.Content);
            StringAssert.Contains("\"draft_id\":\"draft-policy-1\"", reject.Content);
        }

        [Test]
        public void Services_ShouldRespectActionLockForGameplayCommands()
        {
            var sender = new FakeMessageSender();
            ActionLock.Acquire();

            Assert.That(new GameIntentService(sender).SetPolicy("war_preparedness"), Is.False);
            Assert.That(new PlanningIntentService(sender).MoveUnit("unit-1", "node-b"), Is.False);
            Assert.That(new MinisterCommandService(sender).AcceptDraft("draft-1"), Is.False);
            Assert.That(sender.Sent, Is.Empty);
        }

        [Test]
        public void GameIntentService_ShouldReleaseActionLockFromStores()
        {
            var sender = new FakeMessageSender();
            var turnStore = new TurnStore();
            var gameOverStore = new GameOverStore();
            using var service = new GameIntentService(sender, turnStore, gameOverStore);

            ActionLock.Acquire();
            turnStore.Replace(new TurnState(phase: "planning", isInteractive: true));
            Assert.That(ActionLock.IsLocked, Is.False);

            ActionLock.Acquire();
            gameOverStore.Replace(new GameOverState(isGameOver: true));
            Assert.That(ActionLock.IsLocked, Is.False);
        }

        private sealed class FakeMessageSender : IClientMessageSender
        {
            public readonly List<IMessage> Sent = new();
            public IMessage Last => Sent.Count == 0 ? null : Sent[^1];

            public bool Send(IMessage message)
            {
                if (message == null)
                {
                    return false;
                }

                Sent.Add(message);
                return true;
            }
        }
    }
}
