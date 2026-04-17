using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Domain;
using Panoptes.Protocol.V1;

namespace Panoptes.Tests.EditMode.UI
{
    public sealed class TechTreePanelStateBuilderTests
    {
        private const string BuilderTypeName = "Panoptes.Presentation.UI.Domestic.TechTreePanelStateBuilder, Panoptes.Presentation";

        [Test]
        public void Build_ShouldPreferPlannedResearchTarget_AndProjectAllFiveStates()
        {
            var builderType = Type.GetType(BuilderTypeName);
            Assert.That(builderType, Is.Not.Null, "缺少 TechTreePanelStateBuilder。");

            var inputType = builderType.GetNestedType("BuildInput", BindingFlags.Public);
            Assert.That(inputType, Is.Not.Null, "TechTreePanelStateBuilder 必须暴露 BuildInput。");
            Assert.That(inputType!.GetProperty("Research"), Is.Null,
                "BuildInput 不应继续暴露旧的 object Research 输入。");
            Assert.That(inputType.GetProperty("ResearchState"), Is.Not.Null,
                "BuildInput 必须改为强类型 ResearchState 输入。");

            var input = Activator.CreateInstance(inputType);
            Assert.That(input, Is.Not.Null);

            SetProperty(inputType, input, "Technologies", new[]
            {
                CreateTechnology("tech_locked", 3, "tech_missing"),
                CreateTechnology("tech_available", 2),
                CreateTechnology("tech_selected", 5),
                CreateTechnology("tech_pending", 2),
                CreateTechnology("tech_active", 1)
            });
            SetProperty(inputType, input, "ResearchState", CreateResearchStateDto(
                currentTargetTechnologyId: "tech_available",
                currentProgress: 1,
                requiredProgress: 2,
                pendingActivationTechnologyIds: new[] { "tech_pending" },
                activeTechnologyIds: new[] { "tech_active" },
                savedProgressEntries: new[]
                {
                    ("tech_selected", 3, 5)
                }));
            SetProperty(inputType, input, "PlannedResearchTargetTechnologyId", "tech_selected");
            SetProperty(inputType, input, "Phase", "planning");
            SetProperty(inputType, input, "IsActionLocked", false);

            var builder = Activator.CreateInstance(builderType);
            Assert.That(builder, Is.Not.Null);

            var buildMethod = builderType.GetMethod("Build", new[] { inputType });
            Assert.That(buildMethod, Is.Not.Null, "TechTreePanelStateBuilder 必须提供 Build(BuildInput)。");

            var result = buildMethod.Invoke(builder, new[] { input }) as IDictionary;
            Assert.That(result, Is.Not.Null, "Build 必须返回按 technology_id 建索引的字典。");

            AssertState(result, "tech_locked", "Locked", 0, 3, false);
            AssertState(result, "tech_available", "Available", 1, 2, true);
            AssertState(result, "tech_selected", "Researching", 3, 5, false);
            AssertState(result, "tech_pending", "PendingActivation", 2, 2, false);
            AssertState(result, "tech_active", "Active", 1, 1, false);
        }

        [Test]
        public void Build_ShouldDisableAllNodes_WhenNotPlanningOrActionLocked()
        {
            var builderType = Type.GetType(BuilderTypeName);
            Assert.That(builderType, Is.Not.Null, "缺少 TechTreePanelStateBuilder。");

            var inputType = builderType.GetNestedType("BuildInput", BindingFlags.Public);
            Assert.That(inputType, Is.Not.Null);

            var input = Activator.CreateInstance(inputType);
            Assert.That(input, Is.Not.Null);

            SetProperty(inputType, input, "Technologies", new[]
            {
                CreateTechnology("tech_available", 2)
            });
            SetProperty(inputType, input, "ResearchState", new TechnologyDto());
            SetProperty(inputType, input, "PlannedResearchTargetTechnologyId", string.Empty);
            SetProperty(inputType, input, "Phase", "resolving");
            SetProperty(inputType, input, "IsActionLocked", true);

            var buildMethod = builderType.GetMethod("Build", new[] { inputType });
            Assert.That(buildMethod, Is.Not.Null);

            var result = buildMethod.Invoke(Activator.CreateInstance(builderType), new[] { input }) as IDictionary;
            Assert.That(result, Is.Not.Null);

            AssertState(result, "tech_available", "Available", 0, 2, false);
        }

        private static TechnologyDto CreateResearchStateDto(
            string currentTargetTechnologyId,
            int currentProgress,
            int requiredProgress,
            IEnumerable<string> pendingActivationTechnologyIds,
            IEnumerable<string> activeTechnologyIds,
            IEnumerable<(string technologyId, int currentProgress, int requiredProgress)> savedProgressEntries)
        {
            var dto = new TechnologyDto
            {
                TechnologyId = currentTargetTechnologyId,
                CurrentProgress = currentProgress,
                RequiredProgress = requiredProgress,
                PendingActivationTechnologyIds = pendingActivationTechnologyIds != null
                    ? new List<string>(pendingActivationTechnologyIds)
                    : new List<string>(),
                ActiveTechnologyIds = activeTechnologyIds != null
                    ? new List<string>(activeTechnologyIds)
                    : new List<string>()
            };

            var savedProgressField = typeof(TechnologyDto).GetField("SavedProgress", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(savedProgressField, Is.Not.Null, "TechnologyDto 必须暴露 SavedProgress 集合。");
            Assert.That(savedProgressField!.FieldType.IsGenericType, Is.True, "SavedProgress 必须是强类型列表。");

            var savedProgressList = Activator.CreateInstance(savedProgressField.FieldType) as IList;
            Assert.That(savedProgressList, Is.Not.Null, "SavedProgress 必须可实例化。");

            var itemType = savedProgressField.FieldType.GetGenericArguments()[0];
            foreach (var (technologyId, entryCurrentProgress, entryRequiredProgress) in savedProgressEntries)
            {
                var entry = Activator.CreateInstance(itemType);
                Assert.That(entry, Is.Not.Null, "SavedProgress 项必须可实例化。");

                SetField(itemType, entry, "TechnologyId", technologyId);
                SetField(itemType, entry, "CurrentProgress", entryCurrentProgress);
                SetField(itemType, entry, "RequiredProgress", entryRequiredProgress);
                savedProgressList!.Add(entry);
            }

            savedProgressField.SetValue(dto, savedProgressList);
            return dto;
        }

        private static StaticCatalogCache.TechnologyEntryJson CreateTechnology(string id, int cost, params string[] prerequisites)
        {
            var technology = new StaticCatalogCache.TechnologyEntryJson
            {
                id = id,
                name = id,
                description = $"{id}_desc",
                icon_key = $"{id}_icon",
                research_cost = cost,
                prerequisites = Array.Empty<StaticCatalogCache.PrerequisiteEntryJson>()
            };

            if (prerequisites == null || prerequisites.Length == 0)
            {
                return technology;
            }

            var result = new StaticCatalogCache.PrerequisiteEntryJson[prerequisites.Length];
            for (var i = 0; i < prerequisites.Length; i++)
            {
                result[i] = new StaticCatalogCache.PrerequisiteEntryJson
                {
                    type = "technology_unlocked",
                    target_id = prerequisites[i]
                };
            }

            technology.prerequisites = result;
            return technology;
        }

        private static void AssertState(IDictionary result, string technologyId, string expectedStatus, int expectedCurrent, int expectedRequired, bool expectedInteractable)
        {
            Assert.That(result.Contains(technologyId), Is.True, $"缺少科技状态 {technologyId}。");

            var runtimeState = result[technologyId];
            Assert.That(runtimeState, Is.Not.Null, $"科技状态 {technologyId} 不能为空。");

            var stateType = runtimeState!.GetType();
            Assert.That(GetPropertyValue(stateType, runtimeState, "Status")?.ToString(), Is.EqualTo(expectedStatus), $"{technologyId} 状态错误。");
            Assert.That(GetPropertyValue(stateType, runtimeState, "CurrentProgress"), Is.EqualTo(expectedCurrent), $"{technologyId} 当前进度错误。");
            Assert.That(GetPropertyValue(stateType, runtimeState, "RequiredProgress"), Is.EqualTo(expectedRequired), $"{technologyId} 所需进度错误。");
            Assert.That(GetPropertyValue(stateType, runtimeState, "IsInteractable"), Is.EqualTo(expectedInteractable), $"{technologyId} 交互态错误。");
        }

        private static object GetPropertyValue(Type targetType, object target, string propertyName)
        {
            var property = targetType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"缺少属性 {propertyName}。");
            return property!.GetValue(target);
        }

        private static void SetProperty(Type targetType, object target, string propertyName, object value)
        {
            var property = targetType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"缺少属性 {propertyName}。");
            property!.SetValue(target, value);
        }

        private static void SetField(Type targetType, object target, string fieldName, object value)
        {
            var field = targetType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, $"缺少字段 {fieldName}。");
            field!.SetValue(target, value);
        }
    }
}
