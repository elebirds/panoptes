using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Panoptes.Editor
{
    public static class TerrainMaterialAutoSetup
    {
        private const string TextureRoot = "Assets/Art/textures";
        private const string MaterialRoot = "Assets/Art/Materials/Terrain";
        private const string NodeTilePrefabPath = "Assets/Prefabs/Map/NodeTile3D.prefab";

        private sealed class TerrainSet
        {
            public string key;
            public string texturePrefix;
            public Color baseTint = Color.white;
            public float smoothness = 0.2f;
            public float normalScale = 1.0f;
            public float tiling = 2.0f;
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
                new TerrainSet { key = "plain", texturePrefix = "grass_bermuda_01", baseTint = new Color(1f, 1f, 1f), smoothness = 0.18f, normalScale = 1f, tiling = 2.4f },
                new TerrainSet { key = "forest", texturePrefix = "muddy_tracks", baseTint = new Color(0.92f, 1.0f, 0.92f), smoothness = 0.15f, normalScale = 1f, tiling = 2.0f },
                new TerrainSet { key = "mountain", texturePrefix = "rocky_terrain_02", baseTint = new Color(1f, 1f, 1f), smoothness = 0.08f, normalScale = 1f, tiling = 1.6f },
                new TerrainSet { key = "river", texturePrefix = "rock_tile_floor", baseTint = new Color(0.72f, 0.82f, 0.95f), smoothness = 0.35f, normalScale = 0.8f, tiling = 2.0f },
                new TerrainSet { key = "snow", texturePrefix = "snow_01", baseTint = new Color(1f, 1f, 1f), smoothness = 0.42f, normalScale = 0.55f, tiling = 2.2f },
                new TerrainSet { key = "forbidden", texturePrefix = "slate_floor_03", baseTint = new Color(0.72f, 0.72f, 0.72f), smoothness = 0.12f, normalScale = 1f, tiling = 1.8f }
            };
        }

        private static void CreateOrUpdateMaterial(Dictionary<string, string> textureIndex, TerrainSet set)
        {
            var diffPath = FindTexture(textureIndex, set.texturePrefix, "_diff_");
            var normalPath = FindTexture(textureIndex, set.texturePrefix, "_nor_");

            if (string.IsNullOrEmpty(diffPath))
            {
                Debug.LogWarning($"[TerrainMaterialAutoSetup] Skip '{set.key}': diffuse map missing for prefix '{set.texturePrefix}'.");
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
            material.SetColor("_BaseColor", set.baseTint);
            material.SetFloat("_Smoothness", set.smoothness);
            material.SetFloat("_Metallic", 0f);

            if (!string.IsNullOrEmpty(normalPath))
            {
                var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                material.SetTexture("_BumpMap", normalTex);
                material.SetFloat("_BumpScale", set.normalScale);
                material.EnableKeyword("_NORMALMAP");
            }

            material.SetTextureScale("_BaseMap", new Vector2(set.tiling, set.tiling));
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

        private static string FindTexture(Dictionary<string, string> index, string prefix, string token)
        {
            var p = prefix.ToLowerInvariant();
            var t = token.ToLowerInvariant();
            foreach (var pair in index)
            {
                var file = pair.Key;
                if (file.Contains(p) && file.Contains(t))
                {
                    return pair.Value;
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
