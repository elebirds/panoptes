using System;
using System.Collections.Generic;
using System.Linq;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Domain;
using Panoptes.Protocol.V1;

namespace Panoptes.Core.Application.Stores
{
    public sealed class StaticCatalogStoreHydrator
    {
        private readonly StaticCatalogStore _store;

        public StaticCatalogStoreHydrator(StaticCatalogStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public void HydrateFromSnapshot(StaticCatalogSnapshot snapshot)
        {
            _store.Replace(StaticCatalogProtocolMapper.ToState(snapshot));
        }

        public void HydrateFromCache(StaticCatalogCache cache)
        {
            _store.Replace(ToState(cache));
        }

        public static StaticCatalogState ToState(StaticCatalogCache cache)
        {
            if (cache == null)
            {
                return new StaticCatalogState();
            }

            cache.TryGetDefaultMap(out var defaultMap);
            return new StaticCatalogState(
                resources: MapResourceHudEntries(cache.Resources),
                points: MapPointHudEntries(cache.Points),
                buildings: MapBuildings(cache.Buildings),
                recipes: MapRecipes(cache.Recipes),
                technologies: MapTechnologies(cache.Technologies),
                policies: MapPolicies(cache.Policies),
                units: MapUnits(cache.Units),
                defaultMap: MapRuntimeBundle(defaultMap));
        }

        private static Dictionary<string, CatalogHudEntryDto> MapResourceHudEntries(
            IReadOnlyDictionary<string, StaticCatalogCache.ResourceEntryJson> source)
        {
            return MapCatalog(source, entry => entry?.key, entry => new CatalogHudEntryDto
            {
                Key = entry.key,
                Name = entry.display_name,
                Description = entry.description,
                IconKey = entry.icon_key,
                SortOrder = entry.sort_order,
                VisibleInHud = entry.visible_in_hud
            });
        }

        private static Dictionary<string, CatalogHudEntryDto> MapPointHudEntries(
            IReadOnlyDictionary<string, StaticCatalogCache.PointEntryJson> source)
        {
            return MapCatalog(source, entry => entry?.key, entry => new CatalogHudEntryDto
            {
                Key = entry.key,
                Name = entry.display_name,
                Description = entry.description,
                IconKey = entry.icon_key,
                SortOrder = entry.sort_order,
                VisibleInHud = entry.visible_in_hud
            });
        }

        private static Dictionary<string, CatalogBuildingDto> MapBuildings(
            IReadOnlyDictionary<string, StaticCatalogCache.BuildingEntryJson> source)
        {
            return MapCatalog(source, entry => entry?.id, entry => new CatalogBuildingDto
            {
                Id = entry.id,
                Name = entry.name,
                Description = entry.description,
                IconKey = entry.icon_key,
                PrefabKey = entry.prefab_key,
                PlacementKind = entry.placement_kind,
                BuildingScope = entry.building_scope,
                RequiredResourceType = entry.required_resource_type,
                TakeoverMode = entry.takeover_mode,
                SortOrder = entry.sort_order,
                DefaultRecipeId = entry.default_recipe_id,
                RecipeIds = ToList(entry.recipe_ids),
                Tags = ToList(entry.tags),
                MaxHp = entry.max_hp
            });
        }

        private static Dictionary<string, CatalogRecipeDto> MapRecipes(
            IReadOnlyDictionary<string, StaticCatalogCache.RecipeEntryJson> source)
        {
            return MapCatalog(source, entry => entry?.id, entry => new CatalogRecipeDto
            {
                Id = entry.id,
                Name = entry.name,
                Description = entry.description,
                IconKey = entry.icon_key,
                BuildingId = entry.building_id,
                WorkAmount = entry.work_amount,
                BaseProgress = entry.base_progress,
                SortOrder = entry.sort_order,
                Tags = ToList(entry.tags),
                ResourceInputs = MapAmounts(entry.resource_inputs),
                PointInputs = MapAmounts(entry.point_inputs),
                Outputs = MapOutputs(entry.outputs)
            });
        }

        private static Dictionary<string, CatalogTechnologyDto> MapTechnologies(
            IReadOnlyDictionary<string, StaticCatalogCache.TechnologyEntryJson> source)
        {
            return MapCatalog(source, entry => entry?.id, entry => new CatalogTechnologyDto
            {
                Id = entry.id,
                Name = entry.name,
                Description = entry.description,
                IconKey = entry.icon_key,
                Branch = entry.branch,
                Tier = entry.tier,
                ResearchCost = entry.research_cost,
                SortOrder = entry.sort_order,
                Tags = ToList(entry.tags),
                Prerequisites = MapPrerequisites(entry.prerequisites),
                ExplicitEffects = MapEffects(entry.explicit_effects)
            });
        }

        private static Dictionary<string, CatalogPolicyDto> MapPolicies(
            IReadOnlyDictionary<string, StaticCatalogCache.PolicyEntryJson> source)
        {
            return MapCatalog(source, entry => entry?.id, entry => new CatalogPolicyDto
            {
                Id = entry.id,
                Name = entry.name,
                Description = entry.description,
                IconKey = entry.icon_key,
                Layer = entry.layer,
                ActivationTiming = entry.activation_timing,
                ModifierEffects = MapModifierEffects(entry.modifier_effects)
            });
        }

        private static List<CatalogPolicyModifierEffectDto> MapModifierEffects(
            StaticCatalogCache.ModifierEffectEntryJson[] source)
        {
            var result = new List<CatalogPolicyModifierEffectDto>();
            if (source == null)
            {
                return result;
            }

            for (var i = 0; i < source.Length; i++)
            {
                var entry = source[i];
                if (entry == null)
                {
                    continue;
                }

                result.Add(new CatalogPolicyModifierEffectDto
                {
                    Trigger = entry.trigger,
                    TargetId = entry.target_id,
                    ResourceKey = entry.resource_key,
                    PointKey = entry.point_key,
                    ModifierType = entry.modifier_type,
                    Value = entry.value
                });
            }

            return result;
        }

        private static Dictionary<string, CatalogUnitDto> MapUnits(
            IReadOnlyDictionary<string, StaticCatalogCache.UnitEntryJson> source)
        {
            return MapCatalog(source, entry => entry?.id, entry => new CatalogUnitDto
            {
                Id = entry.id,
                Name = entry.name,
                Description = entry.description,
                IconKey = entry.icon_key,
                PrefabKey = entry.prefab_key,
                Class = entry.@class,
                MaxHp = entry.max_hp,
                Attack = entry.attack,
                AttackRange = entry.attack_range,
                MoveRange = entry.move_range,
                VisionRange = entry.vision_range,
                RoadSpeedBonus = entry.road_speed_bonus,
                ChargeBonus = entry.charge_bonus,
                Flags = new CatalogUnitFlagsDto
                {
                    CanAttackStructures = entry.flags != null && entry.flags.can_attack_structures
                },
                Tags = ToList(entry.tags)
            });
        }

        private static CatalogMapRuntimeBundleDto MapRuntimeBundle(StaticCatalogCache.MapRuntimeBundleJson source)
        {
            if (source == null)
            {
                return null;
            }

            return new CatalogMapRuntimeBundleDto
            {
                Height = source.height,
                Id = source.id,
                Name = source.name,
                Nodes = MapRuntimeNodes(source.nodes),
                Width = source.width
            };
        }

        private static List<CatalogMapRuntimeNodeDto> MapRuntimeNodes(
            IEnumerable<StaticCatalogCache.MapRuntimeNodeJson> source)
        {
            if (source == null)
            {
                return new List<CatalogMapRuntimeNodeDto>();
            }

            return source
                .Where(entry => entry != null)
                .Select(entry => new CatalogMapRuntimeNodeDto
                {
                    BuildingHp = entry.building_hp,
                    BuildingType = entry.building_type,
                    HasRoad = entry.has_road,
                    Id = entry.id,
                    IsResourcePoint = entry.is_resource_point,
                    NodeName = entry.node_name,
                    Owner = entry.owner,
                    ResourceType = entry.resource_type,
                    Terrain = entry.terrain,
                    TerritoryOwner = entry.territory_owner,
                    X = entry.x,
                    Y = entry.y
                })
                .ToList();
        }

        private static Dictionary<string, TResult> MapCatalog<TSource, TResult>(
            IReadOnlyDictionary<string, TSource> source,
            Func<TSource, string> idSelector,
            Func<TSource, TResult> mapper)
            where TResult : class
        {
            var result = new Dictionary<string, TResult>(StringComparer.OrdinalIgnoreCase);
            if (source == null)
            {
                return result;
            }

            foreach (var pair in source)
            {
                var value = pair.Value;
                if (value == null)
                {
                    continue;
                }

                var id = idSelector(value);
                if (string.IsNullOrWhiteSpace(id))
                {
                    id = pair.Key;
                }

                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                var mapped = mapper(value);
                if (mapped != null)
                {
                    result[id.Trim()] = mapped;
                }
            }

            return result;
        }

        private static CatalogRecipeOutputsDto MapOutputs(StaticCatalogCache.RecipeOutputsJson source)
        {
            if (source == null)
            {
                return null;
            }

            return new CatalogRecipeOutputsDto
            {
                Resources = MapAmounts(source.resources),
                Units = ToList(source.units),
                PointProgress = MapAmounts(source.point_progress),
                StateChanges = MapAmounts(source.state_changes)
            };
        }

        private static List<CatalogTechnologyPrerequisiteDto> MapPrerequisites(
            IEnumerable<StaticCatalogCache.PrerequisiteEntryJson> source)
        {
            return source == null
                ? new List<CatalogTechnologyPrerequisiteDto>()
                : source
                    .Where(entry => entry != null)
                    .Select(entry => new CatalogTechnologyPrerequisiteDto
                    {
                        Type = entry.type,
                        TargetId = entry.target_id
                    })
                    .ToList();
        }

        private static List<CatalogTechnologyEffectDto> MapEffects(
            IEnumerable<StaticCatalogCache.ExplicitEffectEntryJson> source)
        {
            return source == null
                ? new List<CatalogTechnologyEffectDto>()
                : source
                    .Where(entry => entry != null)
                    .Select(entry => new CatalogTechnologyEffectDto
                    {
                        Type = entry.type,
                        TargetId = entry.target_id,
                        InstitutionSlots = entry.institution_slots
                    })
                    .ToList();
        }

        private static List<CatalogAmountDto> MapAmounts(IEnumerable<StaticCatalogCache.IntAmountEntryJson> source)
        {
            return source == null
                ? new List<CatalogAmountDto>()
                : source
                    .Where(entry => entry != null)
                    .Select(entry => new CatalogAmountDto
                    {
                        Key = entry.key,
                        Amount = entry.amount
                    })
                    .ToList();
        }

        private static List<string> ToList(IEnumerable<string> source)
        {
            return source == null ? new List<string>() : source.Where(value => value != null).ToList();
        }
    }
}
