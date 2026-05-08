using System;
using System.IO;
using NUnit.Framework;

namespace Panoptes.Tests.EditMode.Presentation
{
    public sealed class AudioIntegrationTests
    {
        [Test]
        public void AudioAssets_ShouldBeResourcesLoadableAndManifested()
        {
            var manifest = File.ReadAllText(ResolveAssetPath("Art/Audio/manifest.json"));

            Assert.That(manifest, Does.Contain("\"resourcesPath\": \"Audio/BGM/main_menu_countryside_loop\""));
            Assert.That(manifest, Does.Contain("\"resourcesPath\": \"Audio/BGM/first_fifteen_turns_loop\""));
            Assert.That(manifest, Does.Contain("\"resourcesPath\": \"Audio/BGM/later_fifteen_turns_loop\""));
            Assert.That(manifest, Does.Contain("\"resourcesPath\": \"Audio/SFX/UI/click_confirm\""));
            Assert.That(manifest, Does.Contain("\"resourcesPath\": \"Audio/SFX/Attack/blade_hit\""));

            Assert.That(File.Exists(ResolveAssetPath("Art/Audio/Resources/Audio/BGM/main_menu_countryside_loop.wav")), Is.True);
            Assert.That(File.Exists(ResolveAssetPath("Art/Audio/Resources/Audio/SFX/UI/click_confirm.wav")), Is.True);
            Assert.That(File.Exists(ResolveAssetPath("Art/Audio/Resources/Audio/SFX/Attack/blade_hit.wav")), Is.True);
        }

        [Test]
        public void RuntimeAudio_ShouldStayPresentationOnly()
        {
            var service = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/Audio/PresentationAudioService.cs"));
            var playback = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/Map/SettlementPlaybackController.cs"));
            var gameScene = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/UI/Game/GameSceneController.cs"));

            Assert.That(service, Does.Contain("Resources.Load<AudioClip>"));
            Assert.That(service, Does.Not.Contain("NetworkManager"));
            Assert.That(service, Does.Not.Contain("Panoptes.Protocol"));
            Assert.That(playback, Does.Contain("PlayDamageAudio(evt, isBuilding: false)"));
            Assert.That(playback, Does.Contain("PlayDamageAudio(evt, isBuilding: true)"));
            Assert.That(playback, Does.Contain("if (ResolveDamageValue(evt) <= 0)"));
            Assert.That(playback, Does.Contain("_audioService?.PlayAttack(isBuilding ? AttackAudioKind.Siege : AttackAudioKind.Blade);"));
            Assert.That(gameScene, Does.Contain("PlayTurnBgm(state.Turn)"));
        }

        private static string ResolveAssetPath(string assetRelativePath)
        {
            var localPath = Path.GetFullPath(Path.Combine("Assets", assetRelativePath));
            if (File.Exists(localPath) || Directory.Exists(localPath))
            {
                return localPath;
            }

            return Path.GetFullPath(Path.Combine("client", "Assets", assetRelativePath));
        }
    }
}
