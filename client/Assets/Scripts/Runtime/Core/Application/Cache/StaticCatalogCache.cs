/*************************************************
 * Project: Panoptes
 * File: StaticCatalogCache.cs
 * Author: Panoptes Team
 * Date: 2026-04-07
 * Description: Unified static catalog cache backed by generated JSON bundles.
 *************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using Panoptes.Protocol.V1;
using UnityEngine;

namespace Panoptes.Core.Application.Cache
{
    public sealed class StaticCatalogCache : MonoBehaviour
    {
        [Serializable]
        public sealed class ManifestJson
        {
            public string schema_version;
            public string content_version;
            public string default_locale;
            public string default_map_id;
            public string bundle_hash;
        }

        [Serializable]
        public sealed class ResourceEntryJson
        {
            public string key;
            public string display_name;
            public string description;
            public string icon_key;
            public int sort_order;
            public bool visible_in_hud;
        }

        [Serializable]
        public sealed class PointEntryJson
        {
            public string key;
            public string display_name;
            public string description;
            public string icon_key;
            public int sort_order;
            public bool visible_in_hud;
        }

        [Serializable]
        public sealed class BuildingEntryJson
        {
            public string id;
            public string name;
            public string description;
            public string icon_key;
            public string prefab_key;
            public string placement_kind;
            public string building_scope;
            public string required_resource_type;
            public string takeover_mode;
            public int sort_order;
        }

        [Serializable]
        public sealed class TechnologyEntryJson
        {
            public string id;
            public string name;
            public string description;
            public string icon_key;
            public string branch;
            public int tier;
            public int research_cost;
            public int sort_order;
            public string[] tags;
            public PrerequisiteEntryJson[] prerequisites;
            public ExplicitEffectEntryJson[] explicit_effects;
        }

        [Serializable]
        public sealed class ExplicitEffectEntryJson
        {
            public string type;
            public string target_id;
            public int institution_slots;
        }

        [Serializable]
        public sealed class PrerequisiteEntryJson
        {
            public string type;
            public string target_id;
        }

        [Serializable]
        public sealed class PolicyEntryJson
        {
            public string id;
            public string name;
            public string description;
            public string icon_key;
            public string layer;
            public string activation_timing;
        }

        [Serializable]
        public sealed class RecipeEntryJson
        {
            public string id;
            public string name;
            public string description;
            public string icon_key;
            public string building_id;
            public int work_amount;
            public int base_progress;
            public int sort_order;
            public string[] tags;
            public IntAmountEntryJson[] resource_inputs;
            public IntAmountEntryJson[] point_inputs;
            public RecipeOutputsJson outputs;
        }

        [Serializable]
        public sealed class RecipeOutputsJson
        {
            public IntAmountEntryJson[] resources;
            public string[] units;
            public IntAmountEntryJson[] point_progress;
            public IntAmountEntryJson[] state_changes;
        }

        [Serializable]
        public sealed class IntAmountEntryJson
        {
            public string key;
            public int amount;
        }

        [Serializable]
        public sealed class UnitEntryJson
        {
            [Serializable]
            public sealed class UnitFlagsJson
            {
                public bool can_attack_structures;
            }

            public string id;
            public string name;
            public string description;
            public string icon_key;
            public string prefab_key;
            public string @class;
            public int max_hp;
            public int attack;
            public int attack_range;
            public int move_range;
            public int vision_range;
            public int road_speed_bonus;
            public float charge_bonus;
            public UnitFlagsJson flags;
            public string[] tags;
        }

        [Serializable]
        public sealed class TerrainEntryJson
        {
            public string id;
            public string name;
            public string description;
            public string icon_key;
            public string material_key;
            public int move_cost_no_road;
            public bool passable;
            public bool passable_with_road;
            public bool blocks_cavalry;
            public int sort_order;
            public string[] tags;
        }

        [Serializable]
        public sealed class MapEntryJson
        {
            public string id;
            public string name;
            public string description;
            public string thumbnail_key;
            public int width;
            public int height;
        }

        [Serializable]
        private sealed class CatalogBundleJson
        {
            public ManifestJson manifest;
            public ResourceEntryJson[] resources;
            public PointEntryJson[] points;
            public UnitEntryJson[] units;
            public BuildingEntryJson[] buildings;
            public TechnologyEntryJson[] technologies;
            public PolicyEntryJson[] policies;
            public RecipeEntryJson[] recipes;
            public TerrainEntryJson[] terrains;
            public MapEntryJson[] maps;
        }

        [Serializable]
        public sealed class MapRuntimeNodeJson
        {
            public string id;
            public int x;
            public int y;
            public string terrain;
            public bool has_road;
            public bool is_resource_point;
            public string resource_type;
            public string node_name;
            public string owner;
            public string territory_owner;
            public string building_type;
            public int building_hp;
        }

        [Serializable]
        public sealed class MapRuntimeBundleJson
        {
            public string id;
            public string name;
            public int width;
            public int height;
            public MapRuntimeNodeJson[] nodes;
        }

        public static StaticCatalogCache Instance { get; private set; }

        [SerializeField] private bool autoLoadOnAwake = true;
        [SerializeField] private string catalogBundleResourcePath = "Data/catalog.bundle";
        [SerializeField] private bool logStatus = true;

        private readonly Dictionary<string, BuildingEntryJson> _buildingsById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, MapEntryJson> _mapsById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PointEntryJson> _pointsByKey = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PolicyEntryJson> _policiesById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, RecipeEntryJson> _recipesById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ResourceEntryJson> _resourcesByKey = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TerrainEntryJson> _terrainsById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TechnologyEntryJson> _technologiesById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, UnitEntryJson> _unitsById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, MapRuntimeBundleJson> _mapBundleCache = new(StringComparer.OrdinalIgnoreCase);

        public ManifestJson LocalManifest { get; private set; }
        public StaticCatalogManifest ServerManifest { get; private set; }
        public event System.Action CatalogChanged;

        public IReadOnlyDictionary<string, BuildingEntryJson> Buildings => _buildingsById;
        public IReadOnlyDictionary<string, PointEntryJson> Points => _pointsByKey;
        public IReadOnlyDictionary<string, PolicyEntryJson> Policies => _policiesById;
        public IReadOnlyDictionary<string, RecipeEntryJson> Recipes => _recipesById;
        public IReadOnlyDictionary<string, ResourceEntryJson> Resources => _resourcesByKey;
        public IReadOnlyDictionary<string, TerrainEntryJson> Terrains => _terrainsById;
        public IReadOnlyDictionary<string, TechnologyEntryJson> Technologies => _technologiesById;
        public IReadOnlyDictionary<string, UnitEntryJson> Units => _unitsById;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (autoLoadOnAwake)
            {
                LoadLocalCatalog();
            }
        }

        public static StaticCatalogCache EnsureInstance()
        {
            if (Instance != null)
            {
                return Instance;
            }

            var existing = UnityEngine.Object.FindAnyObjectByType<StaticCatalogCache>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            var go = new GameObject("StaticCatalogCache");
            return go.AddComponent<StaticCatalogCache>();
        }

        public bool LoadLocalCatalog()
        {
            var asset = UnityEngine.Resources.Load<TextAsset>(catalogBundleResourcePath);
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
            {
                if (logStatus)
                {
                    Debug.LogWarning($"[StaticCatalogCache] Catalog bundle missing at Resources/{catalogBundleResourcePath}.");
                }
                return false;
            }

            CatalogBundleJson parsed = null;
            try
            {
                parsed = JsonUtility.FromJson<CatalogBundleJson>(asset.text);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[StaticCatalogCache] Failed to parse local catalog bundle: {ex.Message}");
                return false;
            }

            if (parsed == null || parsed.manifest == null)
            {
                return false;
            }

            LocalManifest = parsed.manifest;
            RebuildIndex(_resourcesByKey, parsed.resources, entry => entry != null ? entry.key : string.Empty);
            RebuildIndex(_pointsByKey, parsed.points, entry => entry != null ? entry.key : string.Empty);
            RebuildIndex(_unitsById, parsed.units, entry => entry != null ? entry.id : string.Empty);
            RebuildIndex(_buildingsById, parsed.buildings, entry => entry != null ? entry.id : string.Empty);
            RebuildIndex(_technologiesById, parsed.technologies, entry => entry != null ? entry.id : string.Empty);
            RebuildIndex(_policiesById, parsed.policies, entry => entry != null ? entry.id : string.Empty);
            RebuildIndex(_recipesById, parsed.recipes, entry => entry != null ? entry.id : string.Empty);
            RebuildIndex(_terrainsById, parsed.terrains, entry => entry != null ? entry.id : string.Empty);
            RebuildIndex(_mapsById, parsed.maps, entry => entry != null ? entry.id : string.Empty);
            _mapBundleCache.Clear();

            if (logStatus)
            {
                Debug.Log($"[StaticCatalogCache] Loaded local catalog bundle hash={LocalManifest.bundle_hash}.");
            }

            CatalogChanged?.Invoke();
            return true;
        }

        public void ApplyManifest(StaticCatalogManifest manifest)
        {
            ServerManifest = manifest;
            if (logStatus && LocalManifest != null && manifest != null &&
                !string.IsNullOrWhiteSpace(LocalManifest.bundle_hash) &&
                !string.IsNullOrWhiteSpace(manifest.BundleHash) &&
                !string.Equals(LocalManifest.bundle_hash, manifest.BundleHash, StringComparison.Ordinal))
            {
                Debug.LogWarning($"[StaticCatalogCache] Local bundle hash '{LocalManifest.bundle_hash}' differs from server '{manifest.BundleHash}'.");
            }
        }

        public void ApplySnapshot(StaticCatalogSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            ApplyManifest(snapshot.Manifest);

            // Keep richer local-only fields (e.g. prerequisites) when server snapshot schema is narrower.
            var oldTechnologies = new Dictionary<string, TechnologyEntryJson>(_technologiesById, StringComparer.OrdinalIgnoreCase);
            var oldRecipes = new Dictionary<string, RecipeEntryJson>(_recipesById, StringComparer.OrdinalIgnoreCase);

            RebuildIndex(_resourcesByKey, ConvertResources(snapshot.Resources), entry => entry != null ? entry.key : string.Empty);
            RebuildIndex(_pointsByKey, ConvertPoints(snapshot.Points), entry => entry != null ? entry.key : string.Empty);
            RebuildIndex(_unitsById, ConvertUnits(snapshot.Units), entry => entry != null ? entry.id : string.Empty);
            RebuildIndex(_buildingsById, ConvertBuildings(snapshot.Buildings), entry => entry != null ? entry.id : string.Empty);
            RebuildIndex(_technologiesById, ConvertTechnologies(snapshot.Technologies, oldTechnologies), entry => entry != null ? entry.id : string.Empty);
            RebuildIndex(_policiesById, ConvertPolicies(snapshot.Policies), entry => entry != null ? entry.id : string.Empty);
            RebuildIndex(_recipesById, ConvertRecipes(snapshot.Recipes, oldRecipes), entry => entry != null ? entry.id : string.Empty);
            RebuildIndex(_terrainsById, ConvertTerrains(snapshot.Terrains), entry => entry != null ? entry.id : string.Empty);

            if (logStatus)
            {
                Debug.Log($"[StaticCatalogCache] Applied server snapshot: techs={_technologiesById.Count} recipes={_recipesById.Count}.");
            }

            CatalogChanged?.Invoke();
        }

        public bool TryGetBuilding(string buildingId, out BuildingEntryJson entry)
        {
            return _buildingsById.TryGetValue(Normalize(buildingId), out entry);
        }

        public bool TryGetPoint(string pointKey, out PointEntryJson entry)
        {
            return _pointsByKey.TryGetValue(Normalize(pointKey), out entry);
        }

        public bool TryGetPolicy(string policyId, out PolicyEntryJson entry)
        {
            return _policiesById.TryGetValue(Normalize(policyId), out entry);
        }

        public bool TryGetRecipe(string recipeId, out RecipeEntryJson entry)
        {
            return _recipesById.TryGetValue(Normalize(recipeId), out entry);
        }

        public bool TryGetTerrain(string terrainId, out TerrainEntryJson entry)
        {
            return _terrainsById.TryGetValue(Normalize(terrainId), out entry);
        }

        public bool TryGetTechnology(string technologyId, out TechnologyEntryJson entry)
        {
            return _technologiesById.TryGetValue(Normalize(technologyId), out entry);
        }

        public bool TryGetUnit(string unitId, out UnitEntryJson entry)
        {
            return _unitsById.TryGetValue(Normalize(unitId), out entry);
        }

        public bool TryGetDefaultMap(out MapRuntimeBundleJson bundle)
        {
            bundle = null;
            var mapId = Normalize(LocalManifest != null ? LocalManifest.default_map_id : string.Empty);
            if (string.IsNullOrEmpty(mapId))
            {
                return false;
            }

            return TryGetMapBundle(mapId, out bundle);
        }

        public bool TryGetMapBundle(string mapId, out MapRuntimeBundleJson bundle)
        {
            bundle = null;
            var key = Normalize(mapId);
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            if (_mapBundleCache.TryGetValue(key, out bundle) && bundle != null)
            {
                return true;
            }

            var asset = UnityEngine.Resources.Load<TextAsset>($"Data/maps/{key}.runtime");
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
            {
                return false;
            }

            try
            {
                bundle = JsonUtility.FromJson<MapRuntimeBundleJson>(asset.text);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[StaticCatalogCache] Failed to parse map bundle '{key}': {ex.Message}");
                return false;
            }

            if (bundle == null)
            {
                return false;
            }

            _mapBundleCache[key] = bundle;
            return true;
        }

        public void Clear()
        {
            ServerManifest = null;
            _pointsByKey.Clear();
            _policiesById.Clear();
            _recipesById.Clear();
            _resourcesByKey.Clear();
            _technologiesById.Clear();
            _unitsById.Clear();
            _buildingsById.Clear();
            _terrainsById.Clear();
            _mapsById.Clear();
            _mapBundleCache.Clear();
        }

        private static void RebuildIndex<T>(Dictionary<string, T> target, T[] source, Func<T, string> keySelector)
        {
            target.Clear();
            if (source == null)
            {
                return;
            }

            for (var i = 0; i < source.Length; i++)
            {
                var entry = source[i];
                var key = Normalize(keySelector(entry));
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                target[key] = entry;
            }
        }

        private static ResourceEntryJson[] ConvertResources(System.Collections.Generic.IList<ResourceDescriptor> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<ResourceEntryJson>();
            }

            var result = new ResourceEntryJson[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var item = source[i];
                result[i] = new ResourceEntryJson
                {
                    key = item != null ? item.Key : string.Empty,
                    display_name = item != null ? item.DisplayName : string.Empty,
                    description = item != null ? item.Description : string.Empty,
                    icon_key = item != null ? item.IconKey : string.Empty,
                    sort_order = item != null ? item.SortOrder : 0,
                    visible_in_hud = item != null && item.VisibleInHud
                };
            }

            return result;
        }

        private static PointEntryJson[] ConvertPoints(System.Collections.Generic.IList<PointDescriptor> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<PointEntryJson>();
            }

            var result = new PointEntryJson[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var item = source[i];
                result[i] = new PointEntryJson
                {
                    key = item != null ? item.Key : string.Empty,
                    display_name = item != null ? item.DisplayName : string.Empty,
                    description = item != null ? item.Description : string.Empty,
                    icon_key = item != null ? item.IconKey : string.Empty,
                    sort_order = item != null ? item.SortOrder : 0,
                    visible_in_hud = item != null && item.VisibleInHud
                };
            }

            return result;
        }

        private static UnitEntryJson[] ConvertUnits(System.Collections.Generic.IList<UnitCatalogEntry> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<UnitEntryJson>();
            }

            var result = new UnitEntryJson[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var item = source[i];
                result[i] = new UnitEntryJson
                {
                    id = item != null ? item.Id : string.Empty,
                    name = item != null ? item.Name : string.Empty,
                    description = item != null ? item.Description : string.Empty,
                    icon_key = item != null ? item.IconKey : string.Empty,
                    prefab_key = item != null ? item.PrefabKey : string.Empty,
                    tags = item != null ? item.Tags.ToArray() : Array.Empty<string>()
                };
            }

            return result;
        }

        private static BuildingEntryJson[] ConvertBuildings(System.Collections.Generic.IList<BuildingCatalogEntry> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<BuildingEntryJson>();
            }

            var result = new BuildingEntryJson[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var item = source[i];
                result[i] = new BuildingEntryJson
                {
                    id = item != null ? item.Id : string.Empty,
                    name = item != null ? item.Name : string.Empty,
                    description = item != null ? item.Description : string.Empty,
                    icon_key = item != null ? item.IconKey : string.Empty,
                    prefab_key = item != null ? item.PrefabKey : string.Empty,
                    placement_kind = item != null ? item.PlacementKind : string.Empty,
                    building_scope = item != null ? item.BuildingScope : string.Empty,
                    required_resource_type = item != null ? item.RequiredResourceType : string.Empty,
                    takeover_mode = item != null ? item.TakeoverMode : string.Empty
                };
            }

            return result;
        }

        private static TechnologyEntryJson[] ConvertTechnologies(
            System.Collections.Generic.IList<TechnologyCatalogEntry> source,
            IReadOnlyDictionary<string, TechnologyEntryJson> previous)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<TechnologyEntryJson>();
            }

            var result = new TechnologyEntryJson[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var item = source[i];
                var id = item != null ? item.Id : string.Empty;
                previous.TryGetValue(Normalize(id), out var old);
                result[i] = new TechnologyEntryJson
                {
                    id = id,
                    name = item != null ? item.Name : string.Empty,
                    description = item != null ? item.Description : string.Empty,
                    icon_key = item != null ? item.IconKey : string.Empty,
                    branch = item != null ? item.Branch : string.Empty,
                    tier = item != null ? item.Tier : 0,
                    research_cost = item != null ? item.ResearchCost : 0,
                    tags = item != null ? item.Tags.ToArray() : (old != null ? old.tags : Array.Empty<string>()),
                    sort_order = old != null ? old.sort_order : 0,
                    prerequisites = old != null ? old.prerequisites : Array.Empty<PrerequisiteEntryJson>(),
                    explicit_effects = old != null ? old.explicit_effects : Array.Empty<ExplicitEffectEntryJson>()
                };
            }

            return result;
        }

        private static PolicyEntryJson[] ConvertPolicies(System.Collections.Generic.IList<PolicyCatalogEntry> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<PolicyEntryJson>();
            }

            var result = new PolicyEntryJson[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var item = source[i];
                result[i] = new PolicyEntryJson
                {
                    id = item != null ? item.Id : string.Empty,
                    name = item != null ? item.Name : string.Empty,
                    description = item != null ? item.Description : string.Empty,
                    icon_key = item != null ? item.IconKey : string.Empty,
                    layer = item != null ? item.Layer : string.Empty,
                    activation_timing = item != null ? item.ActivationTiming : string.Empty
                };
            }

            return result;
        }

        private static RecipeEntryJson[] ConvertRecipes(
            System.Collections.Generic.IList<RecipeCatalogEntry> source,
            IReadOnlyDictionary<string, RecipeEntryJson> previous)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<RecipeEntryJson>();
            }

            var result = new RecipeEntryJson[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var item = source[i];
                var id = item != null ? item.Id : string.Empty;
                previous.TryGetValue(Normalize(id), out var old);
                result[i] = new RecipeEntryJson
                {
                    id = id,
                    name = item != null ? item.Name : string.Empty,
                    description = item != null ? item.Description : string.Empty,
                    icon_key = item != null ? item.IconKey : string.Empty,
                    building_id = item != null ? item.BuildingId : string.Empty,
                    work_amount = item != null ? item.WorkAmount : 0,
                    base_progress = item != null ? item.BaseProgress : 0,
                    tags = item != null ? item.Tags.ToArray() : (old != null ? old.tags : Array.Empty<string>()),
                    sort_order = old != null ? old.sort_order : 0,
                    resource_inputs = old != null ? old.resource_inputs : Array.Empty<IntAmountEntryJson>(),
                    point_inputs = old != null ? old.point_inputs : Array.Empty<IntAmountEntryJson>(),
                    outputs = old != null ? old.outputs : new RecipeOutputsJson()
                };
            }

            return result;
        }

        private static TerrainEntryJson[] ConvertTerrains(System.Collections.Generic.IList<TerrainCatalogEntry> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<TerrainEntryJson>();
            }

            var result = new TerrainEntryJson[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var item = source[i];
                result[i] = new TerrainEntryJson
                {
                    id = item != null ? item.Id : string.Empty,
                    name = item != null ? item.Name : string.Empty,
                    description = item != null ? item.Description : string.Empty,
                    icon_key = item != null ? item.IconKey : string.Empty,
                    material_key = item != null ? item.MaterialKey : string.Empty,
                    tags = item != null ? item.Tags.ToArray() : Array.Empty<string>()
                };
            }

            return result;
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }
    }
}
