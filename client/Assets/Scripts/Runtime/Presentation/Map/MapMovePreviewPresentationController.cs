using System;
using System.Collections.Generic;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Planning.Feedback;
using UnityEngine;

namespace Panoptes.Presentation.Map
{
    public sealed class MapMovePreviewPresentationController
    {
        private readonly MovePreviewGhostPresenter _ghostPresenter = new();
        private MapRenderer _mapRenderer;
        private MovePathOverlayController _pathOverlay;
        private MovePreviewOverlayController _previewOverlay;

        public void SetMapRenderer(MapRenderer mapRenderer)
        {
            _mapRenderer = mapRenderer;
            _pathOverlay?.SetMapRenderer(mapRenderer);
            _previewOverlay?.SetMapRenderer(mapRenderer);
        }

        public void Ensure(Transform hostTransform)
        {
            _pathOverlay ??= new MovePathOverlayController(_mapRenderer);
            _previewOverlay ??= new MovePreviewOverlayController(_mapRenderer, hostTransform);
            _previewOverlay.SetHostTransform(hostTransform);
        }

        public void RefreshPreview(
            PathPreviewDto preview,
            string hoverNodeId,
            string selectedUnitId,
            bool hasMapRenderer,
            Color moveInvalidColor,
            Color moveHighlightColor,
            Color moveFirstTurnColor,
            Color moveFutureTurnColor,
            float moveTurnMarkerHeight)
        {
            var plan = MapMovePreviewRenderPlanner.Create(
                preview,
                hoverNodeId,
                selectedUnitId,
                hasMapRenderer);

            switch (plan.Kind)
            {
                case MapMovePreviewRenderKind.Invalid:
                    _previewOverlay?.ShowInvalidPreview(plan.TargetNodeId, moveInvalidColor);
                    break;
                case MapMovePreviewRenderKind.Valid:
                    _previewOverlay?.ShowPreview(
                        plan.Preview,
                        moveHighlightColor,
                        moveFirstTurnColor,
                        moveFutureTurnColor,
                        moveTurnMarkerHeight);
                    break;
            }
        }

        public void ClearPreview(Func<string, NodeView, bool> restoreNodeHighlight)
        {
            _previewOverlay?.ClearPreview(restoreNodeHighlight);
        }

        public bool TryRestorePreviewHighlight(string nodeId, NodeView node)
        {
            return _previewOverlay != null && _previewOverlay.TryRestorePreviewHighlight(nodeId, node);
        }

        public bool TryApplyAuthoritativeMovePathMarkers(
            string unitId,
            string targetNodeId,
            PathPreviewDto preview,
            IReadOnlyDictionary<string, QueuedUnitOrderDto> ordersByUnitId,
            Color arrowColor,
            Color destinationColor)
        {
            return _pathOverlay != null &&
                   _pathOverlay.TryApplyAuthoritativeMovePathMarkers(
                       unitId,
                       targetNodeId,
                       preview,
                       ordersByUnitId,
                       arrowColor,
                       destinationColor);
        }

        public void RefreshQueuedMovePathMarkers(
            IReadOnlyDictionary<string, QueuedUnitOrderDto> ordersByUnitId,
            Color arrowColor,
            Color destinationColor)
        {
            if (_pathOverlay == null)
            {
                return;
            }

            if (ordersByUnitId == null)
            {
                _pathOverlay.ClearAllMovePathMarkers();
                return;
            }

            _pathOverlay.RefreshQueuedMovePathMarkers(
                ordersByUnitId,
                arrowColor,
                destinationColor);
        }

        public void ClearMovePathMarkersForUnit(string unitId)
        {
            _pathOverlay?.ClearMovePathMarkersForUnit(unitId);
        }

        public void ClearAllMovePathMarkers()
        {
            _pathOverlay?.ClearAllMovePathMarkers();
        }

        public void CreateOrUpdateGhost(
            string unitId,
            string targetNodeId,
            MovePreviewGhostPresenter.Settings settings,
            MonoBehaviour coroutineHost)
        {
            _ghostPresenter.CreateOrUpdate(unitId, targetNodeId, settings, coroutineHost);
        }

        public void RemoveGhost(string unitId)
        {
            _ghostPresenter.Remove(unitId);
        }

        public void ClearAllGhosts()
        {
            _ghostPresenter.ClearAll();
        }

        public void DisposeGhostMaterial()
        {
            _ghostPresenter.DisposeMaterial();
        }
    }
}
