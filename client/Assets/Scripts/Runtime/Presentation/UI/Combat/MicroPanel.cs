/*************************************************
 * Project: Panoptes
 * File: MicroPanel.cs
 * Author: Panoptes Team
 * Date: 2026-04-13
 * Description: Runtime-built combat action panel.
 *************************************************/

using Panoptes.Core.Application.Cache;
using Panoptes.Core.Domain;
using Panoptes.Core.Events;
using Panoptes.Presentation.Map;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Presentation.UI.Combat
{
    public sealed class MicroPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform root;
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI detailText;
        [SerializeField] private Button moveButton;
        [SerializeField] private Button attackButton;
        [SerializeField] private Button holdButton;
        [SerializeField] private Button chargeButton;

        private GameStateCache _cache;
        private MapInputHandler _inputHandler;

        private void Awake()
        {
            EnsureUi();
            _cache = GameStateCache.Instance;
            BindButtons();
        }

        private void OnEnable()
        {
            _cache = GameStateCache.Instance;
            if (_cache != null)
            {
                _cache.OnStateChanged += Refresh;
                _cache.OnPhaseChanged += OnPhaseChanged;
            }

            HookInputHandler();
            Refresh();
        }

        private void OnDisable()
        {
            if (_cache != null)
            {
                _cache.OnStateChanged -= Refresh;
                _cache.OnPhaseChanged -= OnPhaseChanged;
            }

            UnhookInputHandler();
        }

        private void Update()
        {
            if (_inputHandler == null)
            {
                HookInputHandler();
                Refresh();
            }
        }

        private void OnPhaseChanged(PhaseChangedEvent _)
        {
            Refresh();
        }

        private void HookInputHandler()
        {
            var handler = MapInputHandler.Instance != null ? MapInputHandler.Instance : UnityEngine.Object.FindAnyObjectByType<MapInputHandler>();
            if (handler == _inputHandler)
            {
                return;
            }

            UnhookInputHandler();
            _inputHandler = handler;
            if (_inputHandler != null)
            {
                _inputHandler.CombatSelectionChanged += Refresh;
            }
        }

        private void UnhookInputHandler()
        {
            if (_inputHandler == null)
            {
                return;
            }

            _inputHandler.CombatSelectionChanged -= Refresh;
            _inputHandler = null;
        }

        private void BindButtons()
        {
            moveButton.onClick.RemoveAllListeners();
            moveButton.onClick.AddListener(() => _inputHandler?.BeginMoveSelection());

            attackButton.onClick.RemoveAllListeners();
            attackButton.onClick.AddListener(() => _inputHandler?.BeginAttackSelection());

            holdButton.onClick.RemoveAllListeners();
            holdButton.onClick.AddListener(() => _inputHandler?.IssueHoldOrder());

            chargeButton.onClick.RemoveAllListeners();
            chargeButton.onClick.AddListener(() => _inputHandler?.BeginChargeSelection());
        }

        private void Refresh()
        {
            EnsureUi();
            HookInputHandler();

            var inCombatPlanning = _cache != null &&
                                  string.Equals(_cache.Phase, GamePhases.CombatPlanning, System.StringComparison.Ordinal) &&
                                  !_cache.IsGameOver;
            if (!inCombatPlanning)
            {
                titleText.text = "战斗操作";
                detailText.text = "等待战斗部署阶段";
                SetButtonState(moveButton, "移动", false, true);
                SetButtonState(attackButton, "攻击", false, false);
                SetButtonState(holdButton, "待命", false, true);
                SetButtonState(chargeButton, "冲锋", false, false);
                return;
            }

            var selectedUnit = _inputHandler != null ? _inputHandler.SelectedUnit : null;
            if (selectedUnit == null)
            {
                titleText.text = "战斗操作";
                detailText.text = "点击己方单位后选择动作";
                SetButtonState(moveButton, "移动", false, true);
                SetButtonState(attackButton, "攻击", false, false);
                SetButtonState(holdButton, "待命", false, true);
                SetButtonState(chargeButton, "冲锋", false, false);
                return;
            }

            var cache = StaticCatalogCache.EnsureInstance();
            StaticCatalogCache.UnitEntryJson entry = null;
            if (cache != null)
            {
                cache.TryGetUnit(selectedUnit.UnitType, out entry);
            }

            titleText.text = entry != null ? $"{entry.name} [{selectedUnit.UnitId}]" : $"{selectedUnit.UnitType} [{selectedUnit.UnitId}]";
            detailText.text = _inputHandler != null ? _inputHandler.CurrentCombatPrompt : "选择动作";

            var canAttack = entry != null && !HasTag(entry, "civilian");
            var canCharge = entry != null && HasTag(entry, "charge");

            SetButtonState(moveButton, "移动", true, true);
            SetButtonState(attackButton, "攻击", true, canAttack);
            SetButtonState(holdButton, "待命", true, true);
            SetButtonState(chargeButton, "冲锋", true, canCharge);
        }

        private void EnsureUi()
        {
            root ??= GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
            root.anchorMin = new Vector2(0f, 0f);
            root.anchorMax = new Vector2(0f, 0f);
            root.pivot = new Vector2(0f, 0f);
            root.anchoredPosition = new Vector2(24f, 24f);
            root.sizeDelta = new Vector2(320f, 220f);

            background = EnsureImage("Background");
            background.color = new Color(0.07f, 0.1f, 0.14f, 0.86f);

            titleText ??= CreateText("Title", new Vector2(16f, -18f), 26f, FontStyles.Bold);
            detailText ??= CreateText("Detail", new Vector2(16f, -54f), 18f, FontStyles.Normal);
            detailText.enableWordWrapping = true;
            detailText.rectTransform.sizeDelta = new Vector2(288f, 48f);

            moveButton ??= CreateButton("MoveButton", "移动", new Vector2(16f, -114f));
            attackButton ??= CreateButton("AttackButton", "攻击", new Vector2(168f, -114f));
            holdButton ??= CreateButton("HoldButton", "待命", new Vector2(16f, -164f));
            chargeButton ??= CreateButton("ChargeButton", "冲锋", new Vector2(168f, -164f));
        }

        private Image EnsureImage(string objectName)
        {
            var existing = root.Find(objectName) as RectTransform;
            var rect = existing;
            if (rect == null)
            {
                var go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(root, false);
                rect = go.GetComponent<RectTransform>();
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect.GetComponent<Image>();
        }

        private TextMeshProUGUI CreateText(string objectName, Vector2 anchoredPosition, float fontSize, FontStyles style)
        {
            var rect = EnsureRect(objectName, new Vector2(288f, 32f), anchoredPosition);
            var text = rect.GetComponent<TextMeshProUGUI>();
            if (text == null)
            {
                text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            }

            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.raycastTarget = false;
            return text;
        }

        private Button CreateButton(string objectName, string label, Vector2 anchoredPosition)
        {
            var rect = EnsureRect(objectName, new Vector2(136f, 36f), anchoredPosition);
            var image = rect.GetComponent<Image>();
            if (image == null)
            {
                image = rect.gameObject.AddComponent<Image>();
            }
            image.color = new Color(0.18f, 0.3f, 0.4f, 0.94f);

            var button = rect.GetComponent<Button>();
            if (button == null)
            {
                button = rect.gameObject.AddComponent<Button>();
            }

            var text = rect.GetComponentInChildren<TextMeshProUGUI>();
            if (text == null)
            {
                var textGo = new GameObject("Label", typeof(RectTransform));
                textGo.transform.SetParent(rect, false);
                var textRect = textGo.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;
                text = textGo.AddComponent<TextMeshProUGUI>();
                text.font = TMP_Settings.defaultFontAsset;
                text.alignment = TextAlignmentOptions.Center;
                text.fontSize = 18f;
                text.raycastTarget = false;
                text.color = Color.white;
            }

            text.text = label;
            return button;
        }

        private RectTransform EnsureRect(string objectName, Vector2 size, Vector2 anchoredPosition)
        {
            var existing = root.Find(objectName) as RectTransform;
            var rect = existing;
            if (rect == null)
            {
                var go = new GameObject(objectName, typeof(RectTransform));
                go.transform.SetParent(root, false);
                rect = go.GetComponent<RectTransform>();
            }

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return rect;
        }

        private void SetButtonState(Button button, string label, bool visible, bool interactable)
        {
            if (button == null)
            {
                return;
            }

            button.gameObject.SetActive(visible);
            button.interactable = interactable;
            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = interactable
                    ? new Color(0.18f, 0.3f, 0.4f, 0.94f)
                    : new Color(0.12f, 0.14f, 0.18f, 0.76f);
            }

            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = label;
                text.color = interactable ? Color.white : new Color(0.72f, 0.72f, 0.72f, 1f);
            }
        }

        private static bool HasTag(StaticCatalogCache.UnitEntryJson entry, string tag)
        {
            if (entry == null || entry.tags == null || string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            var expected = tag.Trim().ToLowerInvariant();
            for (var i = 0; i < entry.tags.Length; i++)
            {
                if (string.Equals((entry.tags[i] ?? string.Empty).Trim().ToLowerInvariant(), expected, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
