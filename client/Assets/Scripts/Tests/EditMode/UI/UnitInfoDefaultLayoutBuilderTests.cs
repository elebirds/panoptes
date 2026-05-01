using NUnit.Framework;
using Panoptes.Presentation.UI.HUD;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Tests.EditMode.UI
{
    public sealed class UnitInfoDefaultLayoutBuilderTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root.transform.root.gameObject);
            }
        }

        [Test]
        public void EnsureLayout_ShouldCreateDefaultCanvasAndExpectedPanelControls()
        {
            var panelRoot = CreateRoot();
            var builder = new UnitInfoDefaultLayoutBuilder();

            var result = builder.EnsureLayout(
                new UnitInfoDefaultLayoutBuilder.References<TestActionButtonSlot>
                {
                    PanelRoot = panelRoot
                },
                new Vector2(90f, 28f),
                new Color(0.2f, 0.45f, 0.8f, 0.92f),
                new UnitInfoActionListBinder(),
                new UnitInfoDirectOrderPanelBinder(),
                () => new TestActionButtonSlot());

            var canvas = panelRoot.GetComponentInParent<Canvas>();
            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.name, Is.EqualTo("HUDCanvas"));
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(canvas.GetComponent<CanvasScaler>().referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));

            Assert.That(panelRoot.anchorMin, Is.EqualTo(new Vector2(1f, 0f)));
            Assert.That(panelRoot.anchorMax, Is.EqualTo(new Vector2(1f, 0f)));
            Assert.That(panelRoot.pivot, Is.EqualTo(new Vector2(1f, 0f)));
            Assert.That(panelRoot.sizeDelta, Is.EqualTo(new Vector2(420f, 280f)));

            Assert.That(result.PanelBackground, Is.Not.Null);
            Assert.That(result.UnitIcon, Is.Not.Null);
            Assert.That(result.UnitPortraitRawImage, Is.Not.Null);
            Assert.That(result.UnitNameText.text, Is.EqualTo("Unit"));
            Assert.That(result.UnitDescriptionText.text, Is.EqualTo(string.Empty));
            Assert.That(result.PlanningSummaryText.gameObject.activeSelf, Is.False);
            Assert.That(result.HpSlider, Is.Not.Null);
            Assert.That(result.HpSlider.interactable, Is.False);
            Assert.That(result.HpValueText.text, Is.EqualTo("0/0"));
            Assert.That(result.ActionButtonsRoot, Is.Not.Null);
            Assert.That(result.DirectOrderButtonsRoot, Is.Not.Null);
        }

        [Test]
        public void EnsureLayout_ShouldCreateDefaultActionSlotsAndDirectOrderButtons()
        {
            var panelRoot = CreateRoot();
            var buttonColor = new Color(0.2f, 0.45f, 0.8f, 0.92f);
            var builder = new UnitInfoDefaultLayoutBuilder();

            var result = builder.EnsureLayout(
                new UnitInfoDefaultLayoutBuilder.References<TestActionButtonSlot>
                {
                    PanelRoot = panelRoot
                },
                new Vector2(90f, 28f),
                buttonColor,
                new UnitInfoActionListBinder(),
                new UnitInfoDirectOrderPanelBinder(),
                () => new TestActionButtonSlot());

            Assert.That(result.ActionButtons, Has.Length.EqualTo(4));
            Assert.That(result.ActionButtons[0].actionId, Is.EqualTo("settle_city"));
            Assert.That(result.ActionButtons[0].label.text, Is.EqualTo("坐城"));
            AssertDirectOrderButton(result.MoveButton, "MoveButton", "移动", buttonColor);
            AssertDirectOrderButton(result.AttackButton, "AttackButton", "攻击", buttonColor);
            AssertDirectOrderButton(result.HoldButton, "HoldButton", "待命", buttonColor);
            AssertDirectOrderButton(result.ChargeButton, "ChargeButton", "冲锋", buttonColor);
        }

        [Test]
        public void EnsureLayout_ShouldPreservePrefabReferencesAndInitializeOptionalControls()
        {
            var panelRoot = CreateRoot();
            var background = CreateChild<Image>(panelRoot, "Background");
            var icon = CreateChild<Image>(panelRoot, "UnitIcon");
            var portrait = CreateChild<RawImage>(panelRoot, "UnitPortrait");
            var name = CreateChild<TextMeshProUGUI>(panelRoot, "UnitName");
            var hpSlider = CreateChild<Slider>(panelRoot, "HpBar");
            var hpText = CreateChild<TextMeshProUGUI>(panelRoot, "HpText");
            var actionRoot = CreateChildRect(panelRoot, "ActionButtons");
            var directRoot = CreateChildRect(panelRoot, "DirectOrderButtons");
            var actionSlots = new[] { new TestActionButtonSlot() };
            var move = CreateChild<Button>(directRoot, "MoveButton");
            var attack = CreateChild<Button>(directRoot, "AttackButton");
            var hold = CreateChild<Button>(directRoot, "HoldButton");
            var charge = CreateChild<Button>(directRoot, "ChargeButton");
            var builder = new UnitInfoDefaultLayoutBuilder();

            icon.rectTransform.anchorMin = new Vector2(0.2f, 0.3f);
            icon.rectTransform.anchorMax = new Vector2(0.2f, 0.3f);
            icon.rectTransform.pivot = new Vector2(0.4f, 0.5f);
            icon.rectTransform.anchoredPosition = new Vector2(31f, 41f);
            icon.rectTransform.sizeDelta = new Vector2(71f, 73f);
            portrait.raycastTarget = true;
            portrait.color = Color.black;

            var result = builder.EnsureLayout(
                new UnitInfoDefaultLayoutBuilder.References<TestActionButtonSlot>
                {
                    PanelRoot = panelRoot,
                    PanelBackground = background,
                    UnitIcon = icon,
                    UnitPortraitRawImage = portrait,
                    UnitNameText = name,
                    HpSlider = hpSlider,
                    HpValueText = hpText,
                    ActionButtonsRoot = actionRoot,
                    DirectOrderButtonsRoot = directRoot,
                    ActionButtons = actionSlots,
                    MoveButton = move,
                    AttackButton = attack,
                    HoldButton = hold,
                    ChargeButton = charge
                },
                new Vector2(90f, 28f),
                Color.blue,
                new UnitInfoActionListBinder(),
                new UnitInfoDirectOrderPanelBinder(),
                () => new TestActionButtonSlot());

            Assert.That(result.PanelBackground, Is.SameAs(background));
            Assert.That(result.UnitIcon, Is.SameAs(icon));
            Assert.That(result.UnitPortraitRawImage, Is.SameAs(portrait));
            Assert.That(result.UnitNameText, Is.SameAs(name));
            Assert.That(result.HpSlider, Is.SameAs(hpSlider));
            Assert.That(result.HpValueText, Is.SameAs(hpText));
            Assert.That(result.ActionButtonsRoot, Is.SameAs(actionRoot));
            Assert.That(result.DirectOrderButtonsRoot, Is.SameAs(directRoot));
            Assert.That(result.ActionButtons, Is.SameAs(actionSlots));
            Assert.That(result.MoveButton, Is.SameAs(move));
            Assert.That(result.AttackButton, Is.SameAs(attack));
            Assert.That(result.HoldButton, Is.SameAs(hold));
            Assert.That(result.ChargeButton, Is.SameAs(charge));
            Assert.That(result.UnitDescriptionText.text, Is.EqualTo(string.Empty));
            Assert.That(result.PlanningSummaryText.gameObject.activeSelf, Is.False);
            Assert.That(portrait.raycastTarget, Is.False);
            Assert.That(portrait.color, Is.EqualTo(Color.white));
            Assert.That(portrait.rectTransform.anchoredPosition, Is.EqualTo(icon.rectTransform.anchoredPosition));
            Assert.That(portrait.rectTransform.sizeDelta, Is.EqualTo(icon.rectTransform.sizeDelta));
        }

        [Test]
        public void EnsurePortraitRawImage_ShouldMirrorUnitIconLayout()
        {
            var panelRoot = CreateRoot();
            var iconRect = new GameObject("UnitIcon", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            iconRect.SetParent(panelRoot, false);
            iconRect.anchorMin = new Vector2(0.1f, 0.2f);
            iconRect.anchorMax = new Vector2(0.1f, 0.2f);
            iconRect.pivot = new Vector2(0.3f, 0.4f);
            iconRect.anchoredPosition = new Vector2(25f, 35f);
            iconRect.sizeDelta = new Vector2(64f, 66f);
            var icon = iconRect.GetComponent<Image>();

            var portrait = UnitInfoDefaultLayoutBuilder.EnsurePortraitRawImage(panelRoot, icon, null);

            Assert.That(portrait, Is.Not.Null);
            Assert.That(portrait.raycastTarget, Is.False);
            Assert.That(portrait.color, Is.EqualTo(Color.white));
            Assert.That(portrait.rectTransform.anchorMin, Is.EqualTo(iconRect.anchorMin));
            Assert.That(portrait.rectTransform.anchorMax, Is.EqualTo(iconRect.anchorMax));
            Assert.That(portrait.rectTransform.pivot, Is.EqualTo(iconRect.pivot));
            Assert.That(portrait.rectTransform.anchoredPosition, Is.EqualTo(iconRect.anchoredPosition));
            Assert.That(portrait.rectTransform.sizeDelta, Is.EqualTo(iconRect.sizeDelta));
            Assert.That(portrait.rectTransform.GetSiblingIndex(), Is.EqualTo(iconRect.GetSiblingIndex() + 1));
        }

        private RectTransform CreateRoot()
        {
            _root = new GameObject("UnitInfoPanel", typeof(RectTransform));
            return _root.GetComponent<RectTransform>();
        }

        private static RectTransform CreateChildRect(Transform parent, string objectName)
        {
            var rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static TComponent CreateChild<TComponent>(Transform parent, string objectName)
            where TComponent : Component
        {
            var rect = CreateChildRect(parent, objectName);
            return rect.gameObject.AddComponent<TComponent>();
        }

        private static void AssertDirectOrderButton(Button button, string objectName, string label, Color expectedColor)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(button.name, Is.EqualTo(objectName));
            Assert.That(button.GetComponent<Image>().color, Is.EqualTo(expectedColor));
            Assert.That(button.GetComponentInChildren<TextMeshProUGUI>().text, Is.EqualTo(label));
        }

        private sealed class TestActionButtonSlot : UnitInfoActionButtonSlot
        {
        }
    }
}
