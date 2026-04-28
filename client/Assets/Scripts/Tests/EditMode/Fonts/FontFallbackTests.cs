using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Panoptes.Tests.EditMode.Fonts
{
    public sealed class FontFallbackTests
    {
        private readonly string _loadingOverlayPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/Common/LoadingOverlay.cs");
        private readonly string _errorToastPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/Common/ErrorToast.cs");
        private readonly string _confirmDialogPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/Common/ConfirmDialog.cs");

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
        public void CjkFallbackAsset_ShouldKeepDynamicAtlasDataClearedForVersionControl()
        {
            var assetPath = Path.GetFullPath("Assets/TextMesh Pro/Resources/Fonts & Materials/Panoptes CJK Fallback.asset");
            Assert.That(File.Exists(assetPath), Is.True, "Panoptes CJK Fallback.asset 不存在。");

            var content = File.ReadAllText(assetPath);
            StringAssert.Contains("m_AtlasPopulationMode: 1", content);
            StringAssert.Contains("m_ClearDynamicDataOnBuild: 1", content);
            StringAssert.Contains("m_GlyphTable: []", content);
            StringAssert.Contains("m_CharacterTable: []", content);
            StringAssert.Contains("m_UsedGlyphRects: []", content);
        }

        [Test]
        public void LobbyScene_ShouldUseInputSystemUiModule()
        {
            var scenePath = Path.GetFullPath("Assets/Scenes/Lobby.unity");
            Assert.That(File.Exists(scenePath), Is.True, "Lobby.unity 不存在。");

            var content = File.ReadAllText(scenePath);
            Assert.That(content, Does.Not.Contain("UnityEngine.EventSystems.StandaloneInputModule"),
                "Lobby 场景仍在使用 StandaloneInputModule。");
            StringAssert.Contains("InputSystemUIInputModule", content);
        }

        [Test]
        public void LobbyScene_ShouldKeepRoomPanelHiddenByDefault()
        {
            var scenePath = Path.GetFullPath("Assets/Scenes/Lobby.unity");
            Assert.That(File.Exists(scenePath), Is.True, "Lobby.unity 不存在。");

            var content = File.ReadAllText(scenePath);
            var roomPanelIndex = content.IndexOf("m_Name: RoomPanel");
            Assert.That(roomPanelIndex, Is.GreaterThanOrEqualTo(0), "Lobby 场景缺少 RoomPanel。");

            var activeFlagIndex = content.IndexOf("m_IsActive: 0", roomPanelIndex);
            Assert.That(activeFlagIndex, Is.GreaterThan(roomPanelIndex),
                "RoomPanel 默认应隐藏，避免 Lobby 与 Room 两个 Panel 同时显示。");
        }

        [Test]
        public void LobbySceneController_ShouldRegisterRoomStateHandler()
        {
            var sourcePath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/Lobby/LobbySceneController.cs");
            Assert.That(File.Exists(sourcePath), Is.True, "LobbySceneController.cs 不存在。");

            var content = File.ReadAllText(sourcePath);
            StringAssert.Contains("_cache.OnRoomStateChanged += OnRoomState;", content,
                "Canvas 层控制器必须订阅 RoomCache 房间状态事件，避免 RoomPanel 默认隐藏时丢失首帧状态。");
        }

        [Test]
        public void LobbyScene_ShouldAttachLobbySceneControllerToCanvas()
        {
            var scenePath = Path.GetFullPath("Assets/Scenes/Lobby.unity");
            Assert.That(File.Exists(scenePath), Is.True, "Lobby.unity 不存在。");

            var content = File.ReadAllText(scenePath);
            StringAssert.Contains("Panoptes.Presentation.UI.Lobby.LobbySceneController", content,
                "Lobby 场景的 Canvas 必须挂载 LobbySceneController。");
        }

        [Test]
        public void LobbyScene_ShouldContainAddBotButton()
        {
            var scenePath = Path.GetFullPath("Assets/Scenes/Lobby.unity");
            Assert.That(File.Exists(scenePath), Is.True, "Lobby.unity 不存在。");

            var content = File.ReadAllText(scenePath);
            StringAssert.Contains("m_Name: AddBotButton", content,
                "Lobby 房间面板缺少 AddBotButton。");
            StringAssert.Contains("m_Name: StartGameButton", content,
                "Lobby 房间面板缺少 StartGameButton。");
        }

        [Test]
        public void PlayerSlotPrefab_ShouldContainBotBadgeAndKickButton()
        {
            var prefabPath = Path.GetFullPath("Assets/Prefabs/UI/PlayerSlot.prefab");
            Assert.That(File.Exists(prefabPath), Is.True, "PlayerSlot.prefab 不存在。");

            var content = File.ReadAllText(prefabPath);
            StringAssert.Contains("m_Name: BotBadge", content);
            StringAssert.Contains("m_Name: KickButton", content);
        }

        [Test]
        public void GameScene_ShouldAttachGameSceneController()
        {
            var scenePath = Path.GetFullPath("Assets/Scenes/Game.unity");
            Assert.That(File.Exists(scenePath), Is.True, "Game.unity 不存在。");

            var content = File.ReadAllText(scenePath);
            StringAssert.Contains("Panoptes.Presentation.UI.Game.GameSceneController", content,
                "Game 场景必须挂载 GameSceneController。");
            StringAssert.Contains("m_Name: StatusText", content,
                "Game 场景必须包含状态占位文本。");
        }

        [Test]
        public void LoadingOverlay_ShouldAssignTmpFont_WhenCreatingRuntimeMessageText()
        {
            Assert.That(File.Exists(_loadingOverlayPath), Is.True, "LoadingOverlay.cs 不存在。");

            var content = File.ReadAllText(_loadingOverlayPath);
            StringAssert.Contains("text.font = TMP_Settings.defaultFontAsset;", content,
                "LoadingOverlay 动态创建 TextMeshProUGUI 时必须显式绑定默认字体。");
        }

        [Test]
        public void ErrorToast_ShouldAssignTmpFont_WhenCreatingRuntimeMessageText()
        {
            Assert.That(File.Exists(_errorToastPath), Is.True, "ErrorToast.cs 不存在。");

            var content = File.ReadAllText(_errorToastPath);
            Assert.That(Regex.IsMatch(content, @"\.font\s*=\s*TMP_Settings\.defaultFontAsset\s*;"), Is.True,
                "ErrorToast 动态创建 TextMeshProUGUI 时必须显式绑定默认字体。");
        }

        [Test]
        public void ConfirmDialog_ShouldAssignTmpFont_WhenCreatingRuntimeTexts()
        {
            Assert.That(File.Exists(_confirmDialogPath), Is.True, "ConfirmDialog.cs 不存在。");

            var content = File.ReadAllText(_confirmDialogPath);
            Assert.That(Regex.Matches(content, @"\.font\s*=\s*TMP_Settings\.defaultFontAsset\s*;").Count, Is.GreaterThanOrEqualTo(2),
                "ConfirmDialog 动态创建 TextMeshProUGUI 时必须显式绑定默认字体。");
        }
    }
}
