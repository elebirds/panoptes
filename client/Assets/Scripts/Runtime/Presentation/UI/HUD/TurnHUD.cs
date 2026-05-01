/*************************************************
 * Project: Panoptes
 * File: TurnHUD.cs
 * Author: Panoptes Team
 * Date: 2026-04-16
 * Description: Turn/phase HUD that can bind to an external TurnPanel (turnNum).
 *************************************************/

using Panoptes.Core.Application.Cache;
using Panoptes.Core.Domain;
using Panoptes.Core.Events;
using Panoptes.Core.Application.Intents;
using Panoptes.Core.Application.Services;
using Panoptes.Presentation.Common;
using Panoptes.Presentation.Composition;
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
        [SerializeField] private bool autoFindNextStageButton = true;
        [SerializeField] private string nextStageButtonName = "NextStageBtn";
        [SerializeField] private bool disableNextStageWhenUnavailable = true;

        private GameStateCache _cache;
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
        private void Construct(GameIntentService gameIntentService)
        {
            _gameIntentService = gameIntentService;
        }

        private void Awake()
        {
            SceneCommandServiceInjector.InjectIfAvailable(this);
            ResolveExternalTurnPanelReferences();
            ResolveNextStageButtonReference();
            EnsureUi();
            RefreshNextStageInteractable();
        }

        private void OnEnable()
        {
            ResolveExternalTurnPanelReferences();
            ResolveNextStageButtonReference();
            _subscriptions.Clear();
            _cache = GameStateCache.Instance;
            if (_cache != null)
            {
                var cache = _cache;
                _subscriptions.Add(
                    () => cache.OnPhaseChanged += OnPhaseChanged,
                    () => cache.OnPhaseChanged -= OnPhaseChanged);
                _subscriptions.Add(
                    () => cache.OnGameOver += OnGameOver,
                    () => cache.OnGameOver -= OnGameOver);
                _subscriptions.Add(
                    () => cache.OnStateChanged += RefreshFromCache,
                    () => cache.OnStateChanged -= RefreshFromCache);
            }

            _subscriptions.Add(
                () => ActionLock.OnChanged += OnActionLockChanged,
                () => ActionLock.OnChanged -= OnActionLockChanged);
            BindNextStageButton();
            RefreshFromCache();
        }

        private void OnDisable()
        {
            _subscriptions.Clear();
            _buttonSubscriptions.Clear();
            _cache = null;
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

        private void OnPhaseChanged(PhaseChangedEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            _currentTurn = evt.Turn;
            _currentPhase = evt.Phase ?? string.Empty;
            _nextPhase = evt.NextPhase ?? string.Empty;
            _isInteractive = evt.IsInteractive;
            _deadline = evt.TimeoutSeconds > 0 && evt.IsInteractive
                ? Time.unscaledTime + evt.TimeoutSeconds
                : -1f;
            _gameEnded = false;
            _lastRemainingSeconds = int.MinValue;
            RefreshText();
            RefreshNextStageInteractable();
        }

        private void OnGameOver(GameOverEvent _)
        {
            _gameEnded = true;
            _deadline = -1f;
            _lastRemainingSeconds = int.MinValue;
            RefreshText();
            RefreshNextStageInteractable();
        }

        private void RefreshFromCache()
        {
            if (_cache == null)
            {
                return;
            }

            _currentTurn = _cache.Turn;
            _currentPhase = _cache.Phase ?? string.Empty;
            _isInteractive = GamePhases.IsPlanning(_currentPhase) && !_cache.IsGameOver;
            _gameEnded = _cache.IsGameOver;
            if (!_isInteractive)
            {
                _deadline = -1f;
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
                externalTurnPanelRoot = FindRectByName("TrunPanel");
                if (externalTurnPanelRoot == null)
                {
                    externalTurnPanelRoot = FindRectByName("TurnPanel");
                }
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

        private void ResolveNextStageButtonReference()
        {
            if (nextStageButton != null || !autoFindNextStageButton)
            {
                return;
            }

            nextStageButton = SceneObjectFinder.FindFirstSceneObject<Button>(button =>
            {
                return !string.IsNullOrWhiteSpace(button.name) &&
                    string.Equals(button.name, nextStageButtonName, System.StringComparison.OrdinalIgnoreCase);
            });
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

        private void RefreshNextStageInteractable()
        {
            if (nextStageButton == null || !disableNextStageWhenUnavailable)
            {
                return;
            }

            nextStageButton.interactable = !_gameEnded && _isInteractive && !ActionLock.IsLocked;
        }

        private static RectTransform FindRectByName(string name)
        {
            return SceneObjectFinder.FindFirstSceneObject<RectTransform>(rect =>
            {
                return !string.IsNullOrWhiteSpace(rect.name) &&
                    string.Equals(rect.name, name, System.StringComparison.OrdinalIgnoreCase);
            });
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
