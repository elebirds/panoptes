/*************************************************
 * Project: Panoptes
 * File: TurnHUD.cs
 * Author: Panoptes Team
 * Date: 2026-04-16
 * Description: Turn/phase HUD that can bind to an external TurnPanel (turnNum).
 *************************************************/

using System;
using Panoptes.Core.Application.Intents;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Application.Stores;
using Panoptes.Presentation.Common;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Panoptes.Presentation.UI.HUD
{
    public sealed class TurnHUD : MonoBehaviour
    {
        [Header("Runtime HUD (fallback)")]
        [SerializeField] private RectTransform root;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI detailText;

        [Header("External Turn Panel")]
        [SerializeField] private bool preferExternalTurnPanel = true;
        [SerializeField] private bool preserveExternalLayout = true;
        [SerializeField] private RectTransform externalTurnPanelRoot;
        [SerializeField] private TextMeshProUGUI externalTurnNumText;
        [SerializeField] private TextMeshProUGUI externalPhaseText;
        [SerializeField] private string externalTurnTextFormat = "当前回合数：{0}\n本回合剩余：{1}";
        [SerializeField] private string turnPanelBackgroundSpriteResource = "Textures/UI/turn_panel_parchment_bg";

        [Header("Submit Button")]
        [SerializeField] private Button nextStageButton;
        [SerializeField] private bool disableNextStageWhenUnavailable = true;
        [SerializeField] private string nextStageButtonSpriteResource = "Icons/UI/next_turn_button";
        [SerializeField] private bool applyNextStageButtonSprite = true;

        private IDisposable _turnSubscription;
        private TurnStore _turnStore;
        private float _deadline = -1f;
        private string _currentPhase = string.Empty;
        private string _nextPhase = string.Empty;
        private int _currentTurn;
        private int _currentTimeoutSeconds;
        private bool _isInteractive;
        private bool _gameEnded;
        private int _lastRemainingSeconds = int.MinValue;
        private readonly EventSubscriptionBag _subscriptions = new();
        private readonly EventSubscriptionBag _buttonSubscriptions = new();
        private GameIntentService _gameIntentService;

        [Inject]
        private void Construct(GameIntentService gameIntentService, TurnStore turnStore)
        {
            _gameIntentService = gameIntentService;
            _turnStore = turnStore;
        }

        private void Awake()
        {
            ResolveExternalTurnPanelReferences();
            ResolveNextStageButtonReference();
            EnsureUi();
            ApplyTurnPanelBackground();
            ApplyNextStageButtonSprite();
            RefreshNextStageInteractable();
        }

        private void OnEnable()
        {
            ResolveExternalTurnPanelReferences();
            ResolveNextStageButtonReference();
            _subscriptions.Clear();
            _turnSubscription?.Dispose();
            _turnSubscription = _turnStore?.State.Subscribe(this, static (state, self) => self.RefreshFromState(state));

            _subscriptions.Add(
                () => ActionLock.OnChanged += OnActionLockChanged,
                () => ActionLock.OnChanged -= OnActionLockChanged);
            BindNextStageButton();
            ApplyTurnPanelBackground();
            ApplyNextStageButtonSprite();
            RefreshFromState(_turnStore?.Snapshot);
        }

        private void OnDisable()
        {
            _subscriptions.Clear();
            _buttonSubscriptions.Clear();
            _turnSubscription?.Dispose();
            _turnSubscription = null;
        }

        private void Update()
        {
            var remaining = GetRemainingSeconds();
            if (remaining == _lastRemainingSeconds)
            {
                return;
            }

            _lastRemainingSeconds = remaining;
            RefreshText();
        }

        private void RefreshFromState(TurnState state)
        {
            if (state == null)
            {
                return;
            }

            var nextTurn = state.Turn;
            var nextPhase = state.Phase ?? string.Empty;
            var nextTimeoutSeconds = state.TimeoutSeconds;
            var nextIsInteractive = state.IsInteractive && !state.IsGameOver;
            var shouldResetDeadline =
                nextTurn != _currentTurn ||
                !string.Equals(nextPhase, _currentPhase, StringComparison.Ordinal) ||
                nextTimeoutSeconds != _currentTimeoutSeconds ||
                nextIsInteractive != _isInteractive ||
                state.IsGameOver != _gameEnded;

            _currentTurn = nextTurn;
            _currentPhase = nextPhase;
            _nextPhase = state.NextPhase ?? string.Empty;
            _currentTimeoutSeconds = nextTimeoutSeconds;
            _isInteractive = nextIsInteractive;
            _gameEnded = state.IsGameOver;
            if (!_isInteractive || _gameEnded || _currentTimeoutSeconds <= 0)
            {
                _deadline = -1f;
            }
            else if (shouldResetDeadline || _deadline <= 0f)
            {
                _deadline = Time.unscaledTime + _currentTimeoutSeconds;
            }

            _lastRemainingSeconds = int.MinValue;
            RefreshText();
            RefreshNextStageInteractable();
        }

        private void OnActionLockChanged(bool _)
        {
            RefreshNextStageInteractable();
        }

        private void OnNextStageButtonClicked()
        {
            if (_gameEnded || !_isInteractive || ActionLock.IsLocked)
            {
                RefreshNextStageInteractable();
                return;
            }

            _gameIntentService?.SubmitTurn();
            RefreshNextStageInteractable();
        }

        private void RefreshText()
        {
            EnsureUi();
            if (titleText == null && externalTurnNumText == null)
            {
                return;
            }

            var turnNum = _currentTurn > 0 ? _currentTurn.ToString() : "--";
            if (externalTurnNumText != null)
            {
                EnsureExternalCountdownTextLayout();
                externalTurnNumText.text = string.Format(
                    externalTurnTextFormat,
                    turnNum,
                    ResolveCountdownDisplay());
            }

            if (titleText != null && titleText != externalTurnNumText)
            {
                titleText.text = _currentTurn > 0
                    ? $"当前回合数：{turnNum}"
                    : "当前回合数：--";
            }

            if (_gameEnded)
            {
                SetDetailText("本回合剩余：--");
                return;
            }

            SetDetailText($"本回合剩余：{ResolveCountdownDisplay()}");
        }

        private void SetDetailText(string value)
        {
            if (detailText != null)
            {
                detailText.text = value;
            }

            if (externalPhaseText != null)
            {
                externalPhaseText.text = string.Empty;
            }
        }

        private int GetRemainingSeconds()
        {
            if (_gameEnded || !_isInteractive || _deadline <= 0f)
            {
                return int.MinValue;
            }

            return Mathf.Max(0, Mathf.CeilToInt(_deadline - Time.unscaledTime));
        }

        private string ResolveCountdownDisplay()
        {
            if (_gameEnded || !_isInteractive)
            {
                return "--";
            }

            if (_deadline <= 0f)
            {
                return "--";
            }

            var remaining = Mathf.Max(0, Mathf.CeilToInt(_deadline - Time.unscaledTime));
            return FormatRemainingSeconds(remaining);
        }

        private static string FormatRemainingSeconds(int remaining)
        {
            remaining = Mathf.Max(0, remaining);
            var seconds = remaining % 60;
            var totalMinutes = remaining / 60;
            if (totalMinutes < 60)
            {
                return $"{totalMinutes:00}:{seconds:00}";
            }

            var hours = totalMinutes / 60;
            var minutes = totalMinutes % 60;
            return $"{hours}:{minutes:00}:{seconds:00}";
        }

        private void ResolveExternalTurnPanelReferences()
        {
            if (!preferExternalTurnPanel)
            {
                return;
            }

            if (externalTurnPanelRoot == null)
            {
                return;
            }

            if (externalTurnNumText == null)
            {
                externalTurnNumText = FindTextByName(externalTurnPanelRoot, "turnNum");
            }

            if (externalPhaseText == null)
            {
                externalPhaseText = FindTextByName(externalTurnPanelRoot, "phaseText");
            }
        }

        private void EnsureExternalCountdownTextLayout()
        {
            if (externalTurnNumText == null)
            {
                return;
            }

            if (externalTurnPanelRoot != null)
            {
                externalTurnPanelRoot.sizeDelta = new Vector2(
                    Mathf.Max(externalTurnPanelRoot.sizeDelta.x, 320f),
                    Mathf.Max(externalTurnPanelRoot.sizeDelta.y, 108f));
            }

            externalTurnNumText.enableAutoSizing = true;
            externalTurnNumText.fontSize = Mathf.Min(externalTurnNumText.fontSize, 36f);
            externalTurnNumText.fontSizeMin = Mathf.Max(externalTurnNumText.fontSizeMin, 20f);
            externalTurnNumText.fontSizeMax = 36f;
            externalTurnNumText.textWrappingMode = TextWrappingModes.NoWrap;
            externalTurnNumText.overflowMode = TextOverflowModes.Overflow;
            externalTurnNumText.alignment = TextAlignmentOptions.Center;
            externalTurnNumText.margin = Vector4.zero;

            var rect = externalTurnNumText.rectTransform;
            if (rect == null)
            {
                return;
            }

            rect.sizeDelta = new Vector2(
                Mathf.Max(rect.sizeDelta.x, 288f),
                Mathf.Max(rect.sizeDelta.y, 72f));
            rect.anchoredPosition = new Vector2(
                rect.anchoredPosition.x,
                Mathf.Min(rect.anchoredPosition.y, 18f));
        }

        private void BindNextStageButton()
        {
            _buttonSubscriptions.Clear();
            ResolveNextStageButtonReference();

            var button = nextStageButton;
            if (button == null)
            {
                return;
            }

            _buttonSubscriptions.Add(
                () => button.onClick.AddListener(OnNextStageButtonClicked),
                () =>
                {
                    if (button != null)
                    {
                        button.onClick.RemoveListener(OnNextStageButtonClicked);
                    }
                });
        }

        private void ResolveNextStageButtonReference()
        {
            if (nextStageButton != null)
            {
                return;
            }

            nextStageButton = FindButtonByName("NextStageBtn");
        }

        private void RefreshNextStageInteractable()
        {
            if (nextStageButton == null || !disableNextStageWhenUnavailable)
            {
                return;
            }

            nextStageButton.interactable = !_gameEnded && _isInteractive && !ActionLock.IsLocked;
        }

        private void ApplyNextStageButtonSprite()
        {
            if (!applyNextStageButtonSprite || nextStageButton == null || string.IsNullOrWhiteSpace(nextStageButtonSpriteResource))
            {
                return;
            }

            var image = nextStageButton.targetGraphic as Image;
            if (image == null)
            {
                image = nextStageButton.GetComponent<Image>();
            }

            var sprite = Resources.Load<Sprite>(nextStageButtonSpriteResource.Trim());
            if (image == null || sprite == null)
            {
                return;
            }

            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
        }

        private void ApplyTurnPanelBackground()
        {
            if (string.IsNullOrWhiteSpace(turnPanelBackgroundSpriteResource))
            {
                return;
            }

            EnsureUi();
            if (root == null)
            {
                return;
            }

            var image = root.GetComponent<Image>();
            if (image == null)
            {
                image = root.gameObject.AddComponent<Image>();
            }

            var sprite = Resources.Load<Sprite>(turnPanelBackgroundSpriteResource.Trim());
            if (sprite == null)
            {
                return;
            }

            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.color = Color.white;
            image.raycastTarget = false;
        }

        private static TextMeshProUGUI FindTextByName(RectTransform rootRect, string name)
        {
            if (rootRect == null)
            {
                return null;
            }

            var all = rootRect.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (var i = 0; i < all.Length; i++)
            {
                var text = all[i];
                if (text == null || string.IsNullOrWhiteSpace(text.name))
                {
                    continue;
                }

                if (string.Equals(text.name, name, System.StringComparison.OrdinalIgnoreCase))
                {
                    return text;
                }
            }

            return null;
        }

        private static Button FindButtonByName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            var go = GameObject.Find(objectName);
            return go != null ? go.GetComponent<Button>() : null;
        }

        private void EnsureUi()
        {
            ResolveExternalTurnPanelReferences();
            var usingExternalPanel = preferExternalTurnPanel && externalTurnPanelRoot != null;

            if (usingExternalPanel)
            {
                root = externalTurnPanelRoot;
            }

            if (root == null)
            {
                root = GetComponent<RectTransform>();
                if (root == null)
                {
                    root = gameObject.AddComponent<RectTransform>();
                }
            }

            if (!(usingExternalPanel && preserveExternalLayout))
            {
                root.anchorMin = new Vector2(0f, 1f);
                root.anchorMax = new Vector2(0f, 1f);
                root.pivot = new Vector2(0f, 1f);
                root.anchoredPosition = new Vector2(28f, -24f);
                root.sizeDelta = new Vector2(320f, 92f);
            }

            if (!usingExternalPanel)
            {
                titleText ??= CreateText("Title", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, -32f), 28f, FontStyles.Bold);
                detailText ??= CreateText("Detail", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, -84f), 22f, FontStyles.Normal);
                detailText.textWrappingMode = TextWrappingModes.Normal;
            }
        }

        private TextMeshProUGUI CreateText(string objectName, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, float fontSize, FontStyles style)
        {
            var existing = root.Find(objectName) as RectTransform;
            var rect = existing;
            if (rect == null)
            {
                var go = new GameObject(objectName, typeof(RectTransform));
                go.transform.SetParent(root, false);
                rect = go.GetComponent<RectTransform>();
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(0f, 32f);

            var text = rect.GetComponent<TextMeshProUGUI>();
            if (text == null)
            {
                text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            }

            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.raycastTarget = false;
            return text;
        }
    }
}
