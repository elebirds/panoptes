/*************************************************
 * Project: Panoptes
 * File: SquadUnitVisualController.cs
 * Author: Panoptes Team
 * Date: 2026-04-11
 * Description: Squad-level visual driver for multi-member units.
 *************************************************/

using System;
using System.Collections;
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
            [NonSerialized] public string debugName;
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
            [NonSerialized] public bool warnedMissingController;
            [NonSerialized] public bool warnedMissingIdleState;
            [NonSerialized] public bool warnedMissingMoveState;
            [NonSerialized] public bool warnedMoveStateNotEntered;
            [NonSerialized] public bool warnedIdleStateNotEntered;
            [NonSerialized] public bool warnedAnimatorSpeedReset;
            [NonSerialized] public bool warnedLayerWeightReset;
            [NonSerialized] public bool warnedNoWeightedSkinnedMesh;
            [NonSerialized] public bool warnedStateTimeNotAdvancing;
            [NonSerialized] public int lastObservedStateHash;
            [NonSerialized] public float lastObservedNormalizedTime;
            [NonSerialized] public int stalledStateFrames;
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
        [SerializeField] private float attackReturnToIdleSeconds = 0.8f;
        [SerializeField] private bool useStateFallbackWhenNoParams = false;
        [SerializeField] private bool forceStatePlayback = true;
        [SerializeField] private bool preferDirectStatePlay = true;
        [SerializeField] private string idleStateName = "Idle";
        [SerializeField] private string moveStateName = "Run";
        [SerializeField] private float stateCrossFadeSeconds = 0.08f;
        [SerializeField] private bool manualIdleSamplingFallback = true;
        [SerializeField] private int idleSamplingStallFrameThreshold = 10;
        [SerializeField] private float idleSamplingPlaybackSpeed = 1f;
        [SerializeField] private bool forceContinuousIdleSamplingWhenStationary = true;
        [SerializeField] private bool forceUnscaledAnimatorUpdate = true;

        [Header("Appearance")]
        [SerializeField] private bool applyAppearanceOnBind = false;
        [SerializeField] private int bodyVariantIndex = -1;
        [SerializeField] private int headVariantIndex = -1;
        [SerializeField] private int weaponVariantIndex = -1;
        [SerializeField] private bool forceSingleBodyVariant = true;
        [SerializeField] private bool forceSingleHeadVariant = true;
        [SerializeField] private bool randomizeVariantPerMember = false;
        [SerializeField] private bool forceApplyConfiguredVariantsOnBind = true;
        [SerializeField] private string[] bodyNamePrefixes = { "body_" };
        [SerializeField] private string[] headNamePrefixes = { "head_" };
        [SerializeField] private string[] weaponNamePrefixes = { "weapon_", "w_", "sword_", "bow_", "crossbow_", "spear_", "halberd_", "staff_", "hammer_", "shield_" };
        [SerializeField] private bool enforceSingleModelForUnitTypes = true;
        [SerializeField] private bool singleModelKeepsBody = true;
        [SerializeField] private string[] singleModelUnitTypeAliases = { "settler" };

        [Header("Unit Role Variant")]
        [SerializeField] private bool applyRoleVariantOnBind = true;
        [SerializeField] private RuntimeAnimatorController unarmedAnimatorController;
        [SerializeField] private RuntimeAnimatorController swordAnimatorController;
        [SerializeField] private RuntimeAnimatorController bowAnimatorController;
        [SerializeField] private string unarmedIdleState = "infantry_01_idle";
        [SerializeField] private string unarmedMoveState = "infantry_03_run";
        [SerializeField] private string unarmedAttackState = "infantry_04_attack_A";
        [SerializeField] private string swordIdleState = "twohanded_01_idle";
        [SerializeField] private string swordMoveState = "twohanded_03_run";
        [SerializeField] private string swordAttackState = "twohanded_04_attack_A";
        [SerializeField] private string bowIdleState = "archer_01_idle";
        [SerializeField] private string bowMoveState = "archer_03_run";
        [SerializeField] private string bowAttackState = "archer_04_attack_A";
        [SerializeField] private string[] unarmedUnitTypeAliases = { "unarmed", "settler", "civilian", "fighter_basic", "fighter", "militia", "pioneer", "expander", "engineer" };
        [SerializeField] private string[] swordUnitTypeAliases = { "sword", "swordsman", "melee", "fighter_sword", "infantry", "cavalry" };
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
        [SerializeField] private bool autoApplyRenderBudgetWhenOverdrawRiskHigh = false;
        [SerializeField] private int highRendererCountThresholdPerMember = 18;
        [SerializeField] private string[] rendererPriorityKeywords = { "body", "head", "weapon", "shield", "helmet", "cape", "cloak" };

        [Header("Diagnostics")]
        [SerializeField] private bool enableAnimationDiagnostics = true;
        [SerializeField] private bool logMemberBindingDetails = true;
        [SerializeField] private bool logMoveStateTransitions = true;
        [SerializeField] private bool logSkinnedMeshDiagnostics = true;

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

                if (member.animator.runtimeAnimatorController == null)
                {
                    if (!member.warnedMissingController)
                    {
                        member.warnedMissingController = true;
                        LogAnimationWarning($"Member '{member.debugName}' has no RuntimeAnimatorController, cannot play move/idle states.");
                    }
                    continue;
                }

                EnsurePrimaryLayerWeight(member);

                if (member.animator.speed <= 0f)
                {
                    member.animator.speed = 1f;
                    if (!member.warnedAnimatorSpeedReset)
                    {
                        member.warnedAnimatorSpeedReset = true;
                        LogAnimationWarning($"Member '{member.debugName}' animator.speed<=0, force set to 1.");
                    }
                }

                if (member.hasMoveBoolParam)
                {
                    member.animator.SetBool(member.moveBoolHash, isMoving);
                }

                if (member.hasMoveSpeedParam)
                {
                    member.animator.SetFloat(member.moveSpeedHash, Mathf.Max(0f, normalizedSpeed));
                }

                var shouldUseStatePlayback = forceStatePlayback
                                             || (useStateFallbackWhenNoParams && !member.hasMoveBoolParam && !member.hasMoveSpeedParam);
                if (!shouldUseStatePlayback)
                {
                    if (isMoving)
                    {
                        member.idleStallFrames = 0;
                        member.lastIdleNormalizedTime = -1f;
                    }
                    else
                    {
                        member.isMoveStatePlaying = false;
                    }
                    continue;
                }

                if (isMoving && member.hasMoveState && !member.isMoveStatePlaying)
                {
                    if (logMoveStateTransitions)
                    {
                        var stateBefore = member.animator.GetCurrentAnimatorStateInfo(0);
                        LogAnimationInfo(
                            $"MoveStart member='{member.debugName}' fromShort={stateBefore.shortNameHash} " +
                            $"toMove='{moveStateName}' hash={member.moveStateHash} fade={Mathf.Max(0f, stateCrossFadeSeconds)}");
                    }
                    PlayState(member.animator, member.moveStateHash, restartAtZero: true);
                    member.isMoveStatePlaying = true;
                    member.warnedMoveStateNotEntered = false;
                }
                else if (isMoving && !member.hasMoveState && !member.warnedMissingMoveState)
                {
                    member.warnedMissingMoveState = true;
                    LogAnimationWarning($"Member '{member.debugName}' missing move state '{moveStateName}'.");
                }
                else if (isMoving && member.hasMoveState && member.isMoveStatePlaying && !IsAnimatorInState(member.animator, member.moveStateHash))
                {
                    if (!member.warnedMoveStateNotEntered)
                    {
                        member.warnedMoveStateNotEntered = true;
                        var current = member.animator.GetCurrentAnimatorStateInfo(0);
                        LogAnimationWarning(
                            $"MoveStateNotEntered member='{member.debugName}' expected='{moveStateName}' hash={member.moveStateHash} " +
                            $"currentShort={current.shortNameHash} currentNorm={current.normalizedTime:0.000}");
                    }
                }
                else if (!isMoving && member.hasIdleState && member.isMoveStatePlaying)
                {
                    if (logMoveStateTransitions)
                    {
                        var stateBefore = member.animator.GetCurrentAnimatorStateInfo(0);
                        LogAnimationInfo(
                            $"MoveStop member='{member.debugName}' fromShort={stateBefore.shortNameHash} " +
                            $"toIdle='{idleStateName}' hash={member.idleStateHash} fade={Mathf.Max(0f, stateCrossFadeSeconds)}");
                    }
                    PlayState(member.animator, member.idleStateHash, restartAtZero: false);
                    member.isMoveStatePlaying = false;
                    member.warnedIdleStateNotEntered = false;
                }
                else if (!isMoving && member.hasIdleState && !IsAnimatorInState(member.animator, member.idleStateHash))
                {
                    PlayState(member.animator, member.idleStateHash, restartAtZero: false);
                    member.isMoveStatePlaying = false;
                    if (!member.warnedIdleStateNotEntered)
                    {
                        member.warnedIdleStateNotEntered = true;
                        var current = member.animator.GetCurrentAnimatorStateInfo(0);
                        LogAnimationWarning(
                            $"IdleStateNotEntered member='{member.debugName}' expected='{idleStateName}' hash={member.idleStateHash} " +
                            $"currentShort={current.shortNameHash} currentNorm={current.normalizedTime:0.000}");
                    }
                }
                else if (!isMoving && !member.hasIdleState && !member.warnedMissingIdleState)
                {
                    member.warnedMissingIdleState = true;
                    LogAnimationWarning($"Member '{member.debugName}' missing idle state '{idleStateName}'.");
                }

                MonitorStateProgress(member, isMoving);
                if (!isMoving)
                {
                    ApplyContinuousIdleSampling(member);
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
                    member.isMoveStatePlaying = false;
                    played = true;
                }
            }

            if (played)
            {
                StartCoroutine(ReturnMembersToIdleAfterAttack());
            }

            return played;
        }

        private IEnumerator ReturnMembersToIdleAfterAttack()
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, attackReturnToIdleSeconds));
            ForceIdlePose();
        }

        public void OnUnitBound(string unitId, string unitType, string faction)
        {
            var seed = string.Concat(unitId ?? string.Empty, "|", unitType ?? string.Empty, "|", faction ?? string.Empty);
            if (ShouldApplyAppearanceOnBind())
            {
                ApplyAppearance(seed);
            }

            ApplySingleModelOverride(unitType);

            if (applyRoleVariantOnBind)
            {
                var roleVariant = ResolveRoleVariant(unitType);
                ApplyRoleVariant(roleVariant);
                LogAnimationInfo($"OnUnitBound unitId='{unitId}' unitType='{unitType}' role='{roleVariant}' members={(members == null ? 0 : members.Length)} idle='{idleStateName}' move='{moveStateName}'.");
            }

            if (applyRenderBudgetOnBind)
            {
                LogAnimationInfo("ApplyRenderBudget on bind because applyRenderBudgetOnBind=true.");
                ApplyRenderBudget();
            }
            else if (autoApplyRenderBudgetWhenOverdrawRiskHigh && IsRendererCountOverThreshold())
            {
                LogAnimationInfo("ApplyRenderBudget on bind because renderer count exceeded threshold.");
                ApplyRenderBudget();
            }

            // Idle/move pose reset is driven by UnitView on first bind only.
        }

        public void ApplyAppearance(string seed)
        {
            if (members == null || members.Length == 0)
            {
                return;
            }

            var shouldApplyBody = forceSingleBodyVariant || bodyVariantIndex >= 0;
            var shouldApplyHead = forceSingleHeadVariant || headVariantIndex >= 0;
            var shouldApplyWeapon = weaponVariantIndex >= 0;
            if (!shouldApplyBody && !shouldApplyHead && !shouldApplyWeapon)
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

                if (shouldApplyBody)
                {
                    var resolvedBodyIndex = bodyVariantIndex >= 0
                        ? bodyVariantIndex
                        : 0;
                    ApplyVariantSet(member.root, bodyNamePrefixes, ResolveVariantIndex(resolvedBodyIndex, memberSeed ^ 0x13579BDF));
                }

                if (shouldApplyHead)
                {
                    var resolvedHeadIndex = headVariantIndex >= 0
                        ? headVariantIndex
                        : 0;
                    ApplyVariantSet(member.root, headNamePrefixes, ResolveVariantIndex(resolvedHeadIndex, memberSeed ^ 0x2468ACE0));
                }

                if (shouldApplyWeapon)
                {
                    ApplyVariantSet(member.root, weaponNamePrefixes, ResolveVariantIndex(weaponVariantIndex, memberSeed ^ 0x5A5A5A5A));
                }
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

        private bool ShouldApplyAppearanceOnBind()
        {
            if (applyAppearanceOnBind)
            {
                return true;
            }

            if (forceSingleBodyVariant || forceSingleHeadVariant)
            {
                return true;
            }

            if (!forceApplyConfiguredVariantsOnBind)
            {
                return false;
            }

            return bodyVariantIndex >= 0 || headVariantIndex >= 0 || weaponVariantIndex >= 0;
        }

        private void CollectMembersFromFormationRoot()
        {
            if (formationRoot == null)
            {
                members = Array.Empty<MemberBinding>();
                return;
            }

            var collected = new List<MemberBinding>(8);
            var seenAnimators = new HashSet<Animator>();
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

                    if (!seenAnimators.Add(animator))
                    {
                        continue;
                    }

                    collected.Add(new MemberBinding
                    {
                        root = animator.transform,
                        animator = animator,
                        debugName = animator.name
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

                    if (!seenAnimators.Add(animator))
                    {
                        continue;
                    }

                    collected.Add(new MemberBinding
                    {
                        root = animator.transform,
                        animator = animator,
                        debugName = animator.name
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
                member.debugName = member.animator != null ? member.animator.name : member.root.name;
                member.warnedMissingController = false;
                member.warnedMissingIdleState = false;
                member.warnedMissingMoveState = false;
                member.warnedMoveStateNotEntered = false;
                member.warnedIdleStateNotEntered = false;
                member.warnedAnimatorSpeedReset = false;
                member.warnedLayerWeightReset = false;
                member.warnedNoWeightedSkinnedMesh = false;
                member.warnedStateTimeNotAdvancing = false;
                member.lastObservedStateHash = 0;
                member.lastObservedNormalizedTime = -1f;
                member.stalledStateFrames = 0;

                if (member.animator == null)
                {
                    LogAnimationWarning($"Member '{member.debugName}' has no Animator component.");
                    continue;
                }

                if (!member.animator.enabled)
                {
                    member.animator.enabled = true;
                    LogAnimationWarning($"Member '{member.debugName}' Animator was disabled; force-enabled.");
                }

                if (forceUnscaledAnimatorUpdate)
                {
                    member.animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                }
                member.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                member.animator.applyRootMotion = false;

                if (member.animator.runtimeAnimatorController == null && unarmedAnimatorController != null)
                {
                    // Ensure members always have a playable baseline controller even before unit bind.
                    member.animator.runtimeAnimatorController = unarmedAnimatorController;
                    member.animator.Rebind();
                    member.animator.Update(0f);
                }

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
                    LogAnimationWarning($"Member '{member.debugName}' still has no RuntimeAnimatorController after bind.");
                    continue;
                }

                EnsurePrimaryLayerWeight(member);
                if (member.animator.speed <= 0f)
                {
                    member.animator.speed = 1f;
                    LogAnimationWarning($"Member '{member.debugName}' animator.speed<=0 on bind, force set to 1.");
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

                if (logMemberBindingDetails)
                {
                    var controllerName = member.animator.runtimeAnimatorController != null
                        ? member.animator.runtimeAnimatorController.name
                        : "<null>";
                    LogAnimationInfo(
                        $"Bind member='{member.debugName}' controller='{controllerName}' " +
                        $"boolParam={member.hasMoveBoolParam} speedParam={member.hasMoveSpeedParam} " +
                        $"idleState='{idleStateName}' exists={member.hasIdleState} " +
                        $"moveState='{moveStateName}' exists={member.hasMoveState}");
                }

                LogMemberSkinnedMeshStatus(member, "Bind");
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

                MonitorStateProgress(member, member.isMoveStatePlaying);
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
                LogAnimationWarning(
                    $"IdleSamplingFallback member='{member.debugName}' stateShort={state.shortNameHash} " +
                    $"norm={normalized:0.000} stalledFrames={member.idleStallFrames} updateMode={member.animator.updateMode} " +
                    $"speed={member.animator.speed:0.###} timeScale={Time.timeScale:0.###}");
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
            var visited = new HashSet<GameObject>();
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

                if (!visited.Add(go))
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

        private bool ShouldGroupByVariantFamilies(string[] prefixes)
        {
            if (prefixes == null || prefixes.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < prefixes.Length; i++)
            {
                var prefix = NormalizeToken(prefixes[i]);
                if (prefix == "body_")
                {
                    if (forceSingleBodyVariant)
                    {
                        return false;
                    }
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

        private static bool IsAnimatorInState(Animator animator, int stateHash)
        {
            if (animator == null || stateHash == 0)
            {
                return false;
            }

            var state = animator.GetCurrentAnimatorStateInfo(0);
            return state.shortNameHash == stateHash || state.fullPathHash == stateHash;
        }

        private void EnsurePrimaryLayerWeight(MemberBinding member)
        {
            if (member == null || member.animator == null)
            {
                return;
            }

            if (member.animator.runtimeAnimatorController == null)
            {
                return;
            }

            if (member.animator.layerCount <= 0)
            {
                return;
            }

            var layerWeight = member.animator.GetLayerWeight(0);
            if (layerWeight > 0.0001f)
            {
                return;
            }

            member.animator.SetLayerWeight(0, 1f);
            if (!member.warnedLayerWeightReset)
            {
                member.warnedLayerWeightReset = true;
                LogAnimationWarning($"Member '{member.debugName}' layer0 weight was {layerWeight:0.###}, force set to 1.");
            }
        }

        private void ApplySingleModelOverride(string unitType)
        {
            if (!enforceSingleModelForUnitTypes || !MatchesAnyUnitTypeAlias(unitType, singleModelUnitTypeAliases))
            {
                return;
            }

            var prefixesToDisable = singleModelKeepsBody ? headNamePrefixes : bodyNamePrefixes;
            if (prefixesToDisable == null || prefixesToDisable.Length == 0 || members == null || members.Length == 0)
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

                DisableVariantSet(member.root, prefixesToDisable);
            }
        }

        private static void DisableVariantSet(Transform memberRoot, string[] prefixes)
        {
            if (memberRoot == null || prefixes == null || prefixes.Length == 0)
            {
                return;
            }

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

                go.SetActive(false);
            }
        }

        private static bool MatchesAnyUnitTypeAlias(string unitType, string[] aliases)
        {
            if (string.IsNullOrWhiteSpace(unitType) || aliases == null || aliases.Length == 0)
            {
                return false;
            }

            var normalized = NormalizeToken(unitType);
            for (var i = 0; i < aliases.Length; i++)
            {
                if (string.Equals(normalized, NormalizeToken(aliases[i]), StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void PlayState(Animator animator, int stateHash, bool restartAtZero)
        {
            if (animator == null || stateHash == 0)
            {
                return;
            }

            if (preferDirectStatePlay)
            {
                var normalizedTime = 0f;
                if (!restartAtZero)
                {
                    var current = animator.GetCurrentAnimatorStateInfo(0);
                    normalizedTime = Mathf.Repeat(current.normalizedTime, 1f);
                }
                animator.Play(stateHash, 0, normalizedTime);
                animator.Update(0f);
                return;
            }

            animator.CrossFade(stateHash, Mathf.Max(0f, stateCrossFadeSeconds), 0);
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
            string attackState;

            switch (variant)
            {
                case UnitRoleVariant.Bow:
                    controller = bowAnimatorController;
                    idleState = bowIdleState;
                    moveState = bowMoveState;
                    attackState = bowAttackState;
                    break;
                case UnitRoleVariant.Sword:
                    controller = swordAnimatorController;
                    idleState = swordIdleState;
                    moveState = swordMoveState;
                    attackState = swordAttackState;
                    break;
                default:
                    controller = unarmedAnimatorController;
                    idleState = unarmedIdleState;
                    moveState = unarmedMoveState;
                    attackState = unarmedAttackState;
                    break;
            }

            ApplyAnimatorController(controller, idleState, moveState, attackState);
            ApplyWeaponVisualByRole(variant);
        }

        private void ApplyAnimatorController(RuntimeAnimatorController controller, string idleState, string moveState, string attackState)
        {
            var changedController = false;
            var changedStates = false;

            if (members != null)
            {
                for (var i = 0; i < members.Length; i++)
                {
                    var member = members[i];
                    if (member == null || member.animator == null)
                    {
                        continue;
                    }

                    if (controller != null && !IsEquivalentController(member.animator.runtimeAnimatorController, controller))
                    {
                        member.animator.runtimeAnimatorController = controller;
                        member.animator.Rebind();
                        member.animator.Update(0f);
                        changedController = true;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(idleState) && !string.Equals(idleStateName, idleState, StringComparison.Ordinal))
            {
                idleStateName = idleState;
                changedStates = true;
            }

            if (!string.IsNullOrWhiteSpace(moveState) && !string.Equals(moveStateName, moveState, StringComparison.Ordinal))
            {
                moveStateName = moveState;
                changedStates = true;
            }

            if (!string.IsNullOrWhiteSpace(attackState) && !string.Equals(attackStateName, attackState, StringComparison.Ordinal))
            {
                attackStateName = attackState;
                changedStates = true;
            }

            if (changedController || changedStates)
            {
                BindAnimators();
            }
        }

        private static bool IsEquivalentController(RuntimeAnimatorController current, RuntimeAnimatorController target)
        {
            if (ReferenceEquals(current, target))
            {
                return true;
            }

            if (current == null || target == null)
            {
                return false;
            }

            // In some runtime paths Unity may provide cloned controller instances
            // with different references but identical semantics.
            return string.Equals(current.name, target.name, StringComparison.Ordinal);
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

            var visited = new HashSet<GameObject>();
            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null || t == root)
                {
                    continue;
                }

                var go = t.gameObject;
                if (go == null || !visited.Add(go))
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

        private void LogMemberSkinnedMeshStatus(MemberBinding member, string phase)
        {
            if (!enableAnimationDiagnostics || !logSkinnedMeshDiagnostics || member == null || member.animator == null || member.root == null)
            {
                return;
            }

            var skinnedMeshes = member.root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var totalSkinned = 0;
            var activeSkinned = 0;
            var weightedSkinned = 0;
            var firstMeshName = "<none>";
            for (var i = 0; i < skinnedMeshes.Length; i++)
            {
                var skinned = skinnedMeshes[i];
                if (skinned == null)
                {
                    continue;
                }

                totalSkinned++;
                if (string.Equals(firstMeshName, "<none>", StringComparison.Ordinal) && skinned.sharedMesh != null)
                {
                    firstMeshName = skinned.sharedMesh.name;
                }

                if (!skinned.enabled || skinned.gameObject == null || !skinned.gameObject.activeInHierarchy)
                {
                    continue;
                }

                activeSkinned++;
                if (skinned.rootBone != null && skinned.bones != null && skinned.bones.Length > 0 && skinned.sharedMesh != null)
                {
                    weightedSkinned++;
                }
            }

            var state = member.animator.GetCurrentAnimatorStateInfo(0);
            var layerWeight = member.animator.layerCount > 0 ? member.animator.GetLayerWeight(0) : 0f;
            var avatar = member.animator.avatar;
            var avatarStatus = avatar == null ? "<null>" : $"valid={avatar.isValid} human={avatar.isHuman}";
            var clips = member.animator.GetCurrentAnimatorClipInfo(0);
            var clipName = clips != null && clips.Length > 0 && clips[0].clip != null ? clips[0].clip.name : "<none>";

            LogAnimationInfo(
                $"RigCheck[{phase}] member='{member.debugName}' controller='{member.animator.runtimeAnimatorController?.name ?? "<null>"}' " +
                $"stateShort={state.shortNameHash} norm={state.normalizedTime:0.000} clip='{clipName}' " +
                $"updateMode={member.animator.updateMode} speed={member.animator.speed:0.###} layer0={layerWeight:0.###} " +
                $"timeScale={Time.timeScale:0.###} avatar={avatarStatus} skinnedTotal={totalSkinned} " +
                $"skinnedActive={activeSkinned} skinnedWeighted={weightedSkinned} firstMesh='{firstMeshName}'");

            if (weightedSkinned <= 0 && !member.warnedNoWeightedSkinnedMesh)
            {
                member.warnedNoWeightedSkinnedMesh = true;
                LogAnimationWarning(
                    $"Member '{member.debugName}' has no active weighted SkinnedMeshRenderer. " +
                    "Animator state may change but no mesh is being deformed.");
            }
        }

        private void MonitorStateProgress(MemberBinding member, bool isMoving)
        {
            if (!enableAnimationDiagnostics || member == null || member.animator == null || member.animator.runtimeAnimatorController == null)
            {
                return;
            }

            var state = member.animator.GetCurrentAnimatorStateInfo(0);
            if (state.shortNameHash != member.lastObservedStateHash)
            {
                member.lastObservedStateHash = state.shortNameHash;
                member.lastObservedNormalizedTime = state.normalizedTime;
                member.stalledStateFrames = 0;
                member.warnedStateTimeNotAdvancing = false;
                return;
            }

            if (member.lastObservedNormalizedTime >= 0f && Mathf.Abs(state.normalizedTime - member.lastObservedNormalizedTime) < 0.00001f)
            {
                member.stalledStateFrames++;
            }
            else
            {
                member.stalledStateFrames = 0;
                member.warnedStateTimeNotAdvancing = false;
            }

            member.lastObservedNormalizedTime = state.normalizedTime;

            var threshold = Mathf.Max(15, idleSamplingStallFrameThreshold * 2);
            if (member.stalledStateFrames < threshold || member.warnedStateTimeNotAdvancing)
            {
                return;
            }

            var expectedStateHash = isMoving ? member.moveStateHash : member.idleStateHash;
            if (expectedStateHash != 0 && state.shortNameHash != expectedStateHash && state.fullPathHash != expectedStateHash)
            {
                return;
            }

            member.warnedStateTimeNotAdvancing = true;
            var clips = member.animator.GetCurrentAnimatorClipInfo(0);
            var clipName = clips != null && clips.Length > 0 && clips[0].clip != null ? clips[0].clip.name : "<none>";
            var clipLength = clips != null && clips.Length > 0 && clips[0].clip != null ? clips[0].clip.length : 0f;

            LogAnimationWarning(
                $"StateTimeNotAdvancing member='{member.debugName}' moving={isMoving} " +
                $"stateShort={state.shortNameHash} norm={state.normalizedTime:0.000} stalledFrames={member.stalledStateFrames} " +
                $"clip='{clipName}' clipLen={clipLength:0.###} updateMode={member.animator.updateMode} " +
                $"speed={member.animator.speed:0.###} timeScale={Time.timeScale:0.###} hasBoundPlayables={member.animator.hasBoundPlayables}");
            LogMemberSkinnedMeshStatus(member, "StateStall");
        }

        private void ApplyContinuousIdleSampling(MemberBinding member)
        {
            if (!forceContinuousIdleSamplingWhenStationary || member == null || member.animator == null || !member.hasIdleState)
            {
                return;
            }

            var state = member.animator.GetCurrentAnimatorStateInfo(0);
            if (state.shortNameHash != member.idleStateHash && state.fullPathHash != member.idleStateHash)
            {
                return;
            }

            var playbackSpeed = Mathf.Max(0.01f, idleSamplingPlaybackSpeed);
            member.animator.Play(member.idleStateHash, 0, Mathf.Repeat(Time.unscaledTime * playbackSpeed, 1f));
            member.animator.Update(0f);
        }

        private void LogAnimationInfo(string message)
        {
            if (!enableAnimationDiagnostics || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            PanoptesLog.Log($"[SquadUnitVisualController] {message}", this);
        }

        private void LogAnimationWarning(string message)
        {
            if (!enableAnimationDiagnostics || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            PanoptesLog.Warning($"[SquadUnitVisualController] {message}", this);
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

            var options = new SquadUnitRenderBudgetPresenter.Options(
                maxVisibleRenderersPerMember,
                disableCastShadows,
                disableReceiveShadows,
                rendererPriorityKeywords);
            for (var i = 0; i < members.Length; i++)
            {
                var member = members[i];
                if (member == null || member.root == null)
                {
                    continue;
                }

                SquadUnitRenderBudgetPresenter.Apply(member.root, options);
            }
        }

        private bool IsRendererCountOverThreshold()
        {
            if (members == null || members.Length == 0)
            {
                return false;
            }

            var threshold = Mathf.Max(1, highRendererCountThresholdPerMember);
            for (var i = 0; i < members.Length; i++)
            {
                var member = members[i];
                if (member == null || member.root == null)
                {
                    continue;
                }

                if (SquadUnitRenderBudgetPresenter.IsOverThreshold(member.root, threshold))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
