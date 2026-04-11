/*************************************************
 * Project: Panoptes
 * File: SquadUnitVisualController.cs
 * Author: Panoptes Team
 * Date: 2026-04-11
 * Description: Squad-level visual driver for multi-member units.
 *************************************************/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Panoptes.Presentation.Map
{
    public sealed class SquadUnitVisualController : MonoBehaviour
    {
        [Serializable]
        private sealed class MemberBinding
        {
            public Transform root;
            [NonSerialized] public Animator animator;
            [NonSerialized] public bool hasMoveBoolParam;
            [NonSerialized] public bool hasMoveSpeedParam;
            [NonSerialized] public int moveBoolHash;
            [NonSerialized] public int moveSpeedHash;
            [NonSerialized] public bool isMoveStatePlaying;
            [NonSerialized] public int idleStateHash;
            [NonSerialized] public int moveStateHash;
            [NonSerialized] public bool hasIdleState;
            [NonSerialized] public bool hasMoveState;
        }

        [Header("Members")]
        [SerializeField] private Transform formationRoot;
        [SerializeField] private bool autoCollectMembers = true;
        [SerializeField] private MemberBinding[] members = Array.Empty<MemberBinding>();

        [Header("Animation")]
        [SerializeField] private string movingBoolParam = "isMoving";
        [SerializeField] private string speedFloatParam = "moveSpeed";
        [SerializeField] private bool useStateFallbackWhenNoParams = false;
        [SerializeField] private string idleStateName = "Idle";
        [SerializeField] private string moveStateName = "Run";
        [SerializeField] private float stateCrossFadeSeconds = 0.08f;

        [Header("Appearance")]
        [SerializeField] private bool applyAppearanceOnBind = false;
        [SerializeField] private int bodyVariantIndex = -1;
        [SerializeField] private int headVariantIndex = -1;
        [SerializeField] private int weaponVariantIndex = -1;
        [SerializeField] private bool randomizeVariantPerMember = false;
        [SerializeField] private string[] bodyNamePrefixes = { "body_" };
        [SerializeField] private string[] headNamePrefixes = { "head_" };
        [SerializeField] private string[] weaponNamePrefixes = { "weapon_", "w_", "sword_", "bow_", "crossbow_", "spear_", "halberd_", "staff_", "hammer_", "shield_" };

        private readonly List<GameObject> _variantBuffer = new();

        public bool HasAnimators
        {
            get
            {
                if (members == null)
                {
                    return false;
                }

                for (var i = 0; i < members.Length; i++)
                {
                    if (members[i] != null && members[i].animator != null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private void Awake()
        {
            RefreshMembers();
        }

        public void RefreshMembers(bool forceAutoCollect = false)
        {
            if (formationRoot == null)
            {
                formationRoot = transform;
            }

            if (forceAutoCollect || autoCollectMembers || members == null || members.Length == 0)
            {
                CollectMembersFromFormationRoot();
            }

            BindAnimators();
        }

        public void ApplyMoveState(bool isMoving, float normalizedSpeed)
        {
            if (members == null || members.Length == 0)
            {
                return;
            }

            for (var i = 0; i < members.Length; i++)
            {
                var member = members[i];
                if (member == null || member.animator == null)
                {
                    continue;
                }

                if (member.hasMoveBoolParam)
                {
                    member.animator.SetBool(member.moveBoolHash, isMoving);
                }

                if (member.hasMoveSpeedParam)
                {
                    member.animator.SetFloat(member.moveSpeedHash, Mathf.Max(0f, normalizedSpeed));
                }

                if (!useStateFallbackWhenNoParams || member.hasMoveBoolParam || member.hasMoveSpeedParam)
                {
                    continue;
                }

                if (isMoving && member.hasMoveState && !member.isMoveStatePlaying)
                {
                    member.animator.CrossFade(member.moveStateHash, Mathf.Max(0f, stateCrossFadeSeconds), 0);
                    member.isMoveStatePlaying = true;
                }
                else if (!isMoving && member.hasIdleState && member.isMoveStatePlaying)
                {
                    member.animator.CrossFade(member.idleStateHash, Mathf.Max(0f, stateCrossFadeSeconds), 0);
                    member.isMoveStatePlaying = false;
                }
            }
        }

        public void OnUnitBound(string unitId, string unitType, string faction)
        {
            if (!applyAppearanceOnBind)
            {
                return;
            }

            var seed = string.Concat(unitId ?? string.Empty, "|", unitType ?? string.Empty, "|", faction ?? string.Empty);
            ApplyAppearance(seed);
        }

        public void ApplyAppearance(string seed)
        {
            if (members == null || members.Length == 0)
            {
                return;
            }

            if (bodyVariantIndex < 0 && headVariantIndex < 0 && weaponVariantIndex < 0)
            {
                return;
            }

            var baseSeed = StableHash(seed ?? string.Empty);

            for (var i = 0; i < members.Length; i++)
            {
                var member = members[i];
                if (member == null || member.root == null)
                {
                    continue;
                }

                var memberSeed = unchecked((int)(baseSeed + (uint)(i * 2654435761)));

                ApplyVariantSet(member.root, bodyNamePrefixes, ResolveVariantIndex(bodyVariantIndex, memberSeed ^ 0x13579BDF));
                ApplyVariantSet(member.root, headNamePrefixes, ResolveVariantIndex(headVariantIndex, memberSeed ^ 0x2468ACE0));
                ApplyVariantSet(member.root, weaponNamePrefixes, ResolveVariantIndex(weaponVariantIndex, memberSeed ^ 0x5A5A5A5A));
            }
        }

        private int ResolveVariantIndex(int configuredIndex, int memberSeed)
        {
            if (configuredIndex < 0)
            {
                return -1;
            }

            if (!randomizeVariantPerMember)
            {
                return configuredIndex;
            }

            return Mathf.Abs(configuredIndex + memberSeed);
        }

        private void CollectMembersFromFormationRoot()
        {
            if (formationRoot == null)
            {
                members = Array.Empty<MemberBinding>();
                return;
            }

            var collected = new List<MemberBinding>(8);
            for (var i = 0; i < formationRoot.childCount; i++)
            {
                var child = formationRoot.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                var animator = child.GetComponentInChildren<Animator>(true);
                if (animator == null)
                {
                    continue;
                }

                collected.Add(new MemberBinding
                {
                    root = child,
                    animator = animator
                });
            }

            if (collected.Count == 0)
            {
                var animators = formationRoot.GetComponentsInChildren<Animator>(true);
                for (var i = 0; i < animators.Length; i++)
                {
                    var animator = animators[i];
                    if (animator == null)
                    {
                        continue;
                    }

                    collected.Add(new MemberBinding
                    {
                        root = animator.transform,
                        animator = animator
                    });
                }
            }

            members = collected.ToArray();
        }

        private void BindAnimators()
        {
            if (members == null || members.Length == 0)
            {
                return;
            }

            var moveBoolHash = string.IsNullOrWhiteSpace(movingBoolParam) ? 0 : Animator.StringToHash(movingBoolParam);
            var speedHash = string.IsNullOrWhiteSpace(speedFloatParam) ? 0 : Animator.StringToHash(speedFloatParam);
            var idleHash = string.IsNullOrWhiteSpace(idleStateName) ? 0 : Animator.StringToHash(idleStateName);
            var moveHash = string.IsNullOrWhiteSpace(moveStateName) ? 0 : Animator.StringToHash(moveStateName);

            for (var i = 0; i < members.Length; i++)
            {
                var member = members[i];
                if (member == null)
                {
                    continue;
                }

                if (member.root == null)
                {
                    continue;
                }

                member.animator = member.animator != null
                    ? member.animator
                    : member.root.GetComponentInChildren<Animator>(true);

                if (member.animator == null)
                {
                    continue;
                }

                member.moveBoolHash = moveBoolHash;
                member.moveSpeedHash = speedHash;
                member.hasMoveBoolParam = moveBoolHash != 0
                                          && HasAnimatorParameter(member.animator, moveBoolHash, AnimatorControllerParameterType.Bool);
                member.hasMoveSpeedParam = speedHash != 0
                                           && HasAnimatorParameter(member.animator, speedHash, AnimatorControllerParameterType.Float);
                member.idleStateHash = idleHash;
                member.moveStateHash = moveHash;
                member.hasIdleState = idleHash != 0 && member.animator.HasState(0, idleHash);
                member.hasMoveState = moveHash != 0 && member.animator.HasState(0, moveHash);
                member.isMoveStatePlaying = false;
            }
        }

        private void ApplyVariantSet(Transform memberRoot, string[] prefixes, int variantIndex)
        {
            if (memberRoot == null || prefixes == null || prefixes.Length == 0 || variantIndex < 0)
            {
                return;
            }

            _variantBuffer.Clear();
            var visited = new HashSet<int>();
            var renderers = memberRoot.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                var go = renderer.gameObject;
                if (go == null || !NameStartsWithAny(go.name, prefixes))
                {
                    continue;
                }

                if (!visited.Add(go.GetInstanceID()))
                {
                    continue;
                }

                _variantBuffer.Add(go);
            }

            if (_variantBuffer.Count == 0)
            {
                return;
            }

            _variantBuffer.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            var resolvedIndex = Mathf.Abs(variantIndex) % _variantBuffer.Count;

            for (var i = 0; i < _variantBuffer.Count; i++)
            {
                var go = _variantBuffer[i];
                if (go != null)
                {
                    go.SetActive(i == resolvedIndex);
                }
            }
        }

        private static bool NameStartsWithAny(string name, string[] prefixes)
        {
            if (string.IsNullOrEmpty(name) || prefixes == null || prefixes.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < prefixes.Length; i++)
            {
                var prefix = prefixes[i];
                if (string.IsNullOrWhiteSpace(prefix))
                {
                    continue;
                }

                if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasAnimatorParameter(Animator animator, int hash, AnimatorControllerParameterType type)
        {
            if (animator == null || animator.parameters == null)
            {
                return false;
            }

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

        private static uint StableHash(string value)
        {
            unchecked
            {
                const uint offset = 2166136261u;
                const uint prime = 16777619u;
                var hash = offset;
                for (var i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= prime;
                }
                return hash;
            }
        }
    }
}
