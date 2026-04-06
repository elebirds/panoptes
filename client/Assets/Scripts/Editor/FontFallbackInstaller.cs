using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Panoptes.Editor
{
    public static class FontFallbackInstaller
    {
        private const string SourceFontPath = "Assets/Fonts/SourceHanSansSC-Regular.otf";
        private const string FallbackFontAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/Panoptes CJK Fallback.asset";
        private const string WarmupCharacters = "大厅当前玩家创建房间加入输入邀请码准备取消离开等待全员已游戏即将开始连接断开正在重连连接中已连接未连接请求格式错误用户名密码系统未初始化服务器错误请稍后重试登录注册";

        [MenuItem("Panoptes/UI/Install CJK TMP Fallback")]
        public static void InstallCjkFallback()
        {
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (sourceFont == null)
            {
                throw new System.InvalidOperationException($"找不到字体文件：{SourceFontPath}");
            }

            var fontAsset = RecreateFallbackFontAsset(sourceFont);
            fontAsset.TryAddCharacters(WarmupCharacters, out _);

            var settings = TMP_Settings.GetSettings() ?? Resources.Load<TMP_Settings>("TMP Settings");
            if (settings == null)
            {
                throw new System.InvalidOperationException("找不到 TMP Settings。");
            }

            SetTmpSettingsFallbacks(settings, fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[FontFallbackInstaller] Installed TMP CJK fallback.");
        }

        public static void InstallCjkFallbackBatch()
        {
            InstallCjkFallback();
            EditorApplication.Exit(0);
        }

        private static TMP_FontAsset RecreateFallbackFontAsset(Font sourceFont)
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackFontAssetPath) != null)
            {
                AssetDatabase.DeleteAsset(FallbackFontAssetPath);
            }

            var fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                90,
                9,
                GlyphRenderMode.SDFAA,
                1024,
                1024,
                AtlasPopulationMode.Dynamic,
                true);

            fontAsset.name = "Panoptes CJK Fallback";
            AssetDatabase.CreateAsset(fontAsset, FallbackFontAssetPath);

            var atlasTexture = fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0
                ? fontAsset.atlasTextures[0]
                : null;
            if (atlasTexture == null)
            {
                throw new System.InvalidOperationException("TMP 创建的 fallback 字体缺少 atlas texture。");
            }

            atlasTexture.name = "Panoptes CJK Fallback Atlas";
            AssetDatabase.AddObjectToAsset(atlasTexture, fontAsset);

            var material = fontAsset.material;
            if (material == null)
            {
                throw new System.InvalidOperationException("TMP 创建的 fallback 字体缺少材质。");
            }

            material.name = atlasTexture.name + " Material";
            AssetDatabase.AddObjectToAsset(material, fontAsset);

            EditorUtility.SetDirty(fontAsset);
            EditorUtility.SetDirty(atlasTexture);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return fontAsset;
        }

        private static void SetTmpSettingsFallbacks(TMP_Settings settings, TMP_FontAsset fallbackFontAsset)
        {
            var settingsSerialized = new SerializedObject(settings);
            var fallbackFonts = settingsSerialized.FindProperty("m_fallbackFontAssets");
            fallbackFonts.ClearArray();
            fallbackFonts.InsertArrayElementAtIndex(0);
            fallbackFonts.GetArrayElementAtIndex(0).objectReferenceValue = fallbackFontAsset;
            settingsSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);

            var defaultFont = TMP_Settings.defaultFontAsset;
            if (defaultFont == null)
            {
                return;
            }

            var defaultFontSerialized = new SerializedObject(defaultFont);
            var fontFallbacks = defaultFontSerialized.FindProperty("m_FallbackFontAssetTable");
            fontFallbacks.ClearArray();
            if (fontFallbacks.arraySize == 0)
            {
                fontFallbacks.InsertArrayElementAtIndex(0);
            }

            fontFallbacks.GetArrayElementAtIndex(0).objectReferenceValue = fallbackFontAsset;
            defaultFontSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(defaultFont);
        }
    }
}
