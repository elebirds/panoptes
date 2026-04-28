/*************************************************
 * Project: Panoptes
 * File: MapInputHandler.MovePreviewGhost.cs
 * Author: Panoptes Team
 * Date: 2026-04-29
 * Description: Runtime move preview ghost creation, animation, rendering, and cleanup.
 *************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Intents;
using Panoptes.Core.Domain;
using Panoptes.Core.Events;
using Panoptes.Presentation.Animation;
using Panoptes.Presentation.UI.Common;
using Panoptes.Presentation.UI.HUD;
using UnityEngine;
using UnityEngine.Rendering;

namespace Panoptes.Presentation.Map
{
    public sealed partial class MapInputHandler
    {
        private void CreateOrUpdateMovePreview(string unitId, string targetNodeId)
        {
            if (!enableMovePreviewGhost || string.IsNullOrEmpty(unitId) || string.IsNullOrEmpty(targetNodeId))
            {
                return;
            }

            var map = MapRenderer.Instance;
            if (map == null)
            {
                return;
            }

            if (!map.TryGetUnitView(unitId, out var sourceUnit) || sourceUnit == null)
            {
                return;
            }

            if (!map.TryGetNodeView(targetNodeId, out var targetNode) || targetNode == null)
            {
                return;
            }

            RemoveMovePreview(unitId);

            var ghost = CreateMovePreviewObject(sourceUnit);
            if (ghost == null)
            {
                return;
            }

            ghost.name = $"MoveGhost_{unitId}";
            ghost.transform.SetParent(map.transform, true);
            ghost.AddComponent<MoveGhostTag>();

            var useProxy = ShouldUseLightweightProxy(sourceUnit);
            if (useProxy)
            {
                var ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
                if (ignoreRaycastLayer >= 0)
                {
                    SetLayerRecursively(ghost.transform, ignoreRaycastLayer);
                }
                ApplyGhostVisual(ghost);
            }
            else
            {
                DisableBehavioursAndColliders(ghost, keepAnimators: true);
                ApplyGhostVisual(ghost);
                SetGhostMoveState(ghost, isMoving: true, normalizedSpeed: 1f);
            }

            _movePreviewByUnitId[unitId] = ghost;

            var destination = targetNode.UnitAnchor != null
                ? targetNode.UnitAnchor.position
                : targetNode.transform.position + Vector3.up * 0.2f;
            destination.y += movePreviewTargetYOffset;

            StartCoroutine(AnimateMovePreview(ghost, destination));
        }

        private bool ShouldUseLightweightProxy(UnitView sourceUnit)
        {
            if (!movePreviewUseLightweightProxy)
            {
                return false;
            }

            if (movePreviewAlwaysMatchUnitVisual)
            {
                return false;
            }

            return sourceUnit == null;
        }

        private GameObject CreateMovePreviewObject(UnitView sourceUnit)
        {
            if (sourceUnit == null)
            {
                return null;
            }

            if (!ShouldUseLightweightProxy(sourceUnit))
            {
                var sourceVisualRoot = sourceUnit.VisualRoot;
                var sourceObject = sourceVisualRoot != null ? sourceVisualRoot.gameObject : sourceUnit.gameObject;
                var clone = Instantiate(sourceObject);
                clone.transform.position = sourceObject.transform.position;
                clone.transform.rotation = sourceObject.transform.rotation;
                return clone;
            }

            var proxy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            proxy.transform.position = sourceUnit.transform.position + Vector3.up * movePreviewProxyYOffset;
            proxy.transform.rotation = sourceUnit.transform.rotation;
            proxy.transform.localScale = movePreviewProxyScale;

            var collider = proxy.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var renderer = proxy.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = GetOrCreateMovePreviewProxyMaterial();
                renderer.shadowCastingMode = movePreviewProxyCastShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderer.receiveShadows = movePreviewProxyCastShadow;
            }

            return proxy;
        }

        private Material GetOrCreateMovePreviewProxyMaterial()
        {
            if (_movePreviewProxyMaterial != null)
            {
                return _movePreviewProxyMaterial;
            }

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                return null;
            }

            _movePreviewProxyMaterial = new Material(shader);
            _movePreviewProxyMaterial.name = "MovePreviewProxyMat_Runtime";
            _movePreviewProxyMaterial.hideFlags = HideFlags.DontSave;

            if (_movePreviewProxyMaterial.HasProperty("_Surface"))
            {
                _movePreviewProxyMaterial.SetFloat("_Surface", 1f);
            }

            if (_movePreviewProxyMaterial.HasProperty("_Blend"))
            {
                _movePreviewProxyMaterial.SetFloat("_Blend", 0f);
            }

            if (_movePreviewProxyMaterial.HasProperty("_SrcBlend"))
            {
                _movePreviewProxyMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            }

            if (_movePreviewProxyMaterial.HasProperty("_DstBlend"))
            {
                _movePreviewProxyMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            }

            if (_movePreviewProxyMaterial.HasProperty("_ZWrite"))
            {
                _movePreviewProxyMaterial.SetFloat("_ZWrite", 0f);
            }

            _movePreviewProxyMaterial.renderQueue = (int)RenderQueue.Transparent;
            return _movePreviewProxyMaterial;
        }

        private void DisposeMovePreviewProxyMaterial()
        {
            if (_movePreviewProxyMaterial == null)
            {
                return;
            }

            Destroy(_movePreviewProxyMaterial);
            _movePreviewProxyMaterial = null;
        }

        private IEnumerator AnimateMovePreview(GameObject ghost, Vector3 destination)
        {
            if (ghost == null)
            {
                yield break;
            }

            var start = ghost.transform.position;
            var duration = Mathf.Max(0.01f, movePreviewTravelDuration);
            var elapsed = 0f;

            while (elapsed < duration && ghost != null)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var pos = Vector3.Lerp(start, destination, t);
                if (movePreviewArcHeight > 0.0001f)
                {
                    pos.y += Mathf.Sin(t * Mathf.PI) * movePreviewArcHeight;
                }

                var moveDir = destination - ghost.transform.position;
                moveDir.y = 0f;
                if (moveDir.sqrMagnitude > 0.0001f)
                {
                    ghost.transform.rotation = Quaternion.Slerp(
                        ghost.transform.rotation,
                        Quaternion.LookRotation(moveDir.normalized, Vector3.up),
                        Time.deltaTime * 18f);
                }

                ghost.transform.position = pos;
                yield return null;
            }

            if (ghost != null)
            {
                ghost.transform.position = destination;
                SetGhostMoveState(ghost, isMoving: false, normalizedSpeed: 0f);
            }
        }

        private void ApplyGhostVisual(GameObject ghostRoot)
        {
            if (ghostRoot == null)
            {
                return;
            }

            var renderers = ghostRoot.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                var mats = renderer.sharedMaterials;
                if (mats == null || mats.Length == 0)
                {
                    continue;
                }

                for (var m = 0; m < mats.Length; m++)
                {
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block, m);
                    var ghostColor = movePreviewKeepTextureColor
                        ? new Color(1f, 1f, 1f, movePreviewGhostColor.a)
                        : Color.Lerp(Color.white, movePreviewGhostColor, movePreviewTintStrength);
                    block.SetColor("_BaseColor", ghostColor);
                    block.SetColor("_Color", ghostColor);
                    renderer.SetPropertyBlock(block, m);
                }
            }
        }

        private static void DisableBehavioursAndColliders(GameObject root, bool keepAnimators)
        {
            if (root == null)
            {
                return;
            }

            var ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
            if (ignoreRaycastLayer >= 0)
            {
                SetLayerRecursively(root.transform, ignoreRaycastLayer);
            }

            var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (var i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour == null)
                {
                    continue;
                }

                behaviour.enabled = false;
            }

            var animators = root.GetComponentsInChildren<Animator>(true);
            for (var i = 0; i < animators.Length; i++)
            {
                var animator = animators[i];
                if (animator != null)
                {
                    animator.enabled = keepAnimators;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }
            }

            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                if (collider != null)
                {
                    collider.enabled = false;
                }
            }
        }

        private static void SetGhostMoveState(GameObject ghostRoot, bool isMoving, float normalizedSpeed)
        {
            if (ghostRoot == null)
            {
                return;
            }

            var animators = ghostRoot.GetComponentsInChildren<Animator>(true);
            for (var i = 0; i < animators.Length; i++)
            {
                var animator = animators[i];
                if (animator == null)
                {
                    continue;
                }

                if (HasAnimatorParameter(animator, "isMoving", AnimatorControllerParameterType.Bool))
                {
                    animator.SetBool("isMoving", isMoving);
                }

                if (HasAnimatorParameter(animator, "moveSpeed", AnimatorControllerParameterType.Float))
                {
                    animator.SetFloat("moveSpeed", Mathf.Max(0f, normalizedSpeed));
                }
            }
        }

        private static bool HasAnimatorParameter(Animator animator, string paramName, AnimatorControllerParameterType type)
        {
            if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(paramName) || animator.parameters == null)
            {
                return false;
            }

            var hash = Animator.StringToHash(paramName);
            var parameters = animator.parameters;
            for (var i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].nameHash == hash && parameters[i].type == type)
                {
                    return true;
                }
            }

            return false;
        }

        private static void SetLayerRecursively(Transform node, int layer)
        {
            if (node == null)
            {
                return;
            }

            node.gameObject.layer = layer;
            for (var i = 0; i < node.childCount; i++)
            {
                SetLayerRecursively(node.GetChild(i), layer);
            }
        }

        private void RemoveMovePreview(string unitId)
        {
            if (string.IsNullOrEmpty(unitId))
            {
                return;
            }

            if (!_movePreviewByUnitId.TryGetValue(unitId, out var preview) || preview == null)
            {
                _movePreviewByUnitId.Remove(unitId);
                return;
            }

            Destroy(preview);
            _movePreviewByUnitId.Remove(unitId);
        }

        private void ClearAllMovePreviews()
        {
            if (_movePreviewByUnitId.Count == 0)
            {
                return;
            }

            foreach (var pair in _movePreviewByUnitId)
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value);
                }
            }

            _movePreviewByUnitId.Clear();
        }
    }
}
