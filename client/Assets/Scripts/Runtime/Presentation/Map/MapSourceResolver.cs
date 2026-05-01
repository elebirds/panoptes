using System.Collections.Generic;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Domain;
using UnityEngine;

namespace Panoptes.Presentation.Map
{
    internal sealed class MapSourceResolver
    {
        [System.Serializable]
        private sealed class MapJsonEnvelope
        {
            public MapJsonConfig map;
        }

        [System.Serializable]
        private sealed class MapJsonConfig
        {
            public string mapId;
            public int width;
            public int height;
            public string defaultTerrain = "plain";
            public MapJsonNode[] nodes;
            public MapJsonUnit[] units;
        }

        [System.Serializable]
        private sealed class MapJsonNode
        {
            public string id;
            public int x;
            public int y;
            public string terrain;
            public bool hasRoad;
            public bool has_road;
            public bool isResourcePoint;
            public bool is_resource_point;
            public string resourceType;
            public string resource_type;
            public string buildingType;
            public string building_type;
            public int buildingHp;
            public int building_hp;
            public string owner;
            public string territoryOwner;
            public string territory_owner;
        }

        [System.Serializable]
        private sealed class MapJsonUnit
        {
            public string id;
            public string faction;
            public string unitType;
            public int hp = 100;
            public int maxHp = 100;
            public int x;
            public int y;
        }

        internal struct Options
        {
            public bool AutoFillMissingJsonTiles { get; set; }
            public string LocalFallbackMapResourcePath { get; set; }
            public string ServerMapConfigKey { get; set; }
        }

        private readonly Options _options;

        public MapSourceResolver(Options options)
        {
            _options = options;
        }

        public bool TryResolveServerConfig(ConfigCache cache, out MapSourceSnapshot snapshot)
        {
            snapshot = default;
            if (cache == null || string.IsNullOrWhiteSpace(_options.ServerMapConfigKey))
            {
                return false;
            }

            if (!cache.TryGetJson(_options.ServerMapConfigKey.Trim(), out var json) || string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            return TryParseJsonString(json, out snapshot);
        }

        public bool TryResolveStaticCatalog(StaticCatalogCache cache, out MapSourceSnapshot snapshot)
        {
            snapshot = default;
            if (cache == null)
            {
                return false;
            }

            if (!cache.TryGetDefaultMap(out var mapBundle) || mapBundle?.nodes == null || mapBundle.nodes.Length == 0)
            {
                return false;
            }

            var nodes = new List<NodeDto>(mapBundle.nodes.Length);
            for (var i = 0; i < mapBundle.nodes.Length; i++)
            {
                var node = mapBundle.nodes[i];
                if (node == null)
                {
                    continue;
                }

                var buildingType = MapRenderTokens.Normalize(node.building_type);
                var axial = HexGrid.OffsetToAxial(node.x, node.y);
                nodes.Add(new NodeDto
                {
                    Id = string.IsNullOrWhiteSpace(node.id) ? $"N_{node.x}_{node.y}" : node.id.Trim(),
                    Q = axial.x,
                    R = axial.y,
                    Terrain = MapRenderTokens.Normalize(node.terrain),
                    HasRoad = node.has_road,
                    IsResourcePoint = node.is_resource_point,
                    ResourceType = MapRenderTokens.Normalize(node.resource_type),
                    Owner = MapRenderTokens.Normalize(node.owner),
                    TerritoryOwner = MapRenderTokens.Normalize(node.territory_owner),
                    BuildingType = buildingType,
                    BuildingHp = string.IsNullOrEmpty(buildingType) ? 0 : Mathf.Max(0, node.building_hp),
                    BuildingMaxHp = MapRenderTokens.ResolveBuildingMaxHp(buildingType, node.building_hp)
                });
            }

            if (nodes.Count == 0)
            {
                return false;
            }

            snapshot = new MapSourceSnapshot(nodes, new List<UnitDto>(), string.Empty);
            return true;
        }

        public bool TryResolveLocalFallback(out MapSourceSnapshot snapshot)
        {
            snapshot = default;
            if (string.IsNullOrWhiteSpace(_options.LocalFallbackMapResourcePath))
            {
                return false;
            }

            var asset = Resources.Load<TextAsset>(_options.LocalFallbackMapResourcePath.Trim());
            return asset != null && TryParseJsonAsset(asset, out snapshot);
        }

        public bool TryParseJsonAsset(TextAsset jsonAsset, out MapSourceSnapshot snapshot)
        {
            snapshot = default;
            if (jsonAsset == null)
            {
                Debug.LogWarning("[MapRenderer] LoadMapFromJsonAsset failed: asset is null.");
                return false;
            }

            return TryParseJsonString(jsonAsset.text, out snapshot);
        }

        public bool TryParseJsonString(string json, out MapSourceSnapshot snapshot)
        {
            snapshot = default;
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning("[MapRenderer] Map JSON is empty.");
                return false;
            }

            MapJsonConfig config = null;

            try
            {
                config = JsonUtility.FromJson<MapJsonConfig>(json);
            }
            catch
            {
                // Fall through to envelope parse.
            }

            if (config == null || config.nodes == null || config.nodes.Length == 0)
            {
                try
                {
                    var envelope = JsonUtility.FromJson<MapJsonEnvelope>(json);
                    config = envelope?.map;
                }
                catch
                {
                    // Keep null config; handled below.
                }
            }

            if (config == null || config.nodes == null || config.nodes.Length == 0)
            {
                Debug.LogWarning("[MapRenderer] Failed to parse map JSON or JSON has no nodes.");
                return false;
            }

            var nodes = BuildNodesFromJsonConfig(config);
            if (nodes.Count == 0)
            {
                Debug.LogWarning("[MapRenderer] Parsed map JSON but got 0 valid nodes.");
                return false;
            }

            var units = BuildUnitsFromJsonConfig(config);
            snapshot = new MapSourceSnapshot(nodes, units, config.mapId);
            Debug.Log($"[MapRenderer] Loaded map JSON: mapId='{config.mapId}', nodes={nodes.Count}, units={units.Count}.");
            return true;
        }

        public static bool HasTerritorySnapshot(List<NodeDto> nodes)
        {
            if (nodes == null || nodes.Count == 0)
            {
                return false;
            }

            var hasTerritory = false;
            var cityCoreCount = 0;
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(node.TerritoryOwner))
                {
                    hasTerritory = true;
                }

                if (string.Equals(MapRenderTokens.Normalize(node.BuildingType), "city_core", System.StringComparison.Ordinal))
                {
                    cityCoreCount++;
                }
            }

            return hasTerritory && cityCoreCount >= 4;
        }

        private List<NodeDto> BuildNodesFromJsonConfig(MapJsonConfig config)
        {
            var result = new List<NodeDto>();
            if (config == null || config.nodes == null)
            {
                return result;
            }

            var width = Mathf.Max(0, config.width);
            var height = Mathf.Max(0, config.height);
            var defaultTerrain = MapRenderTokens.Normalize(config.defaultTerrain);
            if (string.IsNullOrEmpty(defaultTerrain))
            {
                defaultTerrain = "plain";
            }

            var nodeByKey = new Dictionary<string, MapJsonNode>();
            for (var i = 0; i < config.nodes.Length; i++)
            {
                var jsonNode = config.nodes[i];
                if (jsonNode == null)
                {
                    continue;
                }

                if (width > 0 && (jsonNode.x < 0 || jsonNode.x >= width))
                {
                    continue;
                }

                if (height > 0 && (jsonNode.y < 0 || jsonNode.y >= height))
                {
                    continue;
                }

                nodeByKey[MakeCoordKey(jsonNode.x, jsonNode.y)] = jsonNode;
            }

            if (_options.AutoFillMissingJsonTiles && width > 0 && height > 0)
            {
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var key = MakeCoordKey(x, y);
                        if (!nodeByKey.ContainsKey(key))
                        {
                            nodeByKey[key] = new MapJsonNode
                            {
                                x = x,
                                y = y,
                                terrain = defaultTerrain
                            };
                        }
                    }
                }
            }

            foreach (var pair in nodeByKey)
            {
                var jsonNode = pair.Value;
                var terrain = MapRenderTokens.Normalize(jsonNode.terrain);
                if (string.IsNullOrEmpty(terrain))
                {
                    terrain = defaultTerrain;
                }

                var buildingType = MapRenderTokens.Normalize(jsonNode.buildingType);
                if (string.IsNullOrEmpty(buildingType))
                {
                    buildingType = MapRenderTokens.Normalize(jsonNode.building_type);
                }

                var resourceType = MapRenderTokens.Normalize(jsonNode.resourceType);
                if (string.IsNullOrEmpty(resourceType))
                {
                    resourceType = MapRenderTokens.Normalize(jsonNode.resource_type);
                }

                var hasRoad = jsonNode.hasRoad || jsonNode.has_road;
                var isResourcePoint = jsonNode.isResourcePoint || jsonNode.is_resource_point || !string.IsNullOrEmpty(resourceType);
                var hasBuilding = !string.IsNullOrEmpty(buildingType);
                var buildingHp = jsonNode.buildingHp > 0 ? jsonNode.buildingHp : jsonNode.building_hp;
                var territoryOwner = MapRenderTokens.Normalize(jsonNode.territoryOwner);
                if (string.IsNullOrEmpty(territoryOwner))
                {
                    territoryOwner = MapRenderTokens.Normalize(jsonNode.territory_owner);
                }

                var axial = HexGrid.OffsetToAxial(jsonNode.x, jsonNode.y);
                result.Add(new NodeDto
                {
                    Id = string.IsNullOrWhiteSpace(jsonNode.id) ? $"N_{jsonNode.x}_{jsonNode.y}" : jsonNode.id.Trim(),
                    Q = axial.x,
                    R = axial.y,
                    Terrain = terrain,
                    HasRoad = hasRoad,
                    IsResourcePoint = isResourcePoint,
                    ResourceType = isResourcePoint ? resourceType : string.Empty,
                    BuildingType = buildingType,
                    BuildingHp = hasBuilding ? (buildingHp > 0 ? buildingHp : 100) : 0,
                    BuildingMaxHp = hasBuilding ? MapRenderTokens.ResolveBuildingMaxHp(buildingType, buildingHp) : 0,
                    Owner = (jsonNode.owner ?? string.Empty).Trim(),
                    TerritoryOwner = territoryOwner
                });
            }

            return result;
        }

        private static List<UnitDto> BuildUnitsFromJsonConfig(MapJsonConfig config)
        {
            var output = new List<UnitDto>();
            if (config?.units == null)
            {
                return output;
            }

            for (var i = 0; i < config.units.Length; i++)
            {
                var src = config.units[i];
                if (src == null)
                {
                    continue;
                }

                var axial = HexGrid.OffsetToAxial(src.x, src.y);
                output.Add(new UnitDto
                {
                    Id = string.IsNullOrWhiteSpace(src.id) ? $"U_{src.x}_{src.y}_{i}" : src.id.Trim(),
                    Owner = (src.faction ?? string.Empty).Trim(),
                    Type = string.IsNullOrWhiteSpace(src.unitType) ? "infantry" : src.unitType.Trim(),
                    Hp = Mathf.Max(0, src.hp),
                    MaxHp = Mathf.Max(1, src.maxHp),
                    Q = axial.x,
                    R = axial.y,
                });
            }

            return output;
        }

        private static string MakeCoordKey(int x, int y)
        {
            return $"{x}_{y}";
        }
    }
}
