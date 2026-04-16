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
        public sealed class TechnologyEntryJson
        {
            public string id;
            public string name;
            public string description;
            public string icon_key;
            public string branch;
            public int tier;
            public int tech_point_cost;
            public string[] prerequisite_technology_ids;
        }

        [Serializable]
        public sealed class TechnologyTreePointJson
        {
            public float x;
            public float y;
        }

        [Serializable]
        public sealed class TechnologyTreeNodeJson
        {
            public string id;
            public string technology_id;
            public string title;
            public string description;
            public float x;
            public float y;
            public float width;
            public float height;
            public bool visible;
        }

        [Serializable]
        public sealed class TechnologyTreeEdgeJson
        {
            public string id;
            public string from;
            public string to;
            public string arrow;
            public bool show_arrow;
            public float thickness;
            public TechnologyTreePointJson[] points;
        }

        [Serializable]
        public sealed class TechnologyTreeLayoutJson
        {
            public string config_version;
            public TechnologyTreeNodeJson[] nodes;
            public TechnologyTreeEdgeJson[] edges;
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
            public TechnologyEntryJson[] technologies;
            public TechnologyTreeLayoutJson technology_tree;
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
        private readonly Dictionary<string, TechnologyEntryJson> _technologiesById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, UnitEntryJson> _unitsById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, MapRuntimeBundleJson> _mapBundleCache = new(StringComparer.OrdinalIgnoreCase);

        public ManifestJson LocalManifest { get; private set; }
        public StaticCatalogManifest ServerManifest { get; private set; }
        public TechnologyTreeLayoutJson TechnologyTreeLayout { get; private set; }
        public bool HasServerSnapshot { get; private set; }
        public event System.Action CatalogChanged;

        public IReadOnlyDictionary<string, BuildingEntryJson> Buildings => _buildingsById;
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
            RebuildIndex(_unitsById, parsed.units, entry => entry != null ? entry.id : string.Empty);
            RebuildIndex(_buildingsById, parsed.buildings, entry => entry != null ? entry.id : string.Empty);
            RebuildIndex(_technologiesById, parsed.technologies, entry => entry != null ? entry.id : string.Empty);
            RebuildIndex(_terrainsById, parsed.terrains, entry => entry != null ? entry.id : string.Empty);
            RebuildIndex(_mapsById, parsed.maps, entry => entry != null ? entry.id : string.Empty);
            TechnologyTreeLayout = parsed.technology_tree;
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

            RebuildIndex(_resourcesByKey, snapshot.Resources, entry => entry != null ? entry.Key : string.Empty, ConvertResource);
            RebuildIndex(_unitsById, snapshot.Units, entry => entry != null ? entry.Id : string.Empty, ConvertUnit);
            RebuildIndex(_buildingsById, snapshot.Buildings, entry => entry != null ? entry.Id : string.Empty, ConvertBuilding);
            RebuildIndex(_technologiesById, snapshot.Technologies, entry => entry != null ? entry.Id : string.Empty, ConvertTechnology);
            RebuildIndex(_terrainsById, snapshot.Terrains, entry => entry != null ? entry.Id : string.Empty, ConvertTerrain);

            TechnologyTreeLayout = ConvertTechnologyTree(snapshot.TechnologyTree);
            HasServerSnapshot = true;
            CatalogChanged?.Invoke();
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

        public bool TryGetTechnology(string technologyId, out TechnologyEntryJson entry)
        {
            return _technologiesById.TryGetValue(Normalize(technologyId), out entry);
        }

        public bool TryGetTechnologyTree(out TechnologyTreeLayoutJson layout)
        {
            layout = TechnologyTreeLayout;
            return layout != null && layout.nodes != null && layout.nodes.Length > 0;
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
            TechnologyTreeLayout = null;
            HasServerSnapshot = false;
        }

        private static ResourceEntryJson ConvertResource(ResourceDescriptor source)
        {
            if (source == null)
            {
                return null;
            }

            return new ResourceEntryJson
            {
                key = source.Key,
                display_name = source.DisplayName,
                description = source.Description,
                icon_key = source.IconKey,
                sort_order = source.SortOrder,
                visible_in_hud = source.VisibleInHud
            };
        }

        private static UnitEntryJson ConvertUnit(UnitCatalogEntry source)
        {
            if (source == null)
            {
                return null;
            }

            return new UnitEntryJson
            {
                id = source.Id,
                name = source.Name,
                description = source.Description,
                icon_key = source.IconKey,
                prefab_key = source.PrefabKey
            };
        }

        private static BuildingEntryJson ConvertBuilding(BuildingCatalogEntry source)
        {
            if (source == null)
            {
                return null;
            }

            return new BuildingEntryJson
            {
                id = source.Id,
                name = source.Name,
                description = source.Description,
                icon_key = source.IconKey,
                prefab_key = source.PrefabKey
            };
        }

        private static TerrainEntryJson ConvertTerrain(TerrainCatalogEntry source)
        {
            if (source == null)
            {
                return null;
            }

            return new TerrainEntryJson
            {
                id = source.Id,
                name = source.Name,
                description = source.Description,
                icon_key = source.IconKey,
                material_key = source.MaterialKey
            };
        }

        private static TechnologyEntryJson ConvertTechnology(TechnologyCatalogEntry source)
        {
            if (source == null)
            {
                return null;
            }

            return new TechnologyEntryJson
            {
                id = source.Id,
                name = source.Name,
                description = source.Description,
                icon_key = source.IconKey,
                branch = source.Branch,
                tier = source.Tier,
                tech_point_cost = source.TechPointCost,
                prerequisite_technology_ids = Array.Empty<string>()
            };
        }

        private static TechnologyTreeLayoutJson ConvertTechnologyTree(TechnologyTreeLayout source)
        {
            if (source == null)
            {
                return null;
            }

            var nodes = source.Nodes;
            var convertedNodes = new TechnologyTreeNodeJson[nodes.Count];
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                convertedNodes[i] = new TechnologyTreeNodeJson
                {
                    id = node.Id,
                    technology_id = node.TechnologyId,
                    title = node.Title,
                    description = node.Description,
                    x = node.X,
                    y = node.Y,
                    width = node.Width,
                    height = node.Height,
                    visible = node.Visible
                };
            }

            var edges = source.Edges;
            var convertedEdges = new TechnologyTreeEdgeJson[edges.Count];
            for (var i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                var points = edge.Points;
                var convertedPoints = new TechnologyTreePointJson[points.Count];
                for (var p = 0; p < points.Count; p++)
                {
                    convertedPoints[p] = new TechnologyTreePointJson
                    {
                        x = points[p].X,
                        y = points[p].Y
                    };
                }

                convertedEdges[i] = new TechnologyTreeEdgeJson
                {
                    id = edge.Id,
                    from = edge.From,
                    to = edge.To,
                    arrow = edge.Arrow,
                    show_arrow = edge.ShowArrow,
                    thickness = edge.Thickness,
                    points = convertedPoints
                };
            }

            return new TechnologyTreeLayoutJson
            {
                config_version = source.ConfigVersion,
                nodes = convertedNodes,
                edges = convertedEdges
            };
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

        private static void RebuildIndex<TTarget, TSource>(
            Dictionary<string, TTarget> target,
            System.Collections.Generic.IEnumerable<TSource> source,
            Func<TSource, string> keySelector,
            Func<TSource, TTarget> converter)
            where TTarget : class
        {
            target.Clear();
            if (source == null || converter == null)
            {
                return;
            }

            foreach (var sourceEntry in source)
            {
                if (sourceEntry == null)
                {
                    continue;
                }

                var key = Normalize(keySelector(sourceEntry));
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                var targetEntry = converter(sourceEntry);
                if (targetEntry == null)
                {
                    continue;
                }

                target[key] = targetEntry;
            }
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }
    }
}
