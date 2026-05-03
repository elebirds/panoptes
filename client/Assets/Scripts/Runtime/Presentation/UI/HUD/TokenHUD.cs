/*************************************************
 * Project: Panoptes
 * File: TokenHUD.cs
 * Author: Panoptes Team
 * Date: 2026-04-13
 * Description: Token/submit-state HUD bound to prefab references only.
 *************************************************/

using System;
using Panoptes.Presentation.ViewModels;
using R3;
using TMPro;
using UnityEngine;
using VContainer;

namespace Panoptes.Presentation.UI.HUD
{
    public sealed class TokenHUD : MonoBehaviour
    {
        [SerializeField] private RectTransform root;
        [SerializeField] private TextMeshProUGUI tokenText;
        [SerializeField] private TextMeshProUGUI stateText;

        private IDisposable _stateSubscription;
        private TokenHudViewModel _viewModel;
        private bool _warnedMissingUi;

        [Inject]
        private void Construct(TokenHudViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        private void Awake()
        {
            TryResolveUiReferences(false);
        }

        private void OnEnable()
        {
            _stateSubscription?.Dispose();
            _stateSubscription = _viewModel?.State.Subscribe(this, static (state, self) => self.Render(state));
            Render(_viewModel?.Current ?? new TokenHudState(statusText: "Waiting Sync"));
        }

        private void OnDisable()
        {
            _stateSubscription?.Dispose();
            _stateSubscription = null;
        }

        private void Render(TokenHudState state)
        {
            if (!TryResolveUiReferences(true))
            {
                return;
            }

            state ??= new TokenHudState(statusText: "Waiting Sync");
            tokenText.text = state.TokenText;
            stateText.text = state.StatusText;
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
