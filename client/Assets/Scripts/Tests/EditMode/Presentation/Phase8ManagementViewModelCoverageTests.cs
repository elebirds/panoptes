using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using Panoptes.Presentation.ViewModels;

namespace Panoptes.Tests.EditMode.Presentation
{
    public sealed class Phase8ManagementViewModelCoverageTests
    {
        [Test]
        public void RecipeSynthesis_ShouldGroupRecipesAndMarkSelectedRows()
        {
            var catalogStore = new StaticCatalogStore();
            var draftStore = new PlanningDraftStore();
            var gameStateStore = new GameStateStore();
            using var contextStore = new RecipeSynthesisContextStore();
            using var viewModel = new RecipeSynthesisViewModel(catalogStore, draftStore, contextStore, gameStateStore);

            catalogStore.Replace(new StaticCatalogState(recipes: new Dictionary<string, CatalogRecipeDto>
            {
                ["grain"] = new CatalogRecipeDto { Id = "grain", Name = "Mill Grain", BuildingId = "mill", WorkAmount = 3, BaseProgress = 1 },
                ["ore"] = new CatalogRecipeDto { Id = "ore", Name = "Smelt Ore", BuildingId = "foundry", WorkAmount = 5, BaseProgress = 2 }
            }));
            Assert.That(viewModel.Current.Groups, Is.Empty);

            draftStore.Replace(new PlanningDraftState(recipeSelections: new[]
            {
                new QueuedRecipeSelectionDto { RecipeId = "ore", NodeId = "other" },
                new QueuedRecipeSelectionDto { RecipeId = " grain ", NodeId = "n1" }
            }));
            contextStore.SetContext(" n1 ", " mill ", "player-1");

            Assert.That(viewModel.Current.Groups[0].Id, Is.EqualTo("mill"));
            Assert.That(viewModel.Current.Groups[0].Rows, Has.Count.EqualTo(1));
            Assert.That(viewModel.Current.Groups[0].Rows[0].Status, Is.EqualTo("已选择"));
            Assert.That(viewModel.Current.Groups[0].Rows[0].ActionLabel, Is.EqualTo("取消选择"));
            Assert.That(viewModel.Current.Groups[0].Rows[0].EmptyCostsLabel, Is.EqualTo("无消耗"));
        }

        [Test]
        public void RecipeSynthesis_ShouldMarkOperationSelectedRecipeWhenDraftIsEmpty()
        {
            var catalogStore = new StaticCatalogStore();
            var draftStore = new PlanningDraftStore();
            var gameStateStore = new GameStateStore();
            using var contextStore = new RecipeSynthesisContextStore();
            using var viewModel = new RecipeSynthesisViewModel(catalogStore, draftStore, contextStore, gameStateStore);

            catalogStore.Replace(new StaticCatalogState(recipes: new Dictionary<string, CatalogRecipeDto>
            {
                ["grain"] = new CatalogRecipeDto { Id = "grain", Name = "Mill Grain", BuildingId = "mill" }
            }));
            gameStateStore.Replace(new GameStateStoreState(nodes: new Dictionary<string, NodeDto>
            {
                ["n1"] = new NodeDto { Id = "n1", OperationSelectedRecipeId = " grain " }
            }));
            contextStore.SetContext(" n1 ", " mill ", "player-1");

            Assert.That(viewModel.Current.Groups[0].Rows[0].Status, Is.EqualTo("已选择"));
            Assert.That(viewModel.Current.Groups[0].Rows[0].ActionLabel, Is.EqualTo("\u53d6\u6d88\u9009\u62e9"));
        }

        [Test]
        public void RecipeSynthesis_ShouldMatchSelectedRecipeWithUppercaseNodeIds()
        {
            var catalogStore = new StaticCatalogStore();
            var draftStore = new PlanningDraftStore();
            var gameStateStore = new GameStateStore();
            using var contextStore = new RecipeSynthesisContextStore();
            using var viewModel = new RecipeSynthesisViewModel(catalogStore, draftStore, contextStore, gameStateStore);

            catalogStore.Replace(new StaticCatalogState(recipes: new Dictionary<string, CatalogRecipeDto>
            {
                ["grain"] = new CatalogRecipeDto { Id = "grain", Name = "Mill Grain", BuildingId = "mill" }
            }));
            draftStore.Replace(new PlanningDraftState(recipeSelections: new[]
            {
                new QueuedRecipeSelectionDto { NodeId = "B1", RecipeId = "grain" }
            }));
            contextStore.SetContext("B1", "mill", "player-1");

            Assert.That(viewModel.Current.Groups[0].Rows[0].Status, Is.EqualTo("已选择"));
            Assert.That(viewModel.Current.Groups[0].Rows[0].ActionLabel, Is.EqualTo("\u53d6\u6d88\u9009\u62e9"));
        }

        [Test]
        public void RecipeSynthesis_ShouldHideActiveSelectionWhenCancellationIsQueued()
        {
            var catalogStore = new StaticCatalogStore();
            var draftStore = new PlanningDraftStore();
            var gameStateStore = new GameStateStore();
            using var contextStore = new RecipeSynthesisContextStore();
            using var viewModel = new RecipeSynthesisViewModel(catalogStore, draftStore, contextStore, gameStateStore);

            catalogStore.Replace(new StaticCatalogState(recipes: new Dictionary<string, CatalogRecipeDto>
            {
                ["grain"] = new CatalogRecipeDto { Id = "grain", Name = "Mill Grain", BuildingId = "mill" }
            }));
            gameStateStore.Replace(new GameStateStoreState(nodes: new Dictionary<string, NodeDto>
            {
                ["n1"] = new NodeDto { Id = "n1", OperationSelectedRecipeId = "grain" }
            }));
            draftStore.Replace(new PlanningDraftState(recipeSelections: new[]
            {
                new QueuedRecipeSelectionDto { NodeId = "n1", RecipeId = string.Empty }
            }));
            contextStore.SetContext("n1", "mill", "player-1");

            Assert.That(viewModel.Current.Groups[0].Rows[0].Status, Is.EqualTo(string.Empty));
            Assert.That(viewModel.Current.Groups[0].Rows[0].ActionLabel, Is.EqualTo("\u9009\u62e9"));
        }

        [Test]
        public void RecipeSynthesis_ShouldMarkPreviewOnlyForActiveNode()
        {
            var catalogStore = new StaticCatalogStore();
            var draftStore = new PlanningDraftStore();
            var gameStateStore = new GameStateStore();
            using var contextStore = new RecipeSynthesisContextStore();
            using var viewModel = new RecipeSynthesisViewModel(catalogStore, draftStore, contextStore, gameStateStore);

            catalogStore.Replace(new StaticCatalogState(recipes: new Dictionary<string, CatalogRecipeDto>
            {
                ["grain"] = new CatalogRecipeDto { Id = "grain", Name = "Mill Grain", BuildingId = "mill" }
            }));
            contextStore.SetContext("n1", "mill", "player-1");

            draftStore.Replace(new PlanningDraftState(currentRecipePreview: new RecipePreviewDto
            {
                NodeId = "other",
                RecipeId = "grain",
                Valid = true
            }));

            Assert.That(viewModel.Current.Groups[0].Rows[0].Status, Is.EqualTo(string.Empty));

            draftStore.Replace(new PlanningDraftState(currentRecipePreview: new RecipePreviewDto
            {
                NodeId = "n1",
                RecipeId = "grain",
                Valid = true
            }));

            Assert.That(viewModel.Current.Groups[0].Rows[0].Status, Is.EqualTo("预览可用"));
        }

        [Test]
        public void RecipeSynthesis_ShouldDimTechnologyLockedRecipes()
        {
            var catalogStore = new StaticCatalogStore();
            var draftStore = new PlanningDraftStore();
            var gameStateStore = new GameStateStore();
            using var contextStore = new RecipeSynthesisContextStore();
            using var viewModel = new RecipeSynthesisViewModel(catalogStore, draftStore, contextStore, gameStateStore);

            catalogStore.Replace(new StaticCatalogState(
                recipes: new Dictionary<string, CatalogRecipeDto>
                {
                    ["mine_ore"] = new CatalogRecipeDto { Id = "mine_ore", Name = "Mine Ore", BuildingId = "mine" }
                },
                technologies: new Dictionary<string, CatalogTechnologyDto>
                {
                    ["mining_survey"] = new CatalogTechnologyDto
                    {
                        Id = "mining_survey",
                        ExplicitEffects = new List<CatalogTechnologyEffectDto>
                        {
                            new() { Type = "unlock_recipe", TargetId = "mine_ore" }
                        }
                    }
                }));
            contextStore.SetContext("n1", "mine", "player-1");

            var row = viewModel.Current.Groups[0].Rows[0];
            Assert.That(row.Status, Is.EqualTo("科技未解锁"));
            Assert.That(row.ActionLabel, Is.Empty);

            gameStateStore.Replace(new GameStateStoreState(researchState: new TechnologyDto
            {
                ActiveTechnologyIds = new List<string> { "mining_survey" }
            }));

            Assert.That(viewModel.Current.Groups[0].Rows[0].Status, Is.Empty);
            Assert.That(viewModel.Current.Groups[0].Rows[0].ActionLabel, Is.EqualTo("选择"));
        }

        [Test]
        public void MinisterReport_ShouldProjectInteractiveDrafts()
        {
            var draftStore = new PlanningDraftStore();
            using var viewModel = new MinisterReportViewModel(draftStore);

            draftStore.Replace(new PlanningDraftState(ministerDrafts: new[]
            {
                new MinisterDraftDto
                {
                    DraftId = "d1",
                    MinisterRole = "domestic",
                    Title = "Expand farms",
                    Summary = "Food first",
                    Rationale = "Winter pressure",
                    Status = "pending",
                    Available = true
                }
            }));

            Assert.That(viewModel.Current.Groups[0].Id, Is.EqualTo("domestic"));
            Assert.That(viewModel.Current.Groups[0].Rows[0].ActionLabel, Is.EqualTo("Review"));
        }

        [Test]
        public void MinisterReport_ShouldDescribeOperationDraftBatches()
        {
            var draftStore = new PlanningDraftStore();
            using var viewModel = new MinisterReportViewModel(draftStore);

            draftStore.Replace(new PlanningDraftState(ministerDrafts: new[]
            {
                new MinisterDraftDto
                {
                    DraftId = "op-1",
                    MinisterRole = "military",
                    Kind = "operation",
                    Title = "北线行动",
                    Summary = "压迫敌军前线。",
                    Rationale = "敌军补给不足。",
                    Status = "pending",
                    Available = true,
                    Objective = "夺取北部渡口",
                    OperationCommands = new[]
                    {
                        new MinisterOperationCommandDto
                        {
                            Label = "弓兵前压至 N2",
                            Kind = "unit_order",
                            UnitId = "u-archer",
                            Action = "move",
                            TargetNodeId = "N2"
                        },
                        new MinisterOperationCommandDto
                        {
                            Kind = "build",
                            NodeId = "V3",
                            BuildingTypeId = "watchtower"
                        }
                    }
                }
            }));

            var messageText = viewModel.Current.Messages[0].Text;
            StringAssert.Contains("目标：夺取北部渡口", messageText);
            StringAssert.Contains("行动批次：", messageText);
            StringAssert.Contains("弓兵前压至 N2", messageText);
            StringAssert.Contains("建造 watchtower V3", messageText);
            StringAssert.Contains("目标：夺取北部渡口", viewModel.Current.Groups[0].Rows[0].Detail);
        }

        [Test]
        public void PolicyFocus_ShouldShowOnlyNationalPolicies()
        {
            var catalogStore = new StaticCatalogStore();
            var draftStore = new PlanningDraftStore();
            using var viewModel = new PolicyFocusViewModel(catalogStore, draftStore);

            catalogStore.Replace(new StaticCatalogState(policies: new Dictionary<string, CatalogPolicyDto>
            {
                ["centralization"] = new CatalogPolicyDto { Id = "centralization", Name = "Centralization", Layer = "national_focus" },
                ["archives"] = new CatalogPolicyDto { Id = "archives", Name = "Archives", Layer = "institution" }
            }));
            draftStore.Replace(new PlanningDraftState(
                plannedNationalPolicyId: " centralization ",
                plannedInstitutionIds: new[] { " archives " }));

            Assert.That(viewModel.Current.Groups, Has.Count.EqualTo(1));
            Assert.That(viewModel.Current.Groups[0].Id, Is.EqualTo("national"));
            Assert.That(viewModel.Current.Groups[0].Rows, Has.Count.EqualTo(1));
            Assert.That(viewModel.Current.Groups[0].Rows[0].Status, Is.EqualTo("已规划"));
            Assert.That(viewModel.Current.Groups[0].Rows[0].Id, Is.EqualTo("centralization"));
        }

        [Test]
        public void InstitutionViewModel_ShouldGroupInstitutionsByCategory()
        {
            var catalogStore = new StaticCatalogStore();
            var draftStore = new PlanningDraftStore();
            using var viewModel = new InstitutionViewModel(catalogStore, draftStore);

            catalogStore.Replace(new StaticCatalogState(
                institutionCategories: new Dictionary<string, CatalogInstitutionCategoryDto>
                {
                    ["power"] = new CatalogInstitutionCategoryDto { Id = "power", Name = "Power", SortOrder = 10 },
                    ["economy"] = new CatalogInstitutionCategoryDto { Id = "economy", Name = "Economy", SortOrder = 20 }
                },
                institutions: new Dictionary<string, CatalogInstitutionDto>
                {
                    ["central_archives"] = new CatalogInstitutionDto { Id = "central_archives", Name = "Central Archives", Category = "power" },
                    ["free_market"] = new CatalogInstitutionDto { Id = "free_market", Name = "Free Market", Category = "economy" }
                }));
            draftStore.Replace(new PlanningDraftState(
                plannedInstitutionIds: new[] { " free_market " }));

            Assert.That(viewModel.Current.Title, Is.EqualTo("制度"));
            Assert.That(viewModel.Current.Groups, Has.Count.EqualTo(2));
            Assert.That(viewModel.Current.Groups[0].Id, Is.EqualTo("power"));
            Assert.That(viewModel.Current.Groups[1].Id, Is.EqualTo("economy"));
            Assert.That(viewModel.Current.Groups[1].Rows[0].Status, Is.EqualTo("已规划"));
            Assert.That(viewModel.BuildLoadoutForSelection("central_archives"), Is.EquivalentTo(new[] { "free_market", "central_archives" }));
        }

        [Test]
        public void PolicyFocus_ShouldLocalizeModifierEffectSummaryFromCatalog()
        {
            var catalogStore = new StaticCatalogStore();
            var draftStore = new PlanningDraftStore();
            using var viewModel = new PolicyFocusViewModel(catalogStore, draftStore);

            catalogStore.Replace(new StaticCatalogState(
                recipes: new Dictionary<string, CatalogRecipeDto>
                {
                    ["farm_food"] = new CatalogRecipeDto { Id = "farm_food", Name = "基础农耕" }
                },
                policies: new Dictionary<string, CatalogPolicyDto>
                {
                    ["recovery"] = new CatalogPolicyDto
                    {
                        Id = "recovery",
                        Name = "恢复",
                        Layer = "national_focus",
                        ModifierEffects = new List<CatalogPolicyModifierEffectDto>
                        {
                            new CatalogPolicyModifierEffectDto
                            {
                                Trigger = "recipe.resource_output",
                                TargetId = "farm_food",
                                ResourceKey = "food",
                                Value = 1
                            }
                        }
                    }
                }));

            var summary = viewModel.Current.Groups[0].Rows[0].Summary;
            Assert.That(summary, Is.EqualTo("基础农耕 粮食产出 +1"));
            Assert.That(summary, Does.Not.Contain("farm food"));
            Assert.That(summary, Does.Not.Contain("food产出"));
        }

        [Test]
        public void NationalLedger_ShouldProjectGameAndCatalogCounters()
        {
            var gameStateStore = new GameStateStore();
            var catalogStore = new StaticCatalogStore();
            using var viewModel = new NationalLedgerViewModel(gameStateStore, catalogStore);

            gameStateStore.Replace(new GameStateStoreState(
                turn: 7,
                phase: "planning",
                mapWidth: 9,
                mapHeight: 5,
                tokensLeft: 2,
                myResources: new ResourceDto { Food = 11, Wood = 3, Ore = 1, IndustryOutput = 4 }));
            catalogStore.Replace(new StaticCatalogState(units: new Dictionary<string, CatalogUnitDto>
            {
                ["scout"] = new CatalogUnitDto { Id = "scout", Name = "Scout" }
            }));

            Assert.That(viewModel.Current.Groups[0].Rows[0].Summary, Is.EqualTo("7"));
            Assert.That(viewModel.Current.Groups[1].Rows[0].Summary, Is.EqualTo("11"));

            var catalogRows = viewModel.Current.Groups[2].Rows;
            Assert.That(catalogRows.Single(row => row.Id == "institutions").Summary, Is.EqualTo("0"));
            Assert.That(catalogRows.Single(row => row.Id == "units").Summary, Is.EqualTo("1"));
        }
    }
}
