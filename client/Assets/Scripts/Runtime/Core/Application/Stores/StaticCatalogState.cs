using System.Collections.Generic;
using Panoptes.Core.Domain;

namespace Panoptes.Core.Application.Stores
{
    public sealed class StaticCatalogState
    {
        public StaticCatalogState(
            IReadOnlyDictionary<string, CatalogBuildingDto> buildings = null,
            IReadOnlyDictionary<string, CatalogRecipeDto> recipes = null,
            IReadOnlyDictionary<string, CatalogTechnologyDto> technologies = null,
            IReadOnlyDictionary<string, CatalogPolicyDto> policies = null,
            IReadOnlyDictionary<string, CatalogUnitDto> units = null)
        {
            Buildings = StoreSnapshotCloner.CloneCatalogBuildings(buildings);
            Recipes = StoreSnapshotCloner.CloneCatalogRecipes(recipes);
            Technologies = StoreSnapshotCloner.CloneCatalogTechnologies(technologies);
            Policies = StoreSnapshotCloner.CloneCatalogPolicies(policies);
            Units = StoreSnapshotCloner.CloneCatalogUnits(units);
        }

        public IReadOnlyDictionary<string, CatalogBuildingDto> Buildings { get; }
        public IReadOnlyDictionary<string, CatalogRecipeDto> Recipes { get; }
        public IReadOnlyDictionary<string, CatalogTechnologyDto> Technologies { get; }
        public IReadOnlyDictionary<string, CatalogPolicyDto> Policies { get; }
        public IReadOnlyDictionary<string, CatalogUnitDto> Units { get; }

        internal StaticCatalogState Clone()
        {
            return new StaticCatalogState(Buildings, Recipes, Technologies, Policies, Units);
        }
    }
}
