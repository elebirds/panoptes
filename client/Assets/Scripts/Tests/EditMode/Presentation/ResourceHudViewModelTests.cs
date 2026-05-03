using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using Panoptes.Presentation.ViewModels;
using R3;

namespace Panoptes.Tests.EditMode.Presentation
{
    public sealed class ResourceHudViewModelTests
    {
        [Test]
        public void Current_ShouldOrderVisibleCatalogResourcesThenPoints()
        {
            var gameStateStore = new GameStateStore();
            var staticCatalogStore = new StaticCatalogStore();
            using var viewModel = new ResourceHudViewModel(gameStateStore, staticCatalogStore);

            gameStateStore.Replace(new GameStateStoreState(
                myResources: new ResourceDto
                {
                    Food = 7,
                    Ore = 3,
                    Wood = 5,
                    IndustryOutput = 2
                }));
            staticCatalogStore.Replace(new StaticCatalogState(
                resources: new[]
                {
                    Entry("wood", "wood_icon", 20, true),
                    Entry("ore", "ore_icon", 10, true),
                    Entry("food", "food_icon", 30, false)
                }.ToDictionary(entry => entry.Key),
                points: new[]
                {
                    Entry("industry_output", "industry_icon", 5, true)
                }.ToDictionary(entry => entry.Key)));

            var rows = viewModel.Current.Rows;

            Assert.That(rows.Select(row => row.Key), Is.EqualTo(new[] { "ore", "wood", "industry_output" }));
            Assert.That(rows.Select(row => row.Amount), Is.EqualTo(new[] { 3, 5, 2 }));
            Assert.That(rows[0].IconKey, Is.EqualTo("ore_icon"));
            Assert.That(rows[2].IsPoint, Is.True);
        }

        [Test]
        public void Current_ShouldUseGenericResourceAndPointAmountsForCatalogRows()
        {
            var gameStateStore = new GameStateStore();
            var staticCatalogStore = new StaticCatalogStore();
            using var viewModel = new ResourceHudViewModel(gameStateStore, staticCatalogStore);

            gameStateStore.Replace(new GameStateStoreState(
                myResources: new ResourceDto
                {
                    Food = 99,
                    IndustryOutput = 99,
                    ResourceAmounts = new Dictionary<string, int>
                    {
                        ["coal"] = 12
                    },
                    PointAmounts = new Dictionary<string, int>
                    {
                        ["logistics_capacity"] = 8
                    }
                }));
            staticCatalogStore.Replace(new StaticCatalogState(
                resources: new[]
                {
                    Entry("coal", "coal_icon", 10, true),
                    Entry("food", "food_icon", 20, true)
                }.ToDictionary(entry => entry.Key),
                points: new[]
                {
                    Entry("logistics_capacity", "logistics_icon", 5, true),
                    Entry("industry_output", "industry_icon", 15, true)
                }.ToDictionary(entry => entry.Key)));

            var rows = viewModel.Current.Rows;

            Assert.That(rows.Select(row => row.Key), Is.EqualTo(new[]
            {
                "coal",
                "food",
                "logistics_capacity",
                "industry_output"
            }));
            Assert.That(rows.Select(row => row.Amount), Is.EqualTo(new[] { 12, 99, 8, 99 }));
            Assert.That(rows[2].IsPoint, Is.True);
        }

        [Test]
        public void Current_ShouldFallbackToResourceAmountsWhenCatalogHasNoHudRows()
        {
            var gameStateStore = new GameStateStore();
            var staticCatalogStore = new StaticCatalogStore();
            using var viewModel = new ResourceHudViewModel(gameStateStore, staticCatalogStore);

            gameStateStore.Replace(new GameStateStoreState(
                myResources: new ResourceDto
                {
                    Food = 7,
                    Ore = 3,
                    Wood = 5,
                    IndustryOutput = 2
                }));

            var rows = viewModel.Current.Rows;

            Assert.That(rows.Select(row => row.Key), Is.EqualTo(new[] { "food", "ore", "wood", "industry_output" }));
            Assert.That(rows.Select(row => row.Amount), Is.EqualTo(new[] { 7, 3, 5, 2 }));
            Assert.That(rows.Last().IsPoint, Is.True);
        }

        [Test]
        public void SetIncludePoints_ShouldRemovePointRowsAndPublish()
        {
            var gameStateStore = new GameStateStore();
            var staticCatalogStore = new StaticCatalogStore();
            using var viewModel = new ResourceHudViewModel(gameStateStore, staticCatalogStore);
            var publishCount = 0;
            using var subscription = viewModel.State.Subscribe(_ => publishCount++);

            gameStateStore.Replace(new GameStateStoreState(
                myResources: new ResourceDto
                {
                    Food = 1,
                    IndustryOutput = 9
                }));

            viewModel.SetIncludePoints(false);

            Assert.That(viewModel.Current.Rows.Select(row => row.Key), Is.EqualTo(new[] { "food", "ore", "wood" }));
            Assert.That(viewModel.Current.Rows.Any(row => row.IsPoint), Is.False);
            Assert.That(publishCount, Is.GreaterThanOrEqualTo(2));
        }

        private static CatalogHudEntryDto Entry(string key, string iconKey, int sortOrder, bool visible)
        {
            return new CatalogHudEntryDto
            {
                Key = key,
                IconKey = iconKey,
                SortOrder = sortOrder,
                VisibleInHud = visible
            };
        }
    }
}
