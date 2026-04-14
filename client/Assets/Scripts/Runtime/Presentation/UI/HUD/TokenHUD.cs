/*************************************************
 * Project: Panoptes
 * File: TokenHUD.cs
 * Author: Panoptes Team
 * Date: 2026-04-13
 * Description: Runtime-built token/submit-state HUD.
 *************************************************/

using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Intents;
using Panoptes.Core.Domain;
using Panoptes.Core.Events;
using TMPro;
using UnityEngine;

namespace Panoptes.Presentation.UI.HUD
{
    public sealed class TokenHUD : MonoBehaviour
    {
        [SerializeField] private RectTransform root;
        [SerializeField] private TextMeshProUGUI tokenText;
        [SerializeField] private TextMeshProUGUI stateText;

        private GameStateCache _cache;

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
                _cache.OnTokensChanged += OnTokensChanged;
                _cache.OnPhaseChanged += OnPhaseChanged;
                _cache.OnGameOver += OnGameOver;
                _cache.OnStateChanged += Refresh;
            }

            ActionLock.OnChanged += OnActionLockChanged;
            Refresh();
        }

        private void OnDisable()
        {
            if (_cache != null)
            {
                _cache.OnTokensChanged -= OnTokensChanged;
                _cache.OnPhaseChanged -= OnPhaseChanged;
                _cache.OnGameOver -= OnGameOver;
                _cache.OnStateChanged -= Refresh;
            }

            ActionLock.OnChanged -= OnActionLockChanged;
        }

        private void OnTokensChanged(TokensChangedEvent _)
        {
            Refresh();
        }

        private void OnPhaseChanged(PhaseChangedEvent _)
        {
            Refresh();
        }

        private void OnGameOver(GameOverEvent _)
        {
            Refresh();
        }

        private void OnActionLockChanged(bool _)
        {
            Refresh();
        }

        private void Refresh()
        {
            EnsureUi();
            if (tokenText == null || stateText == null)
            {
                return;
            }

            var cache = _cache ?? GameStateCache.Instance;
            var tokens = cache != null ? cache.TokensLeft : 0;
            tokenText.text = $"令牌 {tokens}";

            if (cache == null)
            {
                stateText.text = "等待同步";
                return;
            }

            if (cache.IsGameOver)
            {
                stateText.text = "对局结束";
                return;
            }

            if (ActionLock.IsLocked)
            {
                stateText.text = "已提交，等待推进";
                return;
            }

            stateText.text = GamePhases.IsResolving(cache.Phase)
                ? "服务器结算中"
                : "可继续操作";
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

            root.anchorMin = new Vector2(1f, 1f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(1f, 1f);
            root.anchoredPosition = new Vector2(-28f, -24f);
            root.sizeDelta = new Vector2(260f, 88f);

            tokenText ??= CreateText("Tokens", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), 28f, FontStyles.Bold);
            stateText ??= CreateText("State", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -34f), 20f, FontStyles.Normal);
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
            rect.sizeDelta = new Vector2(240f, 32f);

            var text = rect.GetComponent<TextMeshProUGUI>();
            if (text == null)
            {
                text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            }

            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopRight;
            text.raycastTarget = false;
            return text;
        }
    }
}
