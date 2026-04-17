using System;
using System.Collections.Generic;
using System.Collections;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Intents;

namespace Panoptes.Presentation.UI.Domestic
{
    public enum TechTreeNodeStatus
    {
        Locked = 0,
        Available = 1,
        Researching = 2,
        PendingActivation = 3,
        Active = 4
    }

    public sealed class TechTreeNodeRuntimeState
    {
        public string TechnologyId { get; set; } = string.Empty;
        public TechTreeNodeStatus Status { get; set; }
        public int CurrentProgress { get; set; }
        public int RequiredProgress { get; set; }
        public bool IsInteractable { get; set; }
    }

    public sealed class TechTreeNodeRenderModel
    {
        public string TechnologyId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconKey { get; set; } = string.Empty;
        public TechTreeNodeStatus Status { get; set; }
        public string StatusLabel { get; set; } = string.Empty;
        public int CurrentProgress { get; set; }
        public int RequiredProgress { get; set; }
        public bool IsInteractable { get; set; }
    }

    public sealed class TechTreePanelStateBuilder
    {
        private readonly struct ProgressState
        {
            public readonly int Current;
            public readonly int Required;

            public ProgressState(int current, int required)
            {
                Current = current;
                Required = required;
            }
        }

        public sealed class BuildInput
        {
            public IReadOnlyCollection<StaticCatalogCache.TechnologyEntryJson> Technologies { get; set; } =
                Array.Empty<StaticCatalogCache.TechnologyEntryJson>();

            public object Research { get; set; }
            public string PlannedResearchTargetTechnologyId { get; set; } = string.Empty;
            public string Phase { get; set; } = string.Empty;
            public bool IsActionLocked { get; set; }
        }

        public Dictionary<string, TechTreeNodeRuntimeState> Build(BuildInput input)
        {
            var result = new Dictionary<string, TechTreeNodeRuntimeState>(StringComparer.OrdinalIgnoreCase);
            if (input?.Technologies == null)
            {
                return result;
            }

            var research = input.Research;
            var activeTechnologyIds = ToNormalizedSet(GetStringSequenceProperty(research, "ActiveTechnologyIds"));
            var pendingTechnologyIds = ToNormalizedSet(GetStringSequenceProperty(research, "PendingActivationTechnologyIds"));
            var selectedTechnologyId = Normalize(input.PlannedResearchTargetTechnologyId);
            var currentTargetTechnologyId = Normalize(GetStringProperty(research, "CurrentTargetTechnologyId"));
            var savedProgress = BuildSavedProgressIndex(research);
            var canInteract = string.Equals(Normalize(input.Phase), "planning", StringComparison.Ordinal) && !input.IsActionLocked;

            if (string.IsNullOrWhiteSpace(selectedTechnologyId))
            {
                selectedTechnologyId = currentTargetTechnologyId;
            }

            foreach (var technology in input.Technologies)
            {
                if (technology == null || string.IsNullOrWhiteSpace(technology.id))
                {
                    continue;
                }

                var technologyId = Normalize(technology.id);
                var requiredProgress = Math.Max(technology.research_cost, 0);
                var currentProgress = 0;
                var hasSavedProgress = savedProgress.TryGetValue(technologyId, out var progressState);
                if (hasSavedProgress)
                {
                    currentProgress = progressState.Current;
                    if (progressState.Required > 0)
                    {
                        requiredProgress = progressState.Required;
                    }
                }

                if (string.Equals(technologyId, currentTargetTechnologyId, StringComparison.Ordinal))
                {
                    currentProgress = Math.Max(0, GetIntProperty(research, "CurrentProgress"));
                    var currentRequiredProgress = GetIntProperty(research, "RequiredProgress");
                    if (currentRequiredProgress > 0)
                    {
                        requiredProgress = currentRequiredProgress;
                    }
                }

                var status = ResolveStatus(technology, technologyId, activeTechnologyIds, pendingTechnologyIds, selectedTechnologyId);
                switch (status)
                {
                    case TechTreeNodeStatus.Active:
                    case TechTreeNodeStatus.PendingActivation:
                        currentProgress = requiredProgress;
                        break;
                    case TechTreeNodeStatus.Researching:
                        if (!string.Equals(technologyId, currentTargetTechnologyId, StringComparison.Ordinal) && !hasSavedProgress)
                        {
                            currentProgress = 0;
                        }
                        break;
                }

                if (requiredProgress > 0)
                {
                    currentProgress = Math.Min(currentProgress, requiredProgress);
                }

                result[technologyId] = new TechTreeNodeRuntimeState
                {
                    TechnologyId = technologyId,
                    Status = status,
                    CurrentProgress = Math.Max(0, currentProgress),
                    RequiredProgress = Math.Max(0, requiredProgress),
                    IsInteractable = canInteract && status == TechTreeNodeStatus.Available
                };
            }

            return result;
        }

        public Dictionary<string, TechTreeNodeRuntimeState> BuildFromCaches(
            StaticCatalogCache catalog,
            GameStateCache gameState,
            PlanningDraftCache planningDraft)
        {
            return Build(new BuildInput
            {
                Technologies = catalog != null
                    ? new List<StaticCatalogCache.TechnologyEntryJson>(catalog.Technologies.Values)
                    : Array.Empty<StaticCatalogCache.TechnologyEntryJson>(),
                Research = GetPropertyValue(GetPropertyValue(gameState, "MyPlayer"), "Research"),
                PlannedResearchTargetTechnologyId = planningDraft != null ? planningDraft.PlannedResearchTargetTechnologyId : string.Empty,
                Phase = gameState != null ? gameState.Phase : string.Empty,
                IsActionLocked = ActionLock.IsLocked
            });
        }

        public static string GetStatusLabel(TechTreeNodeStatus status)
        {
            return status switch
            {
                TechTreeNodeStatus.Locked => "未解锁",
                TechTreeNodeStatus.Available => "可研究",
                TechTreeNodeStatus.Researching => "研究中",
                TechTreeNodeStatus.PendingActivation => "已完成，下回合生效",
                TechTreeNodeStatus.Active => "已解锁",
                _ => string.Empty
            };
        }

        private static TechTreeNodeStatus ResolveStatus(
            StaticCatalogCache.TechnologyEntryJson technology,
            string technologyId,
            HashSet<string> activeTechnologyIds,
            HashSet<string> pendingTechnologyIds,
            string selectedTechnologyId)
        {
            if (activeTechnologyIds.Contains(technologyId))
            {
                return TechTreeNodeStatus.Active;
            }

            if (pendingTechnologyIds.Contains(technologyId))
            {
                return TechTreeNodeStatus.PendingActivation;
            }

            if (!string.IsNullOrWhiteSpace(selectedTechnologyId) &&
                string.Equals(technologyId, selectedTechnologyId, StringComparison.Ordinal))
            {
                return TechTreeNodeStatus.Researching;
            }

            return ArePrerequisitesSatisfied(technology, activeTechnologyIds)
                ? TechTreeNodeStatus.Available
                : TechTreeNodeStatus.Locked;
        }

        private static bool ArePrerequisitesSatisfied(
            StaticCatalogCache.TechnologyEntryJson technology,
            HashSet<string> activeTechnologyIds)
        {
            if (technology?.prerequisites == null || technology.prerequisites.Length == 0)
            {
                return true;
            }

            for (var i = 0; i < technology.prerequisites.Length; i++)
            {
                var prerequisite = technology.prerequisites[i];
                if (prerequisite == null)
                {
                    continue;
                }

                if (!string.Equals(Normalize(prerequisite.type), "technology_unlocked", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!activeTechnologyIds.Contains(Normalize(prerequisite.target_id)))
                {
                    return false;
                }
            }

            return true;
        }

        private static Dictionary<string, ProgressState> BuildSavedProgressIndex(object research)
        {
            var result = new Dictionary<string, ProgressState>(StringComparer.OrdinalIgnoreCase);
            var savedProgress = GetObjectSequenceProperty(research, "SavedProgress");
            if (savedProgress == null)
            {
                return result;
            }

            foreach (var progress in savedProgress)
            {
                var technologyId = GetStringProperty(progress, "TechnologyId");
                if (string.IsNullOrWhiteSpace(technologyId))
                {
                    continue;
                }

                result[Normalize(technologyId)] = new ProgressState(
                    Math.Max(0, GetIntProperty(progress, "CurrentProgress")),
                    Math.Max(0, GetIntProperty(progress, "RequiredProgress")));
            }

            return result;
        }

        private static IEnumerable<string> GetStringSequenceProperty(object target, string propertyName)
        {
            var value = GetPropertyValue(target, propertyName);
            if (value is not IEnumerable enumerable)
            {
                yield break;
            }

            foreach (var entry in enumerable)
            {
                if (entry is string text)
                {
                    yield return text;
                }
            }
        }

        private static IEnumerable<object> GetObjectSequenceProperty(object target, string propertyName)
        {
            var value = GetPropertyValue(target, propertyName);
            if (value is not IEnumerable enumerable)
            {
                yield break;
            }

            foreach (var entry in enumerable)
            {
                if (entry != null)
                {
                    yield return entry;
                }
            }
        }

        private static string GetStringProperty(object target, string propertyName)
        {
            return GetPropertyValue(target, propertyName) as string ?? string.Empty;
        }

        private static int GetIntProperty(object target, string propertyName)
        {
            var value = GetPropertyValue(target, propertyName);
            return value switch
            {
                int intValue => intValue,
                long longValue => (int)longValue,
                _ => 0
            };
        }

        private static object GetPropertyValue(object target, string propertyName)
        {
            if (target == null || string.IsNullOrWhiteSpace(propertyName))
            {
                return null;
            }

            var property = target.GetType().GetProperty(propertyName);
            return property != null ? property.GetValue(target) : null;
        }

        private static HashSet<string> ToNormalizedSet(IEnumerable<string> values)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (values == null)
            {
                return result;
            }

            foreach (var value in values)
            {
                var normalized = Normalize(value);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    result.Add(normalized);
                }
            }

            return result;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
        }
    }
}
