/*************************************************
 * Project: Panoptes
 * File: BuildItemView.cs
 * Author: Panoptes Team
 * Date: 2026-04-16
 * Description: Build list item view (icon + name + desc + material requirements).
 *************************************************/

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Panoptes.Presentation.UI.Domestic
{
    public sealed class BuildItemView : MonoBehaviour
    {
        [Serializable]
        public struct MaterialRequirement
        {
            public string key;
            public string displayName;
            public int amount;
            public Sprite icon;
        }

        [Serializable]
        private sealed class MaterialSlot
        {
            public RectTransform root;
            public Image icon;
            public TMP_Text amountText;
        }

        [Header("Item Root")]
        [SerializeField] private Button clickButton;
        [SerializeField] private Image buildingIcon;
        [SerializeField] private TMP_Text buildingNameText;
        [SerializeField] private TMP_Text buildDescriptionText;

        [Header("NeedMatrialList")]
        [SerializeField] private RectTransform needMatrialListRoot;
        [SerializeField] private RectTransform materialSlotTemplate;
        [SerializeField] private bool autoFindReferences = true;
        [SerializeField] private string amountFormat = "x{0}";

        [Header("Locked State")]
        [SerializeField] private GameObject lockOverlayRoot;
        [SerializeField] private Image lockMaskImage;
        [SerializeField] private Image lockIconImage;
        [SerializeField] private Sprite fallbackLockIcon;
        [SerializeField] private Color lockMaskColor = new Color(0f, 0f, 0f, 0.55f);
        [SerializeField] private Color lockIconColor = Color.white;

        private readonly List<MaterialSlot> _slots = new();

        public Button ClickButton => clickButton;

        private void Awake()
        {
            EnsureReferences();
            EnsureButton();
            RebuildMaterialSlots();
            EnsureLockOverlay();
            SetLocked(false);
        }

        public void ConfigureVisual(
            string buildingName,
            string buildingDescription,
            Sprite buildingSprite,
            IReadOnlyList<MaterialRequirement> requirements)
        {
            EnsureReferences();
            RebuildMaterialSlots();

            if (buildingNameText != null)
            {
                buildingNameText.text = string.IsNullOrWhiteSpace(buildingName) ? "Unknown Building" : buildingName;
            }

            if (buildDescriptionText != null)
            {
                buildDescriptionText.text = buildingDescription ?? string.Empty;
            }

            if (buildingIcon != null && buildingSprite != null)
            {
                buildingIcon.sprite = buildingSprite;
                buildingIcon.preserveAspect = true;
            }

            ApplyRequirements(requirements);
        }

        public void SetClickAction(UnityAction action)
        {
            EnsureButton();
            if (clickButton == null)
            {
                return;
            }

            clickButton.onClick.RemoveAllListeners();
            if (action != null)
            {
                clickButton.onClick.AddListener(action);
            }
        }

        public void ClearClickAction()
        {
            if (clickButton != null)
            {
                clickButton.onClick.RemoveAllListeners();
            }
        }

        public void SetLocked(bool isLocked, Sprite lockIcon = null)
        {
            EnsureReferences();
            EnsureLockOverlay();
            EnsureButton();

            if (lockOverlayRoot != null)
            {
                lockOverlayRoot.SetActive(isLocked);
            }

            if (lockMaskImage != null)
            {
                lockMaskImage.color = lockMaskColor;
            }

            if (lockIconImage != null)
            {
                var resolvedIcon = lockIcon != null ? lockIcon : fallbackLockIcon;
                if (resolvedIcon != null)
                {
                    lockIconImage.sprite = resolvedIcon;
                    lockIconImage.color = lockIconColor;
                    lockIconImage.enabled = true;
                }
                else
                {
                    lockIconImage.enabled = false;
                }
            }

            if (clickButton != null)
            {
                clickButton.interactable = !isLocked;
            }
        }

        private void EnsureReferences()
        {
            if (!autoFindReferences)
            {
                return;
            }

            if (buildingIcon == null)
            {
                var iconNode = transform.Find("Image");
                if (iconNode != null)
                {
                    buildingIcon = iconNode.GetComponent<Image>();
                }

                if (buildingIcon == null)
                {
                    buildingIcon = GetComponentInChildren<Image>(true);
                }
            }

            if (buildingNameText == null)
            {
                var nameNode = transform.Find("BuildName");
                if (nameNode != null)
                {
                    buildingNameText = nameNode.GetComponent<TMP_Text>();
                }

                if (buildingNameText == null)
                {
                    buildingNameText = FindTextByName("BuildName");
                }
            }

            if (buildDescriptionText == null)
            {
                var descNode = transform.Find("BuildDes");
                if (descNode != null)
                {
                    buildDescriptionText = descNode.GetComponent<TMP_Text>();
                }

                if (buildDescriptionText == null)
                {
                    buildDescriptionText = FindTextByName("BuildDes");
                }
            }

            if (needMatrialListRoot == null)
            {
                var listNode = transform.Find("NeedMatrialList");
                if (listNode != null)
                {
                    needMatrialListRoot = listNode as RectTransform;
                }
            }

            if (materialSlotTemplate == null && needMatrialListRoot != null && needMatrialListRoot.childCount > 0)
            {
                materialSlotTemplate = needMatrialListRoot.GetChild(0) as RectTransform;
            }

            if (lockOverlayRoot == null)
            {
                var lockNode = transform.Find("LockOverlay");
                if (lockNode != null)
                {
                    lockOverlayRoot = lockNode.gameObject;
                    lockMaskImage = lockNode.GetComponent<Image>();
                    var iconNode = lockNode.Find("LockIcon");
                    if (iconNode != null)
                    {
                        lockIconImage = iconNode.GetComponent<Image>();
                    }
                }
            }
        }

        private TMP_Text FindTextByName(string nodeName)
        {
            var texts = GetComponentsInChildren<TMP_Text>(true);
            for (var i = 0; i < texts.Length; i++)
            {
                var text = texts[i];
                if (text == null || text.transform == null)
                {
                    continue;
                }

                if (string.Equals(text.transform.name, nodeName, StringComparison.OrdinalIgnoreCase))
                {
                    return text;
                }
            }

            return null;
        }

        private void EnsureButton()
        {
            if (clickButton == null)
            {
                clickButton = GetComponent<Button>();
            }

            if (clickButton == null)
            {
                clickButton = gameObject.AddComponent<Button>();
                clickButton.transition = Selectable.Transition.ColorTint;
            }

            var target = clickButton.targetGraphic;
            if (target == null)
            {
                var image = GetComponent<Image>();
                if (image == null)
                {
                    image = gameObject.AddComponent<Image>();
                    image.color = new Color(1f, 1f, 1f, 0f);
                }

                clickButton.targetGraphic = image;
            }
        }

        private void EnsureLockOverlay()
        {
            if (lockMaskImage == null && lockOverlayRoot != null)
            {
                lockMaskImage = lockOverlayRoot.GetComponent<Image>();
            }

            if (lockIconImage == null && lockOverlayRoot != null)
            {
                var icon = lockOverlayRoot.transform.Find("LockIcon");
                if (icon != null)
                {
                    lockIconImage = icon.GetComponent<Image>();
                }
            }
        }

        private void RebuildMaterialSlots()
        {
            _slots.Clear();
            if (needMatrialListRoot == null)
            {
                return;
            }

            for (var i = 0; i < needMatrialListRoot.childCount; i++)
            {
                var child = needMatrialListRoot.GetChild(i) as RectTransform;
                if (child == null)
                {
                    continue;
                }

                var slot = BuildSlot(child);
                if (slot != null)
                {
                    _slots.Add(slot);
                }
            }
        }

        private MaterialSlot BuildSlot(RectTransform root)
        {
            if (root == null)
            {
                return null;
            }

            var icon = root.GetComponentInChildren<Image>(true);
            var amount = root.GetComponentInChildren<TMP_Text>(true);
            return new MaterialSlot
            {
                root = root,
                icon = icon,
                amountText = amount
            };
        }

        private void ApplyRequirements(IReadOnlyList<MaterialRequirement> requirements)
        {
            if (needMatrialListRoot == null)
            {
                return;
            }

            var count = requirements != null ? requirements.Count : 0;
            EnsureSlotCount(count);

            for (var i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot == null || slot.root == null)
                {
                    continue;
                }

                if (i >= count)
                {
                    slot.root.gameObject.SetActive(false);
                    continue;
                }

                var req = requirements[i];
                slot.root.gameObject.SetActive(true);
                if (slot.icon != null)
                {
                    if (req.icon != null)
                    {
                        slot.icon.sprite = req.icon;
                    }
                    slot.icon.preserveAspect = true;
                }

                if (slot.amountText != null)
                {
                    var format = string.IsNullOrWhiteSpace(amountFormat) ? "x{0}" : amountFormat;
                    slot.amountText.text = string.Format(format, Mathf.Max(0, req.amount));
                }
            }
        }

        private void EnsureSlotCount(int requiredCount)
        {
            if (requiredCount <= _slots.Count)
            {
                return;
            }

            if (needMatrialListRoot == null)
            {
                return;
            }

            if (materialSlotTemplate == null && _slots.Count > 0)
            {
                materialSlotTemplate = _slots[0].root;
            }

            if (materialSlotTemplate == null)
            {
                return;
            }

            while (_slots.Count < requiredCount)
            {
                var clone = Instantiate(materialSlotTemplate.gameObject, needMatrialListRoot, false);
                clone.name = $"{materialSlotTemplate.name}_{_slots.Count + 1}";
                var slot = BuildSlot(clone.transform as RectTransform);
                if (slot == null)
                {
                    break;
                }

                _slots.Add(slot);
            }
        }
    }
}
