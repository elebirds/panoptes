/*************************************************
 * Project: Panoptes
 * File: GameStateCache.cs
 * Author: Panoptes Team
 * Date: 2026-04-04
 * Description: Server-state mirror cache placeholder.
 *************************************************/

using System;
using System.Collections.Generic;
using UnityEngine;
using Panoptes.Protocol.V1;

namespace Panoptes.Runtime.Cache
{
    public class GameStateCache : MonoBehaviour
    {
        public static GameStateCache Instance { get; private set; }

        // 基本信息
        public string GameID { get; private set; }
        public string MyPlayerID { get; private set; }
        public int Turn { get; private set; }
        public string Phase { get; private set; }
        public int MapWidth { get; private set; }
        public int MapHeight { get; private set; }

        // 节点（key = node_id）
        private readonly Dictionary<string, NodeView> _nodes = new();
        public IReadOnlyDictionary<string, NodeView> Nodes => _nodes;

        // 单位（key = unit_id）
        private readonly Dictionary<string, UnitView> _units = new();
        public IReadOnlyDictionary<string, UnitView> Units => _units;

        // 玩家
        public PlayerView MyPlayer { get; private set; }

        // 部长
        private readonly List<MinisterView> _ministers = new();
        public IReadOnlyList<MinisterView> Ministers => _ministers;

        // 令牌
        public int TokensLeft { get; private set; }

        public event Action OnStateChanged;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // 由 AppManager 或 GameScene 注册，收到 MsgGameInit 时调用
        public void ApplyGameInit(MsgGameInit msg)
        {
            if (msg == null)
            {
                return;
            }

            GameID = msg.GameId;
            MyPlayerID = msg.YourPlayerId;
            Turn = msg.Turn;
            Phase = msg.Phase;
            MapWidth = msg.MapWidth;
            MapHeight = msg.MapHeight;

            _nodes.Clear();
            foreach (var node in msg.Nodes)
                _nodes[node.Id] = node;

            _units.Clear();
            foreach (var unit in msg.Units)
                _units[unit.Id] = unit;

            MyPlayer = msg.MyPlayer;
            TokensLeft = msg.MyPlayer != null ? msg.MyPlayer.TokensLeft : 0;

            _ministers.Clear();
            _ministers.AddRange(msg.Ministers);

            Debug.Log($"[Cache] GameInit applied: {_nodes.Count} nodes, {_units.Count} units");
            OnStateChanged?.Invoke();
        }

        public void UpdateTokens(int tokensLeft)
        {
            TokensLeft = tokensLeft;
            if (MyPlayer != null)
                MyPlayer.TokensLeft = tokensLeft;
            OnStateChanged?.Invoke();
        }

        public void UpdateNode(NodeView node)
        {
            if (node == null)
            {
                return;
            }

            _nodes[node.Id] = node;
            OnStateChanged?.Invoke();
        }

        public NodeView GetNode(string nodeId)
        {
            _nodes.TryGetValue(nodeId, out var node);
            return node;
        }

        public UnitView GetUnit(string unitId)
        {
            _units.TryGetValue(unitId, out var unit);
            return unit;
        }

        public void Clear()
        {
            GameID = string.Empty;
            MyPlayerID = string.Empty;
            Turn = 0;
            Phase = string.Empty;
            MapWidth = 0;
            MapHeight = 0;
            _nodes.Clear();
            _units.Clear();
            MyPlayer = null;
            _ministers.Clear();
            TokensLeft = 0;
            OnStateChanged?.Invoke();
        }
    }
}
