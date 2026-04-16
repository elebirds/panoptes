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
using TMPro;
using UnityEngine;

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

        private GameStateCache _cache;
        private float _deadline = -1f;
        private string _currentPhase = string.Empty;
        private string _nextPhase = string.Empty;
        private int _currentTurn;
        private bool _isInteractive;
        private bool _gameEnded;

        private void Awake()
        {
            ResolveExternalTurnPanelReferences();
            EnsureUi();
            _cache = GameStateCache.Instance;
        }

        private void OnEnable()
        {
            ResolveExternalTurnPanelReferences();
            _cache = GameStateCache.Instance;
            if (_cache != null)
            {
                _cache.OnPhaseChanged += OnPhaseChanged;
                _cache.OnGameOver += OnGameOver;
                _cache.OnStateChanged += RefreshFromCache;
            }

            RefreshFromCache();
        }

        private void OnDisable()
        {
            if (_cache == null)
            {
                return;
            }

            _cache.OnPhaseChanged -= OnPhaseChanged;
            _cache.OnGameOver -= OnGameOver;
            _cache.OnStateChanged -= RefreshFromCache;
        }

        private void Update()
        {
            if (_deadline <= 0f || _gameEnded)
            {
                return;
            }

            var remaining = Mathf.Max(0, Mathf.CeilToInt(_deadline - Time.unscaledTime));
            var text = remaining > 0
                ? $"{remaining}s"
                : "Waiting server";
            SetDetailText(text);
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
            RefreshText();
        }

        private void OnGameOver(GameOverEvent _)
        {
            _gameEnded = true;
            _deadline = -1f;
            RefreshText();
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

            RefreshText();
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
                externalTurnNumText.text = turnNum;
            }

            if (titleText != null)
            {
                titleText.text = _currentTurn > 0
                    ? $"Turn {_currentTurn}"
                    : "Waiting game start";
            }

            if (_gameEnded)
            {
                SetDetailText("Game Over");
                return;
            }

            var phaseText = GamePhases.ToDisplayText(_currentPhase);
            if (_isInteractive)
            {
                SetDetailText(phaseText);
                return;
            }

            if (!string.IsNullOrWhiteSpace(_nextPhase))
            {
                SetDetailText($"{phaseText}\nNext: {GamePhases.ToDisplayText(_nextPhase)}");
                return;
            }

            SetDetailText(phaseText);
        }

        private void SetDetailText(string value)
        {
            if (detailText != null)
            {
                detailText.text = value;
            }

            if (externalPhaseText != null)
            {
                externalPhaseText.text = value;
            }
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

        private static RectTransform FindRectByName(string name)
        {
            var all = UnityEngine.Object.FindObjectsByType<RectTransform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var i = 0; i < all.Length; i++)
            {
                var rect = all[i];
                if (rect == null || string.IsNullOrWhiteSpace(rect.name))
                {
                    continue;
                }

                if (string.Equals(rect.name, name, System.StringComparison.OrdinalIgnoreCase))
                {
                    return rect;
                }
            }

            return null;
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
