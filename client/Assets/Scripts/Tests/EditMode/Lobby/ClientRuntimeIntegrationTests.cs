using System;
using System.IO;
using NUnit.Framework;
using Panoptes.Presentation.UI.Game;
using UnityEngine;
using UnityEngine.TestTools;

namespace Panoptes.Tests.EditMode.Lobby
{
    public sealed class ClientRuntimeIntegrationTests
    {
        private readonly string _appManagerPath = Path.GetFullPath("Assets/Scripts/Runtime/Core/Application/App/AppManager.cs");
        private readonly string _lobbyServicePath = Path.GetFullPath("Assets/Scripts/Runtime/Core/Infrastructure/Service/LobbyService.cs");
        private readonly string _lobbyScenePath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/Lobby/LobbySceneController.cs");

        [TearDown]
        public void TearDown()
        {
            DestroySingleton("Panoptes.Core.Application.Cache.ClientRuntimeConfigCache, Panoptes.Core");
            DestroySingleton("Panoptes.Core.Application.Cache.GameStateCache, Panoptes.Core");
        }

        [Test]
        public void ClientRuntimeConfigCache_ShouldDefaultToFalse_AndRaiseChangeEvent()
        {
            var cacheType = Type.GetType("Panoptes.Core.Application.Cache.ClientRuntimeConfigCache, Panoptes.Core")
                            ?? throw new AssertionException("ClientRuntimeConfigCache 类型不存在。");
            var cacheObject = new GameObject("ClientRuntimeConfigCache");
            var cache = cacheObject.AddComponent(cacheType);

            var changedCount = 0;
            var changedEvent = cacheType.GetEvent("OnConfigChanged");
            var handler = new Action(() => changedCount++);
            changedEvent?.AddEventHandler(cache, handler);

            Assert.That(GetProperty<bool>(cache, cacheType, "DevMode"), Is.False);

            var msgType = Type.GetType("Panoptes.Protocol.V1.MsgClientRuntimeConfig, Panoptes.Protocol")
                          ?? throw new AssertionException("MsgClientRuntimeConfig 类型不存在。");
            var msg = Activator.CreateInstance(msgType)
                      ?? throw new AssertionException("无法创建 MsgClientRuntimeConfig。");
            msgType.GetProperty("DevMode")?.SetValue(msg, true);
            cacheType.GetMethod("Apply")?.Invoke(cache, new[] { msg });

            Assert.That(GetProperty<bool>(cache, cacheType, "DevMode"), Is.True);
            Assert.That(changedCount, Is.EqualTo(1));

            cacheType.GetMethod("Clear")?.Invoke(cache, Array.Empty<object>());

            Assert.That(GetProperty<bool>(cache, cacheType, "DevMode"), Is.False);
            Assert.That(changedCount, Is.EqualTo(2));
        }

        [Test]
        public void GameStateCache_ShouldRaiseStateChanged_WhenGameInitAppliedAndCleared()
        {
            var cacheObject = new GameObject("GameStateCache");
            var cache = cacheObject.AddComponent<Panoptes.Core.Application.Cache.GameStateCache>();

            var changedCount = 0;
            cache.OnStateChanged += () => changedCount++;

            var gameInitType = Type.GetType("Panoptes.Protocol.V1.MsgGameInit, Panoptes.Protocol")
                               ?? throw new AssertionException("MsgGameInit 类型不存在。");
            var playerViewType = Type.GetType("Panoptes.Protocol.V1.PlayerView, Panoptes.Protocol")
                                ?? throw new AssertionException("PlayerView 类型不存在。");
            var gameInit = Activator.CreateInstance(gameInitType)
                           ?? throw new AssertionException("无法创建 MsgGameInit。");
            var playerView = Activator.CreateInstance(playerViewType)
                            ?? throw new AssertionException("无法创建 PlayerView。");

            gameInitType.GetProperty("GameId")?.SetValue(gameInit, "game-1");
            gameInitType.GetProperty("YourPlayerId")?.SetValue(gameInit, "player-1");
            gameInitType.GetProperty("Turn")?.SetValue(gameInit, 1);
            gameInitType.GetProperty("Phase")?.SetValue(gameInit, "domestic");
            playerViewType.GetProperty("TokensLeft")?.SetValue(playerView, 3);
            gameInitType.GetProperty("MyPlayer")?.SetValue(gameInit, playerView);

            var applyMethod = typeof(Panoptes.Core.Application.Cache.GameStateCache).GetMethod("ApplyGameInit");
            applyMethod?.Invoke(cache, new[] { gameInit });

            Assert.That(cache.GameID, Is.EqualTo("game-1"));
            Assert.That(cache.MyPlayerID, Is.EqualTo("player-1"));
            Assert.That(changedCount, Is.EqualTo(1));

            cache.Clear();

            Assert.That(cache.GameID, Is.Empty);
            Assert.That(cache.MyPlayerID, Is.Empty);
            Assert.That(changedCount, Is.EqualTo(2));
        }

        [Test]
        public void LobbyService_ShouldExposeAddBotAndKickPlayerMessages()
        {
            Assert.That(File.Exists(_lobbyServicePath), Is.True, "LobbyService.cs 不存在。");

            var content = File.ReadAllText(_lobbyServicePath);
            StringAssert.Contains("public void AddBot()", content);
            StringAssert.Contains("MessageSender.Send(new MsgAddBot())", content);
            StringAssert.Contains("public void KickPlayer(string playerId)", content);
            StringAssert.Contains("MessageSender.Send(new MsgKickPlayer", content);
            StringAssert.Contains("public void StartGame()", content);
            StringAssert.Contains("MessageSender.Send(new MsgStartGame())", content);
        }

        [Test]
        public void LobbySceneController_ShouldRegisterPlayerKickedHandler()
        {
            Assert.That(File.Exists(_lobbyScenePath), Is.True, "LobbySceneController.cs 不存在。");

            var content = File.ReadAllText(_lobbyScenePath);
            StringAssert.Contains("_cache.OnPlayerKicked += OnPlayerKicked;", content);
        }

        [Test]
        public void AppManager_ShouldRegisterRuntimeConfig_AndApplyGameInitBeforeTransition()
        {
            Assert.That(File.Exists(_appManagerPath), Is.True, "AppManager.cs 不存在。");

            var content = File.ReadAllText(_appManagerPath);
            StringAssert.Contains("EnsureComponent<ClientRuntimeConfigCache>(managers);", content);
            StringAssert.Contains("EnsureComponent<GameStateCache>(managers);", content);
            StringAssert.Contains("EnsureOptionalLoadingOverlay(managers);", content);
            StringAssert.Contains("Register<MsgClientRuntimeConfig>(\"MsgClientRuntimeConfig\", OnClientRuntimeConfig)", content);

            var applyIndex = content.IndexOf("GameStateCache.Instance?.ApplyGameInit(msg);", StringComparison.Ordinal);
            var transitionIndex = content.IndexOf("TransitionTo(AppState.Game);", StringComparison.Ordinal);
            Assert.That(applyIndex, Is.GreaterThanOrEqualTo(0), "AppManager 必须先写入 GameStateCache。");
            Assert.That(transitionIndex, Is.GreaterThan(applyIndex), "AppManager 必须在 ApplyGameInit 之后再切换 Game 场景。");
        }

        [Test]
        public void GameSceneController_ShouldRenderWaitingStateWithoutWarning_WhenCacheIsEmpty()
        {
            var cacheObject = new GameObject("GameStateCache");
            var cache = cacheObject.AddComponent<Panoptes.Core.Application.Cache.GameStateCache>();
            SetSingletonInstance(typeof(Panoptes.Core.Application.Cache.GameStateCache), cache);

            var controllerObject = new GameObject("GameSceneController");
            var controller = controllerObject.AddComponent<GameSceneController>();

            InvokeLifecycle(controller, "Awake");

            LogAssert.NoUnexpectedReceived();
            controller.RefreshFromCache();
            LogAssert.NoUnexpectedReceived();
        }

        private static void DestroySingleton(string typeName)
        {
            var type = Type.GetType(typeName);
            if (type == null)
            {
                return;
            }

            var existing = UnityEngine.Object.FindAnyObjectByType(type) as Component;
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }

            SetSingletonInstance(type, null);
        }

        private static T GetProperty<T>(Component instance, Type type, string propertyName)
        {
            var property = type.GetProperty(propertyName)
                           ?? throw new AssertionException($"缺少属性 {propertyName}");
            return (T)property.GetValue(instance);
        }

        private static void InvokeLifecycle(object instance, string methodName)
        {
            var method = instance.GetType().GetMethod(methodName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (method == null)
            {
                throw new AssertionException($"缺少生命周期方法 {methodName}");
            }

            method.Invoke(instance, null);
        }

        private static void SetSingletonInstance(Type type, object value)
        {
            var field = type.GetField("<Instance>k__BackingField",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (field == null)
            {
                return;
            }

            field.SetValue(null, value);
        }
    }
}
