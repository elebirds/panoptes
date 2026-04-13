/*************************************************
 * Project: Panoptes
 * File: TurnHUD.cs
 * Author: Panoptes Team
 * Date: 2026-04-13
 * Description: Runtime-built turn/phase HUD.
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
        [SerializeField] private RectTransform root;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI detailText;

        private GameStateCache _cache;
        private float _deadline = -1f;
        private string _currentPhase = string.Empty;
        private string _nextPhase = string.Empty;
        private int _currentTurn;
        private bool _isInteractive;
        private bool _gameEnded;

        private void Awake()
        {
            EnsureUi();
            _cache = GameStateCache.Instance;
        }

        private void OnEnable()
        {
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
            if (_deadline <= 0f || detailText == null || _gameEnded)
            {
                return;
            }

            var remaining = Mathf.Max(0, Mathf.CeilToInt(_deadline - Time.unscaledTime));
            detailText.text = remaining > 0
                ? $"剩余 {remaining}s"
                : "等待服务器推进";
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
            if (titleText == null || detailText == null)
            {
                return;
            }

            titleText.text = _currentTurn > 0
                ? $"第 {_currentTurn} 回合"
                : "等待对局开始";

            if (_gameEnded)
            {
                detailText.text = "本局已结束";
                return;
            }

            var phaseText = GamePhases.ToDisplayText(_currentPhase);
            if (_isInteractive)
            {
                detailText.text = phaseText;
                return;
            }

            if (!string.IsNullOrWhiteSpace(_nextPhase))
            {
                detailText.text = $"{phaseText}\n即将进入 {GamePhases.ToDisplayText(_nextPhase)}";
                return;
            }

            detailText.text = phaseText;
        }

        private void EnsureUi()
        {
            if (root == null)
            {
                root = GetComponent<RectTransform>();
                if (root == null)
                {
                    root = gameObject.AddComponent<RectTransform>();
                }
            }

            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = new Vector2(28f, -24f);
            root.sizeDelta = new Vector2(320f, 92f);

            titleText ??= CreateText("Title", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, -32f), 28f, FontStyles.Bold);
            detailText ??= CreateText("Detail", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, -84f), 22f, FontStyles.Normal);
            detailText.enableWordWrapping = true;
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
