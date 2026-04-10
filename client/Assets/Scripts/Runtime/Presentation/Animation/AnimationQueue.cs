/*************************************************
 * Project: Panoptes
 * File: AnimationQueue.cs
 * Author: Panoptes Team
 * Date: 2026-04-06
 * Description: Sequential animation queue for runtime events.
 *************************************************/

using System.Collections;
using System.Collections.Generic;
using Panoptes.Presentation.Map;
using UnityEngine;

namespace Panoptes.Presentation.Animation
{
    public sealed class AnimationQueue : MonoBehaviour
    {
        private struct UnitMoveCommand
        {
            public string unitId;
            public string targetNodeId;
            public bool followCamera;
        }

        public static AnimationQueue Instance { get; private set; }

        [Header("Unit Move")]
        [SerializeField] private float moveDuration = 0.35f;
        [SerializeField] private bool followCameraOnMove = true;
        [SerializeField] private bool lockManualCameraInputDuringFollow = true;

        private readonly Queue<UnitMoveCommand> _unitMoveQueue = new();
        private bool _isPlayingUnitMoves;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void EnqueueUnitMove(string unitId, string targetNodeId, bool followCamera = true)
        {
            if (string.IsNullOrEmpty(unitId) || string.IsNullOrEmpty(targetNodeId))
            {
                return;
            }

            _unitMoveQueue.Enqueue(new UnitMoveCommand
            {
                unitId = unitId,
                targetNodeId = targetNodeId,
                followCamera = followCamera
            });

            if (!_isPlayingUnitMoves)
            {
                StartCoroutine(PlayUnitMoveQueue());
            }
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
            var map = MapRenderer.Instance;
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
            var camController = camera != null ? camera.GetComponent<TopDownCameraController>() : null;
            var follow = followCameraOnMove && cmd.followCamera && camera != null;

            if (follow && lockManualCameraInputDuringFollow && camController != null)
            {
                camController.enabled = false;
            }

            var target = nodeView.UnitAnchor != null
                ? nodeView.UnitAnchor.position
                : nodeView.transform.position + Vector3.up * 0.2f;

            yield return UnitMoveAnim.Play(unitView, target, moveDuration, camera, follow);

            map.SetUnitNode(cmd.unitId, cmd.targetNodeId);

            if (follow && lockManualCameraInputDuringFollow && camController != null)
            {
                camController.enabled = true;
                camController.SnapTargetToCurrentPosition();
            }
        }
    }
}
