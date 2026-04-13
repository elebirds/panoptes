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
using UnityEngine.Rendering;

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
            [NonSerialized] public float lastIdleNormalizedTime;
            [NonSerialized] public int idleStallFrames;
        }

        [Header("Members")]
        [SerializeField] private Transform formationRoot;
        [SerializeField] private bool autoCollectMembers = true;
        [SerializeField] private MemberBinding[] members = Array.Empty<MemberBinding>();

        [Header("Animation")]
        [SerializeField] private string movingBoolParam = "isMoving";
        [SerializeField] private string speedFloatParam = "moveSpeed";
        [SerializeField] private string attackTriggerParam = "attack";
        [SerializeField] private string attackStateName = "Attack";
        [SerializeField] private bool useStateFallbackWhenNoParams = false;
        [SerializeField] private string idleStateName = "Idle";
        [SerializeField] private string moveStateName = "Run";
        [SerializeField] private float stateCrossFadeSeconds = 0.08f;
        [SerializeField] private bool manualIdleSamplingFallback = true;
        [SerializeField] private int idleSamplingStallFrameThreshold = 10;
        [SerializeField] private float idleSamplingPlaybackSpeed = 1f;

        [Header("Appearance")]
        [SerializeField] private bool applyAppearanceOnBind = false;
        [SerializeField] private int bodyVariantIndex = -1;
        [SerializeField] private int headVariantIndex = -1;
        [SerializeField] private int weaponVariantIndex = -1;
        [SerializeField] private bool randomizeVariantPerMember = false;
        [SerializeField] private string[] bodyNamePrefixes = { "body_" };
        [SerializeField] private string[] headNamePrefixes = { "head_" };
        [SerializeField] private string[] weaponNamePrefixes = { "weapon_", "w_", "sword_", "bow_", "crossbow_", "spear_", "halberd_", "staff_", "hammer_", "shield_" };

        [Header("Unit Role Variant")]
        [SerializeField] private bool applyRoleVariantOnBind = true;
        [SerializeField] private RuntimeAnimatorController unarmedAnimatorController;
        [SerializeField] private RuntimeAnimatorController swordAnimatorController;
        [SerializeField] private RuntimeAnimatorController bowAnimatorController;
        [SerializeField] private string unarmedIdleState = "infantry_01_idle";
        [SerializeField] private string unarmedMoveState = "infantry_03_run";
        [SerializeField] private string swordIdleState = "twohanded_01_idle";
        [SerializeField] private string swordMoveState = "twohanded_03_run";
        [SerializeField] private string bowIdleState = "archer_01_idle";
        [SerializeField] private string bowMoveState = "archer_03_run";
        [SerializeField] private string[] unarmedUnitTypeAliases = { "unarmed", "settler", "civilian", "fighter_basic", "fighter", "militia", "pioneer", "expander", "engineer" };
        [SerializeField] private string[] swordUnitTypeAliases = { "sword", "swordsman", "melee", "fighter_sword", "warrior", "infantry", "cavalry" };
        [SerializeField] private string[] bowUnitTypeAliases = { "bow", "archer", "ranged", "fighter_bow" };
        [SerializeField] private bool enforceSingleWeaponVisual = true;
        [SerializeField] private string[] weaponObjectPrefixes = { "weapon_", "w_", "sword_", "bow_", "crossbow_", "spear_", "halberd_", "staff_", "hammer_", "shield_", "l_sword_" };
        [SerializeField] private string[] swordWeaponNameKeywords = { "sword", "sabre", "katana", "dagger", "axe", "mace", "maul", "hammer", "club", "staff", "halberd", "spear", "shield" };
        [SerializeField] private string[] bowWeaponNameKeywords = { "bow", "crossbow", "recurve", "long_bow", "short_bow" };

        [Header("Render Budget")]
        [SerializeField] private bool applyRenderBudgetOnBind = false;
        [SerializeField] private int maxVisibleRenderersPerMember = 6;
        [SerializeField] private bool disableCastShadows = true;
        [SerializeField] private bool disableReceiveShadows = true;
        [SerializeField] private string[] rendererPriorityKeywords = { "body", "head", "weapon", "shield", "helmet", "cape", "cloak" };

        private readonly List<GameObject> _variantBuffer = new();
        private readonly List<GameObject> _weaponBuffer = new();

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

        private void Update()
        {
            TickIdleAnimationFallback();
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
                    if (isMoving)
                    {
                        member.idleStallFrames = 0;
                        member.lastIdleNormalizedTime = -1f;
                    }
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

                if (isMoving)
                {
                    member.idleStallFrames = 0;
                    member.lastIdleNormalizedTime = -1f;
                }
            }
        }

        public void ForceIdlePose()
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
                    member.animator.SetBool(member.moveBoolHash, false);
                }

                if (member.hasMoveSpeedParam)
                {
                    member.animator.SetFloat(member.moveSpeedHash, 0f);
                }

                if (member.hasIdleState)
                {
                    var state = member.animator.GetCurrentAnimatorStateInfo(0);
                    if (state.shortNameHash != member.idleStateHash && state.fullPathHash != member.idleStateHash)
                    {
                        member.animator.CrossFade(member.idleStateHash, Mathf.Max(0f, stateCrossFadeSeconds), 0);
                    }
                }
                else
                {
                    // Fallback: if idle state hash doesn't exist, reset to controller default state.
                    member.animator.Rebind();
                    member.animator.Update(0f);
                }

                member.isMoveStatePlaying = false;
            }
        }

        public bool PlayAttackAnimation()
        {
            if (members == null || members.Length == 0)
            {
                return false;
            }

            var played = false;
            var attackTriggerHash = string.IsNullOrWhiteSpace(attackTriggerParam) ? 0 : Animator.StringToHash(attackTriggerParam);
            var attackStateHash = string.IsNullOrWhiteSpace(attackStateName) ? 0 : Animator.StringToHash(attackStateName);

            for (var i = 0; i < members.Length; i++)
            {
                var member = members[i];
                if (member == null || member.animator == null || member.animator.runtimeAnimatorController == null)
                {
                    continue;
                }

                if (attackTriggerHash != 0 && HasAnimatorParameter(member.animator, attackTriggerHash, AnimatorControllerParameterType.Trigger))
                {
                    member.animator.SetTrigger(attackTriggerHash);
                    played = true;
                    continue;
                }

                if (attackStateHash != 0 && member.animator.HasState(0, attackStateHash))
                {
                    member.animator.CrossFade(attackStateHash, Mathf.Max(0f, stateCrossFadeSeconds), 0);
                    played = true;
                }
            }

            return played;
        }

        public void OnUnitBound(string unitId, string unitType, string faction)
        {
            var seed = string.Concat(unitId ?? string.Empty, "|", unitType ?? string.Empty, "|", faction ?? string.Empty);
            if (applyAppearanceOnBind)
            {
                ApplyAppearance(seed);
            }

            if (applyRoleVariantOnBind)
            {
                ApplyRoleVariant(ResolveRoleVariant(unitType));
            }

            if (applyRenderBudgetOnBind)
            {
                ApplyRenderBudget();
            }
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
            var seenAnimators = new HashSet<int>();
            for (var i = 0; i < formationRoot.childCount; i++)
            {
                var child = formationRoot.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                // A formation child is often just a container ("Root"), so collect all animators below it.
                var animators = child.GetComponentsInChildren<Animator>(true);
                if (animators == null || animators.Length == 0)
                {
                    continue;
                }

                for (var a = 0; a < animators.Length; a++)
                {
                    var animator = animators[a];
                    if (animator == null)
                    {
                        continue;
                    }

                    if (!seenAnimators.Add(animator.GetInstanceID()))
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

                    if (!seenAnimators.Add(animator.GetInstanceID()))
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

                member.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                member.animator.applyRootMotion = false;

                var hasController = member.animator.runtimeAnimatorController != null;
                if (!hasController)
                {
                    member.moveBoolHash = moveBoolHash;
                    member.moveSpeedHash = speedHash;
                    member.hasMoveBoolParam = false;
                    member.hasMoveSpeedParam = false;
                    member.idleStateHash = idleHash;
                    member.moveStateHash = moveHash;
                    member.hasIdleState = false;
                    member.hasMoveState = false;
                    member.isMoveStatePlaying = false;
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
                member.lastIdleNormalizedTime = -1f;
                member.idleStallFrames = 0;
            }
        }

        private void TickIdleAnimationFallback()
        {
            if (!manualIdleSamplingFallback || members == null || members.Length == 0)
            {
                return;
            }

            var threshold = Mathf.Max(1, idleSamplingStallFrameThreshold);
            var playbackSpeed = Mathf.Max(0.01f, idleSamplingPlaybackSpeed);

            for (var i = 0; i < members.Length; i++)
            {
                var member = members[i];
                if (member == null || member.animator == null || member.animator.runtimeAnimatorController == null || !member.hasIdleState)
                {
                    continue;
                }

                var state = member.animator.GetCurrentAnimatorStateInfo(0);
                if (state.shortNameHash != member.idleStateHash && state.fullPathHash != member.idleStateHash)
                {
                    member.idleStallFrames = 0;
                    member.lastIdleNormalizedTime = -1f;
                    continue;
                }

                var normalized = state.normalizedTime;
                if (member.lastIdleNormalizedTime >= 0f && Mathf.Abs(normalized - member.lastIdleNormalizedTime) < 0.00001f)
                {
                    member.idleStallFrames++;
                }
                else
                {
                    member.idleStallFrames = 0;
                }

                member.lastIdleNormalizedTime = normalized;
                if (member.idleStallFrames < threshold)
                {
                    continue;
                }

                // Fallback path for import/runtime edge cases where idle state time freezes.
                member.animator.Play(member.idleStateHash, 0, Mathf.Repeat(Time.unscaledTime * playbackSpeed, 1f));
                member.animator.Update(0f);
                member.idleStallFrames = 0;
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

            if (!ShouldGroupByVariantFamilies(prefixes))
            {
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

                return;
            }

            // For TT_RTS customizable meshes (e.g. Body_01a...Body_01e), choose one option
            // per body/head group instead of enabling only one mesh globally.
            var grouped = new Dictionary<string, List<GameObject>>(StringComparer.Ordinal);
            for (var i = 0; i < _variantBuffer.Count; i++)
            {
                var go = _variantBuffer[i];
                if (go == null)
                {
                    continue;
                }

                var groupKey = TryBuildVariantGroupKey(go.name, prefixes, out var key)
                    ? key
                    : NormalizeToken(go.name);

                if (!grouped.TryGetValue(groupKey, out var list))
                {
                    list = new List<GameObject>(8);
                    grouped[groupKey] = list;
                }

                list.Add(go);
            }

            foreach (var pair in grouped)
            {
                var list = pair.Value;
                if (list == null || list.Count == 0)
                {
                    continue;
                }

                list.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
                var resolvedIndex = Mathf.Abs(variantIndex) % list.Count;
                for (var i = 0; i < list.Count; i++)
                {
                    var go = list[i];
                    if (go != null)
                    {
                        go.SetActive(i == resolvedIndex);
                    }
                }
            }
        }

        private static bool ShouldGroupByVariantFamilies(string[] prefixes)
        {
            if (prefixes == null || prefixes.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < prefixes.Length; i++)
            {
                var prefix = NormalizeToken(prefixes[i]);
                if (prefix == "body_" || prefix == "head_")
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryBuildVariantGroupKey(string rawName, string[] prefixes, out string groupKey)
        {
            groupKey = string.Empty;
            if (string.IsNullOrWhiteSpace(rawName) || prefixes == null || prefixes.Length == 0)
            {
                return false;
            }

            var normalized = NormalizeToken(rawName);
            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            for (var i = 0; i < prefixes.Length; i++)
            {
                var prefix = NormalizeToken(prefixes[i]);
                if (string.IsNullOrEmpty(prefix) || !normalized.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var suffix = normalized.Substring(prefix.Length);
                if (suffix.Length < 2)
                {
                    groupKey = normalized;
                    return true;
                }

                var last = suffix[suffix.Length - 1];
                if (!char.IsLetter(last))
                {
                    groupKey = normalized;
                    return true;
                }

                var hasDigitBeforeLast = false;
                for (var s = 0; s < suffix.Length - 1; s++)
                {
                    if (char.IsDigit(suffix[s]))
                    {
                        hasDigitBeforeLast = true;
                        break;
                    }
                }

                if (!hasDigitBeforeLast)
                {
                    groupKey = normalized;
                    return true;
                }

                groupKey = prefix + suffix.Substring(0, suffix.Length - 1);
                return true;
            }

            return false;
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
            if (animator == null || animator.runtimeAnimatorController == null || animator.parameters == null)
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

        private enum UnitRoleVariant
        {
            Unarmed = 0,
            Sword = 1,
            Bow = 2
        }

        private UnitRoleVariant ResolveRoleVariant(string unitType)
        {
            if (MatchesAlias(unitType, bowUnitTypeAliases))
            {
                return UnitRoleVariant.Bow;
            }

            if (MatchesAlias(unitType, swordUnitTypeAliases))
            {
                return UnitRoleVariant.Sword;
            }

            if (MatchesAlias(unitType, unarmedUnitTypeAliases))
            {
                return UnitRoleVariant.Unarmed;
            }

            return UnitRoleVariant.Unarmed;
        }

        private static bool MatchesAlias(string unitType, string[] aliases)
        {
            if (aliases == null || aliases.Length == 0)
            {
                return false;
            }

            var normalized = NormalizeToken(unitType);
            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            for (var i = 0; i < aliases.Length; i++)
            {
                if (string.Equals(normalized, NormalizeToken(aliases[i]), StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyRoleVariant(UnitRoleVariant variant)
        {
            RuntimeAnimatorController controller;
            string idleState;
            string moveState;

            switch (variant)
            {
                case UnitRoleVariant.Bow:
                    controller = bowAnimatorController;
                    idleState = bowIdleState;
                    moveState = bowMoveState;
                    break;
                case UnitRoleVariant.Sword:
                    controller = swordAnimatorController;
                    idleState = swordIdleState;
                    moveState = swordMoveState;
                    break;
                default:
                    controller = unarmedAnimatorController;
                    idleState = unarmedIdleState;
                    moveState = unarmedMoveState;
                    break;
            }

            ApplyAnimatorController(controller, idleState, moveState);
            ApplyWeaponVisualByRole(variant);
        }

        private void ApplyAnimatorController(RuntimeAnimatorController controller, string idleState, string moveState)
        {
            var changedController = false;

            if (members != null)
            {
                for (var i = 0; i < members.Length; i++)
                {
                    var member = members[i];
                    if (member == null || member.animator == null)
                    {
                        continue;
                    }

                    if (controller != null && member.animator.runtimeAnimatorController != controller)
                    {
                        member.animator.runtimeAnimatorController = controller;
                        member.animator.Rebind();
                        member.animator.Update(0f);
                        changedController = true;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(idleState))
            {
                idleStateName = idleState;
            }

            if (!string.IsNullOrWhiteSpace(moveState))
            {
                moveStateName = moveState;
            }

            if (changedController)
            {
                BindAnimators();
            }
            else
            {
                // Ensure hashes/states are refreshed if only state names changed.
                BindAnimators();
            }
        }

        private void ApplyWeaponVisualByRole(UnitRoleVariant variant)
        {
            if (members == null || members.Length == 0)
            {
                return;
            }

            for (var i = 0; i < members.Length; i++)
            {
                var member = members[i];
                if (member == null || member.root == null)
                {
                    continue;
                }

                ApplyWeaponVisualForMember(member.root, variant);
            }
        }

        private void ApplyWeaponVisualForMember(Transform memberRoot, UnitRoleVariant variant)
        {
            _weaponBuffer.Clear();
            CollectWeaponObjects(memberRoot, _weaponBuffer);
            if (_weaponBuffer.Count == 0)
            {
                return;
            }

            GameObject selectedWeapon = null;
            if (variant == UnitRoleVariant.Sword)
            {
                selectedWeapon = SelectWeaponByKeywords(_weaponBuffer, swordWeaponNameKeywords, null);
                if (selectedWeapon == null)
                {
                    selectedWeapon = SelectWeaponByKeywords(_weaponBuffer, null, bowWeaponNameKeywords);
                }
            }
            else if (variant == UnitRoleVariant.Bow)
            {
                selectedWeapon = SelectWeaponByKeywords(_weaponBuffer, bowWeaponNameKeywords, null);
            }

            for (var i = 0; i < _weaponBuffer.Count; i++)
            {
                var go = _weaponBuffer[i];
                if (go == null)
                {
                    continue;
                }

                var shouldEnable = selectedWeapon != null && ReferenceEquals(go, selectedWeapon);
                if (!enforceSingleWeaponVisual)
                {
                    shouldEnable = variant == UnitRoleVariant.Unarmed
                        ? false
                        : ShouldEnableByRoleLoose(go.name, variant);
                }

                if (go.activeSelf != shouldEnable)
                {
                    go.SetActive(shouldEnable);
                }
            }
        }

        private static bool ShouldEnableByRoleLoose(string objectName, UnitRoleVariant variant)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return false;
            }

            var normalized = NormalizeToken(objectName);
            if (variant == UnitRoleVariant.Bow)
            {
                return normalized.Contains("bow") || normalized.Contains("crossbow");
            }

            if (variant == UnitRoleVariant.Sword)
            {
                return !normalized.Contains("bow") && !normalized.Contains("crossbow");
            }

            return false;
        }

        private void CollectWeaponObjects(Transform root, List<GameObject> result)
        {
            if (root == null || result == null)
            {
                return;
            }

            var visited = new HashSet<int>();
            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null || t == root)
                {
                    continue;
                }

                var go = t.gameObject;
                if (go == null || !visited.Add(go.GetInstanceID()))
                {
                    continue;
                }

                if (!NameStartsWithAny(go.name, weaponObjectPrefixes))
                {
                    continue;
                }

                result.Add(go);
            }
        }

        private static GameObject SelectWeaponByKeywords(List<GameObject> candidates, string[] includeKeywords, string[] excludeKeywords)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                var go = candidates[i];
                if (go == null)
                {
                    continue;
                }

                var normalized = NormalizeToken(go.name);
                if (HasAnyKeyword(normalized, excludeKeywords))
                {
                    continue;
                }

                if (includeKeywords == null || includeKeywords.Length == 0 || HasAnyKeyword(normalized, includeKeywords))
                {
                    return go;
                }
            }

            return null;
        }

        private static bool HasAnyKeyword(string text, string[] keywords)
        {
            if (string.IsNullOrEmpty(text) || keywords == null || keywords.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < keywords.Length; i++)
            {
                var key = NormalizeToken(keywords[i]);
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                if (text.Contains(key))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeToken(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
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

        private void ApplyRenderBudget()
        {
            if (members == null || members.Length == 0)
            {
                return;
            }

            var cap = Mathf.Max(1, maxVisibleRenderersPerMember);
            for (var i = 0; i < members.Length; i++)
            {
                var member = members[i];
                if (member == null || member.root == null)
                {
                    continue;
                }

                ApplyMemberRenderBudget(member.root, cap);
            }
        }

        private void ApplyMemberRenderBudget(Transform memberRoot, int rendererCap)
        {
            var renderers = memberRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return;
            }

            var scored = new List<(Renderer renderer, int score)>(renderers.Length);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (renderer.gameObject == null || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var score = CalculateRendererScore(renderer);
                scored.Add((renderer, score));
            }

            scored.Sort((a, b) => b.score.CompareTo(a.score));
            var keepSet = new HashSet<int>();
            for (var i = 0; i < scored.Count && i < rendererCap; i++)
            {
                var renderer = scored[i].renderer;
                if (renderer != null)
                {
                    keepSet.Add(renderer.GetInstanceID());
                }
            }

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                var keep = keepSet.Contains(renderer.GetInstanceID());
                renderer.enabled = keep;

                if (!keep)
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
            }
        }

        private int CalculateRendererScore(Renderer renderer)
        {
            if (renderer == null)
            {
                return int.MinValue;
            }

            var score = 0;
            var normalizedName = NormalizeToken(renderer.name);
            var goName = renderer.gameObject != null ? NormalizeToken(renderer.gameObject.name) : string.Empty;
            if (rendererPriorityKeywords != null)
            {
                for (var i = 0; i < rendererPriorityKeywords.Length; i++)
                {
                    var key = NormalizeToken(rendererPriorityKeywords[i]);
                    if (string.IsNullOrEmpty(key))
                    {
                        continue;
                    }

                    if (normalizedName.Contains(key) || goName.Contains(key))
                    {
                        score += 100 - i;
                    }
                }
            }

            if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
            {
                score += Mathf.Clamp(skinned.sharedMesh.vertexCount / 500, 0, 120);
            }

            return score;
        }
    }
}
