using System.Linq;
using NUnit.Framework;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Map;
using Panoptes.Presentation.UI.HUD;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Tests.EditMode.UI
{
    public sealed class UnitInfoActionListBinderTests
    {
        private sealed class SerializedCompatibleSlot : UnitInfoActionButtonSlot
        {
        }

        private GameObject _root;
        private GameObject _registryRoot;
        private GameObject _providerRoot;
        private GameObject _unitRoot;

        [TearDown]
        public void TearDown()
        {
            if (_unitRoot != null)
            {
                Object.DestroyImmediate(_unitRoot);
            }

            if (_registryRoot != null)
            {
                Object.DestroyImmediate(_registryRoot);
            }

            if (_providerRoot != null)
            {
                Object.DestroyImmediate(_providerRoot);
            }

            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
        }

        [Test]
        public void EnsureRequiredSlots_ShouldCreateMissingSlotsWithoutDuplicatingExistingAction()
        {
            var binder = new UnitInfoActionListBinder();
            var root = CreateRoot();
            var existing = binder.BuildDefaultButtonSlot(root, " ACTION_2 ", "Existing", new Vector2(88f, 24f), Color.white);

            var slots = binder.EnsureRequiredSlots(
                new[] { existing },
                root,
                new Vector2(90f, 28f),
                Color.blue);

            Assert.That(slots, Has.Length.EqualTo(5));
            Assert.That(slots.Count(slot => Normalize(slot.actionId) == "action_2"), Is.EqualTo(1));
            Assert.That(slots.Any(slot => Normalize(slot.actionId) == "expand_territory"), Is.True);
            Assert.That(slots.Any(slot => Normalize(slot.actionId) == "open_recipe_synthesis"), Is.True);
        }

        [Test]
        public void EnsureRequiredSlots_ShouldPreserveCallerSlotTypeForSerializedCompatibility()
        {
            var binder = new UnitInfoActionListBinder();
            var root = CreateRoot();
            var existing = binder.BuildDefaultButtonSlot(
                root,
                "action_2",
                "Existing",
                new Vector2(88f, 24f),
                Color.white,
                () => new SerializedCompatibleSlot());

            var slots = binder.EnsureRequiredSlots(
                new[] { existing },
                root,
                new Vector2(90f, 28f),
                Color.blue,
                () => new SerializedCompatibleSlot());

            Assert.That(slots, Is.All.TypeOf<SerializedCompatibleSlot>());
        }

        [Test]
        public void BuildDefaultButtonSlot_ShouldCreateButtonWithLabelAndFallbackSprite()
        {
            var binder = new UnitInfoActionListBinder();
            var root = CreateRoot();

            var slot = binder.BuildDefaultButtonSlot(root, "settle_city", "Settle", new Vector2(90f, 28f), Color.green);

            Assert.That(slot, Is.Not.Null);
            Assert.That(slot.actionId, Is.EqualTo("settle_city"));
            Assert.That(slot.button, Is.Not.Null);
            Assert.That(slot.label.text, Is.EqualTo("Settle"));
            Assert.That(slot.button.GetComponent<Image>().sprite, Is.Not.Null);
            Assert.That(slot.button.transform.parent, Is.EqualTo(root));
        }

        [Test]
        public void RepairLayoutAndVisuals_ShouldRestoreRootLayoutAndButtonVisuals()
        {
            var binder = new UnitInfoActionListBinder();
            var root = CreateRoot();
            root.sizeDelta = Vector2.zero;
            var slot = binder.BuildDefaultButtonSlot(root, "settle_city", "Settle", Vector2.zero, new Color(1f, 1f, 1f, 0f));
            var image = slot.button.GetComponent<Image>();
            image.sprite = null;
            image.color = Color.clear;

            binder.RepairLayoutAndVisuals(
                new[] { slot },
                root,
                new Vector2(90f, 28f),
                Color.cyan);

            Assert.That(root.GetComponent<HorizontalLayoutGroup>(), Is.Not.Null);
            Assert.That(root.GetComponent<ContentSizeFitter>(), Is.Not.Null);
            Assert.That(root.sizeDelta, Is.EqualTo(new Vector2(190f, 30f)));

            var layout = slot.button.GetComponent<LayoutElement>();
            Assert.That(layout.preferredWidth, Is.EqualTo(90f));
            Assert.That(layout.preferredHeight, Is.EqualTo(28f));
            Assert.That(image.sprite, Is.Not.Null);
            Assert.That(image.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(image.color, Is.EqualTo(Color.cyan));
        }

        [Test]
        public void Refresh_ShouldBindVisibleActionAndHideUnavailableActions()
        {
            var binder = new UnitInfoActionListBinder();
            var root = CreateRoot();
            var visibleSlot = binder.BuildDefaultButtonSlot(root, "visible_action", "Old", new Vector2(90f, 28f), Color.white);
            var hiddenSlot = binder.BuildDefaultButtonSlot(root, "hidden_action", "Hidden", new Vector2(90f, 28f), Color.white);
            var missingSlot = binder.BuildDefaultButtonSlot(root, "missing_action", "Missing", new Vector2(90f, 28f), Color.white);
            var registry = CreateRegistry();
            var unit = CreateUnit();
            var clicked = false;
            UnitView clickedUnit = null;

            registry.RegisterAction(
                "visible_action",
                selected =>
                {
                    clicked = true;
                    clickedUnit = selected;
                },
                "Visible",
                _ => true);
            registry.RegisterAction("hidden_action", _ => { }, "Hidden", _ => false);

            binder.Refresh(new[] { visibleSlot, hiddenSlot, missingSlot }, registry, unit);

            Assert.That(visibleSlot.button.gameObject.activeSelf, Is.True);
            Assert.That(visibleSlot.label.text, Is.EqualTo("Visible"));
            visibleSlot.button.onClick.Invoke();
            Assert.That(clicked, Is.True);
            Assert.That(clickedUnit, Is.SameAs(unit));
            Assert.That(hiddenSlot.button.gameObject.activeSelf, Is.False);
            Assert.That(missingSlot.button.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void Refresh_ShouldReplaceOldListenersInsteadOfAccumulatingCallbacks()
        {
            var binder = new UnitInfoActionListBinder();
            var root = CreateRoot();
            var slot = binder.BuildDefaultButtonSlot(root, "visible_action", "Old", new Vector2(90f, 28f), Color.white);
            var registry = CreateRegistry();
            var unit = CreateUnit();
            var staleClickCount = 0;
            var currentClickCount = 0;

            slot.button.onClick.AddListener(() => staleClickCount++);
            registry.RegisterAction("visible_action", _ => currentClickCount++, "Visible", _ => true);

            binder.Refresh(new[] { slot }, registry, unit);
            binder.Refresh(new[] { slot }, registry, unit);
            slot.button.onClick.Invoke();

            Assert.That(staleClickCount, Is.Zero);
            Assert.That(currentClickCount, Is.EqualTo(1));
        }

        [Test]
        public void Refresh_ShouldEnsureInactiveActionProvidersRegistered()
        {
            var binder = new UnitInfoActionListBinder();
            var root = CreateRoot();
            _providerRoot = new GameObject("Provider", typeof(TestActionProvider));
            _providerRoot.SetActive(false);
            var slot = binder.BuildDefaultButtonSlot(root, "provided_action", "Old", new Vector2(90f, 28f), Color.white);
            var registry = CreateRegistry();
            var unit = CreateUnit();

            binder.Refresh(new[] { slot }, registry, unit);

            Assert.That(slot.button.gameObject.activeSelf, Is.True);
            Assert.That(slot.label.text, Is.EqualTo("Provided"));
        }

        private RectTransform CreateRoot()
        {
            _root = new GameObject("ActionRoot", typeof(RectTransform));
            return _root.GetComponent<RectTransform>();
        }

        private UnitInfoActionRegistry CreateRegistry()
        {
            _registryRoot = new GameObject("ActionRegistry", typeof(UnitInfoActionRegistry));
            return _registryRoot.GetComponent<UnitInfoActionRegistry>();
        }

        private UnitView CreateUnit()
        {
            _unitRoot = new GameObject("Unit", typeof(UnitView));
            var unit = _unitRoot.GetComponent<UnitView>();
            unit.Bind(
                new UnitDto
                {
                    Id = "unit-1",
                    Owner = "player-1",
                    Type = "settler",
                    Hp = 10,
                    MaxHp = 10
                },
                Vector3.zero);
            return unit;
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        private sealed class TestActionProvider : UnitInfoActionProviderBase
        {
            protected override void RegisterActions(UnitInfoActionRegistry registry)
            {
                registry.RegisterAction("provided_action", _ => { }, "Provided", _ => true);
            }
        }
    }
}
