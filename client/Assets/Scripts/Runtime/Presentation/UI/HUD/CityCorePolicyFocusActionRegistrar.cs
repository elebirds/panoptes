using System;
using System.Collections;
using Panoptes.Core.Application.Stores;
using Panoptes.Presentation.ViewModels;
using R3;
using UnityEngine;
using VContainer;

namespace Panoptes.Presentation.UI.HUD
{
    public sealed class CityCorePolicyFocusActionRegistrar : UnitInfoActionProviderBase
    {
        [Header("Unit Info Action")]
#pragma warning disable CS0414
        [SerializeField] private string policyActionId = "open_policy_focus";
        [SerializeField] private string policyActionLabel = "国策";
#pragma warning restore CS0414

        [Header("Initial Policy Prompt")]
        [SerializeField] private bool showPolicyFocusOnStart = true;
        [SerializeField] private int initialPolicyFocusMaxWaitFrames = 90;

        private BuildCatalogContextStore _buildCatalogContextStore;
        private ManagementPanelVisibilityStore _managementPanelVisibilityStore;
        private RecipeSynthesisContextStore _recipeSynthesisContextStore;
        private StaticCatalogStore _staticCatalogStore;
        private Coroutine _initialPolicyFocusRoutine;
        private IDisposable _catalogSubscription;
        private bool _initialPolicyFocusShown;

        protected override void RegisterActions(UnitInfoActionRegistry registry)
        {
            // The policy panel is now opened from the resource HUD. Keep this registrar
            // only for the initial policy prompt and do not add a city-core info button.
        }

        [Inject]
        private void Construct(
            StaticCatalogStore staticCatalogStore,
            ManagementPanelVisibilityStore managementPanelVisibilityStore,
            BuildCatalogContextStore buildCatalogContextStore,
            RecipeSynthesisContextStore recipeSynthesisContextStore)
        {
            _staticCatalogStore = staticCatalogStore;
            _managementPanelVisibilityStore = managementPanelVisibilityStore;
            _buildCatalogContextStore = buildCatalogContextStore;
            _recipeSynthesisContextStore = recipeSynthesisContextStore;
            _catalogSubscription?.Dispose();
            _catalogSubscription = _staticCatalogStore.State.Subscribe(this, static (_, self) => self.TryShowInitialPolicyFocus());
        }

        private void Start()
        {
            if (_initialPolicyFocusRoutine != null)
            {
                StopCoroutine(_initialPolicyFocusRoutine);
            }

            _initialPolicyFocusRoutine = StartCoroutine(ShowInitialPolicyFocusAfterCatalogReady());
        }

        private void OnDisable()
        {
            if (_initialPolicyFocusRoutine == null)
            {
                return;
            }

            StopCoroutine(_initialPolicyFocusRoutine);
            _initialPolicyFocusRoutine = null;
        }

        private IEnumerator ShowInitialPolicyFocusAfterCatalogReady()
        {
            yield return null;
            if (!showPolicyFocusOnStart || _initialPolicyFocusShown)
            {
                _initialPolicyFocusRoutine = null;
                yield break;
            }

            var remainingFrames = Mathf.Max(0, initialPolicyFocusMaxWaitFrames);
            while (!HasPolicyChoices() && remainingFrames > 0)
            {
                remainingFrames--;
                yield return null;
            }

            if (HasPolicyChoices())
            {
                TryShowInitialPolicyFocus();
            }

            _initialPolicyFocusRoutine = null;
        }

        private void ShowPolicyFocus()
        {
            if (_managementPanelVisibilityStore == null)
            {
                PanoptesLog.Warning("[CityCorePolicyFocusActionRegistrar] Policy focus visibility store is missing.");
                return;
            }

            _buildCatalogContextStore?.Clear();
            _recipeSynthesisContextStore?.Clear();
            ClearEditorPreviewSelection();
            _managementPanelVisibilityStore.Show(ManagementPanelId.PolicyFocus);
        }

        private bool IsCityCoreUnit(Map.UnitView unit)
        {
            if (unit == null)
            {
                return false;
            }

            return string.Equals(NormalizeToken(unit.UnitType), "city_core", StringComparison.Ordinal) ||
                   string.Equals(NormalizeToken(unit.UnitId), "city_core", StringComparison.Ordinal);
        }

        private void TryShowInitialPolicyFocus()
        {
            if (!showPolicyFocusOnStart || _initialPolicyFocusShown || !HasPolicyChoices())
            {
                return;
            }

            _initialPolicyFocusShown = true;
            ShowPolicyFocus();
        }

        private bool HasPolicyChoices()
        {
            var policies = _staticCatalogStore?.Snapshot?.Policies;
            return policies != null && policies.Count > 0;
        }

        private static string NormalizeToken(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
        }

        private void OnDestroy()
        {
            _catalogSubscription?.Dispose();
            _catalogSubscription = null;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void ClearEditorPreviewSelection()
        {
#if UNITY_EDITOR
            UnityEditor.Selection.activeObject = null;
#endif
        }
    }
}
