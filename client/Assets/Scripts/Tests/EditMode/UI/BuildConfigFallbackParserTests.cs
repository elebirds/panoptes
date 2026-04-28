using System.Collections.Generic;
using NUnit.Framework;
using Panoptes.Presentation.UI.Domestic;

namespace Panoptes.Tests.EditMode.UI
{
    public sealed class BuildConfigFallbackParserTests
    {
        [Test]
        public void EnumerateBuildingCosts_ShouldExtractResourceAndPointCosts()
        {
            const string json = "{\"buildings\":[{\"id\":\"farm\",\"resource_costs\":{\"wood\":2},\"point_costs\":{\"labor\":1}}]}";
            var parsed = new Dictionary<string, List<BuildConfigFallbackParser.CostEntry>>();

            BuildConfigFallbackParser.EnumerateBuildingCosts(json, (id, costs) => parsed[id] = new List<BuildConfigFallbackParser.CostEntry>(costs));

            Assert.That(parsed.ContainsKey("farm"), Is.True);
            Assert.That(parsed["farm"], Has.Count.EqualTo(2));
            Assert.That(parsed["farm"][0].Key, Is.EqualTo("wood"));
            Assert.That(parsed["farm"][0].Amount, Is.EqualTo(2));
            Assert.That(parsed["farm"][0].IsPoint, Is.False);
            Assert.That(parsed["farm"][1].Key, Is.EqualTo("labor"));
            Assert.That(parsed["farm"][1].Amount, Is.EqualTo(1));
            Assert.That(parsed["farm"][1].IsPoint, Is.True);
        }
    }
}
