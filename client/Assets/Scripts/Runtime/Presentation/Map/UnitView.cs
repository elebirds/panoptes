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
        public enum UnitAnimationCommand
        {
            Idle = 0,
            Move = 1,
            Attack = 2
        }

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
        [SerializeField] private bool enableBaseVehicleRenderQualityOverride = true;
        [SerializeField] private bool baseVehicleCastShadows = true;
        [SerializeField] private bool baseVehicleReceiveShadows = true;
        [SerializeField] private string[] baseVehicleUnitTypeAliases = { "settler", "pioneer", "expander", "engineer" };
        
        [Header("Animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private SquadUnitVisualController squadVisualController;
        [SerializeField] private string movingBoolParam = "isMoving";
        [SerializeField] private string speedFloatParam = "moveSpeed";
        [SerializeField] private string attackTriggerParam = "attack";
        [SerializeField] private string idleStateName = "Idle";
        [SerializeField] private bool forceIdleAnimation = true;
        [SerializeField] private bool forceIdleAnimationOnBind = true;
        [SerializeField] private float rotateLerpSpeed = 18f;

        [Header("Movement")]
        [SerializeField] private float moveArcHeight = 0.08f;

        [Header("Selection Beam")]
        [SerializeField] private bool useSelectionBeam = true;
        [SerializeField] private bool autoCreateSelectionBeam = true;
        [SerializeField] private Transform selectionBeamRoot;
        [SerializeField] private float selectionBeamHeight = 1.5f;
        [SerializeField] private float selectionBeamRadius = 0.22f;
        [SerializeField] private Color selectionBeamColor = new Color(0.45f, 0.95f, 0.55f, 0.35f);

        public string UnitId { get; private set; } = string.Empty;
        public string Faction { get; private set; } = string.Empty;
        public string UnitType { get; private set; } = string.Empty;
        public int HitPoints { get; private set; }
        public int MaxHitPoints { get; private set; }
        public Vector2Int GridPos { get; private set; }
        public Transform VisualRoot => visualRoot != null ? visualRoot : transform;
        private int _movingBoolHash;
        private int _speedFloatHash;
        private int _attackTriggerHash;
        private int _idleStateHash;
        private Renderer _selectionBeamRenderer;
        private Material _selectionBeamMaterial;
        private MaterialPropertyBlock _selectionBeamBlock;

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
            _attackTriggerHash = string.IsNullOrWhiteSpace(attackTriggerParam) ? 0 : Animator.StringToHash(attackTriggerParam);
            _idleStateHash = string.IsNullOrWhiteSpace(idleStateName) ? 0 : Animator.StringToHash(idleStateName);
            EnsureSelectionBeam();

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

            if (forceIdleAnimationOnBind)
            {
                SetForceIdleAnimation(true);
            }
            else if (forceIdleAnimation)
            {
                PlayIdleAnimation();
            }

            ApplyRenderBudgetForCurrentUnitType();
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

            if (_selectionBeamRenderer != null)
            {
                _selectionBeamRenderer.gameObject.SetActive(selected);
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
            if (forceIdleAnimation)
            {
                PlayIdleAnimation();
            }
            else
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

        public void SetForceIdleAnimation(bool enabled)
        {
            forceIdleAnimation = enabled;
            if (forceIdleAnimation)
            {
                PlayIdleAnimation();
            }
        }

        public void ApplyAnimationCommand(UnitAnimationCommand command, float normalizedSpeed = 1f, Vector3 worldMoveDirection = default)
        {
            switch (command)
            {
                case UnitAnimationCommand.Attack:
                    PlayAttackAnimation();
                    break;
                case UnitAnimationCommand.Move:
                    SetMovingVisual(true, normalizedSpeed, worldMoveDirection);
                    break;
                default:
                    PlayIdleAnimation();
                    break;
            }
        }

        public void PlayIdleAnimation()
        {
            if (animator != null)
            {
                if (_movingBoolHash != 0)
                {
                    animator.SetBool(_movingBoolHash, false);
                }

                if (_speedFloatHash != 0)
                {
                    animator.SetFloat(_speedFloatHash, 0f);
                }

                if (_idleStateHash != 0 && animator.runtimeAnimatorController != null && animator.HasState(0, _idleStateHash))
                {
                    var state = animator.GetCurrentAnimatorStateInfo(0);
                    if (state.shortNameHash != _idleStateHash && state.fullPathHash != _idleStateHash)
                    {
                        animator.CrossFade(_idleStateHash, 0.05f, 0);
                    }
                }
                else if (animator.runtimeAnimatorController != null)
                {
                    animator.Rebind();
                    animator.Update(0f);
                }
            }

            if (squadVisualController != null)
            {
                squadVisualController.ForceIdlePose();
            }
        }

        public bool PlayAttackAnimation()
        {
            if (forceIdleAnimation)
            {
                return false;
            }

            var played = false;
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                if (_attackTriggerHash != 0 && HasAnimatorParameter(animator, _attackTriggerHash, AnimatorControllerParameterType.Trigger))
                {
                    animator.SetTrigger(_attackTriggerHash);
                    played = true;
                }
            }

            if (squadVisualController != null)
            {
                played = squadVisualController.PlayAttackAnimation() || played;
            }

            return played;
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

        private void ApplyRenderBudgetForCurrentUnitType()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return;
            }

            var isBaseVehicle = enableBaseVehicleRenderQualityOverride && IsBaseVehicleUnitType(UnitType);
            var shouldDisableCast = disableCastShadows && !isBaseVehicle;
            var shouldDisableReceive = disableReceiveShadows && !isBaseVehicle;

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (shouldDisableCast)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
                else if (isBaseVehicle && baseVehicleCastShadows)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                }

                if (shouldDisableReceive)
                {
                    renderer.receiveShadows = false;
                }
                else if (isBaseVehicle && baseVehicleReceiveShadows)
                {
                    renderer.receiveShadows = true;
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

        private static bool HasAnimatorParameter(Animator targetAnimator, int hash, AnimatorControllerParameterType type)
        {
            if (targetAnimator == null || targetAnimator.runtimeAnimatorController == null || targetAnimator.parameters == null)
            {
                return false;
            }

            var parameters = targetAnimator.parameters;
            for (var i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].nameHash == hash && parameters[i].type == type)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsBaseVehicleUnitType(string unitType)
        {
            if (string.IsNullOrWhiteSpace(unitType) || baseVehicleUnitTypeAliases == null || baseVehicleUnitTypeAliases.Length == 0)
            {
                return false;
            }

            var normalized = (unitType ?? string.Empty).Trim().ToLowerInvariant();
            for (var i = 0; i < baseVehicleUnitTypeAliases.Length; i++)
            {
                var alias = (baseVehicleUnitTypeAliases[i] ?? string.Empty).Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(alias))
                {
                    continue;
                }

                if (string.Equals(normalized, alias, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void EnsureSelectionBeam()
        {
            if (!useSelectionBeam)
            {
                return;
            }

            if (selectionBeamRoot == null && autoCreateSelectionBeam)
            {
                var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                beam.name = "SelectedBeam";
                beam.transform.SetParent(transform, false);
                beam.transform.localPosition = new Vector3(0f, selectionBeamHeight * 0.5f, 0f);
                beam.transform.localScale = new Vector3(selectionBeamRadius, selectionBeamHeight * 0.5f, selectionBeamRadius);

                var collider = beam.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }

                selectionBeamRoot = beam.transform;
                _selectionBeamRenderer = beam.GetComponent<Renderer>();
            }
            else if (selectionBeamRoot != null)
            {
                _selectionBeamRenderer = selectionBeamRoot.GetComponentInChildren<Renderer>(true);
            }

            if (_selectionBeamRenderer == null)
            {
                return;
            }

            _selectionBeamMaterial = CreateSelectionBeamMaterial();
            if (_selectionBeamMaterial != null)
            {
                _selectionBeamRenderer.sharedMaterial = _selectionBeamMaterial;
            }

            if (_selectionBeamBlock == null)
            {
                _selectionBeamBlock = new MaterialPropertyBlock();
            }
            _selectionBeamRenderer.GetPropertyBlock(_selectionBeamBlock);
            _selectionBeamBlock.SetColor("_BaseColor", selectionBeamColor);
            _selectionBeamBlock.SetColor("_Color", selectionBeamColor);
            _selectionBeamRenderer.SetPropertyBlock(_selectionBeamBlock);
            _selectionBeamRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _selectionBeamRenderer.receiveShadows = false;
            _selectionBeamRenderer.gameObject.SetActive(false);
        }

        private Material CreateSelectionBeamMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader)
            {
                name = "UnitSelectionBeamMat_Runtime",
                hideFlags = HideFlags.DontSave
            };

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        private void OnDestroy()
        {
            if (_selectionBeamMaterial != null)
            {
                Destroy(_selectionBeamMaterial);
                _selectionBeamMaterial = null;
            }
        }
    }
}
