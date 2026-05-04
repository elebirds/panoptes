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
        [SerializeField] private Button ministerButton;

        [Header("Data")]
        [SerializeField] private bool includePoints = true;
        [SerializeField] private bool logWarnings = false;

        [Header("Icons")]
        [SerializeField] private string[] iconResourcesRoots = { "Icons/Resources", "Icons/Points", "Icons" };
        [SerializeField] private string panelBackgroundSpriteResource = "Textures/UI/resource_panel_parchment_bg";
        [SerializeField] private string techButtonSpriteResource = "Icons/UI/icon_tech_tree_round";
        [SerializeField] private string ministerButtonSpriteResource = "Icons/UI/icon_minister_round";
        [SerializeField] private Vector2 managementButtonSize = new(54f, 54f);
        [SerializeField] private float managementButtonSpacing = 8f;

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
            BindManagementButtons();
        }

        private void Awake()
        {
            ResolvePrefabReferences();
            ApplyGeneratedPanelArt();
        }

        private void OnEnable()
        {
            ResolvePrefabReferences();
            EnsureBinder();
            _viewModel?.SetIncludePoints(includePoints);
            SubscribeState();
            BindManagementButtons();
            ApplyGeneratedPanelArt();
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
            EnsureMinisterButtonReference();
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

        private void EnsureMinisterButtonReference()
        {
            if (ministerButton != null)
            {
                return;
            }

            var ministerBtnTransform = transform.Find("MinisterBtn");
            if (ministerBtnTransform != null)
            {
                ministerButton = ministerBtnTransform.GetComponent<Button>();
                return;
            }

            if (techButton == null)
            {
                return;
            }

            var clone = Instantiate(techButton.gameObject, techButton.transform.parent, false);
            clone.name = "MinisterBtn";
            ministerButton = clone.GetComponent<Button>();
            ministerButton?.onClick.RemoveAllListeners();
            var rect = clone.transform as RectTransform;
            var techRect = techButton.transform as RectTransform;
            if (rect != null && techRect != null)
            {
                rect.anchorMin = techRect.anchorMin;
                rect.anchorMax = techRect.anchorMax;
                rect.pivot = techRect.pivot;
                rect.sizeDelta = managementButtonSize;
                rect.anchoredPosition = techRect.anchoredPosition + new Vector2(managementButtonSize.x + managementButtonSpacing, 0f);
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

        private void BindManagementButtons()
        {
            _buttonSubscriptions.Clear();
            ResolveTechButtonReference();
            EnsureMinisterButtonReference();

            BindButton(techButton, OnTechButtonClicked);
            BindButton(ministerButton, OnMinisterButtonClicked);
        }

        private void BindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null || action == null)
            {
                return;
            }

            _buttonSubscriptions.Add(
                () => button.onClick.AddListener(action),
                () =>
                {
                    if (button != null)
                    {
                        button.onClick.RemoveListener(action);
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

        private void OnMinisterButtonClicked()
        {
            if (_managementPanelVisibilityStore == null)
            {
                if (logWarnings)
                {
                    Debug.LogWarning("[ResourceHUD] ManagementPanelVisibilityStore not injected.");
                }
                return;
            }

            _managementPanelVisibilityStore.Toggle(ManagementPanelId.PolicyFocus);
        }

        private void ApplyGeneratedPanelArt()
        {
            ResolvePrefabReferences();
            ApplyPanelBackground();
            ApplyButtonSprite(techButton, techButtonSpriteResource);
            ApplyButtonSprite(ministerButton, ministerButtonSpriteResource);
        }

        private void ApplyPanelBackground()
        {
            if (string.IsNullOrWhiteSpace(panelBackgroundSpriteResource))
            {
                return;
            }

            var image = GetComponent<Image>();
            if (image == null)
            {
                image = gameObject.AddComponent<Image>();
            }

            var sprite = Resources.Load<Sprite>(panelBackgroundSpriteResource.Trim());
            if (sprite == null)
            {
                return;
            }

            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.color = Color.white;
            image.raycastTarget = false;
        }

        private void ApplyButtonSprite(Button button, string spriteResource)
        {
            if (button == null || string.IsNullOrWhiteSpace(spriteResource))
            {
                return;
            }

            var image = button.targetGraphic as Image;
            if (image == null)
            {
                image = button.GetComponent<Image>();
            }

            var sprite = Resources.Load<Sprite>(spriteResource.Trim());
            if (image == null || sprite == null)
            {
                return;
            }

            var rect = button.transform as RectTransform;
            if (rect != null)
            {
                rect.sizeDelta = managementButtonSize;
            }

            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;

            var labels = button.GetComponentsInChildren<TMPro.TMP_Text>(true);
            for (var i = 0; i < labels.Length; i++)
            {
                labels[i].text = string.Empty;
            }
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
