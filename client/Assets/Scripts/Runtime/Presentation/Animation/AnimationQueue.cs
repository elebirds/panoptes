/*************************************************
 * Project: Panoptes
 * File: AnimationQueue.cs
 * Author: Panoptes Team
 * Date: 2026-04-06
 * Description: Sequential animation queue for runtime events.
 *************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using Panoptes.Presentation.Map;
using UnityEngine;
using VContainer;

namespace Panoptes.Presentation.Animation
{
    public sealed class AnimationQueue : MonoBehaviour
    {
        private struct UnitMoveCommand
        {
            public string unitId;
            public string targetNodeId;
            public bool followCamera;
            public List<string> pathNodeIds;
        }

        [Header("Unit Move")]
        [SerializeField] private float moveDuration = 0.98f;
        [SerializeField] private bool followCameraOnMove = true;
        [SerializeField] private float cameraFocusLeadSeconds = 0.65f;

        private readonly Queue<UnitMoveCommand> _unitMoveQueue = new();
        private MapRenderer _mapRenderer;
        private bool _isPlayingUnitMoves;

        [Inject]
        private void Construct(MapRenderer mapRenderer)
        {
            _mapRenderer = mapRenderer;
        }

        public void EnqueueUnitMove(string unitId, string targetNodeId, bool followCamera = true, IReadOnlyList<string> pathNodeIds = null)
        {
            if (string.IsNullOrEmpty(unitId) || string.IsNullOrEmpty(targetNodeId))
            {
                return;
            }

            _unitMoveQueue.Enqueue(new UnitMoveCommand
            {
                unitId = unitId,
                targetNodeId = targetNodeId,
                followCamera = followCamera,
                pathNodeIds = pathNodeIds != null ? new List<string>(pathNodeIds) : null
            });

            if (!_isPlayingUnitMoves)
            {
                StartCoroutine(PlayUnitMoveQueue());
            }
        }

        public IEnumerator PlayUnitMoveNow(string unitId, string targetNodeId, bool followCamera = true, IReadOnlyList<string> pathNodeIds = null)
        {
            yield return PlaySingleUnitMove(new UnitMoveCommand
            {
                unitId = unitId,
                targetNodeId = targetNodeId,
                followCamera = followCamera,
                pathNodeIds = pathNodeIds != null ? new List<string>(pathNodeIds) : null
            });
        }

        private IEnumerator PlayUnitMoveQueue()
        {
            _isPlayingUnitMoves = true;

            while (_unitMoveQueue.Count > 0)
            {
                var cmd = _unitMoveQueue.Dequeue();
                yield return PlaySingleUnitMove(cmd);
            }

            _isPlayingUnitMoves = false;
        }

        private IEnumerator PlaySingleUnitMove(UnitMoveCommand cmd)
        {
            var map = _mapRenderer;
            if (map == null)
            {
                yield break;
            }

            if (!map.TryGetUnitView(cmd.unitId, out var unitView) || unitView == null)
            {
                yield break;
            }

            if (!map.TryGetNodeView(cmd.targetNodeId, out var nodeView) || nodeView == null)
            {
                yield break;
            }

            var camera = Camera.main;
            var follow = followCameraOnMove && cmd.followCamera;

            var target = nodeView.UnitAnchor != null
                ? nodeView.UnitAnchor.position
                : nodeView.transform.position + Vector3.up * 0.2f;

            var waypoints = BuildWaypoints(map, unitView, cmd.targetNodeId, target, cmd.pathNodeIds);
            if (follow)
            {
                CinemachineMapCameraController.TryFocus(unitView.transform.position, false);
                if (cameraFocusLeadSeconds > 0.0001f)
                {
                    yield return new WaitForSecondsRealtime(cameraFocusLeadSeconds);
                }
            }

            unitView.SetSelected(true);
            var segmentDuration = Mathf.Max(0.17f, moveDuration / Mathf.Max(1, waypoints.Count));
            for (var i = 0; i < waypoints.Count; i++)
            {
                yield return UnitMoveAnim.Play(unitView, waypoints[i], segmentDuration, camera, follow);
            }
            unitView.SetSelected(false);

            if (map.TryGetUnitView(cmd.unitId, out var stillAliveUnit) && stillAliveUnit != null)
            {
                map.SetUnitNode(cmd.unitId, cmd.targetNodeId);
            }
        }

        private static List<Vector3> BuildWaypoints(MapRenderer map, UnitView unitView, string targetNodeId, Vector3 fallbackTarget, IReadOnlyList<string> pathNodeIds)
        {
            var waypoints = new List<Vector3>();
            if (map == null || unitView == null)
            {
                return waypoints;
            }

            if (pathNodeIds != null && pathNodeIds.Count > 0)
            {
                var currentNodeId = string.Empty;
                map.TryGetNodeIdByGrid(unitView.GridPos, out currentNodeId);

                var startIndex = 0;
                if (!string.IsNullOrWhiteSpace(currentNodeId))
                {
                    for (var i = 0; i < pathNodeIds.Count; i++)
                    {
                        if (string.Equals(pathNodeIds[i], currentNodeId, StringComparison.Ordinal))
                        {
                            startIndex = i + 1;
                            break;
                        }
                    }
                }

                for (var i = startIndex; i < pathNodeIds.Count; i++)
                {
                    var nodeId = pathNodeIds[i];
                    if (string.IsNullOrWhiteSpace(nodeId) || !map.TryGetNodeView(nodeId, out var pathNode) || pathNode == null)
                    {
                        continue;
                    }

                    var waypoint = pathNode.UnitAnchor != null
                        ? pathNode.UnitAnchor.position
                        : pathNode.transform.position + Vector3.up * 0.2f;
                    if (waypoints.Count == 0 || Vector3.Distance(waypoints[waypoints.Count - 1], waypoint) > 0.001f)
                    {
                        waypoints.Add(waypoint);
                    }

                    if (string.Equals(nodeId, targetNodeId, StringComparison.Ordinal))
                    {
                        break;
                    }
                }
            }

            if (waypoints.Count == 0 || Vector3.Distance(waypoints[waypoints.Count - 1], fallbackTarget) > 0.001f)
            {
                waypoints.Add(fallbackTarget);
            }

            return waypoints;
        }
    }
}
