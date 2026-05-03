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
        [SerializeField] private string externalTurnTextFormat = "当前回合数：{0}\n当前回合倒计时：{1}";

        [Header("Submit Button")]
        [SerializeField] private Button nextStageButton;
        [SerializeField] private bool disableNextStageWhenUnavailable = true;

        private IDisposable _turnSubscription;
        private TurnStore _turnStore;
        private float _deadline = -1f;
        private string _currentPhase = string.Empty;
        private string _nextPhase = string.Empty;
        private int _currentTurn;
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
            EnsureUi();
            RefreshNextStageInteractable();
        }

        private void OnEnable()
        {
            ResolveExternalTurnPanelReferences();
            _subscriptions.Clear();
            _turnSubscription?.Dispose();
            _turnSubscription = _turnStore?.State.Subscribe(this, static (state, self) => self.RefreshFromState(state));

            _subscriptions.Add(
                () => ActionLock.OnChanged += OnActionLockChanged,
                () => ActionLock.OnChanged -= OnActionLockChanged);
            BindNextStageButton();
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

            _currentTurn = state.Turn;
            _currentPhase = state.Phase ?? string.Empty;
            _nextPhase = state.NextPhase ?? string.Empty;
            _isInteractive = state.IsInteractive && !state.IsGameOver;
            _gameEnded = state.IsGameOver;
            _deadline = _isInteractive && state.TimeoutSeconds > 0
                ? Time.unscaledTime + state.TimeoutSeconds
                : -1f;

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
                externalTurnNumText.text = string.Format(
                    externalTurnTextFormat,
                    turnNum,
                    ResolveCountdownDisplay());
            }

            if (titleText != null)
            {
                titleText.text = _currentTurn > 0
                    ? $"当前回合数：{turnNum}"
                    : "当前回合数：--";
            }

            if (_gameEnded)
            {
                SetDetailText("当前回合倒计时：--");
                return;
            }

            SetDetailText($"当前回合倒计时：{ResolveCountdownDisplay()}");
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
            return $"{remaining}s";
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

        private void BindNextStageButton()
        {
            _buttonSubscriptions.Clear();

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

        private void RefreshNextStageInteractable()
        {
            if (nextStageButton == null || !disableNextStageWhenUnavailable)
            {
                return;
            }

            nextStageButton.interactable = !_gameEnded && _isInteractive && !ActionLock.IsLocked;
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
