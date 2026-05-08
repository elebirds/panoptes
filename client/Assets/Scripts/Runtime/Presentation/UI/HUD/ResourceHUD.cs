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
        private const string InstitutionAttentionBadgeName = "InstitutionAttentionBadge";
        private const string MinisterThinkingFeedbackCode = "minister_thinking";
        private const string MinisterThinkingFeedbackMessage = "大臣正在思考中";

        [Header("Root")]
        [SerializeField] private RectTransform resourceListRoot;
        [SerializeField] private Button techButton;
        [SerializeField] private Button policyButton;
        [SerializeField] private Button institutionButton;
        [SerializeField] private Button ministerButton;

        [Header("Data")]
        [SerializeField] private bool includePoints = true;
        [SerializeField] private bool logWarnings = false;

        [Header("Icons")]
        [SerializeField] private string[] iconResourcesRoots = { "Icons/Resources", "Icons/Points", "Icons" };
        [SerializeField] private string panelBackgroundSpriteResource = "Textures/UI/resource_panel_parchment_bg";
        [SerializeField] private string techButtonSpriteResource = "Icons/UI/icon_tech_tree_round";
        [SerializeField] private string policyButtonSpriteResource = "Icons/UI/icon_policy_round";
        [SerializeField] private string institutionButtonSpriteResource = "Icons/UI/icon_institution_round";
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
        private GameObject _institutionAttentionBadge;
        private IDisposable _institutionAttentionSubscription;
        private InstitutionViewModel _institutionViewModel;
        private GameObject _ministerAttentionBadge;
        private IDisposable _ministerAttentionSubscription;
        private IDisposable _ministerTurnSubscription;
        private GameplayFeedbackStore _feedbackStore;
        private PlanningDraftStore _planningDraftStore;
        private IDisposable _stateSubscription;
        private TurnStore _turnStore;
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
        private void ConstructMinisterGate(
            TurnStore turnStore,
            GameplayFeedbackStore feedbackStore)
        {
            _turnStore = turnStore;
            _feedbackStore = feedbackStore;
            SubscribeMinisterTurn();
            UpdateMinisterAttentionBadge();
        }

        [Inject]
        private void ConstructInstitutionAttention(InstitutionViewModel institutionViewModel)
        {
            _institutionViewModel = institutionViewModel;
            SubscribeInstitutionAttention();
            UpdateInstitutionAttentionBadge();
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
            SubscribeInstitutionAttention();
            SubscribeMinisterAttention();
            SubscribeMinisterTurn();
            BindManagementButtons();
            ApplyGeneratedPanelArt();
            _binder?.Render(_viewModel?.Current ?? new ResourceHudState());
            UpdateInstitutionAttentionBadge();
            UpdateMinisterAttentionBadge();
        }

        private void OnDisable()
        {
            UnsubscribeState();
            UnsubscribeInstitutionAttention();
            UnsubscribeMinisterAttention();
            UnsubscribeMinisterTurn();
            UnbindTechButton();
            _binder?.StopAllHideCoroutines();
        }

        private void OnDestroy()
        {
            UnsubscribeMinisterTurn();
            UnsubscribeMinisterAttention();
            UnsubscribeInstitutionAttention();
            _binder?.Dispose();
            _binder = null;
        }

        private void ResolvePrefabReferences()
        {
            ResolveResourceListRoot();
            ResolveTechButtonReference();
            EnsurePolicyButtonReference();
            EnsureInstitutionButtonReference();
            EnsureMinisterButtonReference();
            LayoutManagementButtons();
            EnsureInstitutionAttentionBadge();
            EnsureMinisterAttentionBadge();
            UpdateInstitutionAttentionBadge();
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

        private void EnsureInstitutionButtonReference()
        {
            if (institutionButton != null)
            {
                return;
            }

            var institutionBtnTransform = transform.Find("InstitutionBtn");
            if (institutionBtnTransform != null)
            {
                institutionButton = institutionBtnTransform.GetComponent<Button>();
                return;
            }

            var clone = CreateManagementButtonClone("InstitutionBtn");
            if (clone == null)
            {
                return;
            }

            institutionButton = clone.GetComponent<Button>();
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
            LayoutManagementButton(institutionButton, origin + new Vector2((managementButtonSize.x + managementButtonSpacing) * 2f, 0f));
            LayoutManagementButton(ministerButton, origin + new Vector2((managementButtonSize.x + managementButtonSpacing) * 3f, 0f));
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

        private void SubscribeMinisterTurn()
        {
            if (_turnStore == null || _ministerTurnSubscription != null)
            {
                return;
            }

            _ministerTurnSubscription = _turnStore.State.Subscribe(
                this,
                static (_, self) => self.UpdateMinisterAttentionBadge());
        }

        private void UnsubscribeMinisterTurn()
        {
            _ministerTurnSubscription?.Dispose();
            _ministerTurnSubscription = null;
        }

        private void SubscribeInstitutionAttention()
        {
            if (_institutionViewModel == null || _institutionAttentionSubscription != null)
            {
                return;
            }

            _institutionAttentionSubscription = _institutionViewModel.State.Subscribe(
                this,
                static (_, self) => self.UpdateInstitutionAttentionBadge());
        }

        private void UnsubscribeInstitutionAttention()
        {
            _institutionAttentionSubscription?.Dispose();
            _institutionAttentionSubscription = null;
        }

        private void BindManagementButtons()
        {
            _buttonSubscriptions.Clear();
            ResolveTechButtonReference();
            EnsurePolicyButtonReference();
            EnsureInstitutionButtonReference();
            EnsureMinisterButtonReference();
            LayoutManagementButtons();

            BindButton(techButton, OnTechButtonClicked);
            BindButton(policyButton, OnPolicyButtonClicked);
            BindButton(institutionButton, OnInstitutionButtonClicked);
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
                    PanoptesLog.Warning("[ResourceHUD] ManagementPanelVisibilityStore not injected.");
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
                    PanoptesLog.Warning("[ResourceHUD] ManagementPanelVisibilityStore not injected.");
                }
                return;
            }

            if (!HasCurrentTurnMinisterDrafts(_planningDraftStore?.Snapshot, _turnStore?.Snapshot))
            {
                PublishMinisterThinkingFeedback();
                return;
            }

            OpenManagementPanel(ManagementPanelId.MinisterReport);
        }

        private void OnInstitutionButtonClicked()
        {
            if (_managementPanelVisibilityStore == null)
            {
                if (logWarnings)
                {
                    PanoptesLog.Warning("[ResourceHUD] ManagementPanelVisibilityStore not injected.");
                }
                return;
            }

            OpenManagementPanel(ManagementPanelId.Institutions);
        }

        private void EnsureInstitutionAttentionBadge()
        {
            if (institutionButton == null)
            {
                return;
            }

            var buttonRect = institutionButton.transform as RectTransform;
            if (buttonRect == null)
            {
                return;
            }

            if (_institutionAttentionBadge != null)
            {
                return;
            }

            var existing = buttonRect.Find(InstitutionAttentionBadgeName);
            if (existing != null)
            {
                _institutionAttentionBadge = existing.gameObject;
                return;
            }

            var badge = new GameObject(InstitutionAttentionBadgeName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
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
            _institutionAttentionBadge = badge;
        }

        private void UpdateInstitutionAttentionBadge()
        {
            EnsureInstitutionAttentionBadge();
            if (_institutionAttentionBadge != null)
            {
                _institutionAttentionBadge.SetActive(_institutionViewModel?.HasSelectableCandidate() == true);
            }
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
            var turn = _turnStore?.Snapshot;
            if (!HasCurrentTurnMinisterDrafts(state, turn) &&
                _managementPanelVisibilityStore?.IsVisible(ManagementPanelId.MinisterReport) == true)
            {
                _managementPanelVisibilityStore.Hide();
            }

            if (_ministerAttentionBadge != null)
            {
                _ministerAttentionBadge.SetActive(HasCurrentTurnInteractiveMinisterDrafts(state, turn));
            }
        }

        private static bool HasCurrentTurnMinisterDrafts(PlanningDraftState state, TurnState turn)
        {
            return HasCurrentTurnMinisterDrafts(state?.MinisterDrafts, ResolveCurrentTurn(state, turn), state?.SnapshotTurn ?? 0);
        }

        private static bool HasCurrentTurnMinisterDrafts(
            System.Collections.Generic.IReadOnlyList<MinisterDraftDto> drafts,
            int currentTurn,
            int snapshotTurn)
        {
            if (drafts == null)
            {
                return false;
            }

            for (var i = 0; i < drafts.Count; i++)
            {
                if (IsCurrentTurnDraft(drafts[i], currentTurn, snapshotTurn))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasCurrentTurnInteractiveMinisterDrafts(PlanningDraftState state, TurnState turn)
        {
            return HasCurrentTurnInteractiveMinisterDrafts(state?.MinisterDrafts, ResolveCurrentTurn(state, turn), state?.SnapshotTurn ?? 0);
        }

        private static bool HasCurrentTurnInteractiveMinisterDrafts(
            System.Collections.Generic.IReadOnlyList<MinisterDraftDto> drafts,
            int currentTurn,
            int snapshotTurn)
        {
            if (drafts == null)
            {
                return false;
            }

            for (var i = 0; i < drafts.Count; i++)
            {
                var draft = drafts[i];
                if (draft?.IsInteractive == true && IsCurrentTurnDraft(draft, currentTurn, snapshotTurn))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsCurrentTurnDraft(MinisterDraftDto draft, int currentTurn, int snapshotTurn)
        {
            if (draft == null)
            {
                return false;
            }

            if (currentTurn <= 0)
            {
                return true;
            }

            if (draft.Turn == currentTurn)
            {
                return true;
            }

            return draft.Turn <= 0 && snapshotTurn == currentTurn;
        }

        private static int ResolveCurrentTurn(PlanningDraftState state, TurnState turn)
        {
            if (turn != null && turn.Turn > 0)
            {
                return turn.Turn;
            }

            return state != null && state.SnapshotTurn > 0 ? state.SnapshotTurn : 0;
        }

        private void PublishMinisterThinkingFeedback()
        {
            _feedbackStore?.PublishFeedback(
                "minister",
                MinisterThinkingFeedbackCode,
                MinisterThinkingFeedbackMessage,
                false);
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
                    PanoptesLog.Warning("[ResourceHUD] ManagementPanelVisibilityStore not injected.");
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
            ApplyButtonSprite(institutionButton, institutionButtonSpriteResource);
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
