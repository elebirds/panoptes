using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Presentation.UI.MainMenu
{
    public sealed class MainMenuMinisterDetailsPanel : MonoBehaviour
    {
        private const int ExpectedMinisterCount = 10;
        private const string MinisterDataResourcePath = "Data/sections/ministers";
        private const string AvatarRoot = "Icons/Ministers";

        private static readonly Color PanelColor = new(0.04f, 0.03f, 0.025f, 0.97f);
        private static readonly Color HeaderColor = new(0.11f, 0.085f, 0.065f, 0.98f);
        private static readonly Color CardColor = new(0.09f, 0.075f, 0.06f, 0.94f);
        private static readonly Color CardAltColor = new(0.07f, 0.085f, 0.09f, 0.94f);
        private static readonly Color TextColor = new(0.98f, 0.9f, 0.74f, 1f);
        private static readonly Color MutedTextColor = new(0.76f, 0.74f, 0.68f, 1f);
        private static readonly Dictionary<string, Sprite> AvatarSprites = new(StringComparer.OrdinalIgnoreCase);

        private RectTransform _panelRoot;
        private RectTransform _contentRoot;
        private Button _backButton;
        private Action _backRequested;
        private bool _built;

        private void Awake()
        {
            EnsureBuilt();
        }

        private void OnEnable()
        {
            EnsureBuilt();
            RefreshList();
        }

        private void OnRectTransformDimensionsChange()
        {
            ApplyLayout();
        }

        private void OnDestroy()
        {
            if (_backButton != null)
            {
                _backButton.onClick.RemoveListener(HandleBackClicked);
            }
        }

        public void BindBack(Action backRequested)
        {
            _backRequested = backRequested;
            EnsureBuilt();
        }

        private void HandleBackClicked()
        {
            _backRequested?.Invoke();
        }

        private void EnsureBuilt()
        {
            if (_built)
            {
                return;
            }

            _built = true;
            if (!TryGetComponent<Image>(out var shade))
            {
                shade = gameObject.AddComponent<Image>();
            }

            shade.color = new Color(0.015f, 0.015f, 0.018f, 0.78f);
            shade.raycastTarget = true;

            var root = transform as RectTransform;
            if (root != null)
            {
                Stretch(root, Vector2.zero, Vector2.zero);
            }

            _panelRoot = CreateUiObject("MinisterDetailsPanelRoot", transform);
            _panelRoot.gameObject.AddComponent<Image>().color = PanelColor;
            CreateBorder(_panelRoot);

            var header = CreateUiObject("Header", _panelRoot);
            header.gameObject.AddComponent<Image>().color = HeaderColor;
            Anchor(header, new Vector2(0f, 1f), Vector2.one, Vector2.zero, new Vector2(0f, -70f));

            var title = CreateText(header, "Title", "大臣一览", 30f, FontStyles.Bold, TextAlignmentOptions.Left);
            Anchor(title.rectTransform, Vector2.zero, Vector2.one, new Vector2(28f, 0f), new Vector2(-120f, 0f));

            _backButton = CreateButton(header, "BackButton", "返回", new Vector2(1f, 0.5f), new Vector2(-54f, 0f), new Vector2(92f, 38f));
            _backButton.onClick.AddListener(HandleBackClicked);

            var scrollRoot = CreateUiObject("ScrollView", _panelRoot);
            Anchor(scrollRoot, Vector2.zero, Vector2.one, new Vector2(24f, 24f), new Vector2(-24f, -88f));
            var scrollImage = scrollRoot.gameObject.AddComponent<Image>();
            scrollImage.color = new Color(1f, 1f, 1f, 0.035f);

            var viewport = CreateUiObject("Viewport", scrollRoot);
            Stretch(viewport, Vector2.zero, Vector2.zero);
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.02f);
            var mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            _contentRoot = CreateUiObject("Content", viewport);
            _contentRoot.anchorMin = new Vector2(0f, 1f);
            _contentRoot.anchorMax = new Vector2(1f, 1f);
            _contentRoot.pivot = new Vector2(0.5f, 1f);
            _contentRoot.offsetMin = Vector2.zero;
            _contentRoot.offsetMax = Vector2.zero;

            var layout = _contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 14, 14);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = _contentRoot.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = _contentRoot;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32f;

            ApplyLayout();
            RefreshList();
        }

        private void ApplyLayout()
        {
            if (_panelRoot == null)
            {
                return;
            }

            var canvasSize = ResolveCanvasSize();
            var width = Mathf.Min(980f, Mathf.Max(640f, canvasSize.x - 160f));
            var height = Mathf.Min(760f, Mathf.Max(460f, canvasSize.y - 140f));
            AnchorFixed(_panelRoot, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height));
        }

        private void RefreshList()
        {
            if (_contentRoot == null)
            {
                return;
            }

            ClearChildren(_contentRoot);
            var ministers = LoadMinisters();
            for (var i = 0; i < ministers.Count; i++)
            {
                CreateMinisterRow(ministers[i], i);
            }
        }

        private void CreateMinisterRow(MinisterProfile profile, int index)
        {
            var row = CreateUiObject("Minister-" + SafeName(profile.Role), _contentRoot);
            var layout = row.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 132f;
            layout.preferredHeight = 132f;

            var background = row.gameObject.AddComponent<Image>();
            background.color = index % 2 == 0 ? CardColor : CardAltColor;

            var avatarRect = CreateUiObject("Avatar", row);
            AnchorFixed(avatarRect, new Vector2(0f, 0.5f), new Vector2(62f, 0f), new Vector2(94f, 94f));
            var avatarImage = avatarRect.gameObject.AddComponent<Image>();
            avatarImage.color = new Color(0.24f, 0.25f, 0.28f, 1f);
            var sprite = LoadAvatarSprite(profile.IconKey, profile.Role);
            if (sprite != null)
            {
                avatarImage.sprite = sprite;
                avatarImage.color = Color.white;
                avatarImage.preserveAspect = true;
            }

            var avatarLabel = CreateText(avatarRect, "AvatarText", AvatarText(profile.Name, profile.Role), 28f, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(avatarLabel.rectTransform, Vector2.zero, Vector2.zero);
            avatarLabel.gameObject.SetActive(sprite == null);

            var nameText = CreateText(row, "Name", profile.Name, 22f, FontStyles.Bold, TextAlignmentOptions.Left);
            nameText.color = TextColor;
            Anchor(nameText.rectTransform, new Vector2(0f, 1f), new Vector2(0.52f, 1f), new Vector2(128f, -46f), new Vector2(-8f, -16f));

            var titleText = CreateText(row, "Title", profile.Title, 16f, FontStyles.Normal, TextAlignmentOptions.Left);
            titleText.color = MutedTextColor;
            Anchor(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(0.52f, 1f), new Vector2(128f, -76f), new Vector2(-8f, -48f));

            var personality = CreateText(row, "Personality", profile.PersonalityDesc, 14f, FontStyles.Normal, TextAlignmentOptions.Left);
            personality.color = new Color(0.82f, 0.76f, 0.98f, 1f);
            personality.overflowMode = TextOverflowModes.Ellipsis;
            Anchor(personality.rectTransform, new Vector2(0f, 1f), new Vector2(0.52f, 1f), new Vector2(128f, -104f), new Vector2(-8f, -78f));

            CreateAttributeGrid(row, profile);
        }

        private static void CreateAttributeGrid(RectTransform row, MinisterProfile profile)
        {
            var grid = CreateUiObject("Attributes", row);
            Anchor(grid, new Vector2(0.52f, 0f), Vector2.one, new Vector2(0f, 20f), new Vector2(-18f, -18f));

            var attributes = new[]
            {
                ("能力", profile.Ability),
                ("忠诚", profile.Loyalty),
                ("野心", profile.Ambition),
                ("谨慎", profile.Cautiousness),
                ("果断", profile.Decisiveness),
                ("忠诚倾向", profile.LoyaltyTendency),
                ("野心表现", profile.AmbitionStyle)
            };

            const float rowHeight = 22f;
            const float rowGap = 6f;
            for (var i = 0; i < attributes.Length; i++)
            {
                var column = i % 2;
                var rowIndex = i / 2;
                var xMin = column == 0 ? 0f : 0.5f;
                var xMax = column == 0 ? 0.5f : 1f;
                var item = CreateUiObject("Attribute-" + i, grid);
                var top = -(rowIndex * (rowHeight + rowGap));
                Anchor(item, new Vector2(xMin, 1f), new Vector2(xMax, 1f), new Vector2(column == 0 ? 0f : 8f, top - rowHeight), new Vector2(column == 0 ? -8f : 0f, top));

                var label = CreateText(item, "Label", attributes[i].Item1, 13f, FontStyles.Bold, TextAlignmentOptions.Left);
                label.color = new Color(0.95f, 0.74f, 0.42f, 1f);
                label.enableAutoSizing = true;
                label.fontSizeMin = 10f;
                label.fontSizeMax = 13f;
                Anchor(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-44f, 0f));

                var value = CreateText(item, "Value", attributes[i].Item2.ToString(), 14f, FontStyles.Bold, TextAlignmentOptions.Right);
                value.color = TextColor;
                Anchor(value.rectTransform, new Vector2(1f, 0f), Vector2.one, new Vector2(-42f, 0f), Vector2.zero);
            }
        }

        private static List<MinisterProfile> LoadMinisters()
        {
            var loaded = new List<MinisterProfile>();
            var asset = Resources.Load<TextAsset>(MinisterDataResourcePath);
            if (asset != null && !string.IsNullOrWhiteSpace(asset.text))
            {
                try
                {
                    var section = JsonUtility.FromJson<MinisterSection>(asset.text);
                    if (section?.ministers != null)
                    {
                        loaded.AddRange(section.ministers.Where(profile => profile != null));
                    }
                }
                catch (ArgumentException)
                {
                    loaded.Clear();
                }
            }

            var fallbacks = FallbackMinisters();
            var byRole = new HashSet<string>(loaded.Select(profile => NormalizeRole(profile.role)), StringComparer.OrdinalIgnoreCase);
            foreach (var fallback in fallbacks)
            {
                if (loaded.Count >= ExpectedMinisterCount)
                {
                    break;
                }

                if (byRole.Add(NormalizeRole(fallback.role)))
                {
                    loaded.Add(fallback);
                }
            }

            for (var i = 0; i < loaded.Count; i++)
            {
                NormalizeProfile(loaded[i], i < fallbacks.Length ? fallbacks[i] : fallbacks[fallbacks.Length - 1]);
            }

            return loaded
                .OrderBy(profile => RoleSortKey(profile.Role), StringComparer.OrdinalIgnoreCase)
                .ThenBy(profile => profile.Role, StringComparer.OrdinalIgnoreCase)
                .Take(ExpectedMinisterCount)
                .ToList();
        }

        private static void NormalizeProfile(MinisterProfile profile, MinisterProfile fallback)
        {
            profile.role = Clean(profile.role, fallback.role);
            profile.name = Clean(profile.name, fallback.name);
            profile.icon_key = Clean(profile.icon_key, fallback.icon_key);
            profile.personality_desc = Clean(profile.personality_desc, fallback.personality_desc);
            profile.ability = profile.ability == 0 ? fallback.ability : profile.ability;
            profile.loyalty = profile.loyalty == 0 ? fallback.loyalty : profile.loyalty;
            profile.ambition = profile.ambition == 0 ? fallback.ambition : profile.ambition;
            profile.cautiousness = profile.cautiousness == 0 ? fallback.cautiousness : profile.cautiousness;
            profile.decisiveness = profile.decisiveness == 0 ? fallback.decisiveness : profile.decisiveness;
            profile.loyalty_tendency = profile.loyalty_tendency == 0 ? fallback.loyalty_tendency : profile.loyalty_tendency;
            profile.ambition_style = profile.ambition_style == 0 ? fallback.ambition_style : profile.ambition_style;
        }

        private static MinisterProfile[] FallbackMinisters()
        {
            return new[]
            {
                new MinisterProfile("沈衡", "domestic", "domestic", "稳健审慎", 7, 8, 4, 76, 58, 84, 32),
                new MinisterProfile("许衡", "works", "works", "务实精算", 7, 7, 5, 64, 63, 76, 41),
                new MinisterProfile("韩戎", "defense", "defense", "严整强硬", 8, 8, 6, 58, 78, 72, 54),
                new MinisterProfile("李猛", "command", "command", "果敢激进", 8, 7, 6, 35, 82, 68, 65),
                new MinisterProfile("林远", "frontier", "frontier", "善于开边", 7, 6, 7, 61, 66, 66, 58),
                new MinisterProfile("赵铠", "military", "military", "警觉稳守", 8, 7, 5, 69, 71, 74, 45),
                new MinisterProfile("顾筹", "finance", "domestic", "精算守库", 8, 7, 6, 72, 55, 70, 52),
                new MinisterProfile("周穑", "agriculture", "works", "耐心厚实", 7, 8, 3, 81, 49, 86, 26),
                new MinisterProfile("裴仪", "diplomacy", "frontier", "圆融善辩", 7, 6, 6, 67, 62, 62, 57),
                new MinisterProfile("陆烛", "intelligence", "command", "缜密隐忍", 8, 6, 7, 88, 59, 60, 63)
            };
        }

        private static Sprite LoadAvatarSprite(string iconKey, string role)
        {
            var key = Clean(iconKey, NormalizeRole(role));
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            var path = AvatarRoot + "/" + key.Trim();
            if (AvatarSprites.TryGetValue(path, out var cached))
            {
                return cached;
            }

            var texture = Resources.Load<Texture2D>(path);
            if (texture == null)
            {
                AvatarSprites[path] = null;
                return null;
            }

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = texture.name + "_Sprite";
            AvatarSprites[path] = sprite;
            return sprite;
        }

        private Vector2 ResolveCanvasSize()
        {
            var canvas = GetComponentInParent<Canvas>();
            var canvasRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
            if (canvasRect != null && canvasRect.rect.width > 0f && canvasRect.rect.height > 0f)
            {
                return canvasRect.rect.size;
            }

            return new Vector2(1920f, 1080f);
        }

        private static Button CreateButton(RectTransform parent, string name, string label, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = CreateUiObject(name, parent);
            AnchorFixed(rect, anchor, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.42f, 0.22f, 0.11f, 0.96f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.08f, 0.88f, 0.58f, 1f);
            colors.pressedColor = new Color(0.58f, 0.3f, 0.15f, 1f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var text = CreateText(rect, "Label", label, 18f, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(text.rectTransform, Vector2.zero, Vector2.zero);
            return button;
        }

        private static TextMeshProUGUI CreateText(RectTransform parent, string name, string value, float fontSize, FontStyles style, TextAlignmentOptions alignment)
        {
            var rect = CreateUiObject(name, parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value ?? string.Empty;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = TextColor;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Truncate;
            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }

            return text;
        }

        private static void CreateBorder(RectTransform parent)
        {
            var color = new Color(0.86f, 0.52f, 0.18f, 0.86f);
            CreateLine(parent, "BorderTop", new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -2f), Vector2.zero, color);
            CreateLine(parent, "BorderBottom", Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 2f), color);
            CreateLine(parent, "BorderLeft", Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(2f, 0f), color);
            CreateLine(parent, "BorderRight", new Vector2(1f, 0f), Vector2.one, new Vector2(-2f, 0f), Vector2.zero, color);
        }

        private static void CreateLine(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var rect = CreateUiObject(name, parent);
            Anchor(rect, anchorMin, anchorMax, offsetMin, offsetMax);
            rect.gameObject.AddComponent<Image>().color = color;
        }

        private static RectTransform CreateUiObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void AnchorFixed(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
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

        private static void ClearChildren(RectTransform root)
        {
            for (var i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }

        private static string RoleTitle(string role)
        {
            return NormalizeRole(role) switch
            {
                "works" => "工务大臣",
                "defense" => "军备大臣",
                "command" => "军令大臣",
                "frontier" => "边务大臣",
                "military" => "军务大臣",
                "finance" => "财政大臣",
                "agriculture" => "农政大臣",
                "diplomacy" => "外务大臣",
                "intelligence" => "情报大臣",
                "domestic" => "内政大臣",
                _ => Clean(role, "大臣")
            };
        }

        private static string RoleSortKey(string role)
        {
            return NormalizeRole(role) switch
            {
                "domestic" => "00",
                "works" => "01",
                "defense" => "02",
                "command" => "03",
                "frontier" => "04",
                "military" => "05",
                "finance" => "06",
                "agriculture" => "07",
                "diplomacy" => "08",
                "intelligence" => "09",
                _ => "99-" + role
            };
        }

        private static string AvatarText(string name, string role)
        {
            var source = Clean(name, role);
            return string.IsNullOrEmpty(source) ? "M" : source.Substring(0, 1).ToUpperInvariant();
        }

        private static string NormalizeRole(string role)
        {
            return string.IsNullOrWhiteSpace(role) ? "domestic" : role.Trim().ToLowerInvariant();
        }

        private static string Clean(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? (fallback ?? string.Empty) : value.Trim();
        }

        private static string SafeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "empty";
            }

            var chars = value.Trim().ToLowerInvariant().ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '-' && chars[i] != '_')
                {
                    chars[i] = '-';
                }
            }

            return new string(chars);
        }

        [Serializable]
        private sealed class MinisterSection
        {
            public MinisterProfile[] ministers;
        }

        [Serializable]
        private sealed class MinisterProfile
        {
            public string name;
            public string role;
            public string icon_key;
            public int ability;
            public string personality_desc;
            public int loyalty;
            public int ambition;
            public int cautiousness;
            public int decisiveness;
            public int loyalty_tendency;
            public int ambition_style;

            public MinisterProfile()
            {
            }

            public MinisterProfile(
                string name,
                string role,
                string iconKey,
                string personalityDesc,
                int ability,
                int loyalty,
                int ambition,
                int cautiousness,
                int decisiveness,
                int loyaltyTendency,
                int ambitionStyle)
            {
                this.name = name;
                this.role = role;
                icon_key = iconKey;
                personality_desc = personalityDesc;
                this.ability = ability;
                this.loyalty = loyalty;
                this.ambition = ambition;
                this.cautiousness = cautiousness;
                this.decisiveness = decisiveness;
                loyalty_tendency = loyaltyTendency;
                ambition_style = ambitionStyle;
            }

            public string Name => Clean(name, RoleTitle(Role));
            public string Role => NormalizeRole(role);
            public string Title => RoleTitle(Role);
            public string IconKey => Clean(icon_key, Role);
            public string PersonalityDesc => Clean(personality_desc, string.Empty);
            public int Ability => ability;
            public int Loyalty => loyalty;
            public int Ambition => ambition;
            public int Cautiousness => cautiousness;
            public int Decisiveness => decisiveness;
            public int LoyaltyTendency => loyalty_tendency;
            public int AmbitionStyle => ambition_style;
        }
    }

    internal static class MainMenuMinisterDetailsBootstrap
    {
        public static void Ensure(
            Transform canvasRoot,
            RectTransform menuRoot,
            Button startButton,
            Button settingsButton,
            Button quitButton,
            ref Button ministerDetailsButton,
            ref GameObject ministerDetailsPanel,
            Action backRequested)
        {
            if (menuRoot != null)
            {
                menuRoot.sizeDelta = new Vector2(
                    Mathf.Max(menuRoot.sizeDelta.x, 430f),
                    Mathf.Max(menuRoot.sizeDelta.y, 472f));
                LayoutMenuButton(startButton, new Vector2(0f, -122f));
                ministerDetailsButton ??= FindButton(menuRoot, "MinisterDetailsButton");
                if (ministerDetailsButton == null)
                {
                    ministerDetailsButton = CreateMenuButton(
                        "MinisterDetailsButton",
                        menuRoot,
                        "大臣详细",
                        new Vector2(0f, -204f),
                        new Vector2(310f, 64f),
                        new Color(0.24f, 0.15f, 0.08f, 0.88f));
                }

                LayoutMenuButton(ministerDetailsButton, new Vector2(0f, -204f));
                LayoutMenuButton(settingsButton, new Vector2(0f, -286f));
                LayoutMenuButton(quitButton, new Vector2(0f, -368f));
            }

            if (ministerDetailsPanel == null && canvasRoot != null)
            {
                var panelRect = new GameObject(
                    "MinisterDetailsPanel",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(MainMenuMinisterDetailsPanel)).GetComponent<RectTransform>();
                panelRect.SetParent(canvasRoot, false);
                Stretch(panelRect, Vector2.zero, Vector2.zero);
                panelRect.SetAsLastSibling();
                ministerDetailsPanel = panelRect.gameObject;
            }

            if (ministerDetailsPanel == null)
            {
                return;
            }

            var panel = ministerDetailsPanel.GetComponent<MainMenuMinisterDetailsPanel>();
            if (panel == null)
            {
                panel = ministerDetailsPanel.AddComponent<MainMenuMinisterDetailsPanel>();
            }

            panel.BindBack(backRequested);
            ministerDetailsPanel.SetActive(false);
        }

        private static void LayoutMenuButton(Button button, Vector2 anchoredPosition)
        {
            var rect = button != null ? button.transform as RectTransform : null;
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(310f, 64f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static Button FindButton(Transform root, string name)
        {
            if (root == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var buttons = root.GetComponentsInChildren<Button>(true);
            for (var i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].name == name)
                {
                    return buttons[i];
                }
            }

            return null;
        }

        private static Button CreateMenuButton(string name, Transform parent, string label, Vector2 anchoredPosition, Vector2 size, Color normalColor)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;

            var image = rect.GetComponent<Image>();
            image.color = normalColor;
            image.raycastTarget = true;

            var button = rect.GetComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 0.94f, 0.68f, 1f);
            colors.pressedColor = new Color(0.68f, 0.37f, 0.22f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.25f, 0.25f, 0.25f, 0.7f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var textRect = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<RectTransform>();
            textRect.SetParent(rect, false);
            Stretch(textRect, Vector2.zero, Vector2.zero);
            var text = textRect.GetComponent<TextMeshProUGUI>();
            text.text = label;
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = 30f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(1f, 0.92f, 0.78f, 1f);
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return button;
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
    }
}
