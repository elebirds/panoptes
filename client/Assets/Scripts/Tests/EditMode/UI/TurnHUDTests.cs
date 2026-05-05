using System.Collections.Generic;
using System.Reflection;
using Google.Protobuf;
using NUnit.Framework;
using Panoptes.Core.Application.Intents;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Application.Stores;
using Panoptes.Presentation.UI.HUD;
using Panoptes.Protocol.V1;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Tests.EditMode.UI
{
    public sealed class TurnHUDTests
    {
        [TearDown]
        public void TearDown()
        {
            ActionLock.Release();
        }

        [Test]
        public void TurnHUD_BindsSceneNextStageButtonWhenSerializedReferenceMissing()
        {
            var sender = new FakeMessageSender();
            var turnStore = new TurnStore();
            using var intentService = new GameIntentService(sender, turnStore);

            var buttonObject = new GameObject("NextStageBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            var hudObject = new GameObject("TurnHUD");
            hudObject.SetActive(false);

            try
            {
                var hud = hudObject.AddComponent<TurnHUD>();
                Inject(hud, intentService, turnStore);
                hudObject.SetActive(true);
                InvokeLifecycle(hud, "OnEnable");

                turnStore.Replace(new TurnState(turn: 1, phase: "planning", isInteractive: true));
                var resolvedButton = ResolvedButton(hud);
                Assert.That(resolvedButton, Is.Not.Null);
                resolvedButton.onClick.Invoke();

                Assert.That(sender.Last, Is.TypeOf<MsgSubmitTurn>());
            }
            finally
            {
                Object.DestroyImmediate(hudObject);
                Object.DestroyImmediate(buttonObject);
            }
        }

        private static void Inject(TurnHUD hud, GameIntentService intentService, TurnStore turnStore)
        {
            var method = typeof(TurnHUD).GetMethod("Construct", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(hud, new object[] { intentService, turnStore });
        }

        private static Button ResolvedButton(TurnHUD hud)
        {
            var field = typeof(TurnHUD).GetField("nextStageButton", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return field.GetValue(hud) as Button;
        }

        private static void InvokeLifecycle(TurnHUD hud, string methodName)
        {
            var method = typeof(TurnHUD).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(hud, null);
        }

        private sealed class FakeMessageSender : IClientMessageSender
        {
            private readonly List<IMessage> _sent = new();

            public IMessage Last => _sent.Count == 0 ? null : _sent[^1];

            public bool Send(IMessage message)
            {
                if (message == null)
                {
                    return false;
                }

                _sent.Add(message);
                return true;
            }
        }
    }
}
