using UnityEditor;

namespace Panoptes.EditorTools
{
    public static class UiIconSpriteImporter
    {
        private const string IconRoot = "Assets/Resources/Icons";

        [MenuItem("Panoptes/UI/Import UI Icons As Sprites")]
        public static void ImportUiIconsAsSprites()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { IconRoot });
            var changed = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                for (var i = 0; i < guids.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                    {
                        continue;
                    }

                    var dirty = false;
                    if (importer.textureType != TextureImporterType.Sprite)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        dirty = true;
                    }

                    if (importer.spriteImportMode != SpriteImportMode.Single)
                    {
                        importer.spriteImportMode = SpriteImportMode.Single;
                        dirty = true;
                    }

                    if (!importer.alphaIsTransparency)
                    {
                        importer.alphaIsTransparency = true;
                        dirty = true;
                    }

                    if (importer.mipmapEnabled)
                    {
                        importer.mipmapEnabled = false;
                        dirty = true;
                    }

                    if (!importer.sRGBTexture)
                    {
                        importer.sRGBTexture = true;
                        dirty = true;
                    }
                    if (importer.spritePixelsPerUnit != 128f)
                    {
                        importer.spritePixelsPerUnit = 128f;
                        dirty = true;
                    }

                    if (!dirty)
                    {
                        continue;
                    }

                    importer.SaveAndReimport();
                    changed++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.Refresh();
            UnityEngine.Debug.Log($"[UiIconSpriteImporter] Imported UI icons as sprites. Changed={changed}, scanned={guids.Length}.");
        }
    }
}
