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
            IReadOnlyDictionary<string, CatalogInstitutionCategoryDto> institutionCategories = null,
            IReadOnlyDictionary<string, CatalogInstitutionDto> institutions = null,
            IReadOnlyDictionary<string, CatalogUnitDto> units = null,
            IReadOnlyDictionary<string, CatalogEmoteSeriesDto> emoteSeries = null,
            IReadOnlyDictionary<string, CatalogEmoteDto> emotes = null,
            CatalogMapRuntimeBundleDto defaultMap = null)
        {
            Resources = StoreSnapshotCloner.CloneCatalogHudEntries(resources);
            Points = StoreSnapshotCloner.CloneCatalogHudEntries(points);
            Buildings = StoreSnapshotCloner.CloneCatalogBuildings(buildings);
            Recipes = StoreSnapshotCloner.CloneCatalogRecipes(recipes);
            Technologies = StoreSnapshotCloner.CloneCatalogTechnologies(technologies);
            Policies = StoreSnapshotCloner.CloneCatalogPolicies(policies);
            InstitutionCategories = StoreSnapshotCloner.CloneCatalogInstitutionCategories(institutionCategories);
            Institutions = StoreSnapshotCloner.CloneCatalogInstitutions(institutions);
            Units = StoreSnapshotCloner.CloneCatalogUnits(units);
            EmoteSeries = StoreSnapshotCloner.CloneCatalogEmoteSeries(emoteSeries);
            Emotes = StoreSnapshotCloner.CloneCatalogEmotes(emotes);
            DefaultMap = StoreSnapshotCloner.CloneCatalogMapRuntimeBundle(defaultMap);
        }

        public IReadOnlyDictionary<string, CatalogHudEntryDto> Resources { get; }
        public IReadOnlyDictionary<string, CatalogHudEntryDto> Points { get; }
        public IReadOnlyDictionary<string, CatalogBuildingDto> Buildings { get; }
        public IReadOnlyDictionary<string, CatalogRecipeDto> Recipes { get; }
        public IReadOnlyDictionary<string, CatalogTechnologyDto> Technologies { get; }
        public IReadOnlyDictionary<string, CatalogPolicyDto> Policies { get; }
        public IReadOnlyDictionary<string, CatalogInstitutionCategoryDto> InstitutionCategories { get; }
        public IReadOnlyDictionary<string, CatalogInstitutionDto> Institutions { get; }
        public IReadOnlyDictionary<string, CatalogUnitDto> Units { get; }
        public IReadOnlyDictionary<string, CatalogEmoteSeriesDto> EmoteSeries { get; }
        public IReadOnlyDictionary<string, CatalogEmoteDto> Emotes { get; }
        public CatalogMapRuntimeBundleDto DefaultMap { get; }

        internal StaticCatalogState Clone()
        {
            return new StaticCatalogState(
                Resources,
                Points,
                Buildings,
                Recipes,
                Technologies,
                Policies,
                InstitutionCategories,
                Institutions,
                Units,
                EmoteSeries,
                Emotes,
                DefaultMap);
        }
    }
}
