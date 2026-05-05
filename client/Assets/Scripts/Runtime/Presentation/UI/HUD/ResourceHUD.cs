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
using Panoptes.Presentation.Map;
using Panoptes.Presentation.ViewModels;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using R3;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Panoptes.Presentation.UI.HUD
{
    public sealed class ResourceHUD : MonoBehaviour
    {
        private const string MinisterAttentionBadgeName = "MinisterAttentionBadge";

        [Header("Root")]
        [SerializeField] private RectTransform resourceListRoot;
        [SerializeField] private Button techButton;
        [SerializeField] private Button policyButton;
        [SerializeField] private Button ministerButton;

        [Header("Data")]
        [SerializeField] private bool includePoints = true;
        [SerializeField] private bool logWarnings = false;

        [Header("Icons")]
        [SerializeField] private string[] iconResourcesRoots = { "Icons/Resources", "Icons/Points", "Icons" };
        [SerializeField] private string panelBackgroundSpriteResource = "Textures/UI/resource_panel_parchment_bg";
        [SerializeField] private string techButtonSpriteResource = "Icons/UI/icon_tech_tree_round";
        [SerializeField] private string policyButtonSpriteResource = "Icons/UI/icon_policy_round";
        [SerializeField] private string ministerButtonSpriteResource = "Icons/UI/icon_minister_round";
        [SerializeField] private Vector2 managementButtonSize = new(54f, 54f);
        [SerializeField] private float managementButtonSpacing = 8f;

        [Header("Change Hint")]
        [SerializeField] private Color increaseColor = new Color(0.15f, 0.95f, 0.35f, 1f);
        [SerializeField] private Color decreaseColor = new Color(0.95f, 0.25f, 0.25f, 1f);

        private static Sprite _ministerAttentionSprite;

        private readonly EventSubscriptionBag _buttonSubscriptions = new();
        private ResourceHudUguiBinder _binder;
        private ManagementPanelVisibilityStore _managementPanelVisibilityStore;
        private MapPlanningInputController _mapPlanningInputController;
        private GameObject _ministerAttentionBadge;
        private IDisposable _ministerAttentionSubscription;
        private PlanningDraftStore _planningDraftStore;
        private IDisposable _stateSubscription;
        private ResourceHudViewModel _viewModel;

        [Inject]
        private void Construct(
            ResourceHudViewModel viewModel,
            ManagementPanelVisibilityStore managementPanelVisibilityStore)
        {
            _viewModel = viewModel;
            _managementPanelVisibilityStore = managementPanelVisibilityStore;
            ResolvePrefabReferences();
            BindManagementButtons();
        }

        [Inject]
        private void ConstructMinisterAttention(PlanningDraftStore planningDraftStore)
        {
            _planningDraftStore = planningDraftStore;
            SubscribeMinisterAttention();
            UpdateMinisterAttentionBadge();
        }

        [Inject]
        private void ConstructPlanningInput(
            MapPlanningInputController mapPlanningInputController)
        {
            _mapPlanningInputController = mapPlanningInputController;
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
            SubscribeMinisterAttention();
            BindManagementButtons();
            ApplyGeneratedPanelArt();
            _binder?.Render(_viewModel?.Current ?? new ResourceHudState());
            UpdateMinisterAttentionBadge();
        }

        private void OnDisable()
        {
            UnsubscribeState();
            UnsubscribeMinisterAttention();
            UnbindTechButton();
            _binder?.StopAllHideCoroutines();
        }

        private void OnDestroy()
        {
            UnsubscribeMinisterAttention();
            _binder?.Dispose();
            _binder = null;
        }

        private void ResolvePrefabReferences()
        {
            ResolveResourceListRoot();
            ResolveTechButtonReference();
            EnsurePolicyButtonReference();
            EnsureMinisterButtonReference();
            LayoutManagementButtons();
            EnsureMinisterAttentionBadge();
            UpdateMinisterAttentionBadge();
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

            var clone = CreateManagementButtonClone("MinisterBtn");
            if (clone == null)
            {
                return;
            }

            ministerButton = clone.GetComponent<Button>();
        }

        private void EnsurePolicyButtonReference()
        {
            if (policyButton != null)
            {
                return;
            }

            var policyBtnTransform = transform.Find("PolicyBtn");
            if (policyBtnTransform != null)
            {
                policyButton = policyBtnTransform.GetComponent<Button>();
                return;
            }

            var clone = CreateManagementButtonClone("PolicyBtn");
            if (clone == null)
            {
                return;
            }

            policyButton = clone.GetComponent<Button>();
        }

        private GameObject CreateManagementButtonClone(string buttonName)
        {
            if (techButton == null)
            {
                return null;
            }

            var clone = Instantiate(techButton.gameObject, techButton.transform.parent, false);
            clone.name = buttonName;
            var cloneButton = clone.GetComponent<Button>();
            cloneButton?.onClick.RemoveAllListeners();
            return clone;
        }

        private void LayoutManagementButtons()
        {
            var origin = ResolveManagementButtonOrigin();
            LayoutManagementButton(techButton, origin);
            LayoutManagementButton(policyButton, origin + new Vector2(managementButtonSize.x + managementButtonSpacing, 0f));
            LayoutManagementButton(ministerButton, origin + new Vector2((managementButtonSize.x + managementButtonSpacing) * 2f, 0f));
        }

        private Vector2 ResolveManagementButtonOrigin()
        {
            var techRect = techButton != null ? techButton.transform as RectTransform : null;
            return techRect != null ? techRect.anchoredPosition : Vector2.zero;
        }

        private void LayoutManagementButton(Button button, Vector2 anchoredPosition)
        {
            if (button == null || techButton == null)
            {
                return;
            }

            var rect = button.transform as RectTransform;
            var techRect = techButton.transform as RectTransform;
            if (rect != null && techRect != null)
            {
                rect.anchorMin = techRect.anchorMin;
                rect.anchorMax = techRect.anchorMax;
                rect.pivot = techRect.pivot;
                rect.sizeDelta = managementButtonSize;
                rect.anchoredPosition = anchoredPosition;
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

        private void SubscribeMinisterAttention()
        {
            if (_planningDraftStore == null || _ministerAttentionSubscription != null)
            {
                return;
            }

            _ministerAttentionSubscription = _planningDraftStore.State.Subscribe(
                this,
                static (state, self) => self.UpdateMinisterAttentionBadge(state));
        }

        private void UnsubscribeMinisterAttention()
        {
            _ministerAttentionSubscription?.Dispose();
            _ministerAttentionSubscription = null;
        }

        private void BindManagementButtons()
        {
            _buttonSubscriptions.Clear();
            ResolveTechButtonReference();
            EnsurePolicyButtonReference();
            EnsureMinisterButtonReference();
            LayoutManagementButtons();

            BindButton(techButton, OnTechButtonClicked);
            BindButton(policyButton, OnPolicyButtonClicked);
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

            OpenManagementPanel(ManagementPanelId.TechTree);
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

            OpenManagementPanel(ManagementPanelId.MinisterReport);
        }

        private void EnsureMinisterAttentionBadge()
        {
            if (ministerButton == null)
            {
                return;
            }

            var buttonRect = ministerButton.transform as RectTransform;
            if (buttonRect == null)
            {
                return;
            }

            if (_ministerAttentionBadge != null)
            {
                return;
            }

            var existing = buttonRect.Find(MinisterAttentionBadgeName);
            if (existing != null)
            {
                _ministerAttentionBadge = existing.gameObject;
                return;
            }

            var badge = new GameObject(MinisterAttentionBadgeName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            badge.transform.SetParent(buttonRect, false);
            var rect = badge.transform as RectTransform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(20f, 20f);
            rect.anchoredPosition = new Vector2(-2f, 2f);

            var image = badge.GetComponent<Image>();
            image.sprite = CreateMinisterAttentionSprite();
            image.preserveAspect = true;
            image.raycastTarget = false;
            _ministerAttentionBadge = badge;
        }

        private void UpdateMinisterAttentionBadge()
        {
            UpdateMinisterAttentionBadge(_planningDraftStore?.Snapshot);
        }

        private void UpdateMinisterAttentionBadge(PlanningDraftState state)
        {
            EnsureMinisterAttentionBadge();
            if (_ministerAttentionBadge != null)
            {
                _ministerAttentionBadge.SetActive(HasInteractiveMinisterDrafts(state?.MinisterDrafts));
            }
        }

        private static bool HasInteractiveMinisterDrafts(System.Collections.Generic.IReadOnlyList<MinisterDraftDto> drafts)
        {
            if (drafts == null)
            {
                return false;
            }

            for (var i = 0; i < drafts.Count; i++)
            {
                if (drafts[i]?.IsInteractive == true)
                {
                    return true;
                }
            }

            return false;
        }

        private static Sprite CreateMinisterAttentionSprite()
        {
            if (_ministerAttentionSprite != null)
            {
                return _ministerAttentionSprite;
            }

            const int size = 32;
            const float center = (size - 1) * 0.5f;
            const float radius = 14.5f;
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x - center;
                    var dy = y - center;
                    var insideCircle = dx * dx + dy * dy <= radius * radius;
                    var isStroke = insideCircle && dx * dx + dy * dy >= (radius - 1.7f) * (radius - 1.7f);
                    var isBar = x >= 14 && x <= 17 && y >= 12 && y <= 23;
                    var isDot = x >= 14 && x <= 17 && y >= 7 && y <= 10;
                    pixels[y * size + x] = isBar || isDot
                        ? new Color32(0, 0, 0, 255)
                        : insideCircle
                            ? isStroke
                                ? new Color32(255, 126, 126, 255)
                                : new Color32(230, 25, 40, 255)
                            : new Color32(0, 0, 0, 0);
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.ARGB32, false)
            {
                name = "MinisterAttentionBadgeRuntime",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            _ministerAttentionSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            _ministerAttentionSprite.name = "MinisterAttentionBadgeRuntime";
            return _ministerAttentionSprite;
        }

        private void OnPolicyButtonClicked()
        {
            if (_managementPanelVisibilityStore == null)
            {
                if (logWarnings)
                {
                    Debug.LogWarning("[ResourceHUD] ManagementPanelVisibilityStore not injected.");
                }
                return;
            }

            OpenManagementPanel(ManagementPanelId.PolicyFocus);
        }

        private void OpenManagementPanel(ManagementPanelId panelId)
        {
            _mapPlanningInputController?.CancelCurrentMode();
            _managementPanelVisibilityStore?.Toggle(panelId);
        }

        private void ApplyGeneratedPanelArt()
        {
            ResolvePrefabReferences();
            ApplyPanelBackground();
            ApplyButtonSprite(techButton, techButtonSpriteResource);
            ApplyButtonSprite(policyButton, policyButtonSpriteResource);
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
                    resourceListRoot,
                    iconResourcesRoots,
                    increaseColor,
                    decreaseColor);
                return;
            }

            _binder.RebindReferences(
                resourceListRoot,
                iconResourcesRoots,
                increaseColor,
                decreaseColor);
        }
    }
}
