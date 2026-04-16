/*************************************************
 * Project: Panoptes
 * File: TokenHUD.cs
 * Author: Panoptes Team
 * Date: 2026-04-13
 * Description: Token/submit-state HUD bound to prefab references only.
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
        private bool _warnedMissingUi;

        private void Awake()
        {
            TryResolveUiReferences(false);
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
            if (!TryResolveUiReferences(true))
            {
                return;
            }

            var cache = _cache ?? GameStateCache.Instance;
            var tokens = cache != null ? cache.TokensLeft : 0;
            tokenText.text = $"Tokens {tokens}";

            if (cache == null)
            {
                stateText.text = "Waiting Sync";
                return;
            }

            if (cache.IsGameOver)
            {
                stateText.text = "Game Over";
                return;
            }

            if (ActionLock.IsLocked)
            {
                stateText.text = "Submitted";
                return;
            }

            stateText.text = GamePhases.IsResolving(cache.Phase) ? "Resolving" : "Ready";
        }

        private bool TryResolveUiReferences(bool logWarning)
        {
            if (root == null)
            {
                root = GetComponent<RectTransform>();
            }

            if (root != null)
            {
                if (tokenText == null)
                {
                    var tokenTransform = root.Find("Tokens");
                    if (tokenTransform != null)
                    {
                        tokenText = tokenTransform.GetComponent<TextMeshProUGUI>();
                    }
                }

                if (stateText == null)
                {
                    var stateTransform = root.Find("State");
                    if (stateTransform != null)
                    {
                        stateText = stateTransform.GetComponent<TextMeshProUGUI>();
                    }
                }
            }

            var ok = root != null && tokenText != null && stateText != null;
            if (!ok && logWarning && !_warnedMissingUi)
            {
                _warnedMissingUi = true;
                Debug.LogWarning("[TokenHUD] Missing UI references. Assign root/tokenText/stateText in prefab.");
            }

            return ok;
        }
    }
}
