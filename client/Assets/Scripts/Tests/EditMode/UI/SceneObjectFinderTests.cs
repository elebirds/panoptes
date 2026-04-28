using NUnit.Framework;
using Panoptes.Presentation.Common;
using UnityEngine;

namespace Panoptes.Tests.EditMode.UI
{
    public sealed class SceneObjectFinderTests
    {
        [TearDown]
        public void TearDown()
        {
            foreach (var marker in Object.FindObjectsByType<SceneObjectFinderMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(marker.gameObject);
            }
        }

        [Test]
        public void FindFirstSceneObject_ShouldReturnSceneInstanceMatchingPredicate()
        {
            var ignored = new GameObject("Ignored").AddComponent<SceneObjectFinderMarker>();
            ignored.Key = "ignored";

            var expected = new GameObject("Expected").AddComponent<SceneObjectFinderMarker>();
            expected.Key = "expected";

            var found = SceneObjectFinder.FindFirstSceneObject<SceneObjectFinderMarker>(marker => marker.Key == "expected");

            Assert.That(found, Is.SameAs(expected));
        }

        [Test]
        public void FindSceneRectByName_ShouldMatchAnyNameCaseInsensitively()
        {
            var root = new GameObject("TurnPanel", typeof(RectTransform));
            var expected = root.GetComponent<RectTransform>();

            var found = SceneObjectFinder.FindSceneRectByName("Missing", "turnpanel");

            Assert.That(found, Is.SameAs(expected));
        }

        private sealed class SceneObjectFinderMarker : MonoBehaviour
        {
            public string Key { get; set; }
        }
    }
}
