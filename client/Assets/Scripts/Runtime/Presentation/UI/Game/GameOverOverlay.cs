/*************************************************
 * Project: Panoptes
 * File: GameOverOverlay.cs
 * Author: Panoptes Team
 * Date: 2026-04-13
 * Description: End-of-game overlay (prefab-first with runtime fallback).
 *************************************************/

using System;
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
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class GameOverOverlay : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image screenMask;
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private Image panelBackground;
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
        private LocalGameSessionResetService _resetService;
        private bool _buttonBound;

        [Inject]
        private void Construct(
            GameOverStore gameOverStore,
            GameStateStore gameStateStore,
            LocalGameSessionResetService resetService)
        {
            _gameOverStore = gameOverStore;
            _gameStateStore = gameStateStore;
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

            titleText.text = MapTitle(evt.IsWinner, evt.Reason);
            reasonText.text = MapReason(evt.Reason);
            winnerIdText.text = $"Winner ID: {SafeId(evt.WinnerId)}";

            var loser = !string.IsNullOrWhiteSpace(evt.LoserId)
                ? evt.LoserId
                : (evt.IsWinner ? "unknown" : (_gameStateStore?.Snapshot.MyPlayerId ?? string.Empty));
            loserIdText.text = $"Loser ID: {SafeId(loser)}";

            narrativeText.text = string.IsNullOrWhiteSpace(evt.Narrative)
                ? "Battle report is being generated..."
                : evt.Narrative;

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
            screenMask ??= EnsureImage(maskRect.gameObject, new Color(0f, 0f, 0f, 0.72f));
            screenMask.raycastTarget = true;

            panelRoot ??= EnsureRect("Panel", rootRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(820f, 520f), Vector2.zero);
            panelBackground ??= EnsureImage(panelRoot.gameObject, new Color(0.08f, 0.10f, 0.14f, 0.94f));
            titleText ??= EnsureText("Title", panelRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(720f, 58f), 42f, FontStyles.Bold, TextAlignmentOptions.Center);
            reasonText ??= EnsureText("Reason", panelRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(740f, 40f), 24f, FontStyles.Normal, TextAlignmentOptions.Center);
            winnerIdText ??= EnsureText("WinnerIDText", panelRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -156f), new Vector2(740f, 34f), 21f, FontStyles.Bold, TextAlignmentOptions.Center);
            loserIdText ??= EnsureText("LoserIDText", panelRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -194f), new Vector2(740f, 34f), 21f, FontStyles.Bold, TextAlignmentOptions.Center);
            narrativeText ??= EnsureText("Narrative", panelRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -244f), new Vector2(740f, 150f), 22f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            narrativeText.textWrappingMode = TextWrappingModes.Normal;

            backToBuildRoomButton ??= EnsureButton("BackToBuildRoomButton", panelRoot, "返回建造房间", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(320f, 56f));
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
                Debug.LogWarning($"[GameOverOverlay] Cannot load scene '{sceneName}'. Add it to Build Settings.");
                return;
            }

            SceneManager.LoadScene(sceneName);
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
            text.raycastTarget = false;
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
            image.color = new Color(0.20f, 0.42f, 0.78f, 0.95f);

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
            text.raycastTarget = false;
            text.text = label;

            return button;
        }

        private static string MapTitle(bool isWinner, string reason)
        {
            return reason switch
            {
                "player_disconnected" when isWinner => "Opponent Disconnected",
                "player_disconnected" => "Disconnected",
                "timeout_draw" => "Draw",
                _ when isWinner => "Victory",
                _ => "Defeat"
            };
        }

        private static string SafeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();
        }

        private static string MapReason(string reason)
        {
            return reason switch
            {
                "city_core_destroyed" => "City core destroyed",
                "timeout_draw" => "Turn limit reached, draw",
                "player_disconnected" => "The match ended because a player disconnected.",
                _ => string.IsNullOrWhiteSpace(reason) ? "Game ended" : reason
            };
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
    }
}
