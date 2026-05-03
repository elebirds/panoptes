using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Panoptes.Editor
{
    [InitializeOnLoad]
    internal static class PanoptesAssetIntegrityCheck
    {
        private const string WarningKey = "Panoptes.AssetIntegrityCheck.WarnedMissingLfs";

        private static readonly string[] CriticalLfsAssets =
        {
            "Assets/Art/077-research-2.png",
            "Assets/Art/Sky/HDRI/industrial_sunset_puresky_2k.exr",
            "Assets/Art/Sprites/Terrain/Top_Down_Tile_Pack/Objects/board_object_01.png",
            "Assets/Art/Models/Environment/Nature/Quaternius/Textures/Grass.png"
        };

        static PanoptesAssetIntegrityCheck()
        {
            EditorApplication.delayCall += CheckCriticalAssetsOnce;
        }

        [MenuItem("Panoptes/Diagnostics/Check LFS Assets")]
        private static void CheckCriticalAssetsFromMenu()
        {
            CheckCriticalAssets(logSuccess: true, suppressRepeatedWarning: false);
        }

        private static void CheckCriticalAssetsOnce()
        {
            CheckCriticalAssets(logSuccess: false, suppressRepeatedWarning: true);
        }

        private static void CheckCriticalAssets(bool logSuccess, bool suppressRepeatedWarning)
        {
            var missing = new StringBuilder();
            for (var i = 0; i < CriticalLfsAssets.Length; i++)
            {
                var assetPath = CriticalLfsAssets[i];
                if (!IsAssetPresent(assetPath))
                {
                    missing.AppendLine("- " + assetPath);
                }
            }

            if (missing.Length == 0)
            {
                SessionState.SetBool(WarningKey, false);
                if (logSuccess)
                {
                    Debug.Log("[Panoptes] Critical LFS assets are present.");
                }

                return;
            }

            if (suppressRepeatedWarning && SessionState.GetBool(WarningKey, false))
            {
                return;
            }

            SessionState.SetBool(WarningKey, true);
            Debug.LogWarning("[Panoptes] Critical LFS assets are missing or still Git LFS pointer files. Run `git lfs install && git lfs pull` from the repository root.\n" + missing);
        }

        private static bool IsAssetPresent(string assetPath)
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath) ?? Directory.GetCurrentDirectory();
            var fullPath = Path.Combine(projectRoot, assetPath);
            if (!File.Exists(fullPath))
            {
                return false;
            }

            using var stream = File.OpenRead(fullPath);
            var bytesToRead = (int)System.Math.Min(stream.Length, 80);
            var buffer = new byte[bytesToRead];
            _ = stream.Read(buffer, 0, buffer.Length);
            var prefix = Encoding.UTF8.GetString(buffer);
            return !prefix.StartsWith("version https://git-lfs.github.com/spec/v1", System.StringComparison.Ordinal);
        }
    }
}
