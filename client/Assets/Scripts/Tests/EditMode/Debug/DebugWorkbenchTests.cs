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

        [Test]
        public void DebugTabRegistry_ShouldExposeDefaultFiveTabs()
        {
            var registryType = Type.GetType("Panoptes.DebugTools.DebugTabRegistry, Panoptes.Core")
                               ?? throw new AssertionException("DebugTabRegistry 类型不存在。");
            var createMethod = registryType.GetMethod("CreateDefaultTabs",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                               ?? throw new AssertionException("缺少 CreateDefaultTabs 静态方法。");

            var tabs = createMethod.Invoke(null, null) as IEnumerable
                       ?? throw new AssertionException("CreateDefaultTabs 必须返回可枚举集合。");

            var count = 0;
            var titles = new string[5];
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

            Assert.That(count, Is.EqualTo(5), "首版应固定提供 5 个基础 Tab。");
            CollectionAssert.AreEqual(
                new[] { "总览", "消息时间线", "原始发送器", "Lobby", "Game" },
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

            var knownArgs = new object[] { "MsgSubmitDomestic", "{}", null, null };
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
        public void AppManager_ShouldBootstrapDebugPanel_FromManagers()
        {
            Assert.That(File.Exists(_appManagerPath), Is.True, "AppManager.cs 不存在。");

            var content = File.ReadAllText(_appManagerPath);
            StringAssert.Contains("EnsureComponent<DebugPanel>(managers);", content,
                "多 Tab DebugPanel 应从 Managers 全局挂载，覆盖 Login/Lobby/Game。");
        }
    }
}
