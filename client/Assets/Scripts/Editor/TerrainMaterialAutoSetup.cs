using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Panoptes.Editor
{
    public static class TerrainMaterialAutoSetup
    {
        private const string TextureRoot = "Assets/Art/textures";
        private const string StylizedTextureRoot = "Assets/Art/textures/terrain/stylized/VoxelCoreLab";
        private const string AmbientCgTextureRoot = "Assets/Art/textures/terrain/ambientcg";
        private const string MaterialRoot = "Assets/Art/Materials/Terrain";
        private const string NodeTilePrefabPath = "Assets/Prefabs/Map/NodeTile3D.prefab";
        private const string AutoSetupSignatureKey = "Panoptes.TerrainMaterialAutoSetup.Signature";

        private sealed class TerrainSet
        {
            public string key;
            public string[] diffuseExactNames;
            public string[] diffusePrefixes;
            public string[] normalExactNames;
            public string[] normalPrefixes;
            public Color baseTint = Color.white;
            public float smoothness = 0.2f;
            public float normalScale = 1f;
        }

        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            EditorApplication.delayCall += TryAutoSetupStylizedTerrain;
        }

        [MenuItem("Panoptes/Terrain/Generate Materials From Art/textures")]
        public static void GenerateMaterialsOnly()
        {
            EnsureFolder(MaterialRoot);

            var index = BuildTextureIndex(TextureRoot);
            if (index.Count == 0)
            {
                Debug.LogError($"[TerrainMaterialAutoSetup] No textures found under: {TextureRoot}");
                return;
            }

            foreach (var set in BuildTerrainSets())
            {
                CreateOrUpdateMaterial(index, set);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[TerrainMaterialAutoSetup] Terrain materials generated.");
        }

        [MenuItem("Panoptes/Terrain/Generate + Assign NodeTile3D")]
        public static void GenerateAndAssignNodeTile()
        {
            GenerateMaterialsOnly();
            AssignNodeTileMaterials();
        }

        [MenuItem("Panoptes/Terrain/Assign Materials To NodeTile3D")]
        public static void AssignNodeTileMaterials()
        {
            if (!File.Exists(ToAbsolutePath(NodeTilePrefabPath)))
            {
                Debug.LogError($"[TerrainMaterialAutoSetup] NodeTile prefab not found: {NodeTilePrefabPath}");
                return;
            }

            var nodeRoot = PrefabUtility.LoadPrefabContents(NodeTilePrefabPath);
            try
            {
                var nodeView = FindNodeViewComponent(nodeRoot);
                if (nodeView == null)
                {
                    Debug.LogError("[TerrainMaterialAutoSetup] NodeView component not found in NodeTile3D prefab.");
                    return;
                }

                var plainMat = LoadTerrainMaterial("plain");
                var forestMat = LoadTerrainMaterial("forest");
                var mountainMat = LoadTerrainMaterial("mountain");
                var riverMat = LoadTerrainMaterial("river");
                var snowMat = LoadTerrainMaterial("snow");
                var forbiddenMat = LoadTerrainMaterial("forbidden");

                var serialized = new SerializedObject(nodeView);
                serialized.FindProperty("plainMaterial").objectReferenceValue = plainMat;
                serialized.FindProperty("forestMaterial").objectReferenceValue = forestMat;
                serialized.FindProperty("mountainMaterial").objectReferenceValue = mountainMat;
                serialized.FindProperty("riverMaterial").objectReferenceValue = riverMat;
                serialized.FindProperty("snowMaterial").objectReferenceValue = snowMat;
                serialized.FindProperty("forbiddenMaterial").objectReferenceValue = forbiddenMat;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(nodeRoot, NodeTilePrefabPath);
                Debug.Log("[TerrainMaterialAutoSetup] NodeTile3D terrain material slots assigned.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(nodeRoot);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void TryAutoSetupStylizedTerrain()
        {
            if (!AssetDatabase.IsValidFolder(StylizedTextureRoot) && !AssetDatabase.IsValidFolder(AmbientCgTextureRoot))
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating || Application.isPlaying)
            {
                EditorApplication.delayCall += TryAutoSetupStylizedTerrain;
                return;
            }

            var signature = BuildTextureSignature(StylizedTextureRoot, AmbientCgTextureRoot);
            if (string.IsNullOrEmpty(signature))
            {
                return;
            }

            var appliedSignature = EditorPrefs.GetString(AutoSetupSignatureKey, string.Empty);
            if (string.Equals(signature, appliedSignature, StringComparison.Ordinal))
            {
                return;
            }

            // Mark before running to prevent repeated re-entry during reimport/domain reload.
            EditorPrefs.SetString(AutoSetupSignatureKey, signature);
            GenerateAndAssignNodeTile();
            Debug.Log("[TerrainMaterialAutoSetup] Terrain textures detected, auto-applied.");
        }

        private static string BuildTextureSignature(params string[] rootFolders)
        {
            if (rootFolders == null || rootFolders.Length == 0)
            {
                return string.Empty;
            }

            var allGuids = new List<string>();
            for (var i = 0; i < rootFolders.Length; i++)
            {
                var folder = rootFolders[i];
                if (string.IsNullOrWhiteSpace(folder) || !AssetDatabase.IsValidFolder(folder))
                {
                    continue;
                }

                var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
                if (guids == null || guids.Length == 0)
                {
                    continue;
                }

                allGuids.AddRange(guids);
            }

            if (allGuids.Count == 0)
            {
                return string.Empty;
            }

            allGuids.Sort(StringComparer.Ordinal);
            return string.Join("|", allGuids);
        }

        private static MonoBehaviour FindNodeViewComponent(GameObject root)
        {
            if (root == null)
            {
                return null;
            }

            var allMono = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (var i = 0; i < allMono.Length; i++)
            {
                var mono = allMono[i];
                if (mono == null)
                {
                    continue;
                }

                if (string.Equals(mono.GetType().Name, "NodeView", StringComparison.Ordinal))
                {
                    return mono;
                }
            }

            return null;
        }

        private static IReadOnlyList<TerrainSet> BuildTerrainSets()
        {
            return new[]
            {
                new TerrainSet
                {
                    key = "plain",
                    diffuseExactNames = new[]
                    {
                        "Grass001_2K-JPG_Color.jpg",
                        "Grass001.png",
                        "Grass_02.png",
                        "Grass_01.png"
                    },
                    diffusePrefixes = new[] { "grass_bermuda_01" },
                    normalExactNames = new[] { "Grass001_2K-JPG_NormalGL.jpg", "Grass001_2K-JPG_NormalDX.jpg" },
                    normalPrefixes = new[] { "grass_bermuda_01" },
                    smoothness = 0.18f
                },
                new TerrainSet
                {
                    key = "forest",
                    diffuseExactNames = new[]
                    {
                        "Ground020_2K-JPG_Color.jpg",
                        "Ground020.png",
                        "Grass_04.png",
                        "Dirt_03.png"
                    },
                    diffusePrefixes = new[] { "muddy_tracks" },
                    normalExactNames = new[] { "Ground020_2K-JPG_NormalGL.jpg", "Ground020_2K-JPG_NormalDX.jpg" },
                    normalPrefixes = new[] { "muddy_tracks" },
                    baseTint = new Color(0.92f, 1f, 0.92f),
                    smoothness = 0.12f
                },
                new TerrainSet
                {
                    key = "mountain",
                    diffuseExactNames = new[]
                    {
                        "Ground014_2K-JPG_Color.jpg",
                        "Ground014.png",
                        "Stone_02.png",
                        "Stone_03.png"
                    },
                    diffusePrefixes = new[] { "rocky_terrain_02" },
                    normalExactNames = new[] { "Ground014_2K-JPG_NormalGL.jpg", "Ground014_2K-JPG_NormalDX.jpg" },
                    normalPrefixes = new[] { "rocky_terrain_02" },
                    smoothness = 0.08f
                },
                new TerrainSet
                {
                    key = "river",
                    diffuseExactNames = new[]
                    {
                        "Ground074_2K-JPG_Color.jpg",
                        "Ground074.png",
                        "Water_02.png",
                        "Water_01.png"
                    },
                    diffusePrefixes = new[] { "rock_tile_floor" },
                    normalExactNames = new[] { "Ground074_2K-JPG_NormalGL.jpg", "Ground074_2K-JPG_NormalDX.jpg" },
                    normalPrefixes = new[] { "rock_tile_floor" },
                    baseTint = new Color(0.72f, 0.86f, 1f),
                    smoothness = 0.35f,
                    normalScale = 0.8f
                },
                new TerrainSet
                {
                    key = "snow",
                    diffusePrefixes = new[] { "snow_01" },
                    normalPrefixes = new[] { "snow_01" },
                    smoothness = 0.42f,
                    normalScale = 0.55f
                },
                new TerrainSet
                {
                    key = "forbidden",
                    diffuseExactNames = new[]
                    {
                        "PavingStones050_2K-JPG_Color.jpg",
                        "PavingStones050.png",
                        "Dirt_04.png",
                        "Stone_04.png"
                    },
                    diffusePrefixes = new[] { "slate_floor_03" },
                    normalExactNames = new[] { "PavingStones050_2K-JPG_NormalGL.jpg", "PavingStones050_2K-JPG_NormalDX.jpg" },
                    normalPrefixes = new[] { "slate_floor_03" },
                    baseTint = new Color(0.72f, 0.72f, 0.72f),
                    smoothness = 0.12f
                }
            };
        }

        private static void CreateOrUpdateMaterial(Dictionary<string, string> textureIndex, TerrainSet set)
        {
            var diffPath = FindTexture(textureIndex, set.diffuseExactNames, set.diffusePrefixes, "_diff_");
            var normalPath = FindTexture(textureIndex, set.normalExactNames, set.normalPrefixes, "_nor_");

            if (string.IsNullOrEmpty(diffPath))
            {
                Debug.LogWarning($"[TerrainMaterialAutoSetup] Skip '{set.key}': diffuse map not found.");
                return;
            }

            EnsureTextureImportSettings(diffPath, false);
            if (!string.IsNullOrEmpty(normalPath))
            {
                EnsureTextureImportSettings(normalPath, true);
            }

            var materialPath = $"{MaterialRoot}/M_Terrain_{set.key}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                if (shader == null)
                {
                    Debug.LogError("[TerrainMaterialAutoSetup] Cannot find URP/Lit or Standard shader.");
                    return;
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }

            var diffTex = AssetDatabase.LoadAssetAtPath<Texture2D>(diffPath);
            material.SetTexture("_BaseMap", diffTex);
            material.SetTexture("_MainTex", diffTex);
            material.SetColor("_BaseColor", set.baseTint);
            material.SetColor("_Color", set.baseTint);
            material.SetFloat("_Smoothness", set.smoothness);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_OcclusionStrength", 1f);
            material.SetTextureScale("_BaseMap", Vector2.one);
            material.SetTextureScale("_MainTex", Vector2.one);

            if (!string.IsNullOrEmpty(normalPath))
            {
                var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                material.SetTexture("_BumpMap", normalTex);
                material.SetFloat("_BumpScale", set.normalScale);
                material.EnableKeyword("_NORMALMAP");
            }
            else
            {
                material.SetTexture("_BumpMap", null);
                material.DisableKeyword("_NORMALMAP");
            }

            EditorUtility.SetDirty(material);
        }

        private static Material LoadTerrainMaterial(string key)
        {
            return AssetDatabase.LoadAssetAtPath<Material>($"{MaterialRoot}/M_Terrain_{key}.mat");
        }

        private static Dictionary<string, string> BuildTextureIndex(string root)
        {
            var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!AssetDatabase.IsValidFolder(root))
            {
                return index;
            }

            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { root });
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var fileName = Path.GetFileName(path);
                if (string.IsNullOrEmpty(fileName))
                {
                    continue;
                }

                index[fileName.ToLowerInvariant()] = path;
            }

            return index;
        }

        private static string FindTexture(
            IReadOnlyDictionary<string, string> index,
            IReadOnlyList<string> exactNames,
            IReadOnlyList<string> prefixes,
            string containsToken)
        {
            if (exactNames != null)
            {
                for (var i = 0; i < exactNames.Count; i++)
                {
                    var key = (exactNames[i] ?? string.Empty).Trim().ToLowerInvariant();
                    if (string.IsNullOrEmpty(key))
                    {
                        continue;
                    }

                    if (index.TryGetValue(key, out var exactPath))
                    {
                        return exactPath;
                    }
                }
            }

            if (prefixes == null || prefixes.Count == 0)
            {
                return null;
            }

            var token = (containsToken ?? string.Empty).ToLowerInvariant();
            for (var i = 0; i < prefixes.Count; i++)
            {
                var prefix = (prefixes[i] ?? string.Empty).Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(prefix))
                {
                    continue;
                }

                var path = index
                    .FirstOrDefault(pair => pair.Key.Contains(prefix) && (string.IsNullOrEmpty(token) || pair.Key.Contains(token)))
                    .Value;
                if (!string.IsNullOrEmpty(path))
                {
                    return path;
                }
            }

            return null;
        }

        private static void EnsureTextureImportSettings(string assetPath, bool asNormalMap)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            var changed = false;

            if (importer.wrapMode != TextureWrapMode.Repeat)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                changed = true;
            }

            if (importer.filterMode != FilterMode.Bilinear)
            {
                importer.filterMode = FilterMode.Bilinear;
                changed = true;
            }

            if (asNormalMap)
            {
                if (importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    changed = true;
                }
            }
            else
            {
                if (importer.textureType != TextureImporterType.Default)
                {
                    importer.textureType = TextureImporterType.Default;
                    changed = true;
                }
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parts = folder.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static string ToAbsolutePath(string assetPath)
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(projectRoot))
            {
                return assetPath;
            }

            var normalized = assetPath.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(projectRoot, normalized);
        }
    }
}
