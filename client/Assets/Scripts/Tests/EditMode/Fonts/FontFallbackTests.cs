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
            var sourcePath = Path.GetFullPath("Assets/Scripts/Runtime/UI/Lobby/LobbySceneController.cs");
            Assert.That(File.Exists(sourcePath), Is.True, "LobbySceneController.cs 不存在。");

            var content = File.ReadAllText(sourcePath);
            StringAssert.Contains("Register<MsgRoomState>(\"MsgRoomState\", OnRoomState)", content,
                "Canvas 层控制器必须接住第一条房间状态，避免 RoomPanel 默认隐藏时丢失 MsgRoomState。");
        }

        [Test]
        public void LobbyScene_ShouldAttachLobbySceneControllerToCanvas()
        {
            var scenePath = Path.GetFullPath("Assets/Scenes/Lobby.unity");
            Assert.That(File.Exists(scenePath), Is.True, "Lobby.unity 不存在。");

            var content = File.ReadAllText(scenePath);
            StringAssert.Contains("Panoptes.Runtime.UI.Lobby.LobbySceneController", content,
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
            StringAssert.Contains("Panoptes.Runtime.UI.Game.GameSceneController", content,
                "Game 场景必须挂载 GameSceneController。");
            StringAssert.Contains("m_Name: StatusText", content,
                "Game 场景必须包含状态占位文本。");
        }
    }
}
