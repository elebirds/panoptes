/*************************************************
 * Project: Panoptes
 * File: OrderReviewPanel.cs
 * Author: Panoptes Team
 * Date: 2026-04-13
 * Description: Runtime-built queued combat order review panel.
 *************************************************/

using System.Text;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Intents;
using Panoptes.Core.Domain;
using Panoptes.Core.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Presentation.UI.Combat
{
    public sealed class OrderReviewPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform root;
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI ordersText;
        [SerializeField] private Button submitButton;

        private GameStateCache _cache;
        private CombatDraftCache _draftCache;

        private void Awake()
        {
            EnsureUi();
            BindButton();
            _cache = GameStateCache.Instance;
            _draftCache = CombatDraftCache.EnsureInstance();
        }

        private void OnEnable()
        {
            _cache = GameStateCache.Instance;
            _draftCache = CombatDraftCache.EnsureInstance();

            if (_cache != null)
            {
                _cache.OnStateChanged += Refresh;
                _cache.OnPhaseChanged += OnPhaseChanged;
                _cache.OnGameOver += OnGameOver;
            }

            if (_draftCache != null)
            {
                _draftCache.OrdersChanged += Refresh;
            }

            ActionLock.OnChanged += OnActionLockChanged;
            Refresh();
        }

        private void OnDisable()
        {
            if (_cache != null)
            {
                _cache.OnStateChanged -= Refresh;
                _cache.OnPhaseChanged -= OnPhaseChanged;
                _cache.OnGameOver -= OnGameOver;
            }

            if (_draftCache != null)
            {
                _draftCache.OrdersChanged -= Refresh;
            }

            ActionLock.OnChanged -= OnActionLockChanged;
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

        private void BindButton()
        {
            submitButton.onClick.RemoveAllListeners();
            submitButton.onClick.AddListener(GameIntents.SubmitCombat);
        }

        private void Refresh()
        {
            EnsureUi();
            _draftCache ??= CombatDraftCache.EnsureInstance();

            var inCombatPlanning = _cache != null &&
                                  string.Equals(_cache.Phase, GamePhases.CombatPlanning, System.StringComparison.Ordinal) &&
                                  !_cache.IsGameOver;
            if (!inCombatPlanning)
            {
                titleText.text = "待提交命令";
                ordersText.text = "等待战斗部署阶段";
                submitButton.interactable = false;
                return;
            }

            titleText.text = "待提交命令";
            ordersText.text = BuildOrdersSummary();
            submitButton.interactable = !ActionLock.IsLocked;
            var buttonLabel = submitButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonLabel != null)
            {
                buttonLabel.text = ActionLock.IsLocked ? "已提交" : "提交战斗";
            }
        }

        private string BuildOrdersSummary()
        {
            if (_draftCache == null)
            {
                return "等待服务器同步命令快照";
            }

            var orders = _draftCache.GetOrdersInDisplayOrder();
            if (orders.Count == 0)
            {
                return "当前没有待提交命令";
            }

            var builder = new StringBuilder();
            var catalog = StaticCatalogCache.EnsureInstance();
            for (var i = 0; i < orders.Count; i++)
            {
                var order = orders[i];
                var line = DescribeOrder(order, catalog);
                builder.Append(line);
                if (i < orders.Count - 1)
                {
                    builder.Append('\n');
                }
            }

            return builder.ToString();
        }

        private string DescribeOrder(QueuedCombatOrderDto order, StaticCatalogCache catalog)
        {
            var unitLabel = order.UnitId;
            if (_cache != null)
            {
                var unit = _cache.GetUnit(order.UnitId);
                if (unit != null && catalog != null && catalog.TryGetUnit(unit.Type, out var entry) && entry != null && !string.IsNullOrWhiteSpace(entry.name))
                {
                    unitLabel = entry.name;
                }
            }

            return order.Action switch
            {
                "move" => $"{unitLabel} -> 行军至 {order.TargetNodeId}，首回合停在 {FallbackText(order.FirstTurnNodeId, "沿途")}，预计 {Mathf.Max(1, order.TotalTurns)} 回合",
                "attack" => $"{unitLabel} -> 攻击 {FallbackText(order.TargetUnitId, "目标单位")}",
                "charge" => $"{unitLabel} -> 冲锋 {FallbackText(order.TargetUnitId, order.TargetNodeId)}",
                "hold" => $"{unitLabel} -> 待命",
                _ => $"{unitLabel} -> {FallbackText(order.Action, "未知动作")}"
            };
        }

        private void EnsureUi()
        {
            root ??= GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
            root.anchorMin = new Vector2(1f, 0f);
            root.anchorMax = new Vector2(1f, 0f);
            root.pivot = new Vector2(1f, 0f);
            root.anchoredPosition = new Vector2(-24f, 24f);
            root.sizeDelta = new Vector2(420f, 240f);

            background = EnsureImage("Background");
            background.color = new Color(0.05f, 0.08f, 0.12f, 0.88f);

            titleText ??= CreateText("Title", new Vector2(-16f, -16f), new Vector2(388f, 30f), 26f, FontStyles.Bold, TextAlignmentOptions.TopRight);
            ordersText ??= CreateText("Orders", new Vector2(-16f, -54f), new Vector2(388f, 130f), 18f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            ordersText.enableWordWrapping = true;

            submitButton ??= CreateButton("SubmitButton", "提交战斗", new Vector2(-16f, -194f), new Vector2(180f, 38f));
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

        private TextMeshProUGUI CreateText(string objectName, Vector2 anchoredPosition, Vector2 size, float fontSize, FontStyles style, TextAlignmentOptions alignment)
        {
            var rect = EnsureRect(objectName, anchoredPosition, size);
            var text = rect.GetComponent<TextMeshProUGUI>();
            if (text == null)
            {
                text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            }

            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        private Button CreateButton(string objectName, string label, Vector2 anchoredPosition, Vector2 size)
        {
            var rect = EnsureRect(objectName, anchoredPosition, size);
            var image = rect.GetComponent<Image>();
            if (image == null)
            {
                image = rect.gameObject.AddComponent<Image>();
            }
            image.color = new Color(0.22f, 0.42f, 0.3f, 0.95f);

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

        private RectTransform EnsureRect(string objectName, Vector2 anchoredPosition, Vector2 size)
        {
            var existing = root.Find(objectName) as RectTransform;
            var rect = existing;
            if (rect == null)
            {
                var go = new GameObject(objectName, typeof(RectTransform));
                go.transform.SetParent(root, false);
                rect = go.GetComponent<RectTransform>();
            }

            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return rect;
        }

        private static string FallbackText(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
    }
}
