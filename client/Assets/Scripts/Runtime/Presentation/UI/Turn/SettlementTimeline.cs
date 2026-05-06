/*************************************************
 * Project: Panoptes
 * File: SettlementTimeline.cs
 * Author: Panoptes Team
 * Date: 2026-04-14
 * Description: Displays the latest settlement sections as a lightweight timeline.
 *************************************************/

using System;
using System.Text;
using Panoptes.Core.Application.Feedback;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Panoptes.Presentation.UI.Turn
{
    public sealed class SettlementTimeline : MonoBehaviour
    {
        [SerializeField] private RectTransform root;
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI timelineText;

        private IDisposable _settlementSubscription;
        private SettlementStore _settlementStore;
        private bool _warnedMissingUi;

        [Inject]
        private void Construct(SettlementStore settlementStore)
        {
            _settlementStore = settlementStore;
        }

        private void Awake()
        {
            TryResolveUiReferences(false);
        }

        private void OnEnable()
        {
            _settlementSubscription?.Dispose();
            _settlementSubscription = _settlementStore?.State.Subscribe(this, static (state, self) => self.OnSettlementChanged(state));
            OnSettlementChanged(_settlementStore?.Snapshot);
        }

        private void OnDisable()
        {
            _settlementSubscription?.Dispose();
            _settlementSubscription = null;
        }

        private void OnSettlementChanged(SettlementState state)
        {
            if (!TryResolveUiReferences(true))
            {
                return;
            }

            titleText.text = "结算时间线";
            var settlement = state?.Settlement;
            if (settlement?.Sections == null || settlement.Sections.Count == 0)
            {
                timelineText.text = "本回合没有结算提示";
                return;
            }

            var builder = new StringBuilder();
            var lineCount = 0;
            for (var i = 0; i < settlement.Sections.Count; i++)
            {
                var section = settlement.Sections[i];
                if (section?.Events == null)
                {
                    continue;
                }

                for (var eventIndex = 0; eventIndex < section.Events.Count; eventIndex++)
                {
                    if (!TryDescribeEvent(section.Events[eventIndex], out var line))
                    {
                        continue;
                    }

                    if (builder.Length > 0)
                    {
                        builder.Append('\n');
                    }

                    builder.Append(line);
                    lineCount++;
                }
            }

            timelineText.text = lineCount > 0
                ? builder.ToString()
                : "本回合没有建造或生产相关提示";
        }

        private static bool TryDescribeEvent(TurnEventDto evt, out string description)
        {
            description = string.Empty;
            if (evt == null)
            {
                return false;
            }

            var nodeId = ReadData(evt, "node_id");
            var buildingType = ReadData(evt, "building_type_id", "building_type");
            var recipeId = ReadData(evt, "recipe_id");
            var reasonMessage = ResolveReasonMessage(evt.ReasonMessage, ReadData(evt, "reason"));
            var blockedReasonMessage = ResolveReasonMessage(evt.BlockedReasonMessage, ReadData(evt, "blocked_reason"));

            switch (evt.Type)
            {
                case "city_founded":
                    description = string.IsNullOrWhiteSpace(nodeId)
                        ? "建立了新城市。"
                        : $"节点 {nodeId} 建立了新城市。";
                    return true;
                case "building_built":
                    description = $"节点 {Fallback(nodeId, "未知节点")} 完成了 {Fallback(buildingType, "建筑")}。";
                    return true;
                case "building_skipped":
                    description = $"节点 {Fallback(nodeId, "未知节点")} 的 {Fallback(buildingType, "建筑")} 未能建造：{Fallback(reasonMessage, "当前不能建造。")}";
                    return true;
                case "recipe_skipped":
                    description = $"节点 {Fallback(nodeId, "未知节点")} 的配方 {Fallback(recipeId, "当前配方")} 未推进：{Fallback(reasonMessage, "本回合未生产。")}";
                    return true;
                case "recipe_progressed":
                    if (!string.IsNullOrWhiteSpace(blockedReasonMessage))
                    {
                        description = $"节点 {Fallback(nodeId, "未知节点")} 的配方 {Fallback(recipeId, "当前配方")} 被阻塞：{blockedReasonMessage}";
                        return true;
                    }

                    var amount = ReadData(evt, "amount", "progress", "base_progress");
                    description = string.IsNullOrWhiteSpace(amount)
                        ? $"节点 {Fallback(nodeId, "未知节点")} 的配方 {Fallback(recipeId, "当前配方")} 已推进。"
                        : $"节点 {Fallback(nodeId, "未知节点")} 的配方 {Fallback(recipeId, "当前配方")} 推进了 {amount} 点。";
                    return true;
                case "building_status_changed":
                    var status = ReadData(evt, "status");
                    description = string.IsNullOrWhiteSpace(reasonMessage)
                        ? $"节点 {Fallback(nodeId, "未知节点")} 的建筑状态变为 {Fallback(status, "未知状态")}。"
                        : $"节点 {Fallback(nodeId, "未知节点")} 的建筑状态变为 {Fallback(status, "未知状态")}：{reasonMessage}";
                    return true;
                case "facility_takeover_progressed":
                    var progress = ReadData(evt, "takeover_progress");
                    var required = ReadData(evt, "takeover_required");
                    if (!string.IsNullOrWhiteSpace(progress) && !string.IsNullOrWhiteSpace(required))
                    {
                        description = $"节点 {Fallback(nodeId, "未知节点")} 正在被接管（{progress}/{required}）：{Fallback(reasonMessage, "当前无法正常运作。")}";
                        return true;
                    }

                    description = $"节点 {Fallback(nodeId, "未知节点")} 正在被接管：{Fallback(reasonMessage, "当前无法正常运作。")}";
                    return true;
                default:
                    return false;
            }
        }

        private static string ReadData(TurnEventDto evt, params string[] keys)
        {
            if (evt?.Data == null || keys == null)
            {
                return string.Empty;
            }

            for (var i = 0; i < keys.Length; i++)
            {
                var key = keys[i];
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (evt.Data.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        private static string Fallback(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static string ResolveReasonMessage(string serverMessage, string code)
        {
            if (!string.IsNullOrWhiteSpace(serverMessage))
            {
                return serverMessage.Trim();
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                return string.Empty;
            }

            return GameplayFeedbackText.ResolveMessage(string.Empty, code);
        }

        private bool TryResolveUiReferences(bool logWarning)
        {
            if (root == null)
            {
                root = GetComponent<RectTransform>();
            }

            if (root != null)
            {
                if (background == null)
                {
                    var bgTransform = root.Find("Background");
                    if (bgTransform != null)
                    {
                        background = bgTransform.GetComponent<Image>();
                    }
                }

                if (titleText == null)
                {
                    var titleTransform = root.Find("Title");
                    if (titleTransform != null)
                    {
                        titleText = titleTransform.GetComponent<TextMeshProUGUI>();
                    }
                }

                if (timelineText == null)
                {
                    var timelineTransform = root.Find("Timeline");
                    if (timelineTransform != null)
                    {
                        timelineText = timelineTransform.GetComponent<TextMeshProUGUI>();
                    }
                }
            }

            var ok = root != null && background != null && titleText != null && timelineText != null;
            if (!ok && logWarning && !_warnedMissingUi)
            {
                _warnedMissingUi = true;
                PanoptesLog.Warning("[SettlementTimeline] Missing UI references. Assign root/background/titleText/timelineText in prefab.");
            }

            return ok;
        }
    }
}
