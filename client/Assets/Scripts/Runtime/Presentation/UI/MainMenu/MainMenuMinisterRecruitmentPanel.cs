using System;
using Panoptes.Presentation.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Panoptes.Presentation.UI.MainMenu
{
    public sealed class MainMenuMinisterRecruitmentPanel : MonoBehaviour
    {
        private const string AssetRoot = "Icons/MinisterGacha/";
        private const string PortraitAsset = AssetRoot + "zhuge_liang_feature";
        private const string EntryAsset = AssetRoot + "minister_visit_button";
        private const string StageAsset = AssetRoot + "gacha_stage_bg";

        private static readonly Color Gold = new(0.95f, 0.67f, 0.28f, 1f);
        private static readonly Color PaleGold = new(1f, 0.88f, 0.58f, 1f);
        private static readonly Color Ink = new(0.025f, 0.028f, 0.035f, 1f);
        private static readonly Color DeepJade = new(0.03f, 0.22f, 0.20f, 0.96f);

        private RectTransform _entryRoot;
        private RectTransform _panelRoot;
        private RectTransform _characterRoot;
        private RectTransform _effectRoot;
        private CanvasGroup _panelGroup;
        private TextMeshProUGUI _statusText;
        private GameObject _mainMenuPanel;
        private GameObject _loginPanel;
        private MainMenuController _mainMenuController;
        private PresentationAudioService _audioService;
        private float _entranceTime;
        private float _drawPulse;
        private bool _built;

        private void Awake()
        {
            EnsureBuilt();
        }

        private void OnEnable()
        {
            EnsureBuilt();
            RefreshEntryVisibility();
        }

        public void UseAudioService(PresentationAudioService audioService)
        {
            _audioService = audioService;
        }

        private void Update()
        {
            if (!_built)
            {
                return;
            }

            RefreshEntryVisibility();
            AnimateEntrance();
            AnimateDrawPulse();
        }

        private void EnsureBuilt()
        {
            if (_built)
            {
                return;
            }

            _built = true;
            _mainMenuController = GetComponent<MainMenuController>();
            _mainMenuPanel = FindDirectChild(transform, "MainMenuPanel")?.gameObject;
            _loginPanel = FindDirectChild(transform, "LoginPanel")?.gameObject;
            BuildEntryButton();
            BuildRecruitmentPanel();
            _panelRoot.gameObject.SetActive(false);
        }

        private void BuildEntryButton()
        {
            _entryRoot = CreateUiObject("MinisterRecruitmentEntry", transform);
            AnchorFixed(_entryRoot, Vector2.zero, new Vector2(108f, 104f), new Vector2(150f, 172f));
            _entryRoot.SetAsLastSibling();

            var button = _entryRoot.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;

            var iconMask = CreateUiObject("IconMask", _entryRoot);
            AnchorFixed(iconMask, new Vector2(0.5f, 1f), new Vector2(0f, -64f), new Vector2(126f, 126f));
            var maskImage = iconMask.gameObject.AddComponent<Image>();
            maskImage.sprite = CreateCircleSprite("MinisterVisitCircleMask", 128, new Color(1f, 1f, 1f, 1f), 0f, Color.clear);
            maskImage.raycastTarget = true;
            var mask = iconMask.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            button.targetGraphic = maskImage;

            var icon = CreateImage(iconMask, "Icon", LoadSprite(EntryAsset), Color.white, true);
            Stretch(icon.rectTransform, new Vector2(-5f, -5f), new Vector2(5f, 5f));

            var glow = CreateImage(_entryRoot, "GoldGlow", CreateRingSprite("MinisterVisitEntryRing", 160, 9f, Gold), Color.white, false);
            AnchorFixed(glow.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -64f), new Vector2(140f, 140f));
            glow.transform.SetAsLastSibling();

            var labelBack = CreateImage(_entryRoot, "LabelBack", CreatePanelSprite("MinisterVisitEntryLabel", 128, 42, new Color(0.04f, 0.04f, 0.045f, 0.92f), Gold), Color.white, false);
            AnchorFixed(labelBack.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 21f), new Vector2(128f, 42f));

            var label = CreateText(labelBack.rectTransform, "Label", "\u5927\u81e3\u5bfb\u8bbf", 22f, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(label.rectTransform, Vector2.zero, Vector2.zero);
            label.color = PaleGold;

            button.onClick.AddListener(OpenPanel);
        }

        private void BuildRecruitmentPanel()
        {
            _panelRoot = CreateUiObject("MinisterRecruitmentPanel", transform);
            Stretch(_panelRoot, Vector2.zero, Vector2.zero);
            _panelRoot.SetAsLastSibling();
            _panelGroup = _panelRoot.gameObject.AddComponent<CanvasGroup>();
            _panelGroup.alpha = 0f;
            _panelGroup.blocksRaycasts = true;

            var shade = _panelRoot.gameObject.AddComponent<Image>();
            shade.color = Ink;
            shade.raycastTarget = true;

            var stage = CreateImage(_panelRoot, "StageBackground", LoadSprite(StageAsset), Color.white, false);
            Stretch(stage.rectTransform, Vector2.zero, Vector2.zero);
            stage.type = Image.Type.Simple;
            stage.preserveAspect = false;

            var vignette = CreateImage(_panelRoot, "Vignette", CreateVignetteSprite(), Color.white, false);
            Stretch(vignette.rectTransform, Vector2.zero, Vector2.zero);

            BuildCharacterArea();
            BuildInfoArea();
            BuildActionArea();
            BuildExitButton();
        }

        private void BuildCharacterArea()
        {
            _characterRoot = CreateUiObject("ZhugeLiangCharacter", _panelRoot);
            Anchor(_characterRoot, new Vector2(0.02f, 0f), new Vector2(0.53f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f));

            _effectRoot = CreateUiObject("EntranceEffect", _characterRoot);
            Stretch(_effectRoot, Vector2.zero, Vector2.zero);

            var halo = CreateImage(_effectRoot, "StrategistHalo", CreateRingSprite("StrategistHaloSprite", 420, 8f, new Color(0.96f, 0.78f, 0.32f, 0.72f)), Color.white, false);
            AnchorFixed(halo.rectTransform, new Vector2(0.5f, 0.48f), new Vector2(10f, 0f), new Vector2(470f, 470f));

            for (var i = 0; i < 8; i++)
            {
                var ray = CreateImage(_effectRoot, "GoldenRay" + i, CreateRaySprite("MinisterRecruitmentRay" + i), new Color(1f, 0.78f, 0.32f, 0.35f), false);
                AnchorFixed(ray.rectTransform, new Vector2(0.52f, 0.52f), Vector2.zero, new Vector2(34f, 500f));
                ray.rectTransform.localEulerAngles = new Vector3(0f, 0f, i * 45f);
            }

            var portrait = CreateImage(_characterRoot, "Portrait", LoadSprite(PortraitAsset), Color.white, false);
            Anchor(portrait.rectTransform, new Vector2(0.08f, 0.02f), new Vector2(0.90f, 0.98f), Vector2.zero, Vector2.zero);
            portrait.preserveAspect = true;

            var stats = CreateImage(_characterRoot, "StatsPlate", CreatePanelSprite("MinisterRecruitmentStatsPlate", 420, 150, new Color(0.028f, 0.075f, 0.08f, 0.90f), Gold), Color.white, false);
            AnchorFixed(stats.rectTransform, new Vector2(0.5f, 0.10f), new Vector2(-16f, 0f), new Vector2(430f, 152f));

            var office = CreateText(stats.rectTransform, "Office", "\u804c\u4f4d  \u5185\u653f\u5927\u81e3", 23f, FontStyles.Bold, TextAlignmentOptions.Left);
            Anchor(office.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(24f, -46f), new Vector2(-24f, -14f));
            office.color = PaleGold;

            var attributes = "\u653f\u52a1 99    \u5fe0\u8bda 96    \u8c0b\u7565 100\n\u8c28\u614e 94    \u679c\u65ad 92    \u6c11\u5fc3 98";
            var attrText = CreateText(stats.rectTransform, "Attributes", attributes, 20f, FontStyles.Bold, TextAlignmentOptions.Left);
            Anchor(attrText.rectTransform, Vector2.zero, Vector2.one, new Vector2(24f, 14f), new Vector2(-24f, -55f));
            attrText.color = new Color(0.92f, 1f, 0.93f, 1f);
            attrText.textWrappingMode = TextWrappingModes.Normal;
        }

        private void BuildInfoArea()
        {
            var infoRoot = CreateUiObject("PoolInfo", _panelRoot);
            Anchor(infoRoot, new Vector2(0.55f, 0.20f), new Vector2(0.96f, 0.88f), Vector2.zero, Vector2.zero);

            var tag = CreateText(infoRoot, "PoolTag", "\u9650\u65f6\u5927\u81e3\u5361\u6c60", 18f, FontStyles.Bold, TextAlignmentOptions.Left);
            Anchor(tag.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -30f), new Vector2(0f, -2f));
            tag.color = new Color(0.72f, 0.96f, 0.86f, 1f);

            var title = CreateText(infoRoot, "PoolName", "\u4e09\u987e\u8305\u5e90", 62f, FontStyles.Bold, TextAlignmentOptions.Left);
            Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -98f), new Vector2(0f, -32f));
            title.color = PaleGold;
            title.enableAutoSizing = true;
            title.fontSizeMin = 38f;
            title.fontSizeMax = 58f;

            var name = CreateText(infoRoot, "CharacterName", "\u8bf8\u845b\u4eae", 36f, FontStyles.Bold, TextAlignmentOptions.Left);
            Anchor(name.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -142f), new Vector2(0f, -102f));
            name.color = Color.white;

            var descPanel = CreateImage(infoRoot, "DescriptionPanel", CreatePanelSprite("MinisterRecruitmentDescriptionPanel", 640, 310, new Color(0.025f, 0.045f, 0.055f, 0.88f), new Color(0.55f, 0.93f, 0.76f, 0.84f)), Color.white, false);
            Anchor(descPanel.rectTransform, new Vector2(0f, 0.16f), new Vector2(1f, 0.68f), Vector2.zero, Vector2.zero);

            const string description =
                "\u9686\u4e2d\u5bf9\u5929\u4e0b\uff0c\u7fbd\u6247\u5b9a\u4e7e\u5764\u3002\n" +
                "\u4ed6\u4ee5\u4e00\u7b56\u8054\u52a8\u5c71\u6cb3\u5175\u7532\uff0c\u4ee5\u4e00\u4ee4\u4f7f\u653f\u52a1\u5982\u98ce\u3001\u4e07\u6237\u5f52\u5fc3\u3002\n" +
                "\u706b\u653b\uff1a\u5584\u5bdf\u98ce\u5411\u3001\u7cae\u8349\u4e0e\u8425\u5792\u5f31\u70b9\uff0c\u6761\u4ef6\u6ee1\u8db3\u65f6\u6269\u5927\u706b\u52bf\uff0c\u8ffd\u52a0\u519b\u5fc3\u4e0e\u8865\u7ed9\u6253\u51fb\u3002\n" +
                "\u8c0b\u5b9a\u540e\u52a8\uff1a\u4e3b\u52a8\u8425\u9020\u53ef\u706b\u653b\u6761\u4ef6\uff0c\u8bf1\u654c\u5165\u4f0f\uff0c\u5f85\u4e1c\u98ce\u8d77\u800c\u4e00\u6218\u5b9a\u52bf\u3002\n" +
                "\u5386\u53f2\u52a0\u6210\uff1a\u5c6f\u7530\u3001\u6cbb\u6c34\u3001\u519b\u5c6f\u4e0e\u8fde\u5f29\u7b79\u9020\u6548\u7387\u63d0\u5347\uff0c\u57ce\u5e9c\u6cbb\u7406\u548c\u8fdc\u5f81\u8865\u7ed9\u66f4\u7a33\u3002";
            var desc = CreateText(descPanel.rectTransform, "Description", description, 18f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Stretch(desc.rectTransform, new Vector2(24f, 18f), new Vector2(-24f, -18f));
            desc.color = new Color(0.98f, 0.94f, 0.84f, 1f);
            desc.textWrappingMode = TextWrappingModes.Normal;
            desc.overflowMode = TextOverflowModes.Ellipsis;

            _statusText = CreateText(infoRoot, "DrawStatus", "\u5929\u4e0b\u826f\u81e3\uff0c\u5f85\u541b\u4e09\u987e\u3002", 22f, FontStyles.Bold, TextAlignmentOptions.Left);
            Anchor(_statusText.rectTransform, new Vector2(0f, 0.02f), new Vector2(1f, 0.14f), Vector2.zero, Vector2.zero);
            _statusText.color = new Color(0.72f, 0.96f, 0.86f, 1f);
        }

        private void BuildActionArea()
        {
            var actionBar = CreateUiObject("ActionBar", _panelRoot);
            Anchor(actionBar, new Vector2(0.50f, 0.025f), new Vector2(0.94f, 0.145f), Vector2.zero, Vector2.zero);

            var backing = CreateImage(actionBar, "ActionBarBacking", CreatePanelSprite("MinisterRecruitmentActionBar", 760, 104, new Color(0.065f, 0.055f, 0.045f, 0.78f), new Color(0.87f, 0.55f, 0.20f, 0.82f)), Color.white, false);
            Stretch(backing.rectTransform, Vector2.zero, Vector2.zero);

            var single = CreateActionButton(actionBar, "SingleDrawButton", "\u5355\u6b21\u5bfb\u8bbf", new Vector2(0.33f, 0.50f), new Vector2(250f, 70f));
            single.onClick.AddListener(() => PlayDrawPulse(1));

            var ten = CreateActionButton(actionBar, "TenDrawButton", "\u5341\u8fde\u5bfb\u8bbf", new Vector2(0.76f, 0.50f), new Vector2(270f, 70f));
            ten.onClick.AddListener(() => PlayDrawPulse(10));
        }

        private void BuildExitButton()
        {
            var button = CreateActionButton(_panelRoot, "BackToMainMenuButton", "\u8fd4\u56de", new Vector2(0.94f, 0.94f), new Vector2(124f, 54f));
            button.transform.SetAsLastSibling();
            button.onClick.AddListener(ClosePanel);
            button.gameObject.AddComponent<CloseButtonRelay>().Initialize(this);
        }

        private Button CreateActionButton(RectTransform parent, string name, string label, Vector2 anchor, Vector2 size)
        {
            var rect = CreateUiObject(name, parent);
            AnchorFixed(rect, anchor, Vector2.zero, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = CreatePanelSprite(name + "Sprite", 256, 86, DeepJade, Gold);
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.02f, 0.76f, 1f);
            colors.pressedColor = new Color(0.72f, 0.42f, 0.18f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var text = CreateText(rect, "Label", label, 27f, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(text.rectTransform, Vector2.zero, Vector2.zero);
            text.color = PaleGold;
            return button;
        }

        private void OpenPanel()
        {
            _audioService?.PlayMinisterRecruitmentBgm();
            _panelRoot.gameObject.SetActive(true);
            _panelRoot.SetAsLastSibling();
            _panelGroup.alpha = 0f;
            _panelGroup.interactable = true;
            _panelGroup.blocksRaycasts = true;
            _entranceTime = 0f;
            _drawPulse = 0f;
            if (_characterRoot != null)
            {
                _characterRoot.localScale = Vector3.one * 0.94f;
                _characterRoot.anchoredPosition = new Vector2(-80f, 0f);
            }

            SetMainMenuSurface(false);
            RefreshEntryVisibility();
        }

        public void ClosePanel()
        {
            _audioService?.PlayMainMenuBgm();
            _panelRoot.gameObject.SetActive(false);
            _panelGroup.interactable = false;
            _panelGroup.blocksRaycasts = false;
            SetMainMenuSurface(true);
            if (_mainMenuController != null)
            {
                _mainMenuController.ShowMainMenu();
            }

            RefreshEntryVisibility();
        }

        private sealed class CloseButtonRelay : MonoBehaviour, IPointerClickHandler
        {
            private MainMenuMinisterRecruitmentPanel _owner;

            public void Initialize(MainMenuMinisterRecruitmentPanel owner)
            {
                _owner = owner;
            }

            public void OnPointerClick(PointerEventData eventData)
            {
                _owner?.ClosePanel();
            }
        }

        private void PlayDrawPulse(int count)
        {
            _drawPulse = 1f;
            if (_statusText != null)
            {
                _statusText.text = count >= 10
                    ? "\u5341\u8fde\u7b7e\u8d77\uff0c\u5367\u9f99\u51fa\u5c71\uff1a\u8bf8\u845b\u4eae\u5df2\u5165\u5019\u9009\u540d\u518c\u3002"
                    : "\u4e00\u7b7e\u5b9a\u7b56\uff1a\u8bf8\u845b\u4eae\u5411\u4f60\u62f1\u624b\u884c\u793c\u3002";
            }
        }

        private void SetMainMenuSurface(bool active)
        {
            if (_mainMenuPanel != null)
            {
                _mainMenuPanel.SetActive(active);
            }

            if (_loginPanel != null)
            {
                _loginPanel.SetActive(active);
            }
        }

        private void RefreshEntryVisibility()
        {
            if (_entryRoot == null)
            {
                return;
            }

            var panelOpen = _panelRoot != null && _panelRoot.gameObject.activeSelf;
            var menuVisible = _mainMenuPanel == null || _mainMenuPanel.activeInHierarchy;
            _entryRoot.gameObject.SetActive(!panelOpen && menuVisible);
        }

        private void AnimateEntrance()
        {
            if (_panelRoot == null || !_panelRoot.gameObject.activeSelf)
            {
                return;
            }

            _entranceTime += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(_entranceTime / 0.72f);
            var eased = Mathf.SmoothStep(0f, 1f, t);
            if (_panelGroup != null)
            {
                _panelGroup.alpha = eased;
            }

            if (_characterRoot != null)
            {
                _characterRoot.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, eased);
                _characterRoot.anchoredPosition = Vector2.Lerp(new Vector2(-80f, 0f), Vector2.zero, eased);
            }

            if (_effectRoot != null)
            {
                _effectRoot.localEulerAngles = new Vector3(0f, 0f, _entranceTime * 18f);
                _effectRoot.localScale = Vector3.one * Mathf.Lerp(1.08f, 1f, eased);
            }
        }

        private void AnimateDrawPulse()
        {
            if (_drawPulse <= 0f || _characterRoot == null)
            {
                return;
            }

            _drawPulse = Mathf.Max(0f, _drawPulse - Time.unscaledDeltaTime * 1.6f);
            var pulse = Mathf.Sin(_drawPulse * Mathf.PI) * 0.035f;
            _characterRoot.localScale = Vector3.one * (1f + pulse);
        }

        private static Sprite LoadSprite(string resourcePath)
        {
            var texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                return null;
            }

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = texture.name + "Sprite";
            return sprite;
        }

        private static Image CreateImage(RectTransform parent, string name, Sprite sprite, Color color, bool raycastTarget)
        {
            var rect = CreateUiObject(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycastTarget;
            if (sprite != null)
            {
                image.preserveAspect = true;
            }

            return image;
        }

        private static TextMeshProUGUI CreateText(RectTransform parent, string name, string value, float fontSize, FontStyles style, TextAlignmentOptions alignment)
        {
            var rect = CreateUiObject(name, parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value ?? string.Empty;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Truncate;
            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }

            return text;
        }

        private static RectTransform CreateUiObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static Transform FindDirectChild(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == name)
                {
                    return child;
                }
            }

            return null;
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void AnchorFixed(RectTransform rect, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static Sprite CreateCircleSprite(string name, int size, Color fill, float borderWidth, Color border)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = name + "Texture";
            var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            var radius = size * 0.5f - 1f;
            var inner = Mathf.Max(0f, radius - borderWidth);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x, y), center);
                    var alpha = Mathf.Clamp01(radius + 0.5f - distance);
                    var pixel = Color.clear;
                    if (distance <= radius)
                    {
                        pixel = borderWidth > 0f && distance > inner ? border : fill;
                        pixel.a *= alpha;
                    }

                    texture.SetPixel(x, y, pixel);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite CreateRingSprite(string name, int size, float width, Color color)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = name + "Texture";
            var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            var radius = size * 0.5f - width - 2f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x, y), center);
                    var ring = Mathf.Clamp01(width - Mathf.Abs(distance - radius));
                    var pixel = color;
                    pixel.a *= ring;
                    texture.SetPixel(x, y, pixel);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite CreatePanelSprite(string name, int width, int height, Color fill, Color border)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = name + "Texture";
            var radius = Mathf.Min(18, Mathf.Min(width, height) / 4);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var inside = RoundedRectContains(x, y, width, height, radius);
                    if (!inside)
                    {
                        texture.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    var edge = x < 4 || x >= width - 4 || y < 4 || y >= height - 4;
                    var highlight = Mathf.Clamp01((float)y / Mathf.Max(1, height - 1));
                    var pixel = edge ? border : Color.Lerp(fill, fill + new Color(0.06f, 0.06f, 0.04f, 0f), highlight * 0.45f);
                    texture.SetPixel(x, y, pixel);
                }
            }

            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(24f, 24f, 24f, 24f));
            sprite.name = name;
            return sprite;
        }

        private static Sprite CreateRaySprite(string name)
        {
            const int width = 32;
            const int height = 512;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = name + "Texture";
            var center = (width - 1) * 0.5f;
            for (var y = 0; y < height; y++)
            {
                var vertical = Mathf.Sin((float)y / (height - 1) * Mathf.PI);
                for (var x = 0; x < width; x++)
                {
                    var horizontal = Mathf.Clamp01(1f - Mathf.Abs(x - center) / center);
                    texture.SetPixel(x, y, new Color(1f, 0.78f, 0.28f, vertical * horizontal * 0.65f));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateVignetteSprite()
        {
            const int width = 256;
            const int height = 144;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = "MinisterRecruitmentVignetteTexture";
            var center = new Vector2(width * 0.5f, height * 0.52f);
            var maxDistance = Vector2.Distance(Vector2.zero, center);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x, y), center) / maxDistance;
                    var alpha = Mathf.SmoothStep(0.15f, 0.92f, distance) * 0.62f;
                    texture.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static bool RoundedRectContains(int x, int y, int width, int height, int radius)
        {
            var cx = Mathf.Clamp(x, radius, width - radius - 1);
            var cy = Mathf.Clamp(y, radius, height - radius - 1);
            var dx = x - cx;
            var dy = y - cy;
            return dx * dx + dy * dy <= radius * radius;
        }
    }
}
