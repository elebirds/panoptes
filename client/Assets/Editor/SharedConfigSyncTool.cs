using System.IO;
using UnityEditor;
using UnityEngine;

namespace Panoptes.EditorTools
{
    public static class SharedConfigSyncTool
    {
        private const string MenuPath = "Panoptes/Config/Sync Shared Config To Resources";
        private const string TargetConfigFolder = "Assets/Resources/Config";

        [MenuItem(MenuPath)]
        private static void SyncSharedConfigToResources()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var sharedConfigRoot = Path.Combine(projectRoot, "config");

            if (!Directory.Exists(sharedConfigRoot))
            {
                Debug.LogError($"[SharedConfigSyncTool] Shared config folder not found: {sharedConfigRoot}");
                return;
            }

            EnsureFolderRecursive(TargetConfigFolder);

            var copied = 0;
            copied += CopyJsonFiles(Path.Combine(sharedConfigRoot), Path.GetFullPath(TargetConfigFolder));
            copied += CopyJsonFiles(Path.Combine(sharedConfigRoot, "maps"), Path.GetFullPath(TargetConfigFolder));

            AssetDatabase.Refresh();
            Debug.Log($"[SharedConfigSyncTool] Sync completed. Copied {copied} json file(s) to {TargetConfigFolder}.");
        }

        private static int CopyJsonFiles(string sourceFolder, string destFolder)
        {
            if (!Directory.Exists(sourceFolder))
            {
                return 0;
            }

            var files = Directory.GetFiles(sourceFolder, "*.json", SearchOption.TopDirectoryOnly);
            var count = 0;

            for (var i = 0; i < files.Length; i++)
            {
                var src = files[i];
                var name = Path.GetFileName(src);
                if (name.EndsWith(".schema.json"))
                {
                    continue;
                }

                // When copying maps, keep file name as-is.
                // Example: config/maps/map_initial_4regions_20x20.json -> Assets/Resources/Config/map_initial_4regions_20x20.json
                var dst = Path.Combine(destFolder, name);
                File.Copy(src, dst, true);
                count++;
            }

            return count;
        }

        private static void EnsureFolderRecursive(string folderPath)
        {
            var normalized = folderPath.Replace("\\", "/");
            var parts = normalized.Split('/');
            if (parts.Length == 0) return;

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
    }
}
