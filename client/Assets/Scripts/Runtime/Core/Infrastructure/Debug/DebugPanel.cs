#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Panoptes.DebugTools;
using Panoptes.Protocol.V1;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Intents;
using Panoptes.Core.Domain;
using Panoptes.Core.Infrastructure.Network;
using Panoptes.Core.Infrastructure.Service;
using UnityEngine;

namespace Panoptes.DebugTools
{
    public sealed class DebugPanel : MonoBehaviour
    {
        private bool _expanded;
        private Vector2 _logScroll;
        private const int MaxDisplayEntries = 20;

        private void Awake()
        {
            if (MessageLogger.Instance == null)
            {
                var go = new GameObject("MessageLogger");
                DontDestroyOnLoad(go);
                go.AddComponent<MessageLogger>();
            }
        }

        private void OnGUI()
        {
            if (!ShouldDisplay())
            {
                return;
            }

            if (!_expanded)
            {
                if (GUI.Button(new Rect(10f, 10f, 90f, 28f), "[Debug]"))
                {
                    _expanded = true;
                }
                return;
            }

            var width = Mathf.Min(520f, Screen.width - 20f);
            var height = Mathf.Min(560f, Screen.height - 20f);
            var area = new Rect(10f, 10f, width, height);

            GUI.Box(area, "Debug Panel");
            if (GUI.Button(new Rect(area.x + area.width - 80f, area.y + 8f, 70f, 24f), "收起"))
            {
                _expanded = false;
            }

            var y = area.y + 36f;
            y = DrawConnectionSection(area.x + 10f, y, area.width - 20f);
            y = DrawLogSection(area.x + 10f, y + 8f, area.width - 20f, 250f);
            DrawActionsSection(area.x + 10f, y + 8f, area.width - 20f);
        }

        private static bool ShouldDisplay()
        {
            var cfg = ClientRuntimeConfigCache.Instance;
            if (cfg == null)
            {
                return true;
            }
            return cfg.DevMode;
        }

        private float DrawConnectionSection(float x, float y, float width)
        {
            GUI.Label(new Rect(x, y, width, 22f), "区域一：连接状态");
            y += 22f;

            var connected = NetworkManager.Instance != null && NetworkManager.Instance.IsConnected;
            var session = SessionManager.Instance;
            var cache = GameStateCache.Instance;

            GUI.Label(new Rect(x, y, width, 20f), $"WebSocket: {(connected ? "Connected" : "Disconnected")}");
            y += 20f;
            GUI.Label(new Rect(x, y, width, 20f), $"Player ID: {(session == null ? string.Empty : session.PlayerID)}");
            y += 20f;
            GUI.Label(new Rect(x, y, width, 20f), $"Turn: {(cache == null ? 0 : cache.Turn)}");
            y += 20f;
            GUI.Label(new Rect(x, y, width, 20f), $"Phase: {(cache == null ? string.Empty : cache.Phase)}");
            y += 20f;
            GUI.Label(new Rect(x, y, width, 20f), $"Tokens: {(cache == null ? 0 : cache.TokensLeft)}");
            return y + 4f;
        }

        private float DrawLogSection(float x, float y, float width, float height)
        {
            GUI.Label(new Rect(x, y, width, 22f), "区域二：消息日志");
            y += 24f;

            var box = new Rect(x, y, width, height);
            GUI.Box(box, string.Empty);

            var logger = MessageLogger.Instance;
            var entries = logger != null ? logger.Entries : null;
            var count = entries == null ? 0 : entries.Count;
            var start = Mathf.Max(0, count - MaxDisplayEntries);
            var lineHeight = 20f;
            var contentHeight = Mathf.Max(height - 6f, (count - start) * lineHeight + 6f);

            _logScroll = GUI.BeginScrollView(
                new Rect(box.x + 4f, box.y + 4f, box.width - 8f, box.height - 8f),
                _logScroll,
                new Rect(0f, 0f, box.width - 28f, contentHeight));

            if (entries != null)
            {
                var lineY = 0f;
                for (var i = start; i < count; i++)
                {
                    var entry = entries[i];
                    GUI.color = GetEntryColor(entry.Direction);
                    GUI.Label(new Rect(0f, lineY, box.width - 36f, lineHeight),
                        $"[{ToDirectionLabel(entry.Direction)}] {entry.Timestamp} {entry.MsgType}");
                    lineY += lineHeight;
                }
                GUI.color = Color.white;
            }

            GUI.EndScrollView();
            return y + height;
        }

        private float DrawActionsSection(float x, float y, float width)
        {
            GUI.Label(new Rect(x, y, width, 22f), "区域三：快捷发送按钮");
            y += 26f;

            const float buttonWidth = 150f;
            const float buttonHeight = 28f;
            const float gapX = 8f;
            const float gapY = 6f;

            var col = 0;
            var row = 0;

            DrawButton(x, y, col, row, buttonWidth, buttonHeight, gapX, gapY, "推进当前阶段", SubmitCurrentPhase);
            NextCell(ref col, ref row);

            DrawButton(x, y, col, row, buttonWidth, buttonHeight, gapX, gapY, "提交战斗", () =>
            {
                SubmitCombatDebug();
            });
            NextCell(ref col, ref row);

            DrawButton(x, y, col, row, buttonWidth, buttonHeight, gapX, gapY, "提交内政", () =>
            {
                SubmitDomesticDebug();
            });
            NextCell(ref col, ref row);

            DrawButton(x, y, col, row, buttonWidth, buttonHeight, gapX, gapY, "设置备战国策", () =>
            {
                MessageSender.Send(new MsgSetPolicy { Policy = "ready_for_war" });
            });
            NextCell(ref col, ref row);

            DrawButton(x, y, col, row, buttonWidth, buttonHeight, gapX, gapY, "设置休养国策", () =>
            {
                MessageSender.Send(new MsgSetPolicy { Policy = "recuperation" });
            });
            NextCell(ref col, ref row);

            DrawButton(x, y, col, row, buttonWidth, buttonHeight, gapX, gapY, "建造农场", () =>
            {
                MessageSender.Send(new MsgTokenBuild { NodeId = "res_food", BuildingType = "farm" });
            });
            NextCell(ref col, ref row);

            DrawButton(x, y, col, row, buttonWidth, buttonHeight, gapX, gapY, "建造矿山", () =>
            {
                MessageSender.Send(new MsgTokenBuild { NodeId = "res_ore", BuildingType = "mine" });
            });
            NextCell(ref col, ref row);

            DrawButton(x, y, col, row, buttonWidth, buttonHeight, gapX, gapY, "查看节点", () =>
            {
                MessageSender.Send(new MsgTokenReveal { NodeId = "res_food" });
            });
            NextCell(ref col, ref row);

            DrawButton(x, y, col, row, buttonWidth, buttonHeight, gapX, gapY, "设置战区", () =>
            {
                var msg = new MsgSetWarZone
                {
                    ZoneId = "zone1",
                    Name = "北线"
                };
                msg.NodeIds.Add("res_ore");
                MessageSender.Send(msg);
            });
            NextCell(ref col, ref row);

            DrawButton(x, y, col, row, buttonWidth, buttonHeight, gapX, gapY, "战区进攻", () =>
            {
                MessageSender.Send(new MsgWarZoneDirective
                {
                    ZoneId = "zone1",
                    Directive = "attack"
                });
            });

            return y + (row + 1) * (buttonHeight + gapY);
        }

        private static void SubmitCurrentPhase()
        {
            var phase = GameStateCache.Instance != null ? GameStateCache.Instance.Phase : string.Empty;
            switch (phase)
            {
                case GamePhases.DomesticPlanning:
                    SubmitDomesticDebug();
                    return;
                case GamePhases.CombatPlanning:
                    SubmitCombatDebug();
                    return;
                default:
                    Debug.LogWarning($"[DebugPanel] 当前阶段不可手动推进 phase={phase}");
                    return;
            }
        }

        private static void SubmitDomesticDebug()
        {
            if (GameIntentsIsReady())
            {
                GameIntents.SubmitDomestic();
                return;
            }

            MessageSender.Send(new MsgSubmitDomestic());
        }

        private static void SubmitCombatDebug()
        {
            if (GameIntentsIsReady())
            {
                GameIntents.SubmitCombat();
                return;
            }

            MessageSender.Send(new MsgSubmitCombat());
        }

        private static bool GameIntentsIsReady()
        {
            return GameStateCache.Instance != null;
        }

        private static void DrawButton(
            float originX,
            float originY,
            int col,
            int row,
            float buttonWidth,
            float buttonHeight,
            float gapX,
            float gapY,
            string label,
            System.Action action)
        {
            var rect = new Rect(
                originX + col * (buttonWidth + gapX),
                originY + row * (buttonHeight + gapY),
                buttonWidth,
                buttonHeight);
            if (GUI.Button(rect, label))
            {
                action?.Invoke();
            }
        }

        private static void NextCell(ref int col, ref int row)
        {
            col++;
            if (col >= 3)
            {
                col = 0;
                row++;
            }
        }

        private static Color GetEntryColor(string direction)
        {
            return direction switch
            {
                "IN" => new Color(0.50f, 1.00f, 0.50f),
                "OUT" => new Color(1.00f, 0.90f, 0.20f),
                _ => new Color(1.00f, 0.40f, 0.40f)
            };
        }

        private static string ToDirectionLabel(string direction)
        {
            return direction switch
            {
                "IN" => "收",
                "OUT" => "发",
                _ => "错"
            };
        }
    }
}
#endif
