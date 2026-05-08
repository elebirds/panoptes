/*************************************************
 * Project: Panoptes
 * File: GameOverOverlay.cs
 * Author: Panoptes Team
 * Date: 2026-04-13
 * Description: End-of-game battle result overlay.
 *************************************************/

using System;
using System.Collections.Generic;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Application.Stores;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Panoptes.Presentation.UI.Game
{
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    [RequireComponent(typeof(GraphicRaycaster))]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class GameOverOverlay : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image screenMask;
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private Image panelBackground;
        [SerializeField] private Image titleSwordsImage;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI reasonText;
        [SerializeField] private TextMeshProUGUI winnerIdText;
        [SerializeField] private TextMeshProUGUI loserIdText;
        [SerializeField] private TextMeshProUGUI narrativeText;
        [SerializeField] private Button backToBuildRoomButton;
        [SerializeField] private string buildRoomSceneName = "Lobby";
#if UNITY_EDITOR
        [SerializeField] private bool autoRebuildInEditorWhenEmpty = true;
        private bool _editorRebuilding;
#endif

        private IDisposable _gameOverSubscription;
        private GameStateStore _gameStateStore;
        private GameOverStore _gameOverStore;
        private TurnStore _turnStore;
        private RoomCache _roomCache;
        private GameStateCache _gameStateCache;
        private LocalGameSessionResetService _resetService;
        private bool _buttonBound;

        [Inject]
        private void Construct(
            GameOverStore gameOverStore,
            GameStateStore gameStateStore,
            TurnStore turnStore,
            RoomCache roomCache,
            GameStateCache gameStateCache,
            LocalGameSessionResetService resetService)
        {
            _gameOverStore = gameOverStore;
            _gameStateStore = gameStateStore;
            _turnStore = turnStore;
            _roomCache = roomCache;
            _gameStateCache = gameStateCache;
            _resetService = resetService;
        }

        private void Awake()
        {
            EnsureUi();
            BindBackButton();
            Hide();
        }

        private void OnEnable()
        {
            _gameOverSubscription?.Dispose();
            _gameOverSubscription = _gameOverStore?.State.Subscribe(this, static (state, self) => self.OnGameOver(state));
            OnGameOver(_gameOverStore?.Snapshot);
        }

        private void OnDisable()
        {
            _gameOverSubscription?.Dispose();
            _gameOverSubscription = null;

            if (backToBuildRoomButton != null)
            {
                backToBuildRoomButton.onClick.RemoveListener(OnBackToBuildRoomClicked);
            }
            _buttonBound = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying || !autoRebuildInEditorWhenEmpty || _editorRebuilding)
            {
                return;
            }

            if (transform.childCount > 0 || panelRoot != null)
            {
                return;
            }

            EditorRebuildUiForPrefab();
        }
#endif

        private void OnGameOver(GameOverState evt)
        {
            if (evt == null || !evt.IsGameOver)
            {
                return;
            }

            EnsureUi();
            BindBackButton();
            transform.SetAsLastSibling();

            var winnerId = SafeValue(evt.WinnerId);
            var loserId = ResolveLoserId(evt, winnerId);
            var winnerName = ResolvePlayerName(winnerId, "未知胜方");
            var loserName = ResolvePlayerName(loserId, "未知败方");
            var totalTurns = ResolveTotalTurns();

            titleText.text = "胜负已分";
            winnerIdText.text = $"{winnerName}，胜利";
            loserIdText.text = $"{loserName}，失败";
            reasonText.text = totalTurns > 0 ? $"总回合数：{totalTurns}" : "总回合数：未知";
            if (narrativeText != null)
            {
                narrativeText.text = string.Empty;
                narrativeText.gameObject.SetActive(false);
            }

            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }

        private void Hide()
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        private void EnsureUi()
        {
            canvasGroup ??= GetComponent<CanvasGroup>();

            var canvas = GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 900);
            }

            var scaler = GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            var rootRect = transform as RectTransform;
            if (rootRect == null)
            {
                return;
            }

            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var maskRect = EnsureRect("Mask", rootRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            screenMask ??= EnsureImage(maskRect.gameObject, Color.white);
            screenMask.sprite = BattleResultArt.BackgroundSprite;
            screenMask.type = Image.Type.Simple;
            screenMask.preserveAspect = false;
            screenMask.raycastTarget = true;

            panelRoot ??= EnsureRect("Panel", rootRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(860f, 520f), Vector2.zero);
            panelBackground ??= EnsureImage(panelRoot.gameObject, Color.white);
            panelBackground.sprite = BattleResultArt.PanelSprite;
            panelBackground.type = Image.Type.Simple;
            panelBackground.raycastTarget = true;

            var swordsRect = EnsureRect("TitleSwords", panelRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(520f, 220f), new Vector2(0f, -104f));
            titleSwordsImage ??= EnsureImage(swordsRect.gameObject, Color.white);
            titleSwordsImage.sprite = BattleResultArt.CrossedSwordsSprite;
            titleSwordsImage.preserveAspect = true;
            titleSwordsImage.raycastTarget = false;

            titleText ??= EnsureText("Title", panelRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(760f, 72f), 50f, FontStyles.Bold, TextAlignmentOptions.Center);
            winnerIdText ??= EnsureText("WinnerIDText", panelRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -218f), new Vector2(760f, 48f), 30f, FontStyles.Bold, TextAlignmentOptions.Center);
            loserIdText ??= EnsureText("LoserIDText", panelRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -276f), new Vector2(760f, 48f), 30f, FontStyles.Bold, TextAlignmentOptions.Center);
            reasonText ??= EnsureText("Reason", panelRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -334f), new Vector2(760f, 42f), 26f, FontStyles.Normal, TextAlignmentOptions.Center);

            narrativeText ??= EnsureText("Narrative", panelRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -382f), new Vector2(760f, 40f), 20f, FontStyles.Normal, TextAlignmentOptions.Center);
            narrativeText.gameObject.SetActive(false);

            backToBuildRoomButton ??= EnsureButton("BackToBuildRoomButton", panelRoot, "返回建造房间", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 54f), new Vector2(320f, 58f));
        }

        private void BindBackButton()
        {
            if (_buttonBound || backToBuildRoomButton == null)
            {
                return;
            }

            backToBuildRoomButton.onClick.RemoveListener(OnBackToBuildRoomClicked);
            backToBuildRoomButton.onClick.AddListener(OnBackToBuildRoomClicked);
            _buttonBound = true;
        }

        private void OnBackToBuildRoomClicked()
        {
            var sceneName = string.IsNullOrWhiteSpace(buildRoomSceneName) ? "Lobby" : buildRoomSceneName.Trim();
            _resetService?.ResetLocalGameSession();

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                PanoptesLog.Warning($"[GameOverOverlay] Cannot load scene '{sceneName}'. Add it to Build Settings.");
                return;
            }

            SceneManager.LoadScene(sceneName);
        }

        private int ResolveTotalTurns()
        {
            var turn = _gameStateStore?.Snapshot?.Turn ?? 0;
            if (turn <= 0)
            {
                turn = _turnStore?.Snapshot?.Turn ?? 0;
            }
            if (turn <= 0 && _gameStateCache != null)
            {
                turn = _gameStateCache.Turn;
            }
            return turn;
        }

        private string ResolveLoserId(GameOverState evt, string winnerId)
        {
            if (!string.IsNullOrWhiteSpace(evt?.LoserId))
            {
                return evt.LoserId.Trim();
            }

            var room = _roomCache != null ? _roomCache : RoomCache.Instance;
            if (room?.Players != null)
            {
                for (var i = 0; i < room.Players.Count; i++)
                {
                    var player = room.Players[i];
                    if (player == null || string.IsNullOrWhiteSpace(player.PlayerId))
                    {
                        continue;
                    }

                    var candidate = player.PlayerId.Trim();
                    if (!SameId(candidate, winnerId))
                    {
                        return candidate;
                    }
                }
            }

            var selfId = _gameStateStore?.Snapshot?.MyPlayerId;
            if (!string.IsNullOrWhiteSpace(selfId) && !SameId(selfId, winnerId))
            {
                return selfId.Trim();
            }

            var candidates = CollectKnownPlayerIds();
            for (var i = 0; i < candidates.Count; i++)
            {
                if (!SameId(candidates[i], winnerId))
                {
                    return candidates[i];
                }
            }

            var cacheSelfId = _gameStateCache?.MyPlayerID;
            if (!string.IsNullOrWhiteSpace(cacheSelfId) && !SameId(cacheSelfId, winnerId))
            {
                return cacheSelfId.Trim();
            }

            return string.Empty;
        }

        private List<string> CollectKnownPlayerIds()
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var state = _gameStateStore?.Snapshot;
            if (state?.Units != null)
            {
                foreach (var pair in state.Units)
                {
                    AddCandidate(result, seen, pair.Value?.Owner);
                }
            }

            if (state?.Nodes != null)
            {
                foreach (var pair in state.Nodes)
                {
                    AddCandidate(result, seen, pair.Value?.Owner);
                    AddCandidate(result, seen, pair.Value?.TerritoryOwner);
                }
            }

            return result;
        }

        private string ResolvePlayerName(string playerId, string fallback)
        {
            var id = SafeValue(playerId);
            if (string.IsNullOrWhiteSpace(id))
            {
                return fallback;
            }

            var room = _roomCache != null ? _roomCache : RoomCache.Instance;
            if (room?.Players != null)
            {
                for (var i = 0; i < room.Players.Count; i++)
                {
                    var player = room.Players[i];
                    if (player == null || !SameId(player.PlayerId, id))
                    {
                        continue;
                    }

                    return string.IsNullOrWhiteSpace(player.Username) ? id : player.Username.Trim();
                }
            }

            return id;
        }

        private static void AddCandidate(List<string> target, HashSet<string> seen, string value)
        {
            if (target == null || seen == null || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var candidate = value.Trim();
            if (seen.Add(candidate))
            {
                target.Add(candidate);
            }
        }

        private static bool SameId(string left, string right)
        {
            return string.Equals(SafeValue(left), SafeValue(right), StringComparison.OrdinalIgnoreCase);
        }

        private static string SafeValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static RectTransform EnsureRect(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 anchoredPosition)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing != null)
            {
                existing.anchorMin = anchorMin;
                existing.anchorMax = anchorMax;
                existing.pivot = pivot;
                existing.sizeDelta = size;
                existing.anchoredPosition = anchoredPosition;
                return existing;
            }

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            return rect;
        }

        private static Image EnsureImage(GameObject go, Color color)
        {
            var image = go.GetComponent<Image>();
            if (image == null)
            {
                image = go.AddComponent<Image>();
            }

            image.color = color;
            return image;
        }

        private static TextMeshProUGUI EnsureText(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, float fontSize, FontStyles style, TextAlignmentOptions alignment)
        {
            var rect = EnsureRect(name, parent, anchorMin, anchorMax, pivot, size, anchoredPosition);
            var text = rect.GetComponent<TextMeshProUGUI>();
            if (text == null)
            {
                text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            }

            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            text.gameObject.SetActive(true);

            var shadow = rect.GetComponent<Shadow>();
            if (shadow == null)
            {
                shadow = rect.gameObject.AddComponent<Shadow>();
            }
            shadow.effectColor = new Color(0f, 0f, 0f, 0.68f);
            shadow.effectDistance = new Vector2(2f, -2f);

            return text;
        }

        private static Button EnsureButton(string name, RectTransform parent, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            var rect = EnsureRect(name, parent, anchorMin, anchorMax, pivot, size, anchoredPosition);
            var image = rect.GetComponent<Image>();
            if (image == null)
            {
                image = rect.gameObject.AddComponent<Image>();
            }
            image.color = new Color(0.58f, 0.17f, 0.10f, 0.96f);

            var button = rect.GetComponent<Button>();
            if (button == null)
            {
                button = rect.gameObject.AddComponent<Button>();
            }
            button.targetGraphic = image;

            var labelRect = EnsureRect("Label", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var text = labelRect.GetComponent<TextMeshProUGUI>();
            if (text == null)
            {
                text = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            }
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = 24f;
            text.fontStyle = FontStyles.Bold;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.text = label;
            text.raycastTarget = false;

            return button;
        }

#if UNITY_EDITOR
        [ContextMenu("Rebuild UI (Prefab)")]
        public void EditorRebuildUiForPrefab()
        {
            if (Application.isPlaying || _editorRebuilding)
            {
                return;
            }

            _editorRebuilding = true;
            try
            {
                for (var i = transform.childCount - 1; i >= 0; i--)
                {
                    DestroyImmediate(transform.GetChild(i).gameObject);
                }

                panelRoot = null;
                panelBackground = null;
                screenMask = null;
                titleSwordsImage = null;
                titleText = null;
                reasonText = null;
                winnerIdText = null;
                loserIdText = null;
                narrativeText = null;
                backToBuildRoomButton = null;
                _buttonBound = false;

                EnsureUi();
                BindBackButton();
                Hide();

                EditorUtility.SetDirty(this);
                if (gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(gameObject.scene);
                }
            }
            finally
            {
                _editorRebuilding = false;
            }
        }
#endif

        private static class BattleResultArt
        {
            private static Sprite _backgroundSprite;
            private static Sprite _panelSprite;
            private static Sprite _crossedSwordsSprite;

            public static Sprite BackgroundSprite => _backgroundSprite ??= CreateBackgroundSprite();
            public static Sprite PanelSprite => _panelSprite ??= CreatePanelSprite();
            public static Sprite CrossedSwordsSprite => _crossedSwordsSprite ??= CreateCrossedSwordsSprite();

            private static Sprite CreateBackgroundSprite()
            {
                const int width = 1024;
                const int height = 1024;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.name = "BattleResult_Background";
                texture.wrapMode = TextureWrapMode.Clamp;

                var pixels = new Color32[width * height];
                var top = new Color(0.07f, 0.08f, 0.11f, 1f);
                var middle = new Color(0.18f, 0.15f, 0.13f, 1f);
                var bottom = new Color(0.30f, 0.10f, 0.07f, 1f);
                var center = new Vector2(width * 0.5f, height * 0.48f);
                var maxDistance = new Vector2(width * 0.55f, height * 0.55f).magnitude;

                for (var y = 0; y < height; y++)
                {
                    var t = y / (float)(height - 1);
                    var baseColor = t < 0.58f
                        ? Color.Lerp(bottom, middle, t / 0.58f)
                        : Color.Lerp(middle, top, (t - 0.58f) / 0.42f);

                    for (var x = 0; x < width; x++)
                    {
                        var noise = Mathf.PerlinNoise(x * 0.012f, y * 0.012f) * 0.07f;
                        var distance = Vector2.Distance(new Vector2(x, y), center) / maxDistance;
                        var vignette = Mathf.Clamp01(1.05f - distance * 0.82f);
                        var ember = Mathf.PerlinNoise(x * 0.035f + 11.3f, y * 0.018f + 7.2f);
                        var color = baseColor * (0.72f + vignette * 0.38f + noise);
                        if (ember > 0.82f && y < height * 0.45f)
                        {
                            color += new Color(0.18f, 0.06f, 0.01f, 0f) * ((ember - 0.82f) * 1.8f);
                        }
                        pixels[y * width + x] = ToColor32(color);
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
            }

            private static Sprite CreatePanelSprite()
            {
                const int width = 512;
                const int height = 320;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.name = "BattleResult_Panel";
                texture.wrapMode = TextureWrapMode.Clamp;

                var pixels = new Color32[width * height];
                var center = new Vector2(width * 0.5f, height * 0.52f);
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var edge = Mathf.Min(Mathf.Min(x, width - 1 - x), Mathf.Min(y, height - 1 - y));
                        var distance = Vector2.Distance(new Vector2(x, y), center) / 315f;
                        var color = Color.Lerp(new Color(0.09f, 0.075f, 0.055f, 0.95f), new Color(0.17f, 0.13f, 0.08f, 0.96f), Mathf.Clamp01(1f - distance));
                        var grain = Mathf.PerlinNoise(x * 0.055f, y * 0.055f) * 0.05f;
                        color += new Color(grain, grain * 0.75f, grain * 0.35f, 0f);

                        if (edge < 4f)
                        {
                            color = new Color(0.03f, 0.025f, 0.02f, 0.98f);
                        }
                        else if (edge < 12f)
                        {
                            color = Color.Lerp(new Color(0.72f, 0.49f, 0.22f, 0.98f), new Color(0.26f, 0.15f, 0.07f, 0.98f), edge / 12f);
                        }

                        pixels[y * width + x] = ToColor32(color);
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
            }

            private static Sprite CreateCrossedSwordsSprite()
            {
                const int width = 512;
                const int height = 256;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.name = "BattleResult_CrossedSwords";
                texture.wrapMode = TextureWrapMode.Clamp;

                var clear = new Color32[width * height];
                texture.SetPixels32(clear);
                DrawSword(texture, new Vector2(width * 0.5f, height * 0.42f), 0.76f);
                DrawSword(texture, new Vector2(width * 0.5f, height * 0.42f), Mathf.PI - 0.76f);
                DrawCircle(texture, new Vector2(width * 0.5f, height * 0.42f), 18f, new Color32(92, 45, 24, 255));
                DrawCircle(texture, new Vector2(width * 0.5f, height * 0.42f), 12f, new Color32(217, 168, 76, 255));
                texture.Apply(false, true);
                return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
            }

            private static void DrawSword(Texture2D texture, Vector2 center, float angle)
            {
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var normal = new Vector2(-dir.y, dir.x);
                var bladeBase = center - dir * 56f;
                var bladeTip = center + dir * 132f;
                var guardCenter = center - dir * 72f;
                var gripStart = center - dir * 82f;
                var gripEnd = center - dir * 128f;

                DrawLine(texture, bladeBase, bladeTip - dir * 18f, 10f, new Color32(50, 56, 62, 255));
                DrawLine(texture, bladeBase + dir * 4f, bladeTip - dir * 22f, 6f, new Color32(204, 212, 217, 255));
                DrawLine(texture, bladeBase + normal * 2f + dir * 4f, bladeTip - dir * 32f, 1.6f, new Color32(255, 255, 255, 255));
                DrawTriangle(texture, bladeTip, bladeTip - dir * 28f + normal * 10f, bladeTip - dir * 28f - normal * 10f, new Color32(222, 228, 232, 255));

                DrawLine(texture, guardCenter - normal * 46f, guardCenter + normal * 46f, 10f, new Color32(80, 43, 24, 255));
                DrawLine(texture, guardCenter - normal * 39f, guardCenter + normal * 39f, 6f, new Color32(219, 161, 69, 255));
                DrawLine(texture, gripStart, gripEnd, 13f, new Color32(61, 35, 23, 255));
                DrawLine(texture, gripStart, gripEnd, 8f, new Color32(135, 78, 42, 255));
                DrawCircle(texture, gripEnd - dir * 7f, 12f, new Color32(61, 35, 23, 255));
                DrawCircle(texture, gripEnd - dir * 7f, 8f, new Color32(219, 161, 69, 255));
            }

            private static void DrawLine(Texture2D texture, Vector2 from, Vector2 to, float radius, Color32 color)
            {
                var length = Vector2.Distance(from, to);
                var steps = Mathf.Max(1, Mathf.CeilToInt(length));
                for (var i = 0; i <= steps; i++)
                {
                    var t = i / (float)steps;
                    DrawCircle(texture, Vector2.Lerp(from, to, t), radius, color);
                }
            }

            private static void DrawTriangle(Texture2D texture, Vector2 a, Vector2 b, Vector2 c, Color32 color)
            {
                var minX = Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)));
                var maxX = Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)));
                var minY = Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y)));
                var maxY = Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y)));

                for (var y = minY; y <= maxY; y++)
                {
                    for (var x = minX; x <= maxX; x++)
                    {
                        if (PointInTriangle(new Vector2(x + 0.5f, y + 0.5f), a, b, c))
                        {
                            SetPixel(texture, x, y, color);
                        }
                    }
                }
            }

            private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
            {
                var d1 = Sign(p, a, b);
                var d2 = Sign(p, b, c);
                var d3 = Sign(p, c, a);
                var hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
                var hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
                return !(hasNegative && hasPositive);
            }

            private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
            {
                return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
            }

            private static void DrawCircle(Texture2D texture, Vector2 center, float radius, Color32 color)
            {
                var minX = Mathf.FloorToInt(center.x - radius);
                var maxX = Mathf.CeilToInt(center.x + radius);
                var minY = Mathf.FloorToInt(center.y - radius);
                var maxY = Mathf.CeilToInt(center.y + radius);
                var radiusSquared = radius * radius;

                for (var y = minY; y <= maxY; y++)
                {
                    for (var x = minX; x <= maxX; x++)
                    {
                        var dx = x + 0.5f - center.x;
                        var dy = y + 0.5f - center.y;
                        if (dx * dx + dy * dy <= radiusSquared)
                        {
                            SetPixel(texture, x, y, color);
                        }
                    }
                }
            }

            private static void SetPixel(Texture2D texture, int x, int y, Color32 color)
            {
                if (x < 0 || y < 0 || x >= texture.width || y >= texture.height)
                {
                    return;
                }

                texture.SetPixel(x, y, color);
            }

            private static Color32 ToColor32(Color color)
            {
                return new Color32(
                    (byte)Mathf.Clamp(Mathf.RoundToInt(color.r * 255f), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(color.g * 255f), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(color.b * 255f), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(color.a * 255f), 0, 255));
            }
        }
    }
}
