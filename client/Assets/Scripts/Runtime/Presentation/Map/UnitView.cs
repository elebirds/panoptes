/*************************************************
 * Project: Panoptes
 * File: UnitView.cs
 * Author: Panoptes Team
 * Date: 2026-04-06
 * Description: Runtime unit visual + movement.
 *************************************************/

using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Domain;

namespace Panoptes.Presentation.Map
{
    public sealed class UnitView : MonoBehaviour
    {
        [Header("Visual")]
        [SerializeField] private Renderer[] tintRenderers;
        [SerializeField] private GameObject selectedRing;
        [Range(0f, 1f)] [SerializeField] private float factionTintStrength = 0.35f;
        [SerializeField] private bool autoCollectTintRenderers = true;
        [SerializeField] private bool tintKeyRenderersOnly = true;
        [SerializeField] private int autoTintRendererLimit = 8;
        [SerializeField] private string[] tintRendererNameKeywords =
        {
            "body",
            "head",
            "weapon",
            "shield",
            "helmet",
            "cape",
            "cloak"
        };

        [Header("Render Budget")]
        [SerializeField] private bool disableCastShadows = true;
        [SerializeField] private bool disableReceiveShadows = true;
        [SerializeField] private bool disableSkinnedUpdateWhenOffscreen = true;
        
        [Header("Animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private SquadUnitVisualController squadVisualController;
        [SerializeField] private string movingBoolParam = "isMoving";
        [SerializeField] private string speedFloatParam = "moveSpeed";
        [SerializeField] private float rotateLerpSpeed = 18f;

        [Header("Movement")]
        [SerializeField] private float moveArcHeight = 0.08f;

        public string UnitId { get; private set; } = string.Empty;
        public string Faction { get; private set; } = string.Empty;
        public string UnitType { get; private set; } = string.Empty;
        public int HitPoints { get; private set; }
        public int MaxHitPoints { get; private set; }
        public Vector2Int GridPos { get; private set; }
        public Transform VisualRoot => visualRoot != null ? visualRoot : transform;
        private int _movingBoolHash;
        private int _speedFloatHash;

        private void Awake()
        {
            if ((tintRenderers == null || tintRenderers.Length == 0) && autoCollectTintRenderers)
            {
                tintRenderers = CollectAutoTintRenderers();
            }
            
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }

            if (visualRoot == null)
            {
                visualRoot = transform;
            }

            if (squadVisualController == null)
            {
                squadVisualController = GetComponentInChildren<SquadUnitVisualController>(true);
            }

            if (squadVisualController != null)
            {
                squadVisualController.RefreshMembers();
            }

            ApplyRenderBudget();

            _movingBoolHash = string.IsNullOrWhiteSpace(movingBoolParam) ? 0 : Animator.StringToHash(movingBoolParam);
            _speedFloatHash = string.IsNullOrWhiteSpace(speedFloatParam) ? 0 : Animator.StringToHash(speedFloatParam);

            if (GetComponent<Collider>() == null)
            {
                var collider = gameObject.AddComponent<SphereCollider>();
                collider.radius = 0.35f;
                collider.center = new Vector3(0f, 0.35f, 0f);
            }
        }

        public void Bind(UnitDto unit, Vector3 worldPosition)
        {
            if (unit == null)
            {
                return;
            }

            UnitId = unit.Id ?? string.Empty;
            Faction = unit.Owner ?? string.Empty;
            UnitType = unit.Type ?? string.Empty;
            HitPoints = unit.Hp;
            MaxHitPoints = unit.MaxHp;
            GridPos = new Vector2Int(unit.X, unit.Y);
            transform.position = worldPosition;
            name = string.IsNullOrEmpty(UnitId) ? "Unit" : $"Unit_{UnitId}";

            ApplyFactionTint();
            if (squadVisualController != null)
            {
                squadVisualController.OnUnitBound(UnitId, UnitType, Faction);
            }
        }

        public void SetGridPosition(Vector2Int gridPos)
        {
            GridPos = gridPos;
        }

        public void SetSelected(bool selected)
        {
            if (selectedRing != null)
            {
                selectedRing.SetActive(selected);
            }
        }

        public IEnumerator AnimateMoveTo(Vector3 destination, float duration)
        {
            duration = Mathf.Max(0.01f, duration);

            var start = transform.position;
            var elapsed = 0f;
            var moveDir = destination - start;
            SetMovingVisual(true, 1f, moveDir);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var pos = Vector3.Lerp(start, destination, t);

                if (moveArcHeight > 0.0001f)
                {
                    pos.y += Mathf.Sin(t * Mathf.PI) * moveArcHeight;
                }

                transform.position = pos;
                SetMovingVisual(true, 1f, destination - transform.position);
                yield return null;
            }

            transform.position = destination;
            SetMovingVisual(false, 0f, Vector3.zero);
        }

        public void SetMovingVisual(bool isMoving, float normalizedSpeed, Vector3 worldMoveDirection)
        {
            if (animator != null)
            {
                if (_movingBoolHash != 0)
                {
                    animator.SetBool(_movingBoolHash, isMoving);
                }

                if (_speedFloatHash != 0)
                {
                    animator.SetFloat(_speedFloatHash, Mathf.Max(0f, normalizedSpeed));
                }
            }

            if (squadVisualController != null)
            {
                squadVisualController.ApplyMoveState(isMoving, normalizedSpeed);
            }

            if (visualRoot == null)
            {
                return;
            }

            var flatDir = worldMoveDirection;
            flatDir.y = 0f;
            if (flatDir.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var targetRot = Quaternion.LookRotation(flatDir.normalized, Vector3.up);
            visualRoot.rotation = Quaternion.Slerp(visualRoot.rotation, targetRot, Time.deltaTime * Mathf.Max(0f, rotateLerpSpeed));
        }

        private void ApplyFactionTint()
        {
            if (tintRenderers == null || tintRenderers.Length == 0)
            {
                return;
            }

            var tint = ResolveFactionColor(Faction);
            var finalColor = Color.Lerp(Color.white, tint, factionTintStrength);

            for (int r = 0; r < tintRenderers.Length; r++)
            {
                var renderer = tintRenderers[r];
                if (renderer == null)
                {
                    continue;
                }

                var mats = renderer.sharedMaterials;
                if (mats == null)
                {
                    continue;
                }

                for (int i = 0; i < mats.Length; i++)
                {
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block, i);
                    block.SetColor("_BaseColor", finalColor);
                    block.SetColor("_Color", finalColor);
                    renderer.SetPropertyBlock(block, i);
                }
            }
        }

        private void ApplyRenderBudget()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return;
            }

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (disableCastShadows)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }

                if (disableReceiveShadows)
                {
                    renderer.receiveShadows = false;
                }

                if (!disableSkinnedUpdateWhenOffscreen)
                {
                    continue;
                }

                if (renderer is SkinnedMeshRenderer skinned)
                {
                    skinned.updateWhenOffscreen = false;
                }
            }
        }

        private Renderer[] CollectAutoTintRenderers()
        {
            var all = GetComponentsInChildren<Renderer>(true);
            if (all == null || all.Length == 0)
            {
                return Array.Empty<Renderer>();
            }

            if (!tintKeyRenderersOnly)
            {
                return all;
            }

            var selected = new List<Renderer>(Mathf.Clamp(autoTintRendererLimit, 1, 64));
            var visited = new HashSet<int>();
            var limit = Mathf.Max(1, autoTintRendererLimit);

            for (var i = 0; i < all.Length; i++)
            {
                var renderer = all[i];
                if (renderer == null)
                {
                    continue;
                }

                if (!visited.Add(renderer.GetInstanceID()))
                {
                    continue;
                }

                if (!IsKeyTintRenderer(renderer))
                {
                    continue;
                }

                selected.Add(renderer);
                if (selected.Count >= limit)
                {
                    break;
                }
            }

            if (selected.Count > 0)
            {
                return selected.ToArray();
            }

            // Fallback: keep at least one renderer tinted if no keyword matched.
            selected.Add(all[0]);
            return selected.ToArray();
        }

        private bool IsKeyTintRenderer(Renderer renderer)
        {
            if (renderer == null)
            {
                return false;
            }

            if (tintRendererNameKeywords == null || tintRendererNameKeywords.Length == 0)
            {
                return true;
            }

            var rendererName = renderer.name ?? string.Empty;
            var objectName = renderer.gameObject != null ? renderer.gameObject.name : string.Empty;
            for (var i = 0; i < tintRendererNameKeywords.Length; i++)
            {
                var key = tintRendererNameKeywords[i];
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (rendererName.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    objectName.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static Color ResolveFactionColor(string faction)
        {
            var myPlayerId = GameStateCache.Instance != null ? GameStateCache.Instance.MyPlayerID : string.Empty;
            if (!string.IsNullOrEmpty(myPlayerId) && !string.IsNullOrEmpty(faction))
            {
                return faction == myPlayerId
                    ? new Color(0.25f, 0.78f, 1f, 1f)
                    : new Color(1f, 0.42f, 0.42f, 1f);
            }

            if (string.IsNullOrEmpty(faction))
            {
                return Color.white;
            }

            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < faction.Length; i++)
                {
                    hash ^= faction[i];
                    hash *= 16777619u;
                }

                var hue = (hash % 360u) / 360f;
                return Color.HSVToRGB(hue, 0.45f, 0.95f);
            }
        }
    }
}
