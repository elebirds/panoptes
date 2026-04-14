using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Panoptes.Editor
{
    public static class SkyboxAutoSetup
    {
        private const string SkyTexturePrimaryPath = "Assets/Art/Sky/HDRI/industrial_sunset_puresky_2k.exr";
        private const string SkyMaterialFolder = "Assets/Art/Materials/Sky";
        private const string SkyMaterialPath = SkyMaterialFolder + "/M_Sky_CloudLayers.mat";
        private const string AutoSetupSignatureKey = "Panoptes.SkyboxAutoSetup.Signature";
        private const int AutoSetupVersion = 9;
        private static readonly int ImageTypeId = Shader.PropertyToID("_ImageType");
        private static readonly int MirrorOnBackId = Shader.PropertyToID("_MirrorOnBack");
        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        private static readonly int RotationId = Shader.PropertyToID("_Rotation");
        private static readonly int TintId = Shader.PropertyToID("_Tint");

        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            EditorApplication.delayCall += TryAutoApplySkybox;
        }

        [MenuItem("Panoptes/Sky/Apply Cloud Layers Skybox")]
        public static void ApplyCloudLayersSkybox()
        {
            var skyTexturePath = ResolveSkyTexturePath();
            if (string.IsNullOrEmpty(skyTexturePath))
            {
                Debug.LogWarning("[SkyboxAutoSetup] No sky texture found.");
                return;
            }

            ConfigureSkyTextureImporter(skyTexturePath);

            var skyTex = AssetDatabase.LoadAssetAtPath<Texture>(skyTexturePath);
            if (skyTex == null)
            {
                Debug.LogWarning($"[SkyboxAutoSetup] Failed to load sky texture: {skyTexturePath}");
                return;
            }

            EnsureFolder(SkyMaterialFolder);

            var panoramicShader = Shader.Find("Skybox/Panoramic");
            if (panoramicShader == null)
            {
                Debug.LogError("[SkyboxAutoSetup] Shader Skybox/Panoramic not found.");
                return;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            if (material == null)
            {
                material = new Material(panoramicShader);
                AssetDatabase.CreateAsset(material, SkyMaterialPath);
            }

            material.shader = panoramicShader;
            material.SetTexture("_MainTex", skyTex);
            // Flip vertically so bottom view samples former top-cloud area.
            material.SetTextureScale("_MainTex", new Vector2(1f, -1f));
            material.SetTextureOffset("_MainTex", new Vector2(0f, 1f));
            if (material.HasProperty(ImageTypeId))
            {
                // PureSky HDRI is a 360 equirectangular panorama.
                material.SetFloat(ImageTypeId, 0f);
            }

            if (material.HasProperty(MirrorOnBackId))
            {
                // Mirror top hemisphere to bottom for down-looking camera cloud continuity.
                material.SetFloat(MirrorOnBackId, 1f);
            }

            if (material.HasProperty(ExposureId))
            {
                material.SetFloat(ExposureId, 0.62f);
            }

            if (material.HasProperty(RotationId))
            {
                material.SetFloat(RotationId, 0f);
            }

            if (material.HasProperty(TintId))
            {
                material.SetColor(TintId, new Color(0.9f, 0.87f, 0.82f, 1f));
            }
            EditorUtility.SetDirty(material);

            RenderSettings.skybox = material;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.24f, 0.28f, 0.35f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.13f, 0.15f, 0.19f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.06f, 0.07f, 0.1f, 1f);
            RenderSettings.ambientIntensity = 0.62f;
            RenderSettings.reflectionIntensity = 0.72f;

            var mainCamera = Camera.main;
            if (mainCamera != null)
            {
                mainCamera.clearFlags = CameraClearFlags.Skybox;
            }

            DynamicGI.UpdateEnvironment();
            EditorSceneManager.MarkAllScenesDirty();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[SkyboxAutoSetup] Applied skybox: {Path.GetFileName(skyTexturePath)}");
        }

        private static void TryAutoApplySkybox()
        {
            var skyTexturePath = ResolveSkyTexturePath();
            if (string.IsNullOrEmpty(skyTexturePath))
            {
                return;
            }

            var signature = AssetDatabase.AssetPathToGUID(skyTexturePath);
            if (string.IsNullOrEmpty(signature))
            {
                return;
            }

            signature = $"{signature}:v{AutoSetupVersion}";

            var applied = EditorPrefs.GetString(AutoSetupSignatureKey, string.Empty);
            if (string.Equals(applied, signature))
            {
                return;
            }

            EditorPrefs.SetString(AutoSetupSignatureKey, signature);
            ApplyCloudLayersSkybox();
        }

        private static string ResolveSkyTexturePath()
        {
            if (File.Exists(ToAbsolutePath(SkyTexturePrimaryPath)))
            {
                return SkyTexturePrimaryPath;
            }

            return string.Empty;
        }

        private static void ConfigureSkyTextureImporter(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return;
            }

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            var changed = false;
            if (importer.textureType != TextureImporterType.Default)
            {
                importer.textureType = TextureImporterType.Default;
                changed = true;
            }

            if (importer.sRGBTexture)
            {
                importer.sRGBTexture = false;
                changed = true;
            }

            if (importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = false;
                changed = true;
            }

            if (importer.textureShape != TextureImporterShape.Texture2D)
            {
                importer.textureShape = TextureImporterShape.Texture2D;
                changed = true;
            }

            if (importer.npotScale != TextureImporterNPOTScale.None)
            {
                importer.npotScale = TextureImporterNPOTScale.None;
                changed = true;
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
            for (var i = 1; i < parts.Length; i++)
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
