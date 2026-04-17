using System.Reflection;
using NUnit.Framework;
using Panoptes.Core.Application.Cache;
using Panoptes.Presentation.UI.Domestic;
using Panoptes.Protocol.V1;
using UnityEngine;

namespace Panoptes.Tests.EditMode.UI
{
    public sealed class RecipeSynthesisPanelTests
    {
        [SetUp]
        public void SetUp()
        {
            DestroySingleton(typeof(GameStateCache));
            DestroySingleton(typeof(PlanningDraftCache));
        }

        [TearDown]
        public void TearDown()
        {
            DestroySingleton(typeof(GameStateCache));
            DestroySingleton(typeof(PlanningDraftCache));
        }

        [Test]
        public void OpenForBuilding_ShouldPreferPlanningDraftSelection_OverLocalCachedSelection()
        {
            var cacheObject = new GameObject("GameStateCache");
            var cache = cacheObject.AddComponent<GameStateCache>();
            SetSingletonInstance(typeof(GameStateCache), cache);
            cache.ApplyGameInit(new MsgGameInit
            {
                GameId = "game-1",
                YourPlayerId = "player-1",
                Phase = "planning",
                MyPlayer = new PlayerView
                {
                    Id = "player-1"
                },
                Nodes =
                {
                    new NodeView
                    {
                        Id = "A2",
                        Pos = new Position { X = 1, Y = 0 },
                        ControllerPlayerId = "player-1",
                        TerritoryOwnerPlayerId = "player-1",
                        BuildingTypeId = "farm"
                    }
                }
            });

            var draft = PlanningDraftCache.EnsureInstance();
            draft.ApplyPlanningSnapshot(new MsgPlanningSnapshot
            {
                Phase = "planning",
                RecipeSelections =
                {
                    new QueuedRecipeSelection
                    {
                        NodeId = "A2",
                        RecipeId = "server_recipe"
                    }
                }
            });

            var panelObject = new GameObject("RecipeSynthesisPanel");
            var panel = panelObject.AddComponent<RecipeSynthesisPanel>();
            SeedLocalSelection(panel, "A2", "local_recipe");

            panel.OpenForBuilding("A2", "farm", "player-1");

            Assert.That(GetPrivateField<string>(panel, "_selectedRecipeId"), Is.EqualTo("server_recipe"),
                "配方面板应优先展示服务端草稿，而不是本地缓存残留。");

            Object.DestroyImmediate(panelObject);
            Object.DestroyImmediate(cacheObject);
            Object.DestroyImmediate(draft.gameObject);
        }

        private static void SeedLocalSelection(RecipeSynthesisPanel panel, string nodeId, string recipeId)
        {
            var quantityByNodeId = GetPrivateField<System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, int>>>(panel, "_quantityByNodeId");
            quantityByNodeId[nodeId] = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase)
            {
                [recipeId] = 1
            };

            var selectedRecipeByNodeId = GetPrivateField<System.Collections.Generic.Dictionary<string, string>>(panel, "_selectedRecipeByNodeId");
            selectedRecipeByNodeId[nodeId] = recipeId;
        }

        private static T GetPrivateField<T>(object instance, string fieldName)
        {
            var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"缺少字段 {fieldName}");
            return (T)field!.GetValue(instance);
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
