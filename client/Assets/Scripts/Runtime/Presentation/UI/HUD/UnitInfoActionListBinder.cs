using System;
using Panoptes.Presentation.Map;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Presentation.UI.HUD
{
    [Serializable]
    public class UnitInfoActionButtonSlot
    {
        public string actionId;
        public Button button;
        public TMP_Text label;
    }

    public sealed class UnitInfoActionListBinder
    {
        private static readonly (string ActionId, string Label)[] DefaultSlots =
        {
            ("settle_city", "坐城"),
            ("action_2", "Action2"),
            ("action_3", "Action3"),
            ("action_4", "Action4")
        };

        private static readonly (string ActionId, string Label)[] RequiredSlots =
        {
            ("expand_territory", "Expand"),
            ("action_2", "Action2"),
            ("action_3", "Action3"),
            ("action_4", "Action4"),
            ("open_recipe_synthesis", "Synthesis")
        };

        private static Sprite _fallbackButtonSprite;
        private static Texture2D _fallbackButtonTexture;

        public UnitInfoActionButtonSlot[] EnsureDefaultSlots(
            UnitInfoActionButtonSlot[] slots,
            RectTransform root,
            Vector2 buttonSize,
            Color buttonColor)
        {
            return EnsureDefaultSlots(
                slots,
                root,
                buttonSize,
                buttonColor,
                () => new UnitInfoActionButtonSlot());
        }

        public TSlot[] EnsureDefaultSlots<TSlot>(
            TSlot[] slots,
            RectTransform root,
            Vector2 buttonSize,
            Color buttonColor,
            Func<TSlot> createSlot)
            where TSlot : UnitInfoActionButtonSlot
        {
            if (slots != null && slots.Length > 0)
            {
                return slots;
            }

            if (root == null || createSlot == null)
            {
                return slots;
            }

            var created = new TSlot[DefaultSlots.Length];
            for (var i = 0; i < DefaultSlots.Length; i++)
            {
                var slot = DefaultSlots[i];
                created[i] = BuildDefaultButtonSlot(root, slot.ActionId, slot.Label, buttonSize, buttonColor, createSlot);
            }

            return created;
        }

        public UnitInfoActionButtonSlot[] EnsureRequiredSlots(
            UnitInfoActionButtonSlot[] slots,
            RectTransform root,
            Vector2 buttonSize,
            Color buttonColor)
        {
            return EnsureRequiredSlots(
                slots,
                root,
                buttonSize,
                buttonColor,
                () => new UnitInfoActionButtonSlot());
        }

        public TSlot[] EnsureRequiredSlots<TSlot>(
            TSlot[] slots,
            RectTransform root,
            Vector2 buttonSize,
            Color buttonColor,
            Func<TSlot> createSlot)
            where TSlot : UnitInfoActionButtonSlot
        {
            for (var i = 0; i < RequiredSlots.Length; i++)
            {
                var slot = RequiredSlots[i];
                slots = EnsureSlot(slots, root, slot.ActionId, slot.Label, buttonSize, buttonColor, createSlot);
            }

            return slots;
        }

        public UnitInfoActionButtonSlot BuildDefaultButtonSlot(
            RectTransform root,
            string actionId,
            string defaultLabel,
            Vector2 buttonSize,
            Color buttonColor)
        {
            return BuildDefaultButtonSlot(
                root,
                actionId,
                defaultLabel,
                buttonSize,
                buttonColor,
                () => new UnitInfoActionButtonSlot());
        }

        public TSlot BuildDefaultButtonSlot<TSlot>(
            RectTransform root,
            string actionId,
            string defaultLabel,
            Vector2 buttonSize,
            Color buttonColor,
            Func<TSlot> createSlot)
            where TSlot : UnitInfoActionButtonSlot
        {
            if (root == null || string.IsNullOrWhiteSpace(actionId))
            {
                return null;
            }

            var slot = createSlot?.Invoke();
            if (slot == null)
            {
                return null;
            }

            var buttonGO = new GameObject($"Btn_{actionId}", typeof(RectTransform), typeof(Image), typeof(Button));
            var buttonRT = buttonGO.GetComponent<RectTransform>();
            buttonRT.SetParent(root, false);
            buttonRT.sizeDelta = buttonSize;

            var image = buttonGO.GetComponent<Image>();
            image.color = buttonColor;
            if (image.sprite == null)
            {
                image.sprite = GetFallbackButtonSprite();
            }

            var labelRT = new GameObject("Label", typeof(RectTransform)).GetComponent<RectTransform>();
            labelRT.SetParent(buttonRT, false);
            UnitInfoPanelLayoutBuilder.StretchToParent(labelRT, new Vector2(4f, 2f), new Vector2(-4f, -2f));
            var labelText = UnitInfoPanelLayoutBuilder.CreateTmpText(labelRT, defaultLabel);
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.fontSize = 15f;

            slot.actionId = actionId;
            slot.button = buttonGO.GetComponent<Button>();
            slot.label = labelText;
            return slot;
        }

        public void RepairLayoutAndVisuals(
            UnitInfoActionButtonSlot[] slots,
            RectTransform root,
            Vector2 buttonSize,
            Color buttonColor)
        {
            if (slots == null || slots.Length == 0)
            {
                return;
            }

            if (root != null)
            {
                var rootLayout = root.GetComponent<HorizontalLayoutGroup>();
                if (rootLayout == null)
                {
                    rootLayout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
                }

                rootLayout.spacing = 6f;
                rootLayout.childControlWidth = true;
                rootLayout.childControlHeight = true;
                rootLayout.childForceExpandWidth = false;
                rootLayout.childForceExpandHeight = false;

                var rootFitter = root.GetComponent<ContentSizeFitter>();
                if (rootFitter == null)
                {
                    rootFitter = root.gameObject.AddComponent<ContentSizeFitter>();
                }

                rootFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                rootFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                if (root.sizeDelta.x < 8f || root.sizeDelta.y < 8f)
                {
                    root.sizeDelta = new Vector2(190f, 30f);
                }
            }

            var fallbackSprite = GetFallbackButtonSprite();
            for (var i = 0; i < slots.Length; i++)
            {
                RepairSlotVisual(slots[i], buttonSize, buttonColor, fallbackSprite);
            }
        }

        public void Refresh(UnitInfoActionButtonSlot[] slots, UnitInfoActionRegistry registry, UnitView currentUnit)
        {
            EnsureActionProvidersRegistered();

            if (slots == null || slots.Length == 0)
            {
                return;
            }

            for (var i = 0; i < slots.Length; i++)
            {
                RefreshSlot(slots[i], registry, currentUnit);
            }
        }

        public static Sprite GetFallbackButtonSprite()
        {
            if (_fallbackButtonSprite != null)
            {
                return _fallbackButtonSprite;
            }

            if (_fallbackButtonTexture == null)
            {
                _fallbackButtonTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = "UnitInfoButtonFallbackTex",
                    hideFlags = HideFlags.DontSave
                };
                var pixels = new[]
                {
                    Color.white, Color.white,
                    Color.white, Color.white
                };
                _fallbackButtonTexture.SetPixels(pixels);
                _fallbackButtonTexture.Apply(false, true);
            }

            _fallbackButtonSprite = Sprite.Create(
                _fallbackButtonTexture,
                new Rect(0f, 0f, _fallbackButtonTexture.width, _fallbackButtonTexture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            _fallbackButtonSprite.name = "UnitInfoButtonFallbackSprite";
            return _fallbackButtonSprite;
        }

        private TSlot[] EnsureSlot<TSlot>(
            TSlot[] slots,
            RectTransform root,
            string actionId,
            string defaultLabel,
            Vector2 buttonSize,
            Color buttonColor,
            Func<TSlot> createSlot)
            where TSlot : UnitInfoActionButtonSlot
        {
            if (root == null || string.IsNullOrWhiteSpace(actionId) || createSlot == null || ContainsAction(slots, actionId))
            {
                return slots;
            }

            var newSlot = BuildDefaultButtonSlot(root, actionId, defaultLabel, buttonSize, buttonColor, createSlot);
            if (newSlot == null)
            {
                return slots;
            }

            if (slots == null || slots.Length == 0)
            {
                return new[] { newSlot };
            }

            var expanded = new TSlot[slots.Length + 1];
            Array.Copy(slots, expanded, slots.Length);
            expanded[slots.Length] = newSlot;
            return expanded;
        }

        private static bool ContainsAction(UnitInfoActionButtonSlot[] slots, string actionId)
        {
            if (slots == null)
            {
                return false;
            }

            var normalizedActionId = NormalizeToken(actionId);
            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot == null)
                {
                    continue;
                }

                if (string.Equals(NormalizeToken(slot.actionId), normalizedActionId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void RepairSlotVisual(
            UnitInfoActionButtonSlot slot,
            Vector2 buttonSize,
            Color buttonColor,
            Sprite fallbackSprite)
        {
            if (slot == null || slot.button == null)
            {
                return;
            }

            if (slot.button.transform is RectTransform rect &&
                (rect.sizeDelta.x < 8f || rect.sizeDelta.y < 8f))
            {
                rect.sizeDelta = buttonSize;
            }

            var layoutElement = slot.button.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = slot.button.gameObject.AddComponent<LayoutElement>();
            }
            layoutElement.preferredWidth = buttonSize.x;
            layoutElement.preferredHeight = buttonSize.y;
            layoutElement.minWidth = buttonSize.x;
            layoutElement.minHeight = buttonSize.y;
            layoutElement.flexibleWidth = 0f;

            var image = slot.button.GetComponent<Image>();
            if (image != null)
            {
                if (image.sprite == null)
                {
                    image.sprite = fallbackSprite;
                }
                image.type = Image.Type.Sliced;
                if (image.color.a <= 0.01f)
                {
                    image.color = buttonColor;
                }
            }

            if (slot.label != null && slot.label.font == null && TMP_Settings.defaultFontAsset != null)
            {
                slot.label.font = TMP_Settings.defaultFontAsset;
            }
        }

        private static void RefreshSlot(
            UnitInfoActionButtonSlot slot,
            UnitInfoActionRegistry registry,
            UnitView currentUnit)
        {
            if (slot == null || slot.button == null)
            {
                return;
            }

            slot.button.onClick.RemoveAllListeners();

            if (registry == null || currentUnit == null || string.IsNullOrWhiteSpace(slot.actionId))
            {
                slot.button.gameObject.SetActive(false);
                return;
            }

            if (!registry.TryResolve(slot.actionId, currentUnit, out var handler, out var label, out var visible) ||
                handler == null ||
                !visible)
            {
                slot.button.gameObject.SetActive(false);
                return;
            }

            var buttonHandler = handler;
            var boundUnit = currentUnit;
            slot.button.onClick.AddListener(() => buttonHandler(boundUnit));

            if (slot.label != null)
            {
                slot.label.text = string.IsNullOrWhiteSpace(label) ? slot.actionId : label;
            }

            slot.button.gameObject.SetActive(true);
        }

        private static void EnsureActionProvidersRegistered()
        {
            var providers = UnityEngine.Object.FindObjectsByType<UnitInfoActionProviderBase>(FindObjectsInactive.Include);
            if (providers == null || providers.Length == 0)
            {
                return;
            }

            for (var i = 0; i < providers.Length; i++)
            {
                var provider = providers[i];
                if (provider == null)
                {
                    continue;
                }

                provider.EnsureRegistered();
            }
        }

        private static string NormalizeToken(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }
    }
}
