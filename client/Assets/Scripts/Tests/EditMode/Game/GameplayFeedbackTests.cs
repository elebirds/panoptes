using System.Reflection;
using NUnit.Framework;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Feedback;
using Panoptes.Protocol.V1;
using UnityEngine;

namespace Panoptes.Tests.EditMode.Game
{
    public sealed class GameplayFeedbackTests
    {
        [SetUp]
        public void SetUp()
        {
            DestroySingleton(typeof(PlanningDraftCache));
        }

        [TearDown]
        public void TearDown()
        {
            DestroySingleton(typeof(PlanningDraftCache));
        }

        [Test]
        public void GameplayFeedbackText_ShouldPreferServerMessageThenFallbackThenRawCode()
        {
            Assert.That(GameplayFeedbackText.ResolveMessage("服务端提示", "invalid_request"), Is.EqualTo("服务端提示"));
            Assert.That(GameplayFeedbackText.ResolveMessage(string.Empty, "resource_type_mismatch"), Is.EqualTo("资源点类型不匹配"));
            Assert.That(GameplayFeedbackText.ResolveMessage(string.Empty, "unmapped_code"), Is.EqualTo("unmapped_code"));
        }

        [Test]
        public void PlanningDraftCache_ShouldDiscardStaleBuildPreviewResponses()
        {
            var cache = CreatePlanningDraftCache();
            cache.TrackBuildPreviewRequest("build-preview-1", "A1", "farm", "city-1");
            cache.TrackBuildPreviewRequest("build-preview-2", "A2", "smelter", "city-1");

            cache.ApplyBuildPreviewResponse(new MsgBuildStructurePreviewResponse
            {
                RequestId = "build-preview-1",
                NodeId = "A1",
                BuildingTypeId = "farm",
                CityId = "city-1",
                Valid = false,
                ErrorCode = "outside_territory",
                FeedbackMessage = "旧响应不应覆盖当前预览"
            });

            Assert.That(cache.CurrentBuildPreview, Is.Not.Null);
            Assert.That(cache.CurrentBuildPreview.RequestId, Is.EqualTo("build-preview-2"));
            Assert.That(cache.CurrentBuildPreview.Message, Is.EqualTo("检查中"));

            cache.ApplyBuildPreviewResponse(new MsgBuildStructurePreviewResponse
            {
                RequestId = "build-preview-2",
                NodeId = "A2",
                BuildingTypeId = "smelter",
                CityId = "city-1",
                Valid = false,
                ErrorCode = "terrain_not_buildable",
                FeedbackMessage = "当前地形不能建造冶炼厂",
                FeedbackDetails =
                {
                    new FeedbackDetail { Key = "node_id", Value = "A2" }
                }
            });

            Assert.That(cache.CurrentBuildPreview.RequestId, Is.EqualTo("build-preview-2"));
            Assert.That(cache.CurrentBuildPreview.Message, Is.EqualTo("当前地形不能建造冶炼厂"));
            Assert.That(cache.CurrentBuildPreview.ErrorCode, Is.EqualTo("terrain_not_buildable"));
            Assert.That(cache.CurrentBuildPreview.Details["node_id"], Is.EqualTo("A2"));
        }

        [Test]
        public void PlanningDraftCache_ShouldDiscardStaleRecipePreviewResponses()
        {
            var cache = CreatePlanningDraftCache();
            cache.TrackRecipePreviewRequest("recipe-preview-1", "A2", "smelt_iron");
            cache.TrackRecipePreviewRequest("recipe-preview-2", "A2", "forge_tools");

            cache.ApplyRecipePreviewResponse(new MsgSetBuildingRecipePreviewResponse
            {
                RequestId = "recipe-preview-1",
                NodeId = "A2",
                RecipeId = "smelt_iron",
                Valid = false,
                ErrorCode = "invalid_recipe_selection",
                FeedbackMessage = "旧配方响应不应回刷"
            });

            Assert.That(cache.CurrentRecipePreview, Is.Not.Null);
            Assert.That(cache.CurrentRecipePreview.RequestId, Is.EqualTo("recipe-preview-2"));
            Assert.That(cache.CurrentRecipePreview.Message, Is.EqualTo("检查中"));

            cache.ApplyRecipePreviewResponse(new MsgSetBuildingRecipePreviewResponse
            {
                RequestId = "recipe-preview-2",
                NodeId = "A2",
                RecipeId = "forge_tools",
                Valid = true,
                FeedbackMessage = "当前可切换到该配方",
                FeedbackDetails =
                {
                    new FeedbackDetail { Key = "recipe_id", Value = "forge_tools" }
                }
            });

            Assert.That(cache.CurrentRecipePreview.RequestId, Is.EqualTo("recipe-preview-2"));
            Assert.That(cache.CurrentRecipePreview.Message, Is.EqualTo("当前可切换到该配方"));
            Assert.That(cache.CurrentRecipePreview.Valid, Is.True);
            Assert.That(cache.CurrentRecipePreview.Details["recipe_id"], Is.EqualTo("forge_tools"));
        }

        private static PlanningDraftCache CreatePlanningDraftCache()
        {
            return new GameObject("PlanningDraftCache").AddComponent<PlanningDraftCache>();
        }

        private static void DestroySingleton(System.Type type)
        {
            var existing = Object.FindAnyObjectByType(type) as Component;
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            SetSingletonInstance(type, null);
        }

        private static void SetSingletonInstance(System.Type type, object value)
        {
            var field = type.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            field?.SetValue(null, value);
        }
    }
}
