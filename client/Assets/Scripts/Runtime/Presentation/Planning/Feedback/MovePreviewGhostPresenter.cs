/*************************************************
 * Project: Panoptes
 * File: MovePreviewGhostPresenter.cs
 * Author: Panoptes Team
 * Date: 2026-05-01
 * Description: Runtime move preview ghost presenter for map planning feedback.
 *************************************************/

using System.Collections;
using System.Collections.Generic;
using Panoptes.Presentation.Map;
using Panoptes.Presentation.Map.InputAdapter;
using UnityEngine;
using UnityEngine.Rendering;

namespace Panoptes.Presentation.Planning.Feedback
{
    /// <summary>
    /// Owns runtime ghost objects used to preview server-backed move intents.
    /// </summary>
    public sealed class MovePreviewGhostPresenter
    {
        public struct Settings
        {
            public bool Enable;
            public Color GhostColor;
            public float TravelDuration;
            public float ArcHeight;
            public float TargetYOffset;
            public bool UseLightweightProxy;
            public bool AlwaysMatchUnitVisual;
            public Vector3 ProxyScale;
            public float ProxyYOffset;
            public bool ProxyCastShadow;
            public float TintStrength;
            public bool KeepTextureColor;
        }

        private readonly Dictionary<string, GameObject> _previewsByUnitId = new();
        private Material _proxyMaterial;
        private MapRenderer _mapRenderer;

        public void SetMapRenderer(MapRenderer mapRenderer)
        {
            _mapRenderer = mapRenderer;
        }

        public void CreateOrUpdate(string unitId, string targetNodeId, Settings settings, MonoBehaviour coroutineHost)
        {
            if (!settings.Enable || string.IsNullOrEmpty(unitId) || string.IsNullOrEmpty(targetNodeId))
            {
                return;
            }

            var map = _mapRenderer;
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

            Remove(unitId);

            var ghost = CreateMovePreviewObject(sourceUnit, settings);
            if (ghost == null)
            {
                return;
            }

            ghost.name = $"MoveGhost_{unitId}";
            ghost.transform.SetParent(map.transform, true);
            ghost.AddComponent<MoveGhostTag>();

            var useProxy = ShouldUseLightweightProxy(sourceUnit, settings);
            if (useProxy)
            {
                var ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
                if (ignoreRaycastLayer >= 0)
                {
                    SetLayerRecursively(ghost.transform, ignoreRaycastLayer);
                }

                ApplyGhostVisual(ghost, settings);
            }
            else
            {
                DisableBehavioursAndColliders(ghost, keepAnimators: true);
                ApplyGhostVisual(ghost, settings);
                SetGhostMoveState(ghost, isMoving: true, normalizedSpeed: 1f);
            }

            _previewsByUnitId[unitId] = ghost;

            var destination = targetNode.UnitAnchor != null
                ? targetNode.UnitAnchor.position
                : targetNode.transform.position + Vector3.up * 0.2f;
            destination.y += settings.TargetYOffset;

            if (coroutineHost != null && coroutineHost.isActiveAndEnabled)
            {
                coroutineHost.StartCoroutine(AnimateMovePreview(ghost, destination, settings));
                return;
            }

            ghost.transform.position = destination;
            SetGhostMoveState(ghost, isMoving: false, normalizedSpeed: 0f);
        }

        public void Remove(string unitId)
        {
            if (string.IsNullOrEmpty(unitId))
            {
                return;
            }

            if (!_previewsByUnitId.TryGetValue(unitId, out var preview) || preview == null)
            {
                _previewsByUnitId.Remove(unitId);
                return;
            }

            DestroyObject(preview);
            _previewsByUnitId.Remove(unitId);
        }

        public void ClearAll()
        {
            if (_previewsByUnitId.Count == 0)
            {
                return;
            }

            foreach (var pair in _previewsByUnitId)
            {
                if (pair.Value != null)
                {
                    DestroyObject(pair.Value);
                }
            }

            _previewsByUnitId.Clear();
        }

        public void DisposeMaterial()
        {
            if (_proxyMaterial == null)
            {
                return;
            }

            DestroyObject(_proxyMaterial);
            _proxyMaterial = null;
        }

        private static bool ShouldUseLightweightProxy(UnitView sourceUnit, Settings settings)
        {
            if (!settings.UseLightweightProxy)
            {
                return false;
            }

            if (settings.AlwaysMatchUnitVisual)
            {
                return false;
            }

            return sourceUnit == null;
        }

        private GameObject CreateMovePreviewObject(UnitView sourceUnit, Settings settings)
        {
            if (sourceUnit == null)
            {
                return null;
            }

            if (!ShouldUseLightweightProxy(sourceUnit, settings))
            {
                var sourceVisualRoot = sourceUnit.VisualRoot;
                var sourceObject = sourceVisualRoot != null ? sourceVisualRoot.gameObject : sourceUnit.gameObject;
                var clone = Object.Instantiate(sourceObject);
                clone.transform.position = sourceObject.transform.position;
                clone.transform.rotation = sourceObject.transform.rotation;
                return clone;
            }

            var proxy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            proxy.transform.position = sourceUnit.transform.position + Vector3.up * settings.ProxyYOffset;
            proxy.transform.rotation = sourceUnit.transform.rotation;
            proxy.transform.localScale = settings.ProxyScale;

            var collider = proxy.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyObject(collider);
            }

            var renderer = proxy.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = GetOrCreateProxyMaterial();
                renderer.shadowCastingMode = settings.ProxyCastShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderer.receiveShadows = settings.ProxyCastShadow;
            }

            return proxy;
        }

        private Material GetOrCreateProxyMaterial()
        {
            if (_proxyMaterial != null)
            {
                return _proxyMaterial;
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

            _proxyMaterial = new Material(shader)
            {
                name = "MovePreviewProxyMat_Runtime",
                hideFlags = HideFlags.DontSave
            };

            if (_proxyMaterial.HasProperty("_Surface"))
            {
                _proxyMaterial.SetFloat("_Surface", 1f);
            }

            if (_proxyMaterial.HasProperty("_Blend"))
            {
                _proxyMaterial.SetFloat("_Blend", 0f);
            }

            if (_proxyMaterial.HasProperty("_SrcBlend"))
            {
                _proxyMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            }

            if (_proxyMaterial.HasProperty("_DstBlend"))
            {
                _proxyMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            }

            if (_proxyMaterial.HasProperty("_ZWrite"))
            {
                _proxyMaterial.SetFloat("_ZWrite", 0f);
            }

            _proxyMaterial.renderQueue = (int)RenderQueue.Transparent;
            return _proxyMaterial;
        }

        private static IEnumerator AnimateMovePreview(GameObject ghost, Vector3 destination, Settings settings)
        {
            if (ghost == null)
            {
                yield break;
            }

            var start = ghost.transform.position;
            var duration = Mathf.Max(0.01f, settings.TravelDuration);
            var elapsed = 0f;

            while (elapsed < duration && ghost != null)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var pos = Vector3.Lerp(start, destination, t);
                if (settings.ArcHeight > 0.0001f)
                {
                    pos.y += Mathf.Sin(t * Mathf.PI) * settings.ArcHeight;
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

        private static void ApplyGhostVisual(GameObject ghostRoot, Settings settings)
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
                    var ghostColor = settings.KeepTextureColor
                        ? new Color(1f, 1f, 1f, settings.GhostColor.a)
                        : Color.Lerp(Color.white, settings.GhostColor, settings.TintStrength);
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
            if (animator == null ||
                animator.runtimeAnimatorController == null ||
                string.IsNullOrWhiteSpace(paramName) ||
                animator.parameters == null)
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

        private static void DestroyObject(Object obj)
        {
            if (obj == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(obj);
            }
            else
            {
                Object.DestroyImmediate(obj);
            }
        }
    }
}
