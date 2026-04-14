/*************************************************
 * Project: Panoptes
 * File: StaticCatalogCache.cs
 * Author: Panoptes Team
 * Date: 2026-04-07
 * Description: Unified static catalog cache backed by generated JSON bundles.
 *************************************************/

using System;
using System.Collections.Generic;
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
        public sealed class BuildingEntryJson
        {
            public string id;
            public string name;
            public string description;
            public string icon_key;
            public string prefab_key;
            public string placement_rule;
            public string required_resource_type;
            public int sort_order;
        }

        [Serializable]
        public sealed class UnitEntryJson
        {
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
            public UnitEntryJson[] units;
            public BuildingEntryJson[] buildings;
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
        private readonly Dictionary<string, ResourceEntryJson> _resourcesByKey = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TerrainEntryJson> _terrainsById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, UnitEntryJson> _unitsById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, MapRuntimeBundleJson> _mapBundleCache = new(StringComparer.OrdinalIgnoreCase);

        public ManifestJson LocalManifest { get; private set; }
        public StaticCatalogManifest ServerManifest { get; private set; }
        public event System.Action CatalogChanged;

        public IReadOnlyDictionary<string, BuildingEntryJson> Buildings => _buildingsById;
        public IReadOnlyDictionary<string, ResourceEntryJson> Resources => _resourcesByKey;
        public IReadOnlyDictionary<string, TerrainEntryJson> Terrains => _terrainsById;
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
            RebuildIndex(_unitsById, parsed.units, entry => entry != null ? entry.id : string.Empty);
            RebuildIndex(_buildingsById, parsed.buildings, entry => entry != null ? entry.id : string.Empty);
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

        public bool TryGetBuilding(string buildingId, out BuildingEntryJson entry)
        {
            return _buildingsById.TryGetValue(Normalize(buildingId), out entry);
        }

        public bool TryGetTerrain(string terrainId, out TerrainEntryJson entry)
        {
            return _terrainsById.TryGetValue(Normalize(terrainId), out entry);
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

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }
    }
}
