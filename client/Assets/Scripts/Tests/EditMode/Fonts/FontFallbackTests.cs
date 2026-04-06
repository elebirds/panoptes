using System.IO;
using NUnit.Framework;

namespace Panoptes.Tests.EditMode.Fonts
{
    public sealed class FontFallbackTests
    {
        [Test]
        public void TmpSettings_ShouldConfigureGlobalFallbackFonts()
        {
            var assetPath = Path.GetFullPath("Assets/TextMesh Pro/Resources/TMP Settings.asset");
            Assert.That(File.Exists(assetPath), Is.True, "TMP Settings.asset 不存在。");

            var content = File.ReadAllText(assetPath);
            StringAssert.Contains("m_fallbackFontAssets:", content);
            Assert.That(content, Does.Not.Contain("m_fallbackFontAssets: []"),
                "TMP 全局 fallback 字体为空，中文在不同系统上会出现缺字。");
        }

        [Test]
        public void CjkFallbackAsset_ShouldContainMaterialAndAtlasTexture()
        {
            var assetPath = Path.GetFullPath("Assets/TextMesh Pro/Resources/Fonts & Materials/Panoptes CJK Fallback.asset");
            Assert.That(File.Exists(assetPath), Is.True, "Panoptes CJK Fallback.asset 不存在。");

            var content = File.ReadAllText(assetPath);
            Assert.That(content, Does.Not.Contain("m_Material: {fileID: 0}"),
                "CJK fallback 字体资产缺少材质引用。");
            Assert.That(content, Does.Not.Contain("m_AtlasTextures:\n  - {fileID: 0}"),
                "CJK fallback 字体资产缺少 atlas 纹理。");
        }

        [Test]
        public void LobbyScene_ShouldUseInputSystemUiModule()
        {
            var scenePath = Path.GetFullPath("Assets/Scenes/Lobby.unity");
            Assert.That(File.Exists(scenePath), Is.True, "Lobby.unity 不存在。");

            var content = File.ReadAllText(scenePath);
            Assert.That(content, Does.Not.Contain("UnityEngine.EventSystems.StandaloneInputModule"),
                "Lobby 场景仍在使用 StandaloneInputModule。");
            StringAssert.Contains("UnityEngine.InputSystem.UI.InputSystemUIInputModule", content);
        }
    }
}
