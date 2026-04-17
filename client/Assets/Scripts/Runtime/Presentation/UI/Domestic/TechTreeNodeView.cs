using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Presentation.UI.Domestic
{
    [DisallowMultipleComponent]
    public sealed class TechTreeNodeView : MonoBehaviour
    {
        [Header("Binding")]
        [SerializeField] private Button button;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Outline backgroundOutline;
        [SerializeField] private Image stateFrameImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private RectTransform progressRoot;
        [SerializeField] private RectTransform progressFill;
        [SerializeField] private Image progressFillImage;
        [SerializeField] private Image statusBadgeImage;

        [Header("Colors")]
        [SerializeField] private Color lockedBackgroundColor = new(0.28f, 0.3f, 0.34f, 0.96f);
        [SerializeField] private Color availableBackgroundColor = new(0.94f, 0.95f, 0.98f, 0.98f);
        [SerializeField] private Color researchingBackgroundColor = new(0.83f, 0.91f, 1f, 0.99f);
        [SerializeField] private Color pendingBackgroundColor = new(1f, 0.93f, 0.78f, 0.99f);
        [SerializeField] private Color activeBackgroundColor = new(0.86f, 0.97f, 0.9f, 0.99f);
        [SerializeField] private Color lockedTextColor = new(0.76f, 0.79f, 0.84f, 1f);
        [SerializeField] private Color defaultTextColor = new(0.1f, 0.12f, 0.16f, 1f);
        [SerializeField] private Color lockedFrameColor = new(0.43f, 0.46f, 0.51f, 1f);
        [SerializeField] private Color availableFrameColor = new(0.23f, 0.33f, 0.48f, 1f);
        [SerializeField] private Color researchingFrameColor = new(0.07f, 0.42f, 0.86f, 1f);
        [SerializeField] private Color pendingFrameColor = new(0.83f, 0.56f, 0.12f, 1f);
        [SerializeField] private Color activeFrameColor = new(0.14f, 0.55f, 0.32f, 1f);
        [SerializeField] private Color lockedBadgeColor = new(0.45f, 0.47f, 0.53f, 1f);
        [SerializeField] private Color availableBadgeColor = new(0.18f, 0.43f, 0.85f, 1f);
        [SerializeField] private Color researchingBadgeColor = new(0.05f, 0.45f, 0.95f, 1f);
        [SerializeField] private Color pendingBadgeColor = new(0.88f, 0.6f, 0.14f, 1f);
        [SerializeField] private Color activeBadgeColor = new(0.18f, 0.62f, 0.38f, 1f);
        [SerializeField] private Color lockedProgressColor = new(0.54f, 0.57f, 0.61f, 1f);
        [SerializeField] private Color availableProgressColor = new(0.23f, 0.51f, 0.91f, 1f);
        [SerializeField] private Color researchingProgressColor = new(0.04f, 0.58f, 1f, 1f);
        [SerializeField] private Color pendingProgressColor = new(0.93f, 0.68f, 0.18f, 1f);
        [SerializeField] private Color activeProgressColor = new(0.19f, 0.7f, 0.41f, 1f);

        private Action<string> _clickHandler;
        private string _technologyId = string.Empty;

        private void Awake()
        {
            ResolveReferences();
        }

        public void Bind(TechTreeNodeRenderModel model, Sprite iconSprite, Action<string> onClick)
        {
            ResolveReferences();

            model ??= new TechTreeNodeRenderModel();
            _technologyId = model.TechnologyId ?? string.Empty;
            _clickHandler = onClick;

            if (titleText != null)
            {
                titleText.text = model.Title ?? string.Empty;
            }

            if (descriptionText != null)
            {
                descriptionText.text = model.Description ?? string.Empty;
            }

            if (statusText != null)
            {
                statusText.text = string.IsNullOrWhiteSpace(model.StatusLabel)
                    ? TechTreePanelStateBuilder.GetStatusLabel(model.Status)
                    : model.StatusLabel;
            }

            if (progressText != null)
            {
                progressText.text = $"{Mathf.Max(0, model.CurrentProgress)} / {Mathf.Max(0, model.RequiredProgress)}";
            }

            if (iconImage != null)
            {
                iconImage.sprite = iconSprite;
                iconImage.color = iconSprite != null
                    ? Color.white
                    : (model.Status == TechTreeNodeStatus.Locked ? new Color(1f, 1f, 1f, 0.25f) : new Color(1f, 1f, 1f, 0.7f));
                iconImage.preserveAspect = true;
            }

            ApplyStatusStyle(model.Status);
            ApplyProgress(model.CurrentProgress, model.RequiredProgress);
            BindButton(model.IsInteractable);
        }

        private void BindButton(bool isInteractable)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);
            button.interactable = isInteractable;
        }

        private void ApplyProgress(int currentProgress, int requiredProgress)
        {
            if (progressFill == null)
            {
                return;
            }

            var ratio = requiredProgress > 0
                ? Mathf.Clamp01((float)Mathf.Max(0, currentProgress) / requiredProgress)
                : 0f;
            progressFill.anchorMin = new Vector2(0f, 0f);
            progressFill.anchorMax = new Vector2(ratio, 1f);
            progressFill.offsetMin = Vector2.zero;
            progressFill.offsetMax = Vector2.zero;
        }

        private void ApplyStatusStyle(TechTreeNodeStatus status)
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = status switch
                {
                    TechTreeNodeStatus.Locked => lockedBackgroundColor,
                    TechTreeNodeStatus.Available => availableBackgroundColor,
                    TechTreeNodeStatus.Researching => researchingBackgroundColor,
                    TechTreeNodeStatus.PendingActivation => pendingBackgroundColor,
                    TechTreeNodeStatus.Active => activeBackgroundColor,
                    _ => availableBackgroundColor
                };
            }

            var frameColor = status switch
            {
                TechTreeNodeStatus.Locked => lockedFrameColor,
                TechTreeNodeStatus.Available => availableFrameColor,
                TechTreeNodeStatus.Researching => researchingFrameColor,
                TechTreeNodeStatus.PendingActivation => pendingFrameColor,
                TechTreeNodeStatus.Active => activeFrameColor,
                _ => availableFrameColor
            };

            if (backgroundOutline != null)
            {
                backgroundOutline.effectColor = frameColor;
            }

            if (stateFrameImage != null)
            {
                stateFrameImage.color = frameColor;
            }

            if (statusBadgeImage != null)
            {
                statusBadgeImage.color = status switch
                {
                    TechTreeNodeStatus.Locked => lockedBadgeColor,
                    TechTreeNodeStatus.Available => availableBadgeColor,
                    TechTreeNodeStatus.Researching => researchingBadgeColor,
                    TechTreeNodeStatus.PendingActivation => pendingBadgeColor,
                    TechTreeNodeStatus.Active => activeBadgeColor,
                    _ => availableBadgeColor
                };
            }

            if (progressFillImage != null)
            {
                progressFillImage.color = status switch
                {
                    TechTreeNodeStatus.Locked => lockedProgressColor,
                    TechTreeNodeStatus.Available => availableProgressColor,
                    TechTreeNodeStatus.Researching => researchingProgressColor,
                    TechTreeNodeStatus.PendingActivation => pendingProgressColor,
                    TechTreeNodeStatus.Active => activeProgressColor,
                    _ => availableProgressColor
                };
            }

            var textColor = status == TechTreeNodeStatus.Locked ? lockedTextColor : defaultTextColor;
            if (titleText != null)
            {
                titleText.color = textColor;
            }

            if (descriptionText != null)
            {
                descriptionText.color = status == TechTreeNodeStatus.Locked
                    ? new Color(lockedTextColor.r, lockedTextColor.g, lockedTextColor.b, 0.92f)
                    : new Color(defaultTextColor.r, defaultTextColor.g, defaultTextColor.b, 0.9f);
            }

            if (progressText != null)
            {
                progressText.color = status == TechTreeNodeStatus.Locked
                    ? new Color(lockedTextColor.r, lockedTextColor.g, lockedTextColor.b, 0.98f)
                    : new Color(defaultTextColor.r, defaultTextColor.g, defaultTextColor.b, 0.95f);
            }

            if (statusText != null)
            {
                statusText.color = Color.white;
            }
        }

        private void HandleClick()
        {
            if (string.IsNullOrWhiteSpace(_technologyId))
            {
                return;
            }

            _clickHandler?.Invoke(_technologyId);
        }

        private void ResolveReferences()
        {
            button ??= GetComponent<Button>();
            backgroundImage ??= GetComponent<Image>();
            backgroundOutline ??= GetComponent<Outline>();
            stateFrameImage ??= FindImage("StateFrame");
            iconImage ??= FindImage("Icon");
            titleText ??= FindText("Content/TitleText", "TitleText");
            descriptionText ??= FindText("Content/DescriptionText", "DescriptionText");
            statusText ??= FindText("StatusBadge/StatusBadgeText", "StatusBadgeText");
            progressRoot ??= FindRect("ProgressRoot");
            progressFill ??= FindRect("ProgressRoot/ProgressFill", "ProgressFill");
            progressFillImage ??= FindImage("ProgressRoot/ProgressFill", "ProgressFill");
            progressText ??= FindText("ProgressRoot/ProgressText", "ProgressText");
            statusBadgeImage ??= FindImage("StatusBadge");
        }

        private Image FindImage(params string[] paths)
        {
            var rect = FindRect(paths);
            return rect != null ? rect.GetComponent<Image>() : null;
        }

        private TMP_Text FindText(params string[] paths)
        {
            var rect = FindRect(paths);
            return rect != null ? rect.GetComponent<TMP_Text>() : null;
        }

        private RectTransform FindRect(params string[] paths)
        {
            if (paths == null)
            {
                return null;
            }

            for (var i = 0; i < paths.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(paths[i]))
                {
                    continue;
                }

                var target = transform.Find(paths[i]);
                if (target is RectTransform rect)
                {
                    return rect;
                }
            }

            return null;
        }
    }
}
