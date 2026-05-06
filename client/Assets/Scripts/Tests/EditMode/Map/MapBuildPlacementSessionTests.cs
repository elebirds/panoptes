using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Map;
using UnityEngine;

namespace Panoptes.Tests.EditMode.Map
{
    public sealed class MapBuildPlacementSessionTests
    {
        private readonly List<Object> _objects = new();

        [TearDown]
        public void TearDown()
        {
            for (var i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                {
                    Object.DestroyImmediate(_objects[i]);
                }
            }

            _objects.Clear();
        }

        [Test]
        public void RememberPendingBuild_ShouldTrackAndRemoveByNode()
        {
            var session = new MapBuildPlacementSession();
            session.RememberPendingBuild(new MapPlanningInputController.PendingBuildRecord
            {
                buildingType = "farm",
                nodeId = "node-1",
                ownerId = "blue",
                isGhost = true
            });

            Assert.That(session.PendingBuilds.Count, Is.EqualTo(1));
            Assert.That(session.HasPendingBuild("node-1"), Is.True);

            session.RemovePendingBuild("node-1");

            Assert.That(session.PendingBuilds.Count, Is.Zero);
        }

        [Test]
        public void RollbackPendingBuild_WithoutMap_ShouldStillClearPendingRecord()
        {
            var session = new MapBuildPlacementSession();
            session.RememberPendingBuild(new MapPlanningInputController.PendingBuildRecord
            {
                buildingType = "farm",
                nodeId = "node-1",
                ownerId = "blue",
                isGhost = true
            });

            session.RollbackPendingBuild("node-1");

            Assert.That(session.HasPendingBuild("node-1"), Is.False);
        }

        [Test]
        public void EnterBuildPlacement_ShouldRejectMissingCityContext()
        {
            var session = new MapBuildPlacementSession();
            string error = null;

            var entered = session.EnterBuildPlacement(
                " farm ",
                string.Empty,
                MapPlanningInputController.BuildPlacementRule.AnyTerrain,
                CreateSettings(),
                message => error = message);

            Assert.That(entered, Is.False);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void RestorePendingBuildGhosts_ShouldReapplyGhost_WhenAuthoritativeNodeIsStillEmpty()
        {
            var session = new MapBuildPlacementSession();
            var mapRenderer = CreateMapRendererWithNode("V22", out var nodeView, out _);
            session.Configure(null, null, null, null, null, mapRenderer, null);
            session.RememberPendingBuild(new MapPlanningInputController.PendingBuildRecord
            {
                buildingType = "farm",
                nodeId = "V22",
                ownerId = "blue",
                isGhost = true
            });

            session.RestorePendingBuildGhosts(Color.green, Color.white);

            Assert.That(nodeView.BuildingInstance, Is.Not.Null);
            Assert.That(nodeView.BuildingType, Is.EqualTo("farm"));
            Assert.That(nodeView.BuildingInstance.IsGhost, Is.True);
            Assert.That(GetPrivateField<GameObject>(nodeView, "highlight").activeSelf, Is.True);
            Assert.That(session.HasPendingBuild("V22"), Is.True);
        }

        [Test]
        public void RestorePendingBuildGhosts_ShouldForgetPendingRecord_WhenAuthoritativeNodeNowHasBuilding()
        {
            var session = new MapBuildPlacementSession();
            var mapRenderer = CreateMapRendererWithNode("node-1", out _, out var nodeState);
            nodeState.BuildingType = "farm";
            session.Configure(null, null, null, null, null, mapRenderer, null);
            session.RememberPendingBuild(new MapPlanningInputController.PendingBuildRecord
            {
                buildingType = "farm",
                nodeId = "node-1",
                ownerId = "blue",
                isGhost = true
            });

            session.RestorePendingBuildGhosts(Color.green);

            Assert.That(session.HasPendingBuild("node-1"), Is.False);
            Assert.That(session.PendingBuilds.Count, Is.Zero);
        }

        [Test]
        public void TryRestorePendingBuildHighlight_ShouldKeepEdgeGlow_WhenHoverHighlightRestoresAfterCommit()
        {
            var session = new MapBuildPlacementSession();
            var mapRenderer = CreateMapRendererWithNode("V22", out var nodeView, out _);
            session.Configure(null, null, null, null, null, mapRenderer, node => node.SetHighlightVisible(false));
            session.RememberPendingBuild(new MapPlanningInputController.PendingBuildRecord
            {
                buildingType = "farm",
                nodeId = "V22",
                ownerId = "blue",
                isGhost = true
            });

            var restored = session.TryRestorePendingBuildHighlight("V22", nodeView, Color.white);

            Assert.That(restored, Is.True);
            Assert.That(GetPrivateField<GameObject>(nodeView, "highlight").activeSelf, Is.True);
        }

        private static MapBuildPlacementVisualSettings CreateSettings()
        {
            return new MapBuildPlacementVisualSettings(
                Color.green,
                Color.red,
                Color.yellow,
                Color.cyan,
                Color.white,
                0.1f,
                false,
                System.Array.Empty<string>());
        }

        private MapRenderer CreateMapRendererWithNode(string nodeId, out NodeView nodeView, out NodeDto nodeState)
        {
            var mapObject = new GameObject("MapRendererTest");
            var mapRenderer = mapObject.AddComponent<MapRenderer>();
            _objects.Add(mapObject);

            var nodeObject = new GameObject($"Node_{nodeId}");
            nodeView = nodeObject.AddComponent<NodeView>();
            _objects.Add(nodeObject);

            var highlightObject = new GameObject("Highlight");
            highlightObject.transform.SetParent(nodeObject.transform, false);
            highlightObject.SetActive(false);
            _objects.Add(highlightObject);
            SetPrivateField(nodeView, "highlight", highlightObject);

            var buildingAnchorObject = new GameObject("BuildingAnchor");
            buildingAnchorObject.transform.SetParent(nodeObject.transform, false);
            _objects.Add(buildingAnchorObject);
            SetPrivateField(nodeView, "buildingAnchor", buildingAnchorObject.transform);

            var buildingPrefabObject = new GameObject("BuildingPrefab");
            var buildingPrefab = buildingPrefabObject.AddComponent<BuildingView>();
            _objects.Add(buildingPrefabObject);
            SetPrivateField(nodeView, "defaultBuildingPrefab", buildingPrefab);

            nodeState = new NodeDto
            {
                Id = nodeId,
                Terrain = "plain",
                Owner = string.Empty,
                BuildingType = string.Empty
            };

            GetPrivateField<Dictionary<string, NodeView>>(mapRenderer, "_tileViews")[nodeId] = nodeView;
            GetPrivateField<Dictionary<string, NodeDto>>(mapRenderer, "_nodeStates")[nodeId] = nodeState;
            return mapRenderer;
        }

        private static TField GetPrivateField<TField>(object instance, string fieldName)
        {
            return (TField)instance
                .GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(instance);
        }

        private static void SetPrivateField(object instance, string fieldName, object value)
        {
            instance
                .GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(instance, value);
        }
    }
}
