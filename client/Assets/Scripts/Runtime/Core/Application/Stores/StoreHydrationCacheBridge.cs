using System;
using System.Collections.Generic;
using System.Linq;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Domain;

namespace Panoptes.Core.Application.Stores
{
    // Temporary migration bridge: one-shot seed game/planning stores from legacy
    // caches, while static catalog chunk sync still needs legacy cache events.
    public sealed class StoreHydrationCacheBridge : IDisposable
    {
        private readonly StoreHydrationHelper _helper;
        private StaticCatalogCache _staticCatalogCache;

        public StoreHydrationCacheBridge(StoreHydrationHelper helper)
        {
            _helper = helper ?? throw new ArgumentNullException(nameof(helper));
        }

        public void Attach(
            GameStateCache gameStateCache,
            PlanningDraftCache planningDraftCache,
            StaticCatalogCache staticCatalogCache)
        {
            Detach();
            _staticCatalogCache = staticCatalogCache;

            if (_staticCatalogCache != null)
            {
                _staticCatalogCache.CatalogChanged += HydrateStaticCatalog;
            }

            Seed(gameStateCache, planningDraftCache, staticCatalogCache);
        }

        public void AttachToDefaultCaches()
        {
            Attach(
                GameStateCache.Instance,
                PlanningDraftCache.Instance ?? PlanningDraftCache.EnsureInstance(),
                StaticCatalogCache.EnsureInstance());
        }

        public void Dispose()
        {
            Detach();
        }

        public void Seed(
            GameStateCache gameStateCache,
            PlanningDraftCache planningDraftCache,
            StaticCatalogCache staticCatalogCache)
        {
            _helper.Hydrate(new StoreHydrationSnapshot(
                CaptureGameState(gameStateCache),
                CapturePlanningDraft(planningDraftCache),
                CaptureStaticCatalog(staticCatalogCache),
                CaptureTurn(gameStateCache)));
        }

        public static GameStateStoreState CaptureGameState(GameStateCache cache)
        {
            if (cache == null)
            {
                return new GameStateStoreState();
            }

            return new GameStateStoreState(
                gameId: cache.GameID,
                activeGameSessionId: cache.ActiveGameSessionID,
                myPlayerId: cache.MyPlayerID,
                turn: cache.Turn,
                phase: cache.Phase,
                mapWidth: cache.MapWidth,
                mapHeight: cache.MapHeight,
                isGameOver: cache.IsGameOver,
                tokensLeft: cache.TokensLeft,
                nodes: CopyDictionary(cache.Nodes),
                units: CopyDictionary(cache.Units),
                myResources: cache.GetMyResources());
        }

        public static PlanningDraftState CapturePlanningDraft(PlanningDraftCache cache)
        {
            if (cache == null)
            {
                return new PlanningDraftState();
            }

            return new PlanningDraftState(
                snapshotTurn: cache.SnapshotTurn,
                snapshotPhase: cache.SnapshotPhase,
                unitOrders: cache.GetOrdersInDisplayOrder(),
                buildOrders: cache.BuildOrders,
                recipeSelections: cache.RecipeSelections,
                warZoneDirectives: cache.WarZoneDirectives,
                warZones: cache.WarZones,
                ministerDrafts: cache.MinisterDrafts,
                currentPreview: cache.CurrentPreview,
                currentBuildPreview: cache.CurrentBuildPreview,
                currentRecipePreview: cache.CurrentRecipePreview,
                plannedResearchTargetTechnologyId: cache.PlannedResearchTargetTechnologyId,
                plannedNationalPolicyId: cache.PlannedNationalPolicyId,
                plannedInstitutionPolicyIds: cache.PlannedInstitutionPolicyIds);
        }

        public static StaticCatalogState CaptureStaticCatalog(StaticCatalogCache cache)
        {
            if (cache == null)
            {
                return new StaticCatalogState();
            }

            return new StaticCatalogState(
                buildings: MapBuildings(cache.Buildings),
                recipes: MapRecipes(cache.Recipes),
                technologies: MapTechnologies(cache.Technologies),
                policies: MapPolicies(cache.Policies),
                units: MapUnits(cache.Units));
        }

        public static TurnState CaptureTurn(GameStateCache cache)
        {
            if (cache == null)
            {
                return new TurnState();
            }

            return new TurnState(
                cache.Turn,
                cache.Phase,
                cache.TokensLeft,
                cache.GetPlanningStartEvents(),
                cache.IsGameOver);
        }

        private void Detach()
        {
            if (_staticCatalogCache != null)
            {
                _staticCatalogCache.CatalogChanged -= HydrateStaticCatalog;
            }

            _staticCatalogCache = null;
        }

        private void HydrateStaticCatalog()
        {
            _helper.HydrateStaticCatalog(CaptureStaticCatalog(_staticCatalogCache));
        }

        private static Dictionary<string, TValue> CopyDictionary<TValue>(IReadOnlyDictionary<string, TValue> source)
        {
            var result = new Dictionary<string, TValue>(StringComparer.OrdinalIgnoreCase);
            if (source == null)
            {
                return result;
            }

            foreach (var pair in source)
            {
                if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value != null)
                {
                    result[pair.Key] = pair.Value;
                }
            }

            return result;
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
                ActivationTiming = entry.activation_timing
            });
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
