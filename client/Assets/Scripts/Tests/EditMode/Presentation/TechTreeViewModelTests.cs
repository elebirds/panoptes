using System.Collections.Generic;
using NUnit.Framework;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using Panoptes.Presentation.ViewModels;

namespace Panoptes.Tests.EditMode.Presentation
{
    public sealed class TechTreeViewModelTests
    {
        [Test]
        public void Current_ShouldGroupTechnologiesByBranchAndMarkPlannedResearch()
        {
            var catalogStore = new StaticCatalogStore();
            var draftStore = new PlanningDraftStore();
            using var viewModel = new TechTreeViewModel(catalogStore, draftStore);

            catalogStore.Replace(new StaticCatalogState(technologies: new Dictionary<string, CatalogTechnologyDto>
            {
                ["irrigation"] = new CatalogTechnologyDto
                {
                    Id = "irrigation",
                    Name = "Irrigation",
                    Branch = "economy",
                    Tier = 1,
                    ResearchCost = 4,
                    SortOrder = 2
                },
                ["bronze"] = new CatalogTechnologyDto
                {
                    Id = "bronze",
                    Name = "Bronze Working",
                    Branch = "military",
                    Tier = 1,
                    ResearchCost = 5,
                    SortOrder = 1
                }
            }));
            draftStore.Replace(new PlanningDraftState(plannedResearchTargetTechnologyId: " irrigation "));

            var state = viewModel.Current;

            Assert.That(state.Title, Is.EqualTo("Tech Tree"));
            Assert.That(state.Groups, Has.Count.EqualTo(2));
            Assert.That(state.Groups[0].Id, Is.EqualTo("economy"));
            Assert.That(state.Groups[0].Rows[0].Id, Is.EqualTo("irrigation"));
            Assert.That(state.Groups[0].Rows[0].Status, Is.EqualTo("Planned research"));
            Assert.That(state.Groups[0].Rows[0].ActionLabel, Is.EqualTo("Research"));
        }
    }
}
