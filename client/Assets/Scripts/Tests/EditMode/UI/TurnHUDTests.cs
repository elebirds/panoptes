using System.Collections.Generic;
using System.Reflection;
using Google.Protobuf;
using NUnit.Framework;
using Panoptes.Core.Application.Intents;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Application.Stores;
using Panoptes.Presentation.UI.HUD;
using Panoptes.Protocol.V1;
using TMPro;
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

        [Test]
        public void TurnHUD_WhenExternalAndTitleUseSameText_ShouldKeepCountdownOnSingleSecondLine()
        {
            var sender = new FakeMessageSender();
            var turnStore = new TurnStore();
            using var intentService = new GameIntentService(sender, turnStore);

            var panelObject = new GameObject("TrunPanel", typeof(RectTransform));
            var textObject = new GameObject("TurnNum", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(panelObject.transform, false);
            var hudObject = panelObject;
            hudObject.SetActive(false);

            try
            {
                var hud = hudObject.AddComponent<TurnHUD>();
                var root = panelObject.GetComponent<RectTransform>();
                var text = textObject.GetComponent<TextMeshProUGUI>();
                SetField(hud, "root", root);
                SetField(hud, "titleText", text);
                SetField(hud, "externalTurnPanelRoot", root);
                SetField(hud, "externalTurnNumText", text);
                Inject(hud, intentService, turnStore);

                hudObject.SetActive(true);
                InvokeLifecycle(hud, "OnEnable");
                turnStore.Replace(new TurnState(turn: 7, phase: "planning", timeoutSeconds: 30, isInteractive: true));

                var lines = text.text.Split('\n');
                Assert.That(lines, Has.Length.EqualTo(2));
                Assert.That(lines[0], Is.EqualTo("当前回合数：7"));
                StringAssert.StartsWith("本回合剩余：", lines[1]);
                Assert.That(lines[1], Does.Not.Contain("\n"));
                Assert.That(root.sizeDelta.x, Is.GreaterThanOrEqualTo(320f));
                Assert.That(root.sizeDelta.y, Is.GreaterThanOrEqualTo(108f));
                Assert.That(text.rectTransform.sizeDelta.x, Is.GreaterThanOrEqualTo(288f));
                Assert.That(text.rectTransform.sizeDelta.y, Is.GreaterThanOrEqualTo(72f));
                Assert.That(text.rectTransform.anchoredPosition.y, Is.LessThanOrEqualTo(18f));
                Assert.That(text.enableAutoSizing, Is.True);
                Assert.That(text.fontSizeMax, Is.EqualTo(36f));
                Assert.That(text.fontSizeMin, Is.GreaterThanOrEqualTo(20f));
                Assert.That(text.textWrappingMode, Is.EqualTo(TextWrappingModes.NoWrap));
                Assert.That(text.overflowMode, Is.EqualTo(TextOverflowModes.Overflow));
            }
            finally
            {
                Object.DestroyImmediate(panelObject);
            }
        }

        [Test]
        public void TurnHUD_WhenSamePlanningTurnRefreshes_ShouldNotResetDeadline()
        {
            var sender = new FakeMessageSender();
            var turnStore = new TurnStore();
            using var intentService = new GameIntentService(sender, turnStore);

            var hudObject = new GameObject("TurnHUD");
            hudObject.SetActive(false);

            try
            {
                var hud = hudObject.AddComponent<TurnHUD>();
                Inject(hud, intentService, turnStore);
                hudObject.SetActive(true);
                InvokeLifecycle(hud, "OnEnable");

                turnStore.Replace(new TurnState(turn: 3, phase: "planning", tokensLeft: 5, timeoutSeconds: 45, isInteractive: true));
                var firstDeadline = GetFloatField(hud, "_deadline");
                turnStore.Replace(new TurnState(turn: 3, phase: "planning", tokensLeft: 2, timeoutSeconds: 45, isInteractive: true));
                var secondDeadline = GetFloatField(hud, "_deadline");

                Assert.That(secondDeadline, Is.EqualTo(firstDeadline));
            }
            finally
            {
                Object.DestroyImmediate(hudObject);
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

        private static void SetField(TurnHUD hud, string name, object value)
        {
            var field = typeof(TurnHUD).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(hud, value);
        }

        private static float GetFloatField(TurnHUD hud, string name)
        {
            var field = typeof(TurnHUD).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (float)field.GetValue(hud);
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
