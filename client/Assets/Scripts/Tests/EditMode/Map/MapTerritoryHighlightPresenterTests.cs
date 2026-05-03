using NUnit.Framework;
using Panoptes.Presentation.Map;
using UnityEngine;

namespace Panoptes.Tests.EditMode.Map
{
    public sealed class MapTerritoryHighlightPresenterTests
    {
        private GameObject _nodeObject;

        [TearDown]
        public void TearDown()
        {
            if (_nodeObject != null)
            {
                Object.DestroyImmediate(_nodeObject);
                _nodeObject = null;
            }
        }

        [Test]
        public void Remember_ShouldTrackNodeForRestore()
        {
            var presenter = new MapTerritoryHighlightPresenter();
            presenter.Remember("node-1");

            Assert.That(presenter.Count, Is.EqualTo(1));
            Assert.That(presenter.Contains("node-1"), Is.True);
        }

        [Test]
        public void TryRestore_ShouldReturnTrueForTrackedNode()
        {
            var presenter = new MapTerritoryHighlightPresenter();
            presenter.Remember("node-1");
            var node = CreateNode();

            var restored = presenter.TryRestore("node-1", node, Color.cyan);

            Assert.That(restored, Is.True);
        }

        [Test]
        public void Clear_WithoutMap_ShouldClearBookkeeping()
        {
            var presenter = new MapTerritoryHighlightPresenter();
            presenter.Remember("node-1");

            presenter.Clear(null, default, null);

            Assert.That(presenter.Count, Is.Zero);
        }

        private NodeView CreateNode()
        {
            _nodeObject = new GameObject("node");
            return _nodeObject.AddComponent<NodeView>();
        }
    }
}
