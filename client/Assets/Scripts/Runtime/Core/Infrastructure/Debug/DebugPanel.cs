#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Panoptes.Core.Application.Cache;
using UnityEngine;

namespace Panoptes.DebugTools
{
    public sealed class DebugPanel : MonoBehaviour
    {
        private const float PanelTopOffset = 60f; // default top(10) + 50px downward shift
        private const float PanelSideMargin = 10f;
        private const float PanelBottomMargin = 10f;

        private static DebugPanel _instance;

        private readonly DebugPanelContext.SharedState _sharedState = new();
        private IReadOnlyList<IDebugTab> _tabs;
        private DebugPanelContext _context;
        private bool _expanded;
        private int _selectedTabIndex;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            if (MessageLogger.Instance == null)
            {
                var loggerObject = new GameObject("MessageLogger");
                DontDestroyOnLoad(loggerObject);
                loggerObject.AddComponent<MessageLogger>();
            }

            _context = new DebugPanelContext(_sharedState);
            _tabs = DebugTabRegistry.CreateDefaultTabs();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
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
                if (GUI.Button(new Rect(PanelSideMargin, PanelTopOffset, 96f, 30f), "[Debug]"))
                {
                    _expanded = true;
                }

                return;
            }

            SyncRequestedTab();

            var width = Mathf.Min(980f, Screen.width - PanelSideMargin * 2f);
            var availableHeight = Mathf.Max(160f, Screen.height - PanelTopOffset - PanelBottomMargin);
            var height = Mathf.Min(720f, availableHeight);
            var area = new Rect(PanelSideMargin, PanelTopOffset, width, height);

            GUI.Box(area, string.Empty);
            GUI.Label(new Rect(area.x + 12f, area.y + 10f, 240f, 24f), "Panoptes Debug Workbench");
            if (GUI.Button(new Rect(area.x + area.width - 84f, area.y + 8f, 72f, 24f), "收起"))
            {
                _expanded = false;
            }

            DrawStatusBar(area);
            DrawTabBar(area);
            DrawBody(area);
        }

        private static bool ShouldDisplay()
        {
            var config = ClientRuntimeConfigCache.Instance;
            return config != null && config.DevMode;
        }

        private void DrawStatusBar(Rect area)
        {
            var appState = _context.CurrentAppState.HasValue ? _context.CurrentAppState.Value.ToString() : "-";
            var connection = _context.IsConnected ? "Connected" : _context.IsConnecting ? "Connecting" : "Disconnected";
            var session = _context.Session;
            var username = session != null ? session.Username : string.Empty;
            var room = _context.Room;
            var roomCode = room != null ? room.RoomCode : string.Empty;
            var game = _context.GameState;
            var turn = game != null ? game.Turn.ToString() : "-";
            var phase = game != null ? game.Phase : "-";

            var status = $"Scene={_context.SceneName} | App={appState} | WS={connection} | User={username} | Room={roomCode} | Turn={turn} | Phase={phase}";
            GUI.Box(new Rect(area.x + 10f, area.y + 36f, area.width - 20f, 28f), status);
        }

        private void DrawTabBar(Rect area)
        {
            if (_tabs == null || _tabs.Count == 0)
            {
                return;
            }

            var titles = new string[_tabs.Count];
            for (var i = 0; i < _tabs.Count; i++)
            {
                titles[i] = _tabs[i].Title;
            }

            _selectedTabIndex = GUI.Toolbar(
                new Rect(area.x + 10f, area.y + 72f, area.width - 20f, 28f),
                Mathf.Clamp(_selectedTabIndex, 0, _tabs.Count - 1),
                titles);
        }

        private void DrawBody(Rect area)
        {
            if (_tabs == null || _tabs.Count == 0)
            {
                return;
            }

            var tab = _tabs[Mathf.Clamp(_selectedTabIndex, 0, _tabs.Count - 1)];
            var bodyRect = new Rect(area.x + 10f, area.y + 108f, area.width - 20f, area.height - 118f);

            if (!tab.IsAvailable(_context, out var reason))
            {
                GUI.Box(bodyRect, string.Empty);
                GUILayout.BeginArea(new Rect(bodyRect.x + 12f, bodyRect.y + 12f, bodyRect.width - 24f, bodyRect.height - 24f));
                GUILayout.Label(tab.Title, GUI.skin.box);
                GUILayout.Space(8f);
                GUILayout.Box(string.IsNullOrWhiteSpace(reason) ? "当前上下文不可用。" : reason, GUILayout.ExpandWidth(true));
                GUILayout.EndArea();
                return;
            }

            tab.Draw(_context, bodyRect);
        }

        private void SyncRequestedTab()
        {
            if (string.IsNullOrWhiteSpace(_sharedState.RequestedTabId) || _tabs == null)
            {
                return;
            }

            for (var i = 0; i < _tabs.Count; i++)
            {
                if (string.Equals(_tabs[i].Id, _sharedState.RequestedTabId, System.StringComparison.Ordinal))
                {
                    _selectedTabIndex = i;
                    break;
                }
            }

            _sharedState.RequestedTabId = string.Empty;
        }
    }
}
#endif
