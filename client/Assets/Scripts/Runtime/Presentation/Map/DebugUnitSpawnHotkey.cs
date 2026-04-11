/*************************************************
 * Project: Panoptes
 * File: DebugUnitSpawnHotkey.cs
 * Author: Panoptes Team
 * Date: 2026-04-11
 * Description: Press a hotkey to spawn controllable local test units.
 *************************************************/

using System.Collections.Generic;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Domain;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Panoptes.Presentation.Map
{
    public sealed class DebugUnitSpawnHotkey : MonoBehaviour
    {
        [Header("Switch")]
        [SerializeField] private bool enableHotkey = true;

        [Header("Input")]
        [SerializeField] private KeyCode legacySpawnKey = KeyCode.X;
#if ENABLE_INPUT_SYSTEM
        [SerializeField] private Key spawnKey = Key.X;
#endif

        [Header("Spawn")]
        [SerializeField] private int spawnCount = 8;
        [SerializeField] private string unitType = "fighter";
        [SerializeField] private int hp = 100;
        [SerializeField] private int maxHp = 100;
        [SerializeField] private string ownerIdOverride = string.Empty;
        [SerializeField] private bool clearPreviousSpawnedByThisHotkey = false;
        [SerializeField] private bool logResult = true;

        private readonly List<string> _spawnedByThisTool = new();
        private int _spawnSerial = 1;

        private void Update()
        {
            if (!enableHotkey || !GetSpawnKeyDown())
            {
                return;
            }

            SpawnRandomControllableUnits();
        }

        private void SpawnRandomControllableUnits()
        {
            var map = MapRenderer.Instance != null ? MapRenderer.Instance : FindAnyObjectByType<MapRenderer>();
            if (map == null)
            {
                if (logResult)
                {
                    Debug.LogWarning("[DebugUnitSpawnHotkey] MapRenderer not found.");
                }
                return;
            }

            if (clearPreviousSpawnedByThisHotkey)
            {
                for (var i = 0; i < _spawnedByThisTool.Count; i++)
                {
                    map.RemoveRuntimeUnit(_spawnedByThisTool[i], true);
                }
                _spawnedByThisTool.Clear();
            }

            var occupied = new HashSet<string>();
            foreach (var pair in map.UnitViews)
            {
                var view = pair.Value;
                if (view == null)
                {
                    continue;
                }

                occupied.Add(MakeGridKey(view.GridPos.x, view.GridPos.y));
            }

            var candidates = new List<NodeView>(map.TileViews.Count);
            foreach (var pair in map.TileViews)
            {
                var node = pair.Value;
                if (node == null)
                {
                    continue;
                }

                if (!map.IsNodePassableForMove(node.NodeId))
                {
                    continue;
                }

                if (occupied.Contains(MakeGridKey(node.GridPos.x, node.GridPos.y)))
                {
                    continue;
                }

                candidates.Add(node);
            }

            if (candidates.Count == 0)
            {
                if (logResult)
                {
                    Debug.LogWarning("[DebugUnitSpawnHotkey] No available node to spawn units.");
                }
                return;
            }

            var targetCount = Mathf.Clamp(spawnCount, 1, candidates.Count);
            var ownerId = ResolveLocalOwnerId();
            var spawned = 0;

            for (var i = 0; i < targetCount; i++)
            {
                var pick = Random.Range(i, candidates.Count);
                (candidates[i], candidates[pick]) = (candidates[pick], candidates[i]);
                var node = candidates[i];
                if (node == null)
                {
                    continue;
                }

                var unit = new UnitDto
                {
                    Id = $"U_LOCAL_{_spawnSerial++}",
                    Type = string.IsNullOrWhiteSpace(unitType) ? "fighter" : unitType.Trim().ToLowerInvariant(),
                    Owner = ownerId,
                    X = node.GridPos.x,
                    Y = node.GridPos.y,
                    Hp = Mathf.Max(1, hp),
                    MaxHp = Mathf.Max(1, maxHp)
                };

                if (!map.TrySpawnRuntimeUnit(unit, false, true))
                {
                    continue;
                }

                _spawnedByThisTool.Add(unit.Id);
                spawned++;
            }

            if (logResult)
            {
                Debug.Log($"[DebugUnitSpawnHotkey] Spawned controllable units: {spawned} (owner={ownerId}).");
            }
        }

        private string ResolveLocalOwnerId()
        {
            if (!string.IsNullOrWhiteSpace(ownerIdOverride))
            {
                return ownerIdOverride.Trim();
            }

            var cache = GameStateCache.Instance;
            if (cache != null && !string.IsNullOrWhiteSpace(cache.MyPlayerID))
            {
                return cache.MyPlayerID;
            }

            return "blue";
        }

        private static string MakeGridKey(int x, int y)
        {
            return $"{x}:{y}";
        }

        private bool GetSpawnKeyDown()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard[spawnKey].wasPressedThisFrame;
#else
            return Input.GetKeyDown(legacySpawnKey);
#endif
        }
    }
}
