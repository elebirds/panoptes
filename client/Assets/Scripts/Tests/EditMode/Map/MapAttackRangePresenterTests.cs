using System.Collections.Generic;
using NUnit.Framework;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Map;
using UnityEngine;

namespace Panoptes.Tests.EditMode.Map
{
    public sealed class MapAttackRangePresenterTests
    {
        private readonly List<GameObject> _objects = new();

        [TearDown]
        public void TearDown()
        {
            for (var i = 0; i < _objects.Count; i++)
            {
                if (_objects[i] != null)
                {
                    Object.DestroyImmediate(_objects[i]);
                }
            }

            _objects.Clear();
        }

        [Test]
        public void Refresh_ShouldHighlightNodesWithinRange()
        {
            var presenter = new MapAttackRangePresenter();
            var highlighted = new HashSet<string>();
            var tiles = new Dictionary<string, NodeView>
            {
                ["origin"] = CreateNode("origin", 0, 0),
                ["near"] = CreateNode("near", 1, 0),
                ["far"] = CreateNode("far", 3, 0)
            };

            presenter.Refresh(
                tiles,
                highlighted,
                originGrid: new Vector2Int(0, 0),
                attackRange: 1,
                highlightColor: Color.red);

            Assert.That(highlighted, Does.Contain("origin"));
            Assert.That(highlighted, Does.Contain("near"));
            Assert.That(highlighted, Does.Not.Contain("far"));
        }

        [Test]
        public void IsGridInRange_ShouldUseAxialDistance()
        {
            var presenter = new MapAttackRangePresenter();

            Assert.That(
                presenter.IsGridInRange(new Vector2Int(0, 0), new Vector2Int(1, -1), 1),
                Is.True);
            Assert.That(
                presenter.IsGridInRange(new Vector2Int(0, 0), new Vector2Int(2, 0), 1),
                Is.False);
        }

        [Test]
        public void Refresh_ShouldRespectTargetPredicate()
        {
            var presenter = new MapAttackRangePresenter();
            var highlighted = new HashSet<string>();
            var tiles = new Dictionary<string, NodeView>
            {
                ["enemy"] = CreateNode("enemy", 1, 0),
                ["empty"] = CreateNode("empty", 0, 1)
            };

            presenter.Refresh(
                tiles,
                highlighted,
                originGrid: new Vector2Int(0, 0),
                attackRange: 2,
                highlightColor: Color.red,
                canHighlightTarget: (nodeId, _) => nodeId == "enemy");

            Assert.That(highlighted, Does.Contain("enemy"));
            Assert.That(highlighted, Does.Not.Contain("empty"));
        }

        private NodeView CreateNode(string nodeId, int q, int r)
        {
            var go = new GameObject($"Node_{nodeId}");
            _objects.Add(go);
            var node = go.AddComponent<NodeView>();
            node.Bind(new NodeDto
            {
                Id = nodeId,
                Q = q,
                R = r
            });
            return node;
        }
    }
}
