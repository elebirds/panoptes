using NUnit.Framework;
using Panoptes.Presentation.UI.HUD;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Tests.EditMode.UI
{
    public sealed class UnitInfoDirectOrderPanelBinderTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
        }

        [Test]
        public void EnsureButtons_ShouldCreateDefaultButtonsWithExpectedLabelsAndVisuals()
        {
            var binder = new UnitInfoDirectOrderPanelBinder();
            var root = CreateRoot();
            var buttonColor = new Color(0.2f, 0.45f, 0.8f, 0.92f);

            var buttons = binder.EnsureButtons(root, null, null, null, null, buttonColor);

            AssertCreatedButton(buttons.Move, "MoveButton", "移动", buttonColor);
            AssertCreatedButton(buttons.Attack, "AttackButton", "攻击", buttonColor);
            AssertCreatedButton(buttons.Hold, "HoldButton", "待命", buttonColor);
            AssertCreatedButton(buttons.Charge, "ChargeButton", "冲锋", buttonColor);
        }

        [Test]
        public void BindListeners_ShouldReplaceExistingListenersAndInvokeDirectOrderActions()
        {
            var binder = new UnitInfoDirectOrderPanelBinder();
            var buttons = CreateButtons();
            var staleClickCount = 0;
            var moveClickCount = 0;
            var attackClickCount = 0;
            var holdClickCount = 0;
            var chargeClickCount = 0;

            buttons.Move.onClick.AddListener(() => staleClickCount++);
            binder.BindListeners(
                buttons,
                new UnitInfoDirectOrderButtonActions(
                    () => moveClickCount++,
                    () => attackClickCount++,
                    () => holdClickCount++,
                    () => chargeClickCount++));

            buttons.Move.onClick.Invoke();
            buttons.Attack.onClick.Invoke();
            buttons.Hold.onClick.Invoke();
            buttons.Charge.onClick.Invoke();

            Assert.That(staleClickCount, Is.Zero);
            Assert.That(moveClickCount, Is.EqualTo(1));
            Assert.That(attackClickCount, Is.EqualTo(1));
            Assert.That(holdClickCount, Is.EqualTo(1));
            Assert.That(chargeClickCount, Is.EqualTo(1));
        }

        [Test]
        public void ApplyState_ShouldHideRootAndButtonsWhenDirectOrdersAreUnavailable()
        {
            var binder = new UnitInfoDirectOrderPanelBinder();
            var root = CreateRoot();
            var buttons = CreateButtons(root);

            binder.ApplyState(root, buttons, visible: false, default, actionLocked: false);

            Assert.That(root.gameObject.activeSelf, Is.False);
            Assert.That(buttons.Move.gameObject.activeSelf, Is.False);
            Assert.That(buttons.Attack.gameObject.activeSelf, Is.False);
            Assert.That(buttons.Hold.gameObject.activeSelf, Is.False);
            Assert.That(buttons.Charge.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void ApplyState_ShouldRenderCivilianMoveOnlyAndRespectActionLock()
        {
            var binder = new UnitInfoDirectOrderPanelBinder();
            var root = CreateRoot();
            var buttons = CreateButtons(root);

            binder.ApplyState(
                root,
                buttons,
                visible: true,
                new UnitInfoDirectOrderState(canMove: true, isMilitaryUnit: false, canAttack: false, canCharge: false),
                actionLocked: true);

            Assert.That(root.gameObject.activeSelf, Is.True);
            Assert.That(buttons.Move.gameObject.activeSelf, Is.True);
            Assert.That(buttons.Move.interactable, Is.False);
            Assert.That(buttons.Move.GetComponentInChildren<TextMeshProUGUI>().text, Is.EqualTo("Move"));
            Assert.That(buttons.Attack.gameObject.activeSelf, Is.False);
            Assert.That(buttons.Hold.gameObject.activeSelf, Is.False);
            Assert.That(buttons.Charge.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void ApplyState_ShouldRenderMilitaryActionsAndHideUnavailableCharge()
        {
            var binder = new UnitInfoDirectOrderPanelBinder();
            var root = CreateRoot();
            var buttons = CreateButtons(root);

            binder.ApplyState(
                root,
                buttons,
                visible: true,
                new UnitInfoDirectOrderState(canMove: true, isMilitaryUnit: true, canAttack: true, canCharge: false),
                actionLocked: false);

            Assert.That(buttons.Move.interactable, Is.True);
            Assert.That(buttons.Attack.gameObject.activeSelf, Is.True);
            Assert.That(buttons.Attack.interactable, Is.True);
            Assert.That(buttons.Hold.gameObject.activeSelf, Is.True);
            Assert.That(buttons.Hold.interactable, Is.True);
            Assert.That(buttons.Charge.gameObject.activeSelf, Is.False);
            Assert.That(buttons.Charge.interactable, Is.False);
        }

        private RectTransform CreateRoot()
        {
            _root = new GameObject("DirectOrders", typeof(RectTransform));
            return _root.GetComponent<RectTransform>();
        }

        private UnitInfoDirectOrderButtons CreateButtons()
        {
            return CreateButtons(CreateRoot());
        }

        private static UnitInfoDirectOrderButtons CreateButtons(RectTransform root)
        {
            var binder = new UnitInfoDirectOrderPanelBinder();
            return binder.EnsureButtons(root, null, null, null, null, Color.white);
        }

        private static void AssertCreatedButton(Button button, string objectName, string label, Color color)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(button.name, Is.EqualTo(objectName));
            Assert.That(button.transform.parent, Is.Not.Null);
            Assert.That(button.GetComponentInChildren<TextMeshProUGUI>().text, Is.EqualTo(label));

            var image = button.GetComponent<Image>();
            Assert.That(image, Is.Not.Null);
            Assert.That(image.color, Is.EqualTo(color));
            Assert.That(image.sprite, Is.Not.Null);
        }
    }
}
