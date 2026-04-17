using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace Panoptes.Tests.EditMode.UI
{
    public sealed class BuildPanelRenderBuilderTests
    {
        private const string BuilderTypeName = "Panoptes.Presentation.UI.Domestic.BuildPanelRenderBuilder, Panoptes.Presentation";
        private const string SourceTypeName = "Panoptes.Presentation.UI.Domestic.BuildItemRenderSource, Panoptes.Presentation";
        private const string MetricSourceTypeName = "Panoptes.Presentation.UI.Domestic.BuildMetricSource, Panoptes.Presentation";
        private const string AvailabilityEnumTypeName = "Panoptes.Presentation.UI.Domestic.BuildItemAvailabilityState, Panoptes.Presentation";

        [Test]
        public void Build_ShouldGroupByPlacementKind_AndPreserveGroupInternalOrder()
        {
            var sources = CreateSourceList(
                CreateSource("workshop", "工坊", "城市工坊", "city_territory", "Available"),
                CreateSource("farm", "农场", "基础粮食建筑", "resource_node", "Available"),
                CreateSource("watchtower", "哨塔", "视野建筑", "other_kind", "Available"),
                CreateSource("mine", "矿井", "基础产能建筑", "resource_node", "Available"));

            var groups = InvokeBuild(sources);

            Assert.That(groups.Count, Is.EqualTo(3), "应拆成地块建筑、城市建筑、其他三组。");
            AssertGroup(groups[0], "tile", "地块建筑", new[] { "farm", "mine" });
            AssertGroup(groups[1], "city", "城市建筑", new[] { "workshop" });
            AssertGroup(groups[2], "other", "其他", new[] { "watchtower" });
        }

        [Test]
        public void Build_ShouldPrioritizeIndustryThenFirstTwoResourceCosts_ForSummaryMetrics()
        {
            var source = CreateSource("smelter", "冶炼厂", "把矿石转为产能", "city_territory", "Available");
            AddCost(source, "research_output", "研究", 4, true);
            AddCost(source, "industry_output", "产能", 12, true);
            AddCost(source, "wood", "木材", 3, false);
            AddCost(source, "stone", "石材", 2, false);
            AddCost(source, "food", "粮食", 9, false);

            var groups = InvokeBuild(CreateSourceList(source));
            var item = GetItem(groups, 0, 0);
            var metrics = GetFieldValue<IList>(item, "SummaryMetrics");

            Assert.That(metrics, Has.Count.EqualTo(3), "摘要指标最多只应显示 3 项。");
            AssertMetric(metrics[0], "industry_output", "产能 x12");
            AssertMetric(metrics[1], "wood", "木材 x3");
            AssertMetric(metrics[2], "stone", "石材 x2");
        }

        [Test]
        public void Build_ShouldEmbedLockedAndPendingDetails_IntoTooltipText()
        {
            var locked = CreateSource("tower", "箭塔", "提供远程防御", "city_territory", "Locked");
            AddCost(locked, "industry_output", "产能", 15, true);
            AddRequiredTech(locked, "城防");

            var pending = CreateSource("farm", "农场", "基础粮食建筑", "resource_node", "Pending", requiredResourceType: "小麦");

            var groups = InvokeBuild(CreateSourceList(locked, pending));
            var tilePending = GetItem(groups, 0, 0);
            var cityLocked = GetItem(groups, 1, 0);

            var pendingTooltip = GetFieldValue<string>(tilePending, "TooltipText");
            StringAssert.Contains("状态", pendingTooltip);
            StringAssert.Contains("已加入本回合规划", pendingTooltip);
            StringAssert.Contains("仅可放置在小麦资源地块", pendingTooltip);

            var lockedTooltip = GetFieldValue<string>(cityLocked, "TooltipText");
            StringAssert.Contains("解锁要求", lockedTooltip);
            StringAssert.Contains("城防", lockedTooltip);
            StringAssert.Contains("消耗", lockedTooltip);
        }

        private static IList InvokeBuild(IList sources)
        {
            var builderType = ResolveType(BuilderTypeName);
            var sourceType = ResolveType(SourceTypeName);
            var buildMethod = builderType.GetMethod("Build", BindingFlags.Static | BindingFlags.Public);
            Assert.That(buildMethod, Is.Not.Null, "缺少 BuildPanelRenderBuilder.Build。");
            var result = buildMethod!.Invoke(null, new object[] { sources });
            Assert.That(result, Is.InstanceOf(typeof(IList)), "Build 必须返回列表。");
            return (IList)result!;
        }

        private static void AssertGroup(object group, string expectedId, string expectedTitle, IReadOnlyList<string> expectedBuildingIds)
        {
            Assert.That(GetFieldValue<string>(group, "Id"), Is.EqualTo(expectedId));
            Assert.That(GetFieldValue<string>(group, "Title"), Is.EqualTo(expectedTitle));

            var items = GetFieldValue<IList>(group, "Items");
            Assert.That(items.Count, Is.EqualTo(expectedBuildingIds.Count));
            for (var i = 0; i < expectedBuildingIds.Count; i++)
            {
                Assert.That(GetFieldValue<string>(items[i], "BuildingId"), Is.EqualTo(expectedBuildingIds[i]));
            }
        }

        private static void AssertMetric(object metric, string expectedKey, string expectedText)
        {
            Assert.That(GetFieldValue<string>(metric, "Key"), Is.EqualTo(expectedKey));
            Assert.That(GetFieldValue<string>(metric, "Text"), Is.EqualTo(expectedText));
        }

        private static object GetItem(IList groups, int groupIndex, int itemIndex)
        {
            var items = GetFieldValue<IList>(groups[groupIndex], "Items");
            Assert.That(items.Count, Is.GreaterThan(itemIndex));
            return items[itemIndex];
        }

        private static IList CreateSourceList(params object[] sources)
        {
            var sourceType = ResolveType(SourceTypeName);
            var listType = typeof(List<>).MakeGenericType(sourceType);
            var list = Activator.CreateInstance(listType) as IList;
            Assert.That(list, Is.Not.Null);
            for (var i = 0; i < sources.Length; i++)
            {
                list!.Add(sources[i]);
            }

            return list!;
        }

        private static object CreateSource(
            string buildingId,
            string title,
            string description,
            string placementKind,
            string availabilityState,
            string requiredResourceType = "")
        {
            var sourceType = ResolveType(SourceTypeName);
            var enumType = ResolveType(AvailabilityEnumTypeName);
            var source = Activator.CreateInstance(sourceType);
            Assert.That(source, Is.Not.Null);

            SetFieldValue(source!, "BuildingId", buildingId);
            SetFieldValue(source, "Title", title);
            SetFieldValue(source, "Description", description);
            SetFieldValue(source, "PlacementKind", placementKind);
            SetFieldValue(source, "RequiredResourceType", requiredResourceType);
            SetFieldValue(source, "AvailabilityState", Enum.Parse(enumType, availabilityState));
            SetFieldValue(source, "LockedSuffix", "Locked: requires technology unlock");
            return source;
        }

        private static void AddCost(object source, string key, string displayName, int amount, bool isPoint)
        {
            var costList = GetFieldValue<IList>(source, "Costs");
            costList.Add(CreateMetricSource(key, displayName, amount, isPoint));
        }

        private static void AddRequiredTech(object source, string techName)
        {
            var requiredTechs = GetFieldValue<IList>(source, "RequiredTechNames");
            requiredTechs.Add(techName);
        }

        private static object CreateMetricSource(string key, string displayName, int amount, bool isPoint)
        {
            var metricType = ResolveType(MetricSourceTypeName);
            var ctor = metricType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(string), typeof(string), typeof(int), typeof(UnityEngine.Sprite), typeof(bool) },
                modifiers: null);
            Assert.That(ctor, Is.Not.Null, "缺少 BuildMetricSource 构造函数。");
            return ctor!.Invoke(new object[] { key, displayName, amount, null, isPoint });
        }

        private static T GetFieldValue<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"缺少字段 {fieldName}");
            return (T)field!.GetValue(target);
        }

        private static void SetFieldValue(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"缺少字段 {fieldName}");
            field!.SetValue(target, value);
        }

        private static Type ResolveType(string typeName)
        {
            var type = Type.GetType(typeName);
            Assert.That(type, Is.Not.Null, $"缺少类型 {typeName}");
            return type!;
        }
    }
}
