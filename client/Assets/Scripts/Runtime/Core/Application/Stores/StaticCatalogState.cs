using System.Collections.Generic;
using Panoptes.Core.Domain;

namespace Panoptes.Core.Application.Stores
{
    public sealed class StaticCatalogState
    {
        public StaticCatalogState(
            IReadOnlyDictionary<string, CatalogHudEntryDto> resources = null,
            IReadOnlyDictionary<string, CatalogHudEntryDto> points = null,
            IReadOnlyDictionary<string, CatalogBuildingDto> buildings = null,
            IReadOnlyDictionary<string, CatalogRecipeDto> recipes = null,
            IReadOnlyDictionary<string, CatalogTechnologyDto> technologies = null,
            IReadOnlyDictionary<string, CatalogPolicyDto> policies = null,
            IReadOnlyDictionary<string, CatalogUnitDto> units = null,
            CatalogMapRuntimeBundleDto defaultMap = null)
        {
            Resources = StoreSnapshotCloner.CloneCatalogHudEntries(resources);
            Points = StoreSnapshotCloner.CloneCatalogHudEntries(points);
            Buildings = StoreSnapshotCloner.CloneCatalogBuildings(buildings);
            Recipes = StoreSnapshotCloner.CloneCatalogRecipes(recipes);
            Technologies = StoreSnapshotCloner.CloneCatalogTechnologies(technologies);
            Policies = StoreSnapshotCloner.CloneCatalogPolicies(policies);
            Units = StoreSnapshotCloner.CloneCatalogUnits(units);
            DefaultMap = StoreSnapshotCloner.CloneCatalogMapRuntimeBundle(defaultMap);
        }

        public IReadOnlyDictionary<string, CatalogHudEntryDto> Resources { get; }
        public IReadOnlyDictionary<string, CatalogHudEntryDto> Points { get; }
        public IReadOnlyDictionary<string, CatalogBuildingDto> Buildings { get; }
        public IReadOnlyDictionary<string, CatalogRecipeDto> Recipes { get; }
        public IReadOnlyDictionary<string, CatalogTechnologyDto> Technologies { get; }
        public IReadOnlyDictionary<string, CatalogPolicyDto> Policies { get; }
        public IReadOnlyDictionary<string, CatalogUnitDto> Units { get; }
        public CatalogMapRuntimeBundleDto DefaultMap { get; }

        internal StaticCatalogState Clone()
        {
            return new StaticCatalogState(Resources, Points, Buildings, Recipes, Technologies, Policies, Units, DefaultMap);
        }
    }
}
