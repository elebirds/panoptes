using System.Collections.Generic;
using Panoptes.Core.Domain;
using UnityEngine;

namespace Panoptes.Presentation.Map
{
    public static class DebugMapFactory
    {
        private const int DebugMapSize = 30;
        private const int DebugTerritorySize = 3;

        public struct Options
        {
            public int Width;
            public int Height;
            public int Seed;
            public bool GenerateTerritories;
            public bool GenerateBuildings;
            public int BuildingsPerType;
            public float ExtraBuildingSpawnRate;
        }

        public static List<NodeDto> CreateNodes(Options options)
        {
            var mapWidth = Mathf.Max(DebugMapSize, options.Width);
            var mapHeight = Mathf.Max(DebugMapSize, options.Height);
            var nodes = new List<NodeDto>(mapWidth * mapHeight);
            var rng = new System.Random(options.Seed);

            for (var y = 0; y < mapHeight; y++)
            {
                for (var x = 0; x < mapWidth; x++)
                {
                    nodes.Add(new NodeDto
                    {
                        Id = $"N_{x}_{y}",
                        Q = x,
                        R = y,
                        Terrain = RollTerrain(rng),
                        BuildingType = string.Empty,
                        BuildingHp = 0,
                        Owner = string.Empty,
                        TerritoryOwner = string.Empty,
                        HasRoad = false,
                        IsResourcePoint = false,
                        ResourceType = string.Empty
                    });
                }
            }

            if (options.GenerateTerritories)
            {
                ApplyDebugTerritories(nodes, mapWidth, mapHeight);
            }

            if (options.GenerateBuildings)
            {
                PlaceDebugBuildings(nodes, rng, options.BuildingsPerType, options.ExtraBuildingSpawnRate);
            }

            return nodes;
        }

        private static void ApplyDebugTerritories(List<NodeDto> nodes, int mapWidth, int mapHeight)
        {
            if (nodes == null || nodes.Count == 0)
            {
                return;
            }

            var centers = GetDebugTerritoryCenters(mapWidth, mapHeight);
            var owners = GetDebugTerritoryOwners();
            var territorySizeHalf = Mathf.Max(1, DebugTerritorySize / 2);

            for (var i = 0; i < centers.Length && i < owners.Length; i++)
            {
                var center = centers[i];
                var owner = NormalizeToken(owners[i]);
                if (string.IsNullOrEmpty(owner))
                {
                    continue;
                }

                for (var y = center.y - territorySizeHalf; y <= center.y + territorySizeHalf; y++)
                {
                    for (var x = center.x - territorySizeHalf; x <= center.x + territorySizeHalf; x++)
                    {
                        if (!TryGetDebugNode(nodes, mapWidth, mapHeight, x, y, out var node) || node == null)
                        {
                            continue;
                        }

                        if (HexGrid.AxialDistance(center, new Vector2Int(x, y)) > territorySizeHalf)
                        {
                            continue;
                        }

                        node.TerritoryOwner = owner;
                        node.Terrain = "plain";
                        node.HasRoad = true;
                        node.Owner = string.IsNullOrEmpty(node.Owner) ? string.Empty : node.Owner;
                        node.IsResourcePoint = false;
                        node.ResourceType = string.Empty;
                        node.BuildingType = string.Empty;
                        node.BuildingHp = 0;
                    }
                }

                if (TryGetDebugNode(nodes, mapWidth, mapHeight, center.x, center.y, out var cityCoreNode) && cityCoreNode != null)
                {
                    cityCoreNode.TerritoryOwner = owner;
                    cityCoreNode.Owner = owner;
                    cityCoreNode.BuildingType = "city_core";
                    cityCoreNode.BuildingHp = 200;
                }
            }
        }

        private static Vector2Int[] GetDebugTerritoryCenters(int mapWidth, int mapHeight)
        {
            var leftCenterX = Mathf.Clamp(5, 1, Mathf.Max(1, mapWidth - 2));
            var rightCenterX = Mathf.Clamp(mapWidth - 6, 1, Mathf.Max(1, mapWidth - 2));
            var topCenterY = Mathf.Clamp(5, 1, Mathf.Max(1, mapHeight - 2));
            var bottomCenterY = Mathf.Clamp(mapHeight - 6, 1, Mathf.Max(1, mapHeight - 2));

            return new[]
            {
                new Vector2Int(leftCenterX, topCenterY),
                new Vector2Int(rightCenterX, topCenterY),
                new Vector2Int(leftCenterX, bottomCenterY),
                new Vector2Int(rightCenterX, bottomCenterY)
            };
        }

        private static string[] GetDebugTerritoryOwners()
        {
            return new[] { "blue", "red", "green", "yellow" };
        }

        private static bool TryGetDebugNode(List<NodeDto> nodes, int mapWidth, int mapHeight, int x, int y, out NodeDto node)
        {
            node = null;
            if (nodes == null || x < 0 || y < 0 || x >= mapWidth || y >= mapHeight)
            {
                return false;
            }

            var index = y * mapWidth + x;
            if (index < 0 || index >= nodes.Count)
            {
                return false;
            }

            node = nodes[index];
            return node != null;
        }

        private static string RollTerrain(System.Random rng)
        {
            var r = rng.NextDouble();
            if (r < 0.08d) return "river";
            if (r < 0.24d) return "forest";
            if (r < 0.40d) return "mountain";
            if (r < 0.54d) return "snow";
            return "plain";
        }

        private static void PlaceDebugBuildings(List<NodeDto> nodes, System.Random rng, int buildingsPerType, float extraBuildingSpawnRate)
        {
            if (nodes == null || nodes.Count == 0)
            {
                return;
            }

            var requiredTypes = new[]
            {
                "farm",
                "lumberyard",
                "smelter",
                "engineer",
                "workshop",
                "archery",
                "barracks",
                "blacksmith",
                "tower",
                "watchtower"
            };

            var perType = Mathf.Max(1, buildingsPerType);
            for (var i = 0; i < requiredTypes.Length; i++)
            {
                PlaceDebugBuildingType(nodes, rng, requiredTypes[i], perType);
            }

            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null || !string.IsNullOrEmpty(node.BuildingType))
                {
                    continue;
                }

                if (!IsBuildableTerrainForDebug(node.Terrain))
                {
                    continue;
                }

                if (rng.NextDouble() > extraBuildingSpawnRate)
                {
                    continue;
                }

                var randomType = requiredTypes[rng.Next(requiredTypes.Length)];
                if (CanPlaceDebugBuilding(node, randomType))
                {
                    ApplyDebugBuilding(node, randomType, rng);
                }
            }
        }

        private static void PlaceDebugBuildingType(List<NodeDto> nodes, System.Random rng, string buildingType, int count)
        {
            if (nodes == null || nodes.Count == 0 || count <= 0)
            {
                return;
            }

            var target = Mathf.Max(1, count);
            var placed = 0;
            var maxAttempts = Mathf.Max(nodes.Count * 4, 64);
            var attempts = 0;

            while (placed < target && attempts < maxAttempts)
            {
                attempts++;
                var node = nodes[rng.Next(nodes.Count)];
                if (!CanPlaceDebugBuilding(node, buildingType))
                {
                    continue;
                }

                ApplyDebugBuilding(node, buildingType, rng);
                placed++;
            }
        }

        private static bool CanPlaceDebugBuilding(NodeDto node, string buildingType)
        {
            if (node == null || string.IsNullOrEmpty(buildingType))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(node.BuildingType))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(node.TerritoryOwner))
            {
                return false;
            }

            return IsBuildableTerrainForDebug(node.Terrain);
        }

        private static bool IsBuildableTerrainForDebug(string terrain)
        {
            var normalized = NormalizeToken(terrain);
            return normalized != "river"
                   && normalized != "water"
                   && normalized != "forbidden"
                   && normalized != "blocked";
        }

        private static void ApplyDebugBuilding(NodeDto node, string buildingType, System.Random rng)
        {
            node.BuildingType = buildingType;
            node.BuildingHp = 100;

            if (IsResourceBuilding(buildingType))
            {
                node.IsResourcePoint = true;
                node.ResourceType = GetResourceTypeForBuilding(buildingType);
            }
            else if (!node.IsResourcePoint)
            {
                node.ResourceType = string.Empty;
            }

            node.Owner = GetDebugOwner(rng);
        }

        private static bool IsResourceBuilding(string buildingType)
        {
            switch (NormalizeToken(buildingType))
            {
                case "farm":
                case "lumberyard":
                case "smelter":
                case "mine":
                case "lumber":
                    return true;
                default:
                    return false;
            }
        }

        private static string GetResourceTypeForBuilding(string buildingType)
        {
            switch (NormalizeToken(buildingType))
            {
                case "farm":
                    return "food";
                case "lumberyard":
                case "lumber":
                    return "wood";
                case "smelter":
                case "mine":
                    return "ore";
                default:
                    return string.Empty;
            }
        }

        private static string GetDebugOwner(System.Random rng)
        {
            var owners = new[] { "blue", "red", "green", "yellow" };
            return owners[rng.Next(owners.Length)];
        }

        private static string NormalizeToken(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }
    }
}
