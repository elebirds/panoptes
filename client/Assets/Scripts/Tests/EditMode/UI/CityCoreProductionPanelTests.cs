using System.Reflection;
using NUnit.Framework;
using Panoptes.Presentation.UI.Domestic;

namespace Panoptes.Tests.EditMode.UI
{
    public sealed class CityCoreProductionPanelTests
    {
        [Test]
        public void ResolveLocalPlayerId_ShouldNotFallbackToBlue_WhenAuthoritativeCacheIsMissing()
        {
            var method = typeof(CityCoreProductionPanel).GetMethod("ResolveLocalPlayerId",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "CityCoreProductionPanel 应保留本地玩家解析入口。");

            var result = method!.Invoke(null, null) as string;
            Assert.That(result, Is.EqualTo(string.Empty),
                "主城生产入口在权威状态缺失时应进入空态，而不是猜测为 blue。");
        }
    }
}
