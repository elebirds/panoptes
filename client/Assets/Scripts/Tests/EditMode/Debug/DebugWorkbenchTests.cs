using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Panoptes.Tests.EditMode.Debug
{
    public sealed class DebugWorkbenchTests
    {
        private readonly string _appManagerPath = Path.GetFullPath("Assets/Scripts/Runtime/Core/Application/App/AppManager.cs");
        private readonly string _compositionBootstrapPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/Composition/PanoptesCompositionBootstrap.cs");
        private readonly string _projectCompositionPrefabPath = Path.GetFullPath("Assets/Resources/Prefabs/Composition/PanoptesProjectComposition.prefab");
        private readonly string _gameMessageHandlerPath = Path.GetFullPath("Assets/Scripts/Runtime/Core/Application/Handler/GameMessageHandler.cs");
        private readonly string _gameChatPanelControllerPath = Path.GetFullPath("Assets/Scripts/Runtime/Presentation/UI/HUD/GameChatPanelController.cs");
        private readonly string _debugPanelPath = Path.GetFullPath("Assets/Scripts/Runtime/Core/Infrastructure/Debug/DebugPanel.cs");
        private readonly string _messageLoggerPath = Path.GetFullPath("Assets/Scripts/Runtime/Core/Infrastructure/Debug/MessageLogger.cs");
        private readonly string _networkManagerPath = Path.GetFullPath("Assets/Scripts/Runtime/Core/Infrastructure/Network/NetworkManager.cs");
        private readonly string _messageSendDiagnosticsPath = Path.GetFullPath("Assets/Scripts/Runtime/Core/Infrastructure/Network/MessageSendDiagnostics.cs");

        [Test]
        public void DebugTabRegistry_ShouldExposeDefaultSixTabs()
        {
            var registryType = Type.GetType("Panoptes.DebugTools.DebugTabRegistry, Panoptes.Core")
                               ?? throw new AssertionException("DebugTabRegistry 类型不存在。");
            var createMethod = registryType.GetMethod("CreateDefaultTabs",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                               ?? throw new AssertionException("缺少 CreateDefaultTabs 静态方法。");

            var tabs = createMethod.Invoke(null, null) as IEnumerable
                       ?? throw new AssertionException("CreateDefaultTabs 必须返回可枚举集合。");

            var count = 0;
            var titles = new string[6];
            foreach (var tab in tabs)
            {
                var tabType = tab.GetType();
                var title = tabType.GetProperty("Title")?.GetValue(tab) as string;
                if (count < titles.Length)
                {
                    titles[count] = title;
                }

                count++;
            }

            Assert.That(count, Is.EqualTo(6), "当前默认应提供 6 个调试 Tab。");
            CollectionAssert.AreEqual(
                new[] { "总览", "消息时间线", "原始发送器", "Lobby", "Game", "Commands" },
                titles);
        }

        [Test]
        public void DebugMessageRegistry_ShouldParseKnownMessage_AndRejectUnknownType()
        {
            var registryType = Type.GetType("Panoptes.DebugTools.DebugMessageRegistry, Panoptes.Core")
                               ?? throw new AssertionException("DebugMessageRegistry 类型不存在。");
            var tryCreateMethod = registryType.GetMethod("TryCreateMessage",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                                  ?? throw new AssertionException("缺少 TryCreateMessage 静态方法。");

            var knownArgs = new object[] { "MsgSubmitTurn", "{}", null, null };
            var knownResult = (bool)tryCreateMethod.Invoke(null, knownArgs);
            Assert.That(knownResult, Is.True, "已知消息类型应可被解析。");
            Assert.That(knownArgs[2], Is.Not.Null, "解析成功时应返回消息实例。");
            Assert.That(knownArgs[3] as string, Is.Empty.Or.Null);

            var unknownArgs = new object[] { "MsgDoesNotExist", "{}", null, null };
            var unknownResult = (bool)tryCreateMethod.Invoke(null, unknownArgs);
            Assert.That(unknownResult, Is.False, "未知消息类型必须被拒绝。");
            StringAssert.Contains("unknown", (unknownArgs[3] as string ?? string.Empty).ToLowerInvariant());
        }

        [Test]
        public void MessageLogger_LogEntry_ShouldExposePayloadAndReplayMetadata()
        {
            var entryType = Type.GetType("Panoptes.DebugTools.MessageLogger+LogEntry, Panoptes.Core")
                            ?? throw new AssertionException("MessageLogger.LogEntry 类型不存在。");

            Assert.That(entryType.GetField("PayloadJson"), Is.Not.Null, "日志项必须保留原始 payload。");
            Assert.That(entryType.GetField("CanReplay"), Is.Not.Null, "日志项必须标记是否可重发。");
            Assert.That(entryType.GetField("Error"), Is.Not.Null, "日志项必须支持错误信息。");
        }

        [Test]
        public void MessageLogger_ShouldConsumeTypedDispatchEntries_InsteadOfLegacyEnvelope()
        {
            Assert.That(File.Exists(_messageLoggerPath), Is.True, "MessageLogger.cs 不存在。");

            var content = File.ReadAllText(_messageLoggerPath);
            StringAssert.Contains("OnDispatching(MessageDispatcher.DispatchEntry entry)", content,
                "MessageLogger 必须消费 typed DispatchEntry。");
            Assert.That(content, Does.Not.Contain("OnDispatching(Envelope envelope)"),
                "MessageLogger 不应继续依赖旧 Envelope 入站模型。");
            Assert.That(content, Does.Not.Contain("JsonParser"),
                "MessageLogger 不应为入站日志再次手动 protojson 反序列化。");
        }

        [Test]
        public void MessageLogger_ShouldSummarizeTransportV2Messages()
        {
            Assert.That(File.Exists(_messageLoggerPath), Is.True, "MessageLogger.cs 不存在。");

            var content = File.ReadAllText(_messageLoggerPath);
            StringAssert.Contains("case Problem problem:", content,
                "入站日志应支持统一 Problem 摘要。");
            StringAssert.Contains("MsgIssueUnitOrder issueUnitOrder =>", content,
                "出站日志应支持统一单位指令摘要。");
            StringAssert.Contains("MsgPlanningPathPreviewRequest planningPreview =>", content,
                "出站日志应支持 planning 路径预览摘要。");
        }

        [Test]
        public void AppManager_ShouldBootstrapDebugPanel_FromManagers()
        {
            Assert.That(File.Exists(_compositionBootstrapPath), Is.True, "PanoptesCompositionBootstrap.cs 不存在。");

            var content = File.ReadAllText(_compositionBootstrapPath);
            StringAssert.Contains("EnsureOptionalDebugPanel(managers);", content,
                "多 Tab DebugPanel 应从 Project Composition 根对象挂载，覆盖 Login/Lobby/Game。");
        }

        [Test]
        public void DebugPanel_ShouldSupportReleaseBuildOverrideSymbol()
        {
            Assert.That(File.Exists(_appManagerPath), Is.True, "AppManager.cs 不存在。");
            Assert.That(File.Exists(_debugPanelPath), Is.True, "DebugPanel.cs 不存在。");

            var appManagerContent = File.ReadAllText(_compositionBootstrapPath);
            var debugPanelContent = File.ReadAllText(_debugPanelPath);

            StringAssert.Contains("PANOPTES_DEBUG_PANEL", appManagerContent,
                "AppManager 应允许通过自定义编译符号在正式包中挂载 DebugPanel。");
            StringAssert.Contains("PANOPTES_DEBUG_PANEL", debugPanelContent,
                "DebugPanel 应允许通过自定义编译符号编进正式包。");
            StringAssert.Contains("IsClientDebugPanelBuildEnabled() || (config != null && config.DevMode)", debugPanelContent,
                "DebugPanel 显示条件应支持客户端正式包显式开启，而不只依赖服务端 DevMode。");
        }

        [Test]
        public void AppManager_ShouldBootstrapPlanningDraftCache_InsteadOfCombatDraftCache()
        {
            Assert.That(File.Exists(_projectCompositionPrefabPath), Is.True, "PanoptesProjectComposition.prefab 不存在。");

            var content = File.ReadAllText(_projectCompositionPrefabPath);
            StringAssert.Contains("PlanningDraftCache", content,
                "Managers 应挂载统一的 PlanningDraftCache。");
            Assert.That(content, Does.Not.Contain("CombatDraftCache"),
                "客户端不应再挂载旧 CombatDraftCache。");
        }

        [Test]
        public void AppManager_ShouldBootstrapGameChatCache()
        {
            Assert.That(File.Exists(_projectCompositionPrefabPath), Is.True, "PanoptesProjectComposition.prefab 不存在。");

            var content = File.ReadAllText(_projectCompositionPrefabPath);
            StringAssert.Contains("GameChatCache", content,
                "Managers 应挂载 GameChatCache，保证 HUD 可以直接订阅聊天流。");
        }

        [Test]
        public void GameMessageHandler_ShouldRegisterTurnV2MessagesOnly()
        {
            Assert.That(File.Exists(_gameMessageHandlerPath), Is.True, "GameMessageHandler.cs 不存在。");

            var content = File.ReadAllText(_gameMessageHandlerPath);
            StringAssert.Contains("Register<MsgPlanningStart>(\"MsgPlanningStart\", OnPlanningStart);", content);
            StringAssert.Contains("Register<MsgPlanningSnapshot>(\"MsgPlanningSnapshot\", OnPlanningSnapshot);", content);
            StringAssert.Contains("Register<MsgGameSync>(\"MsgGameSync\", OnGameSync);", content);
            StringAssert.Contains("Register<MsgPlanningPathPreviewResponse>(\"MsgPlanningPathPreviewResponse\", OnPlanningPathPreviewResponse);", content);
            StringAssert.Contains("Register<MsgResearchResult>(\"MsgResearchResult\", HandleResearchResult);", content);
            StringAssert.Contains("Register<MsgSetBuildingRecipeResult>(\"MsgSetBuildingRecipeResult\", HandleSetBuildingRecipeResult);", content);
            StringAssert.Contains("Register<MsgGameChatPosted>(\"MsgGameChatPosted\", HandleGameChatPosted);", content);
            StringAssert.Contains("Register<MsgGameChatSync>(\"MsgGameChatSync\", OnGameChatSync);", content);
            Assert.That(content, Does.Not.Contain("Register<ErrorResponse>(\"ErrorResponse\", OnGameError);"),
                "GameMessageHandler 不应继续注册旧 ErrorResponse。");
            Assert.That(content, Does.Not.Contain("MsgDomesticPhaseStart"));
            Assert.That(content, Does.Not.Contain("MsgCombatPhaseStart"));
            Assert.That(content, Does.Not.Contain("MsgDomesticSettlement"));
            Assert.That(content, Does.Not.Contain("MsgCombatSettlement"));
            Assert.That(content, Does.Not.Contain("MsgCombatOrdersSnapshot"));
            Assert.That(content, Does.Not.Contain("MsgCombatPathPreviewResponse"));
        }

        [Test]
        public void GameMessageHandler_ShouldNotTreatResearchOrPolicyResultsAsActiveStateWrites()
        {
            Assert.That(File.Exists(_gameMessageHandlerPath), Is.True, "GameMessageHandler.cs 不存在。");

            var content = File.ReadAllText(_gameMessageHandlerPath);
            Assert.That(content, Does.Not.Contain("UpdateResearchTarget("),
                "研究成功回执不应直接写 active cache。");
            Assert.That(content, Does.Not.Contain("UpdateActiveNationalPolicy("),
                "国策成功回执不应直接写 active cache。");
        }

        [Test]
        public void GameMessageHandler_ShouldDescribeBuildSuccessAsDraftRecorded_NotQueuedExecution()
        {
            Assert.That(File.Exists(_gameMessageHandlerPath), Is.True, "GameMessageHandler.cs 不存在。");

            var content = File.ReadAllText(_gameMessageHandlerPath);
            StringAssert.Contains("建筑建造草案已记录", content,
                "build success 文案应明确表示只是记录 planning 草案。");
            Assert.That(content, Does.Not.Contain("建筑建造已排队"),
                "build success 文案不应继续暗示 resolving 预算已经锁定。");
        }

        [Test]
        public void GameChatPanelController_ShouldExposeUiHooks_WithoutReferencingProtocol()
        {
            Assert.That(File.Exists(_gameChatPanelControllerPath), Is.True, "GameChatPanelController.cs 不存在。");

            var content = File.ReadAllText(_gameChatPanelControllerPath);
            StringAssert.Contains("SendThumbsUp()", content, "聊天面板脚本应提供直接可绑按钮的快捷方法。");
            StringAssert.Contains("SendThinking()", content, "聊天面板脚本应提供直接可绑按钮的快捷方法。");
            StringAssert.Contains("_gameIntentService.SendChatEmote", content, "聊天面板应通过注入的 GameIntentService 发送表情。");
            Assert.That(content, Does.Not.Contain("Panoptes.Protocol.V1"),
                "Presentation 层聊天脚本不应直接依赖 protocol。");
        }

        [Test]
        public void NetworkRuntime_ShouldNotExposeLegacySendRawPath()
        {
            Assert.That(File.Exists(_networkManagerPath), Is.True, "NetworkManager.cs 不存在。");
            Assert.That(File.Exists(_messageSendDiagnosticsPath), Is.True, "MessageSendDiagnostics.cs 不存在。");

            var networkContent = File.ReadAllText(_networkManagerPath);
            var diagnosticsContent = File.ReadAllText(_messageSendDiagnosticsPath);

            Assert.That(networkContent, Does.Not.Contain("public void SendRaw("),
                "Transport V2 下 NetworkManager 不应继续暴露 SendRaw。");
            Assert.That(diagnosticsContent, Does.Not.Contain("public static void Send("),
                "Transport V2 下诊断事件源不应继续暴露静态发送 API。");
        }

        [Test]
        public void DebugActionCatalog_ShouldExposeConvenientSectionAndActionFactories()
        {
            var catalogType = Type.GetType("Panoptes.DebugTools.DebugActionCatalog, Panoptes.Core")
                              ?? throw new AssertionException("DebugActionCatalog 类型不存在。");

            Assert.That(catalogType.GetMethod("Section", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static),
                Is.Not.Null,
                "应提供 Section 工厂，方便组织一组调试动作。");
            Assert.That(catalogType.GetMethod("Action", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static),
                Is.Not.Null,
                "应提供 Action 工厂，方便新增单个调试动作。");
        }

        [Test]
        public void DebugTabRegistry_ShouldKeepOverviewScrollable_AndProvideCommandsTab()
        {
            var path = Path.GetFullPath("Assets/Scripts/Runtime/Core/Infrastructure/Debug/DebugTabRegistry.cs");
            Assert.That(File.Exists(path), Is.True, "DebugTabRegistry.cs 不存在。");

            var content = File.ReadAllText(path);
            StringAssert.Contains("GUILayout.BeginScrollView", content,
                "总览页应支持滚动，避免内容增多后被截断。");
            StringAssert.Contains("ProgressBar(", content,
                "总览页应提供进度条摘要视图。");
            StringAssert.Contains("CommandDebugTab", content,
                "调试工作台应提供独立的 Commands 面板。");
        }

        [Test]
        public void GameDebugTab_ShouldExposeVisionSectionAndToggleButtons()
        {
            var path = Path.GetFullPath("Assets/Scripts/Runtime/Core/Infrastructure/Debug/DebugTabRegistry.cs");
            Assert.That(File.Exists(path), Is.True, "DebugTabRegistry.cs 不存在。");

            var content = File.ReadAllText(path);
            StringAssert.Contains("DebugGuiUtil.Section(\"Vision\")", content,
                "Game 调试页应提供独立的 Vision 区块。");
            StringAssert.Contains("开启全图", content,
                "Vision 区块应提供开启全图按钮。");
            StringAssert.Contains("关闭全图", content,
                "Vision 区块应提供关闭全图按钮。");
            StringAssert.Contains("ToggleFullMapVisionAsync", content,
                "Vision 区块应通过专用异步方法触发 debug 视野切换。");
        }

        [TestCase("ws://localhost:8080/ws", "http://localhost:8080")]
        [TestCase("wss://dev.panoptes.example/ws", "https://dev.panoptes.example")]
        [TestCase("ws://localhost:8080/game/ws", "http://localhost:8080/game")]
        public void DebugGameHttpService_ShouldResolveHttpBaseUrl_FromWebSocketUrl(string input, string expected)
        {
            var serviceType = Type.GetType("Panoptes.DebugTools.DebugGameHttpService, Panoptes.Core")
                              ?? throw new AssertionException("DebugGameHttpService 类型不存在。");
            var method = serviceType.GetMethod("ResolveBaseUrl",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                         ?? throw new AssertionException("DebugGameHttpService 缺少 ResolveBaseUrl 静态方法。");

            var result = method.Invoke(null, new object[] { input }) as string;
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void ServerEndpointResolver_ShouldChooseLocalhostInEditor_AndRemoteInPlayer()
        {
            var resolverType = Type.GetType("Panoptes.Core.Infrastructure.Network.ServerEndpointResolver, Panoptes.Core")
                               ?? throw new AssertionException("ServerEndpointResolver 类型不存在。");
            var method = resolverType.GetMethod("ResolveDefaultWebSocketUrl",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null,
                new[] { typeof(bool) },
                null)
                         ?? throw new AssertionException("ServerEndpointResolver 缺少 ResolveDefaultWebSocketUrl(bool) 静态方法。");

            var editorUrl = method.Invoke(null, new object[] { true }) as string;
            var playerUrl = method.Invoke(null, new object[] { false }) as string;

            Assert.That(editorUrl, Is.EqualTo("ws://localhost:8080/ws"),
                "Unity 编辑器内默认应连接 localhost。");
            Assert.That(playerUrl, Is.EqualTo("ws://47.116.32.157:8080/ws"),
                "非编辑器环境默认应连接远端服务器。");
        }
    }
}
