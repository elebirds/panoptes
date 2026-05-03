using NUnit.Framework;
using Panoptes.Presentation.Map;
using UnityEngine;

namespace Panoptes.Tests.EditMode.Map
{
    public sealed class MapBuildPlacementCoordinatorTests
    {
        [Test]
        public void Tick_WithoutMapRenderer_ShouldSkipBuildRouting()
        {
            var coordinator = new MapBuildPlacementCoordinator();
            var context = new FakeContext { HasMapRenderer = false };

            coordinator.Tick(context, leftMouseDown: true, rightMouseDown: true);

            Assert.That(context.ExitBuildModeCalls, Is.Zero);
            Assert.That(context.ClearBuildHoverStateCalls, Is.Zero);
        }

        [Test]
        public void Tick_RightClick_ShouldExitBuildMode()
        {
            var coordinator = new MapBuildPlacementCoordinator();
            var context = new FakeContext { HasMapRenderer = true };

            coordinator.Tick(context, leftMouseDown: false, rightMouseDown: true);

            Assert.That(context.ExitBuildModeCalls, Is.EqualTo(1));
            Assert.That(context.TryRaycastBuildNodeCalls, Is.Zero);
        }

        [Test]
        public void Tick_PointerOverUi_ShouldClearHoverState()
        {
            var coordinator = new MapBuildPlacementCoordinator();
            var context = new FakeContext
            {
                HasMapRenderer = true,
                PointerOverUI = true
            };

            coordinator.Tick(context, leftMouseDown: true, rightMouseDown: false);

            Assert.That(context.ClearBuildHoverStateCalls, Is.EqualTo(1));
            Assert.That(context.TryRaycastBuildNodeCalls, Is.Zero);
        }

        [Test]
        public void Tick_InvalidLeftClick_ShouldClearHoverAndLog()
        {
            var coordinator = new MapBuildPlacementCoordinator();
            var context = new FakeContext
            {
                HasMapRenderer = true,
                ShouldLogInvalidBuildClick = true,
                RaycastBuildNodeResult = false
            };

            coordinator.Tick(context, leftMouseDown: true, rightMouseDown: false);

            Assert.That(context.ClearBuildHoverStateCalls, Is.EqualTo(1));
            Assert.That(context.LogInvalidBuildTargetCalls, Is.EqualTo(1));
        }

        private sealed class FakeContext : IMapBuildPlacementCoordinatorContext
        {
            public bool HasMapRenderer { get; set; }
            public bool ShouldLogInvalidBuildClick { get; set; }
            public bool PointerOverUI { get; set; }
            public bool RaycastBuildNodeResult { get; set; }

            public int ExitBuildModeCalls { get; private set; }
            public int ClearBuildHoverStateCalls { get; private set; }
            public int TryRaycastBuildNodeCalls { get; private set; }
            public int LogInvalidBuildTargetCalls { get; private set; }

            public bool IsPointerOverUI()
            {
                return PointerOverUI;
            }

            public bool TryRaycastBuildNode(out NodeView node)
            {
                TryRaycastBuildNodeCalls++;
                node = null;
                return RaycastBuildNodeResult;
            }

            public bool IsCurrentHoverNode(NodeView node)
            {
                return false;
            }

            public bool ShouldRequestBuildPreview(string nodeId)
            {
                return false;
            }

            public Color ResolveBuildPreviewColor(string nodeId)
            {
                return Color.white;
            }

            public void ExitBuildMode()
            {
                ExitBuildModeCalls++;
            }

            public void ClearBuildHoverState()
            {
                ClearBuildHoverStateCalls++;
            }

            public void MoveBuildHoverTo(NodeView node)
            {
            }

            public void RenderBuildHover(NodeView node, Color highlightColor)
            {
            }

            public void RenderBuildGhost(Color highlightColor)
            {
            }

            public void RequestBuildPreview(string nodeId)
            {
            }

            public void LogInvalidBuildTarget()
            {
                LogInvalidBuildTargetCalls++;
            }

            public void TryCommitBuildPlacement(NodeView node)
            {
            }
        }
    }
}
