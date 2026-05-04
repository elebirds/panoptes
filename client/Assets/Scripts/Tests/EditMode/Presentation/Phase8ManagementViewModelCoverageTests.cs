using System.Collections.Generic;
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
            using var contextStore = new RecipeSynthesisContextStore();
            using var viewModel = new RecipeSynthesisViewModel(catalogStore, draftStore, contextStore);

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
            Assert.That(viewModel.Current.Groups[0].Rows[0].Status, Is.EqualTo("Selected"));
            Assert.That(viewModel.Current.Groups[0].Rows[0].ActionLabel, Is.EqualTo("Select"));
        }

        [Test]
        public void RecipeSynthesis_ShouldMarkPreviewOnlyForActiveNode()
        {
            var catalogStore = new StaticCatalogStore();
            var draftStore = new PlanningDraftStore();
            using var contextStore = new RecipeSynthesisContextStore();
            using var viewModel = new RecipeSynthesisViewModel(catalogStore, draftStore, contextStore);

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

            Assert.That(viewModel.Current.Groups[0].Rows[0].Status, Is.EqualTo("Preview valid"));
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
        public void PolicyFocus_ShouldShowNationalPolicyOptionsOnly()
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
                plannedInstitutionPolicyIds: new[] { " archives " }));

            Assert.That(viewModel.Current.Groups, Has.Count.EqualTo(1));
            Assert.That(viewModel.Current.Groups[0].Id, Is.EqualTo("national"));
            Assert.That(viewModel.Current.Groups[0].Rows[0].Status, Is.EqualTo("Planned"));
            Assert.That(viewModel.Current.Groups[0].Rows[0].Id, Is.EqualTo("centralization"));
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
            Assert.That(viewModel.Current.Groups[2].Rows[4].Summary, Is.EqualTo("1"));
        }
    }
}
