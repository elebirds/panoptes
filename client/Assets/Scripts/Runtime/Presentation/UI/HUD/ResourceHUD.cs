/*************************************************
 * Project: Panoptes
 * File: ResourceHUD.cs
 * Author: Panoptes Team
 * Date: 2026-04-16
 * Description: Dynamic resource panel HUD with per-item delta hints and tech button.
 *************************************************/

using System;
using Panoptes.Presentation.Binders.Ugui;
using Panoptes.Presentation.Common;
using Panoptes.Presentation.ViewModels;
using R3;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Panoptes.Presentation.UI.HUD
{
    public sealed class ResourceHUD : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private RectTransform resourceListRoot;
        [SerializeField] private Button techButton;

        [Header("Data")]
        [SerializeField] private bool includePoints = true;
        [SerializeField] private bool logWarnings = false;

        [Header("Icons")]
        [SerializeField] private string[] iconResourcesRoots = { "Icons/Resources", "Icons/Points", "Icons" };

        [Header("Change Hint")]
        [SerializeField] private float changeVisibleSeconds = 3f;
        [SerializeField] private Color increaseColor = new Color(0.15f, 0.95f, 0.35f, 1f);
        [SerializeField] private Color decreaseColor = new Color(0.95f, 0.25f, 0.25f, 1f);

        private readonly EventSubscriptionBag _buttonSubscriptions = new();
        private ResourceHudUguiBinder _binder;
        private ManagementPanelVisibilityStore _managementPanelVisibilityStore;
        private IDisposable _stateSubscription;
        private ResourceHudViewModel _viewModel;

        [Inject]
        private void Construct(ResourceHudViewModel viewModel, ManagementPanelVisibilityStore managementPanelVisibilityStore)
        {
            _viewModel = viewModel;
            _managementPanelVisibilityStore = managementPanelVisibilityStore;
            ResolvePrefabReferences();
            BindTechButton();
        }

        private void Awake()
        {
            ResolvePrefabReferences();
        }

        private void OnEnable()
        {
            ResolvePrefabReferences();
            EnsureBinder();
            _viewModel?.SetIncludePoints(includePoints);
            SubscribeState();
            BindTechButton();
            _binder?.Render(_viewModel?.Current ?? new ResourceHudState());
        }

        private void OnDisable()
        {
            UnsubscribeState();
            UnbindTechButton();
            _binder?.StopAllHideCoroutines();
        }

        private void OnDestroy()
        {
            _binder?.Dispose();
            _binder = null;
        }

        private void ResolvePrefabReferences()
        {
            ResolveResourceListRoot();
            ResolveTechButtonReference();
        }

        private void ResolveResourceListRoot()
        {
            if (resourceListRoot != null)
            {
                return;
            }

            var list = transform.Find("ResourceList");
            resourceListRoot = list as RectTransform;
        }

        private void ResolveTechButtonReference()
        {
            if (techButton != null)
            {
                return;
            }

            var techBtnTransform = transform.Find("TechBtn");
            if (techBtnTransform != null)
            {
                techButton = techBtnTransform.GetComponent<Button>();
            }
        }

        private void SubscribeState()
        {
            _stateSubscription?.Dispose();
            _stateSubscription = _viewModel?.State.Subscribe(this, static (state, self) => self._binder?.Render(state));
        }

        private void UnsubscribeState()
        {
            _stateSubscription?.Dispose();
            _stateSubscription = null;
        }

        private void BindTechButton()
        {
            _buttonSubscriptions.Clear();
            ResolveTechButtonReference();

            var button = techButton;
            if (button == null)
            {
                return;
            }

            _buttonSubscriptions.Add(
                () => button.onClick.AddListener(OnTechButtonClicked),
                () =>
                {
                    if (button != null)
                    {
                        button.onClick.RemoveListener(OnTechButtonClicked);
                    }
                });
        }

        private void UnbindTechButton()
        {
            _buttonSubscriptions.Clear();
        }

        private void OnTechButtonClicked()
        {
            if (_managementPanelVisibilityStore == null)
            {
                if (logWarnings)
                {
                    Debug.LogWarning("[ResourceHUD] ManagementPanelVisibilityStore not injected.");
                }
                return;
            }

            _managementPanelVisibilityStore.Toggle(ManagementPanelId.TechTree);
        }

        private void EnsureBinder()
        {
            if (_binder == null)
            {
                _binder = new ResourceHudUguiBinder(
                    this,
                    resourceListRoot,
                    iconResourcesRoots,
                    changeVisibleSeconds,
                    increaseColor,
                    decreaseColor);
                return;
            }

            _binder.RebindReferences(
                resourceListRoot,
                iconResourcesRoots,
                changeVisibleSeconds,
                increaseColor,
                decreaseColor);
        }
    }
}
