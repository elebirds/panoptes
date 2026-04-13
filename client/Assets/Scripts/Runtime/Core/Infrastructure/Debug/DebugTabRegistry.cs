#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Panoptes.Core.Application.Intents;
using Panoptes.Core.Domain;
using Panoptes.Core.Infrastructure.Network;
using Panoptes.Core.Infrastructure.Service;
using Panoptes.Protocol.V1;
using UnityEngine;

namespace Panoptes.DebugTools
{
    public static class DebugTabRegistry
    {
        public static IReadOnlyList<IDebugTab> CreateDefaultTabs()
        {
            return new IDebugTab[]
            {
                new OverviewDebugTab(),
                new TimelineDebugTab(),
                new RawSenderDebugTab(),
                new LobbyDebugTab(),
                new GameDebugTab(),
            };
        }
    }

    internal static class DebugGuiUtil
    {
        public static void Section(string title)
        {
            GUILayout.Space(4f);
            GUILayout.Label(title, GUI.skin.box);
        }

        public static void KeyValue(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(110f));
            GUILayout.Label(string.IsNullOrWhiteSpace(value) ? "-" : value);
            GUILayout.EndHorizontal();
        }

        public static void HelpBox(string message)
        {
            GUILayout.Box(string.IsNullOrWhiteSpace(message) ? "暂无数据" : message, GUILayout.ExpandWidth(true));
        }

        public static string DirectionLabel(string direction)
        {
            return direction switch
            {
                "IN" => "收",
                "OUT" => "发",
                _ => "错"
            };
        }
    }

    internal sealed class OverviewDebugTab : IDebugTab
    {
        public string Id => "overview";
        public string Title => "总览";

        public bool IsAvailable(DebugPanelContext context, out string reason)
        {
            reason = string.Empty;
            return context != null;
        }

        public void Draw(DebugPanelContext context, Rect rect)
        {
            GUILayout.BeginArea(rect);

            DebugGuiUtil.Section("应用态");
            DebugGuiUtil.KeyValue("Scene", context.SceneName);
            DebugGuiUtil.KeyValue("AppState", context.CurrentAppState.HasValue ? context.CurrentAppState.Value.ToString() : "-");
            DebugGuiUtil.KeyValue("DevMode", context.RuntimeConfig != null && context.RuntimeConfig.DevMode ? "true" : "false");
            DebugGuiUtil.KeyValue("WebSocket", context.IsConnected ? "Connected" : context.IsConnecting ? "Connecting" : "Disconnected");

            DebugGuiUtil.Section("会话");
            var session = context.Session;
            DebugGuiUtil.KeyValue("LoggedIn", session != null && session.IsLoggedIn ? "true" : "false");
            DebugGuiUtil.KeyValue("Username", session != null ? session.Username : string.Empty);
            DebugGuiUtil.KeyValue("PlayerID", session != null ? session.PlayerID : string.Empty);

            DebugGuiUtil.Section("房间");
            var room = context.Room;
            DebugGuiUtil.KeyValue("RoomCode", room != null ? room.RoomCode : string.Empty);
            DebugGuiUtil.KeyValue("RoomName", room != null ? room.RoomName : string.Empty);
            DebugGuiUtil.KeyValue("Players", room != null ? $"{room.Players.Count}/{room.MaxPlayers}" : string.Empty);
            DebugGuiUtil.KeyValue("Status", room != null ? room.Status : string.Empty);

            DebugGuiUtil.Section("战局");
            var game = context.GameState;
            DebugGuiUtil.KeyValue("GameID", game != null ? game.GameID : string.Empty);
            DebugGuiUtil.KeyValue("Turn", game != null ? game.Turn.ToString() : string.Empty);
            DebugGuiUtil.KeyValue("Phase", game != null ? game.Phase : string.Empty);
            DebugGuiUtil.KeyValue("Tokens", game != null ? game.TokensLeft.ToString() : string.Empty);
            DebugGuiUtil.KeyValue("Nodes", game != null ? game.Nodes.Count.ToString() : string.Empty);
            DebugGuiUtil.KeyValue("Units", game != null ? game.Units.Count.ToString() : string.Empty);

            GUILayout.EndArea();
        }
    }

    internal sealed class TimelineDebugTab : IDebugTab
    {
        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private bool _showIn = true;
        private bool _showOut = true;
        private bool _showErr = true;
        private string _typeFilter = string.Empty;
        private int _selectedIndex = -1;

        public string Id => "timeline";
        public string Title => "消息时间线";

        public bool IsAvailable(DebugPanelContext context, out string reason)
        {
            reason = string.Empty;
            return context != null;
        }

        public void Draw(DebugPanelContext context, Rect rect)
        {
            var logger = context.Logger;
            var entries = logger != null ? logger.Entries : null;
            var filteredIndices = BuildFilteredIndices(entries);

            var leftWidth = Mathf.Max(280f, rect.width * 0.45f);
            var leftRect = new Rect(rect.x, rect.y, leftWidth, rect.height);
            var rightRect = new Rect(rect.x + leftWidth + 8f, rect.y, rect.width - leftWidth - 8f, rect.height);

            DrawList(leftRect, entries, filteredIndices);
            DrawDetail(rightRect, context, entries, filteredIndices);
        }

        private List<int> BuildFilteredIndices(IReadOnlyList<MessageLogger.LogEntry> entries)
        {
            var indices = new List<int>();
            if (entries == null)
            {
                return indices;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (!_showIn && string.Equals(entry.Direction, "IN", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!_showOut && string.Equals(entry.Direction, "OUT", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!_showErr && string.Equals(entry.Direction, "ERR", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(_typeFilter) &&
                    entry.MsgType.IndexOf(_typeFilter, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                indices.Add(i);
            }

            return indices;
        }

        private void DrawList(Rect rect, IReadOnlyList<MessageLogger.LogEntry> entries, IReadOnlyList<int> filteredIndices)
        {
            GUI.Box(rect, string.Empty);
            GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f));

            GUILayout.Label("方向筛选");
            GUILayout.BeginHorizontal();
            _showIn = GUILayout.Toggle(_showIn, "IN", GUILayout.Width(70f));
            _showOut = GUILayout.Toggle(_showOut, "OUT", GUILayout.Width(70f));
            _showErr = GUILayout.Toggle(_showErr, "ERR", GUILayout.Width(70f));
            GUILayout.EndHorizontal();

            GUILayout.Label("类型筛选");
            _typeFilter = GUILayout.TextField(_typeFilter ?? string.Empty);
            GUILayout.Space(6f);

            if (filteredIndices == null || filteredIndices.Count == 0)
            {
                DebugGuiUtil.HelpBox("当前筛选条件下没有日志。");
                GUILayout.EndArea();
                return;
            }

            _listScroll = GUILayout.BeginScrollView(_listScroll);
            for (var idx = filteredIndices.Count - 1; idx >= 0; idx--)
            {
                var actualIndex = filteredIndices[idx];
                var entry = entries[actualIndex];
                var prefix = _selectedIndex == actualIndex ? ">" : " ";
                var label = $"{prefix}[{DebugGuiUtil.DirectionLabel(entry.Direction)}] {entry.Timestamp} {entry.MsgType}";
                if (GUILayout.Button(label, GUILayout.Height(28f)))
                {
                    _selectedIndex = actualIndex;
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawDetail(
            Rect rect,
            DebugPanelContext context,
            IReadOnlyList<MessageLogger.LogEntry> entries,
            IReadOnlyList<int> filteredIndices)
        {
            GUI.Box(rect, string.Empty);
            GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f));

            if (entries == null || entries.Count == 0 || filteredIndices == null || filteredIndices.Count == 0)
            {
                DebugGuiUtil.HelpBox("暂无可查看详情的消息。");
                GUILayout.EndArea();
                return;
            }

            if (_selectedIndex < 0 || _selectedIndex >= entries.Count)
            {
                _selectedIndex = filteredIndices[filteredIndices.Count - 1];
            }

            var entry = entries[_selectedIndex];
            DebugGuiUtil.KeyValue("方向", entry.Direction);
            DebugGuiUtil.KeyValue("时间", entry.Timestamp);
            DebugGuiUtil.KeyValue("类型", entry.MsgType);
            DebugGuiUtil.KeyValue("摘要", entry.Summary);
            DebugGuiUtil.KeyValue("可重发", entry.CanReplay ? "true" : "false");

            if (!string.IsNullOrWhiteSpace(entry.Error))
            {
                DebugGuiUtil.KeyValue("错误", entry.Error);
            }

            GUILayout.Space(6f);
            if (entry.CanReplay &&
                GUILayout.Button("送入原始发送器", GUILayout.Height(28f)))
            {
                context.OpenRawSenderDraft(
                    entry.MsgType,
                    string.IsNullOrWhiteSpace(entry.PayloadJson) ? DebugMessageRegistry.CreateDefaultPayload(entry.MsgType) : entry.PayloadJson,
                    $"已从时间线载入 {entry.MsgType}");
            }

            GUILayout.Space(6f);
            GUILayout.Label("Payload");
            _detailScroll = GUILayout.BeginScrollView(_detailScroll);
            GUILayout.TextArea(string.IsNullOrWhiteSpace(entry.PayloadJson) ? "{}" : entry.PayloadJson, GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }

    internal sealed class RawSenderDebugTab : IDebugTab
    {
        private Vector2 _typeScroll;
        private Vector2 _payloadScroll;
        private string _typeFilter = string.Empty;

        public string Id => "raw-sender";
        public string Title => "原始发送器";

        public bool IsAvailable(DebugPanelContext context, out string reason)
        {
            reason = string.Empty;
            return context != null;
        }

        public void Draw(DebugPanelContext context, Rect rect)
        {
            var state = context.State;
            if (string.IsNullOrWhiteSpace(state.RawMessageType))
            {
                state.RawMessageType = "MsgSubmitDomestic";
            }

            if (string.IsNullOrWhiteSpace(state.RawPayloadJson))
            {
                state.RawPayloadJson = DebugMessageRegistry.CreateDefaultPayload(state.RawMessageType);
            }

            var leftWidth = Mathf.Max(240f, rect.width * 0.33f);
            var leftRect = new Rect(rect.x, rect.y, leftWidth, rect.height);
            var rightRect = new Rect(rect.x + leftWidth + 8f, rect.y, rect.width - leftWidth - 8f, rect.height);

            DrawTypeList(leftRect, state);
            DrawEditor(rightRect, context, state);
        }

        private void DrawTypeList(Rect rect, DebugPanelContext.SharedState state)
        {
            GUI.Box(rect, string.Empty);
            GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f));

            GUILayout.Label("消息类型");
            _typeFilter = GUILayout.TextField(_typeFilter ?? string.Empty);
            GUILayout.Space(6f);

            _typeScroll = GUILayout.BeginScrollView(_typeScroll);
            foreach (var messageType in DebugMessageRegistry.SupportedMessageTypes)
            {
                if (!string.IsNullOrWhiteSpace(_typeFilter) &&
                    messageType.IndexOf(_typeFilter, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                var label = string.Equals(state.RawMessageType, messageType, StringComparison.Ordinal)
                    ? $"> {messageType}"
                    : messageType;
                if (GUILayout.Button(label, GUILayout.Height(28f)))
                {
                    state.RawMessageType = messageType;
                    state.RawPayloadJson = DebugMessageRegistry.CreateDefaultPayload(messageType);
                    state.RawStatus = $"已切换为 {messageType}";
                    state.RawStatusIsError = false;
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawEditor(Rect rect, DebugPanelContext context, DebugPanelContext.SharedState state)
        {
            GUI.Box(rect, string.Empty);
            GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f));

            GUILayout.Label("当前消息类型");
            state.RawMessageType = GUILayout.TextField(state.RawMessageType ?? string.Empty);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("套用默认", GUILayout.Height(28f)))
            {
                state.RawPayloadJson = DebugMessageRegistry.CreateDefaultPayload(state.RawMessageType);
                context.SetRawSenderStatus($"已重置 {state.RawMessageType} 默认 payload", false);
            }

            if (GUILayout.Button("清空", GUILayout.Height(28f)))
            {
                state.RawPayloadJson = "{}";
                context.SetRawSenderStatus("payload 已清空", false);
            }

            if (GUILayout.Button("发送", GUILayout.Height(28f)))
            {
                TrySend(state, context);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("Payload JSON");
            _payloadScroll = GUILayout.BeginScrollView(_payloadScroll);
            state.RawPayloadJson = GUILayout.TextArea(state.RawPayloadJson ?? "{}", GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();

            if (!string.IsNullOrWhiteSpace(state.RawStatus))
            {
                var oldColor = GUI.color;
                GUI.color = state.RawStatusIsError ? new Color(1f, 0.45f, 0.45f) : new Color(0.6f, 1f, 0.6f);
                GUILayout.Label(state.RawStatus, GUI.skin.box);
                GUI.color = oldColor;
            }

            GUILayout.EndArea();
        }

        private static void TrySend(DebugPanelContext.SharedState state, DebugPanelContext context)
        {
            if (!DebugMessageRegistry.TryCreateMessage(state.RawMessageType, state.RawPayloadJson, out var message, out var error))
            {
                context.SetRawSenderStatus($"发送失败：{error}", true);
                return;
            }

            MessageSender.Send(message);
            context.SetRawSenderStatus($"已发送 {state.RawMessageType}", false);
        }
    }

    internal sealed class LobbyDebugTab : IDebugTab
    {
        private readonly LobbyService _service = new();
        private string _roomName = "debug-room";
        private string _maxPlayersText = "2";
        private string _roomCode = string.Empty;
        private string _kickPlayerId = string.Empty;
        private Vector2 _playersScroll;

        public string Id => "lobby";
        public string Title => "Lobby";

        public bool IsAvailable(DebugPanelContext context, out string reason)
        {
            if (context.Session == null || !context.Session.IsLoggedIn)
            {
                reason = "未登录，Lobby 调试页已禁用。";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public void Draw(DebugPanelContext context, Rect rect)
        {
            GUILayout.BeginArea(rect);

            DebugGuiUtil.Section("快捷操作");
            GUILayout.Label("房间名");
            _roomName = GUILayout.TextField(_roomName ?? string.Empty);
            GUILayout.Label("最大人数");
            _maxPlayersText = GUILayout.TextField(_maxPlayersText ?? string.Empty);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("创建房间", GUILayout.Height(28f)))
            {
                var maxPlayers = int.TryParse(_maxPlayersText, out var parsed) ? Mathf.Max(2, parsed) : 2;
                _service.CreateRoom(_roomName, maxPlayers);
            }

            if (GUILayout.Button("准备/取消准备", GUILayout.Height(28f)))
            {
                _service.ReadyUp();
            }

            if (GUILayout.Button("离开房间", GUILayout.Height(28f)))
            {
                _service.LeaveRoom();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("邀请码");
            _roomCode = GUILayout.TextField(_roomCode ?? string.Empty);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("加入房间", GUILayout.Height(28f)))
            {
                _service.JoinRoom(_roomCode);
            }

            if (GUILayout.Button("加 Bot", GUILayout.Height(28f)))
            {
                _service.AddBot();
            }

            if (GUILayout.Button("开始游戏", GUILayout.Height(28f)))
            {
                _service.StartGame();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("踢人 PlayerID");
            _kickPlayerId = GUILayout.TextField(_kickPlayerId ?? string.Empty);
            if (GUILayout.Button("踢出玩家", GUILayout.Height(28f)))
            {
                _service.KickPlayer(_kickPlayerId);
            }

            var room = context.Room;
            DebugGuiUtil.Section("房间缓存快照");
            DebugGuiUtil.KeyValue("RoomID", room != null ? room.RoomID : string.Empty);
            DebugGuiUtil.KeyValue("RoomCode", room != null ? room.RoomCode : string.Empty);
            DebugGuiUtil.KeyValue("RoomName", room != null ? room.RoomName : string.Empty);
            DebugGuiUtil.KeyValue("Status", room != null ? room.Status : string.Empty);
            DebugGuiUtil.KeyValue("Host", room != null && room.IsHost ? "true" : "false");

            GUILayout.Space(6f);
            GUILayout.Label("Players");
            _playersScroll = GUILayout.BeginScrollView(_playersScroll, GUILayout.Height(180f));
            if (room == null || room.Players.Count == 0)
            {
                GUILayout.Label("暂无玩家快照。");
            }
            else
            {
                foreach (var player in room.Players)
                {
                    GUILayout.Box(
                        $"{player.Username} | id={player.PlayerId} | host={player.IsHost} | ready={player.IsReady} | bot={player.IsBot}",
                        GUILayout.ExpandWidth(true));
                }
            }
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }
    }

    internal sealed class GameDebugTab : IDebugTab
    {
        private string _buildNodeId = "res_food";
        private string _buildingType = "farm";
        private string _revealNodeId = "res_food";
        private string _warZoneId = "zone1";
        private string _warZoneName = "北线";
        private string _warZoneNodes = "res_ore";
        private string _warDirective = "attack";
        private string _unitId = "unit-1";
        private string _targetNodeId = "node-b";
        private string _targetUnitId = "unit-2";

        public string Id => "game";
        public string Title => "Game";

        public bool IsAvailable(DebugPanelContext context, out string reason)
        {
            if (context.GameState == null || string.IsNullOrWhiteSpace(context.GameState.GameID))
            {
                reason = "等待 MsgGameInit 后再使用 Game 调试页。";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public void Draw(DebugPanelContext context, Rect rect)
        {
            var cache = context.GameState;

            GUILayout.BeginArea(rect);

            DebugGuiUtil.Section("阶段状态");
            DebugGuiUtil.KeyValue("GameID", cache.GameID);
            DebugGuiUtil.KeyValue("Turn", cache.Turn.ToString());
            DebugGuiUtil.KeyValue("Phase", cache.Phase);
            DebugGuiUtil.KeyValue("Tokens", cache.TokensLeft.ToString());

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("推进当前阶段", GUILayout.Height(28f)))
            {
                SubmitCurrentPhase(cache.Phase);
            }

            if (GUILayout.Button("提交内政", GUILayout.Height(28f)))
            {
                GameIntents.SubmitDomestic();
            }

            if (GUILayout.Button("提交战斗", GUILayout.Height(28f)))
            {
                GameIntents.SubmitCombat();
            }
            GUILayout.EndHorizontal();

            DebugGuiUtil.Section("国策");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("设置备战国策", GUILayout.Height(28f)))
            {
                GameIntents.SetPolicy("ready_for_war");
            }

            if (GUILayout.Button("设置休养国策", GUILayout.Height(28f)))
            {
                GameIntents.SetPolicy("recuperation");
            }
            GUILayout.EndHorizontal();

            DebugGuiUtil.Section("令牌操作");
            GUILayout.Label("建造节点");
            _buildNodeId = GUILayout.TextField(_buildNodeId ?? string.Empty);
            GUILayout.Label("建筑类型");
            _buildingType = GUILayout.TextField(_buildingType ?? string.Empty);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("发送建造", GUILayout.Height(28f)))
            {
                GameIntents.BuildToken(_buildNodeId, _buildingType);
            }

            if (GUILayout.Button("农场模板", GUILayout.Height(28f)))
            {
                _buildNodeId = "res_food";
                _buildingType = "farm";
            }

            if (GUILayout.Button("矿山模板", GUILayout.Height(28f)))
            {
                _buildNodeId = "res_ore";
                _buildingType = "mine";
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("侦察节点");
            _revealNodeId = GUILayout.TextField(_revealNodeId ?? string.Empty);
            if (GUILayout.Button("发送侦察", GUILayout.Height(28f)))
            {
                GameIntents.RevealToken(_revealNodeId);
            }

            DebugGuiUtil.Section("战区");
            GUILayout.Label("ZoneId");
            _warZoneId = GUILayout.TextField(_warZoneId ?? string.Empty);
            GUILayout.Label("名称");
            _warZoneName = GUILayout.TextField(_warZoneName ?? string.Empty);
            GUILayout.Label("NodeIds（逗号分隔）");
            _warZoneNodes = GUILayout.TextField(_warZoneNodes ?? string.Empty);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("设置战区", GUILayout.Height(28f)))
            {
                var message = new MsgSetWarZone
                {
                    ZoneId = _warZoneId ?? string.Empty,
                    Name = _warZoneName ?? string.Empty
                };

                foreach (var nodeId in SplitCsv(_warZoneNodes))
                {
                    message.NodeIds.Add(nodeId);
                }

                MessageSender.Send(message);
            }

            _warDirective = GUILayout.TextField(_warDirective ?? string.Empty);
            if (GUILayout.Button("发送战区指令", GUILayout.Height(28f)))
            {
                MessageSender.Send(new MsgWarZoneDirective
                {
                    ZoneId = _warZoneId ?? string.Empty,
                    Directive = _warDirective ?? string.Empty
                });
            }
            GUILayout.EndHorizontal();

            DebugGuiUtil.Section("战斗微操");
            GUILayout.Label("UnitId");
            _unitId = GUILayout.TextField(_unitId ?? string.Empty);
            GUILayout.Label("TargetNodeId");
            _targetNodeId = GUILayout.TextField(_targetNodeId ?? string.Empty);
            GUILayout.Label("TargetUnitId");
            _targetUnitId = GUILayout.TextField(_targetUnitId ?? string.Empty);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Move", GUILayout.Height(28f)))
            {
                GameIntents.MoveUnit(_unitId, _targetNodeId);
            }

            if (GUILayout.Button("Attack", GUILayout.Height(28f)))
            {
                GameIntents.AttackUnit(_unitId, _targetUnitId);
            }

            if (GUILayout.Button("Hold", GUILayout.Height(28f)))
            {
                GameIntents.HoldUnit(_unitId);
            }
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private static void SubmitCurrentPhase(string phase)
        {
            switch (phase)
            {
                case GamePhases.DomesticPlanning:
                    GameIntents.SubmitDomestic();
                    return;
                case GamePhases.CombatPlanning:
                    GameIntents.SubmitCombat();
                    return;
                default:
                    Debug.LogWarning($"[DebugPanel] 当前阶段不可手动推进 phase={phase}");
                    return;
            }
        }

        private static IEnumerable<string> SplitCsv(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            var parts = value.Split(',');
            for (var i = 0; i < parts.Length; i++)
            {
                var trimmed = parts[i].Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    yield return trimmed;
                }
            }
        }
    }
}
#endif
