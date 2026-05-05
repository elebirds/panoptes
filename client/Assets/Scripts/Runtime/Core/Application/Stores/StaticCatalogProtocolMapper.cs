using System;
using System.Collections.Generic;
using System.Linq;
using Panoptes.Core.Domain;
using Panoptes.Protocol.V1;

namespace Panoptes.Core.Application.Stores
{
    public static class StaticCatalogProtocolMapper
    {
        public static StaticCatalogState ToState(StaticCatalogSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return new StaticCatalogState();
            }

            return new StaticCatalogState(
                resources: MapResources(snapshot.Resources),
                points: MapPoints(snapshot.Points),
                buildings: MapBuildings(snapshot.Buildings),
                recipes: MapRecipes(snapshot.Recipes),
                technologies: MapTechnologies(snapshot.Technologies),
                policies: MapPolicies(snapshot.Policies),
                units: MapUnits(snapshot.Units));
        }

        private static Dictionary<string, CatalogHudEntryDto> MapResources(IEnumerable<ResourceDescriptor> source)
        {
            return MapCatalog(source, entry => entry?.Key, entry => new CatalogHudEntryDto
            {
                Key = entry.Key,
                IconKey = entry.IconKey,
                SortOrder = entry.SortOrder,
                VisibleInHud = entry.VisibleInHud
            });
        }

        private static Dictionary<string, CatalogHudEntryDto> MapPoints(IEnumerable<PointDescriptor> source)
        {
            return MapCatalog(source, entry => entry?.Key, entry => new CatalogHudEntryDto
            {
                Key = entry.Key,
                IconKey = entry.IconKey,
                SortOrder = entry.SortOrder,
                VisibleInHud = entry.VisibleInHud
            });
        }

        private static Dictionary<string, CatalogBuildingDto> MapBuildings(IEnumerable<BuildingCatalogEntry> source)
        {
            return MapCatalog(source, entry => entry?.Id, entry => new CatalogBuildingDto
            {
                Id = entry.Id,
                Name = entry.Name,
                Description = entry.Description,
                IconKey = entry.IconKey,
                PrefabKey = entry.PrefabKey,
                PlacementKind = entry.PlacementKind,
                BuildingScope = entry.BuildingScope,
                RequiredResourceType = entry.RequiredResourceType,
                TakeoverMode = entry.TakeoverMode,
                Tags = ToList(entry.Tags)
            });
        }

        private static Dictionary<string, CatalogRecipeDto> MapRecipes(IEnumerable<RecipeCatalogEntry> source)
        {
            return MapCatalog(source, entry => entry?.Id, entry => new CatalogRecipeDto
            {
                Id = entry.Id,
                Name = entry.Name,
                Description = entry.Description,
                IconKey = entry.IconKey,
                BuildingId = entry.BuildingId,
                WorkAmount = entry.WorkAmount,
                BaseProgress = entry.BaseProgress,
                Tags = ToList(entry.Tags)
            });
        }

        private static Dictionary<string, CatalogTechnologyDto> MapTechnologies(IEnumerable<TechnologyCatalogEntry> source)
        {
            return MapCatalog(source, entry => entry?.Id, entry => new CatalogTechnologyDto
            {
                Id = entry.Id,
                Name = entry.Name,
                Description = entry.Description,
                IconKey = entry.IconKey,
                Branch = entry.Branch,
                Tier = entry.Tier,
                ResearchCost = entry.ResearchCost,
                Tags = ToList(entry.Tags)
            });
        }

        private static Dictionary<string, CatalogPolicyDto> MapPolicies(IEnumerable<PolicyCatalogEntry> source)
        {
            return MapCatalog(source, entry => entry?.Id, entry => new CatalogPolicyDto
            {
                Id = entry.Id,
                Name = entry.Name,
                Description = entry.Description,
                IconKey = entry.IconKey,
                Layer = entry.Layer,
                ActivationTiming = entry.ActivationTiming,
                ModifierEffects = new List<CatalogPolicyModifierEffectDto>()
            });
        }

        private static Dictionary<string, CatalogUnitDto> MapUnits(IEnumerable<UnitCatalogEntry> source)
        {
            return MapCatalog(source, entry => entry?.Id, entry => new CatalogUnitDto
            {
                Id = entry.Id,
                Name = entry.Name,
                Description = entry.Description,
                IconKey = entry.IconKey,
                PrefabKey = entry.PrefabKey,
                Flags = new CatalogUnitFlagsDto
                {
                    CanAttackStructures = entry.CanAttackStructures
                },
                Tags = ToList(entry.Tags)
            });
        }

        private static Dictionary<string, TResult> MapCatalog<TSource, TResult>(
            IEnumerable<TSource> source,
            Func<TSource, string> idSelector,
            Func<TSource, TResult> mapper)
            where TResult : class
        {
            var result = new Dictionary<string, TResult>(StringComparer.OrdinalIgnoreCase);
            if (source == null)
            {
                return result;
            }

            foreach (var entry in source)
            {
                if (entry == null)
                {
                    continue;
                }

                var id = idSelector(entry);
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                var mapped = mapper(entry);
                if (mapped != null)
                {
                    result[id.Trim()] = mapped;
                }
            }

            return result;
        }

        private static List<string> ToList(IEnumerable<string> source)
        {
            return source == null
                ? new List<string>()
                : source.Where(value => value != null).ToList();
        }
    }
}
