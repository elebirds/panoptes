/*************************************************
 * Project: Panoptes
 * File: PlanningDraftCache.cs
 * Author: Panoptes Team
 * Date: 2026-04-14
 * Description: Server-authoritative planning draft and path preview cache.
 *************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using Panoptes.Core.Domain;
using Panoptes.Core.Infrastructure.Mapper;
using Panoptes.Protocol.V1;
using UnityEngine;

namespace Panoptes.Core.Application.Cache
{
    public sealed class PlanningDraftCache : MonoBehaviour
    {
        public static PlanningDraftCache Instance { get; private set; }

        private readonly Dictionary<string, QueuedUnitOrderDto> _ordersByUnitId = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<QueuedBuildOrderDto> _buildOrders = new();
        private readonly List<QueuedRecipeSelectionDto> _recipeSelections = new();
        private readonly List<QueuedWarZoneDirectiveDto> _warZoneDirectives = new();
        private readonly List<PlanningWarZoneDto> _warZones = new();
        private readonly List<MinisterDraftDto> _ministerDrafts = new();

        private string _pendingRequestId = string.Empty;
        private string _pendingUnitId = string.Empty;
        private string _pendingAction = string.Empty;
        private string _pendingTargetNodeId = string.Empty;
        private string _pendingBuildPreviewRequestId = string.Empty;
        private string _pendingBuildPreviewNodeId = string.Empty;
        private string _pendingBuildPreviewBuildingTypeId = string.Empty;
        private string _pendingBuildPreviewCityId = string.Empty;
        private string _pendingRecipePreviewRequestId = string.Empty;
        private string _pendingRecipePreviewNodeId = string.Empty;
        private string _pendingRecipePreviewRecipeId = string.Empty;

        public IReadOnlyDictionary<string, QueuedUnitOrderDto> OrdersByUnitId => _ordersByUnitId;
        public IReadOnlyList<QueuedBuildOrderDto> BuildOrders => _buildOrders;
        public IReadOnlyList<QueuedRecipeSelectionDto> RecipeSelections => _recipeSelections;
        public IReadOnlyList<QueuedWarZoneDirectiveDto> WarZoneDirectives => _warZoneDirectives;
        public IReadOnlyList<PlanningWarZoneDto> WarZones => _warZones;
        public IReadOnlyList<MinisterDraftDto> MinisterDrafts => _ministerDrafts;
        public PathPreviewDto CurrentPreview { get; private set; }
        public BuildPreviewDto CurrentBuildPreview { get; private set; }
        public RecipePreviewDto CurrentRecipePreview { get; private set; }
        public int SnapshotTurn { get; private set; }
        public string SnapshotPhase { get; private set; } = string.Empty;
        public string PlannedResearchTargetTechnologyId { get; private set; } = string.Empty;
        public string PlannedNationalPolicyId { get; private set; } = string.Empty;
        public IReadOnlyList<string> PlannedInstitutionIds => _plannedInstitutionIds;
        private readonly List<string> _plannedInstitutionIds = new();

        public event Action PreviewChanged;
        public event Action BuildPreviewChanged;
        public event Action RecipePreviewChanged;
        public event Action OrdersChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public List<QueuedUnitOrderDto> GetOrdersInDisplayOrder()
        {
            return _ordersByUnitId.Values
                .OrderBy(order => order.UnitId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public bool TryGetRecipeSelection(string nodeId, out QueuedRecipeSelectionDto selection)
        {
            selection = null;
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return false;
            }

            for (var i = _recipeSelections.Count - 1; i >= 0; i--)
            {
                var candidate = _recipeSelections[i];
                if (candidate == null || !string.Equals(candidate.NodeId, nodeId.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                selection = new QueuedRecipeSelectionDto
                {
                    NodeId = candidate.NodeId,
                    RecipeId = candidate.RecipeId
                };
                return true;
            }

            return false;
        }

        public List<MinisterDraftDto> GetDomesticMinisterDrafts(bool includeRejected = false)
        {
            var result = new List<MinisterDraftDto>();
            for (var i = 0; i < _ministerDrafts.Count; i++)
            {
                var draft = _ministerDrafts[i];
                if (draft == null || !draft.IsDomestic)
                {
                    continue;
                }

                if (!includeRejected && draft.IsRejected)
                {
                    continue;
                }

                result.Add(draft);
            }

            return result;
        }

        public void TrackPreviewRequest(string requestId, string unitId, string action, string targetNodeId)
        {
            _pendingRequestId = requestId ?? string.Empty;
            _pendingUnitId = unitId ?? string.Empty;
            _pendingAction = action ?? string.Empty;
            _pendingTargetNodeId = targetNodeId ?? string.Empty;
        }

        public void TrackBuildPreviewRequest(string requestId, string nodeId, string buildingTypeId, string cityId)
        {
            _pendingBuildPreviewRequestId = requestId ?? string.Empty;
            _pendingBuildPreviewNodeId = nodeId ?? string.Empty;
            _pendingBuildPreviewBuildingTypeId = buildingTypeId ?? string.Empty;
            _pendingBuildPreviewCityId = cityId ?? string.Empty;
            CurrentBuildPreview = new BuildPreviewDto
            {
                RequestId = _pendingBuildPreviewRequestId,
                NodeId = _pendingBuildPreviewNodeId,
                BuildingTypeId = _pendingBuildPreviewBuildingTypeId,
                CityId = _pendingBuildPreviewCityId,
                Valid = false,
                ErrorCode = string.Empty,
                Message = "检查中",
                Details = new Dictionary<string, string>()
            };
            BuildPreviewChanged?.Invoke();
        }

        public void TrackRecipePreviewRequest(string requestId, string nodeId, string recipeId)
        {
            _pendingRecipePreviewRequestId = requestId ?? string.Empty;
            _pendingRecipePreviewNodeId = nodeId ?? string.Empty;
            _pendingRecipePreviewRecipeId = recipeId ?? string.Empty;
            CurrentRecipePreview = new RecipePreviewDto
            {
                RequestId = _pendingRecipePreviewRequestId,
                NodeId = _pendingRecipePreviewNodeId,
                RecipeId = _pendingRecipePreviewRecipeId,
                Valid = false,
                ErrorCode = string.Empty,
                Message = "检查中",
                Details = new Dictionary<string, string>()
            };
            RecipePreviewChanged?.Invoke();
        }

        public void ApplyPlanningSnapshot(MsgPlanningSnapshot msg)
        {
            ClearPreview();
            ClearBuildPreview();
            ClearRecipePreview();
            SnapshotTurn = msg != null ? msg.Turn : 0;
            SnapshotPhase = msg != null ? (msg.Phase ?? string.Empty) : string.Empty;
            PlannedResearchTargetTechnologyId = msg != null ? (msg.PlannedResearchTargetTechnologyId ?? string.Empty) : string.Empty;
            PlannedNationalPolicyId = msg != null ? (msg.PlannedNationalPolicyId ?? string.Empty) : string.Empty;
            _plannedInstitutionIds.Clear();
            _ordersByUnitId.Clear();
            _buildOrders.Clear();
            _recipeSelections.Clear();
            _warZoneDirectives.Clear();
            _warZones.Clear();
            _ministerDrafts.Clear();

            if (msg != null && msg.UnitOrders != null)
            {
                for (var i = 0; i < msg.UnitOrders.Count; i++)
                {
                    var order = msg.UnitOrders[i];
                    if (order == null || string.IsNullOrWhiteSpace(order.UnitId))
                    {
                        continue;
                    }

                    _ordersByUnitId[order.UnitId] = MapOrder(order);
                }
            }
            if (msg != null && msg.BuildOrders != null)
            {
                for (var i = 0; i < msg.BuildOrders.Count; i++)
                {
                    var order = msg.BuildOrders[i];
                    if (order == null || string.IsNullOrWhiteSpace(order.NodeId))
                    {
                        continue;
                    }

                    _buildOrders.Add(new QueuedBuildOrderDto
                    {
                        NodeId = order.NodeId,
                        BuildingTypeId = order.BuildingTypeId,
                        CityId = order.CityId
                    });
                }
            }
            if (msg != null && msg.RecipeSelections != null)
            {
                for (var i = 0; i < msg.RecipeSelections.Count; i++)
                {
                    var selection = msg.RecipeSelections[i];
                    if (selection == null || string.IsNullOrWhiteSpace(selection.NodeId))
                    {
                        continue;
                    }

                    _recipeSelections.Add(new QueuedRecipeSelectionDto
                    {
                        NodeId = selection.NodeId,
                        RecipeId = selection.RecipeId
                    });
                }
            }
            if (msg != null && msg.WarZoneDirectives != null)
            {
                for (var i = 0; i < msg.WarZoneDirectives.Count; i++)
                {
                    var directive = msg.WarZoneDirectives[i];
                    if (directive == null || string.IsNullOrWhiteSpace(directive.ZoneId))
                    {
                        continue;
                    }

                    _warZoneDirectives.Add(new QueuedWarZoneDirectiveDto
                    {
                        ZoneId = directive.ZoneId,
                        Directive = directive.Directive,
                        TargetNode = directive.TargetNode
                    });
                }
            }
            if (msg != null && msg.WarZones != null)
            {
                for (var i = 0; i < msg.WarZones.Count; i++)
                {
                    var zone = msg.WarZones[i];
                    if (zone == null || string.IsNullOrWhiteSpace(zone.Id))
                    {
                        continue;
                    }

                    _warZones.Add(new PlanningWarZoneDto
                    {
                        Id = zone.Id,
                        Name = zone.Name,
                        NodeIds = zone.NodeIds != null ? zone.NodeIds.ToList() : new List<string>(),
                        Directive = zone.Directive,
                        TargetNode = zone.TargetNode
                    });
                }
            }
            if (msg != null && msg.MinisterDrafts != null)
            {
                for (var i = 0; i < msg.MinisterDrafts.Count; i++)
                {
                    var draft = MinisterMapper.ToDto(msg.MinisterDrafts[i]);
                    if (draft != null)
                    {
                        _ministerDrafts.Add(draft);
                    }
                }
            }
            if (msg != null && msg.PlannedInstitutionIds != null)
            {
                for (var i = 0; i < msg.PlannedInstitutionIds.Count; i++)
                {
                    var policyId = msg.PlannedInstitutionIds[i];
                    if (string.IsNullOrWhiteSpace(policyId))
                    {
                        continue;
                    }

                    _plannedInstitutionIds.Add(policyId);
                }
            }

            OrdersChanged?.Invoke();
        }

        public void ApplyPreviewResponse(MsgPlanningPathPreviewResponse msg)
        {
            if (msg == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(_pendingRequestId) &&
                !string.Equals(msg.RequestId, _pendingRequestId, StringComparison.Ordinal))
            {
                return;
            }

            if (!string.IsNullOrEmpty(_pendingUnitId) &&
                !string.Equals(msg.UnitId, _pendingUnitId, StringComparison.Ordinal))
            {
                return;
            }

            if (!string.IsNullOrEmpty(_pendingAction) &&
                !string.Equals(msg.Action, _pendingAction, StringComparison.Ordinal))
            {
                return;
            }

            if (!string.IsNullOrEmpty(_pendingTargetNodeId) &&
                !string.Equals(msg.TargetNodeId, _pendingTargetNodeId, StringComparison.Ordinal))
            {
                return;
            }

            CurrentPreview = MapPreview(msg);
            PreviewChanged?.Invoke();
        }

        public void ApplyBuildPreviewResponse(MsgBuildStructurePreviewResponse msg)
        {
            if (msg == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(_pendingBuildPreviewRequestId) &&
                !string.Equals(msg.RequestId, _pendingBuildPreviewRequestId, StringComparison.Ordinal))
            {
                return;
            }

            if (!string.IsNullOrEmpty(_pendingBuildPreviewNodeId) &&
                !string.Equals(msg.NodeId, _pendingBuildPreviewNodeId, StringComparison.Ordinal))
            {
                return;
            }

            if (!string.IsNullOrEmpty(_pendingBuildPreviewBuildingTypeId) &&
                !string.Equals(msg.BuildingTypeId, _pendingBuildPreviewBuildingTypeId, StringComparison.Ordinal))
            {
                return;
            }

            if (!string.IsNullOrEmpty(_pendingBuildPreviewCityId) &&
                !string.Equals(msg.CityId, _pendingBuildPreviewCityId, StringComparison.Ordinal))
            {
                return;
            }

            CurrentBuildPreview = MapBuildPreview(msg);
            BuildPreviewChanged?.Invoke();
        }

        public void ApplyRecipePreviewResponse(MsgSetBuildingRecipePreviewResponse msg)
        {
            if (msg == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(_pendingRecipePreviewRequestId) &&
                !string.Equals(msg.RequestId, _pendingRecipePreviewRequestId, StringComparison.Ordinal))
            {
                return;
            }

            if (!string.IsNullOrEmpty(_pendingRecipePreviewNodeId) &&
                !string.Equals(msg.NodeId, _pendingRecipePreviewNodeId, StringComparison.Ordinal))
            {
                return;
            }

            if (!string.IsNullOrEmpty(_pendingRecipePreviewRecipeId) &&
                !string.Equals(msg.RecipeId, _pendingRecipePreviewRecipeId, StringComparison.Ordinal))
            {
                return;
            }

            CurrentRecipePreview = MapRecipePreview(msg);
            RecipePreviewChanged?.Invoke();
        }

        public void ClearPreview()
        {
            _pendingRequestId = string.Empty;
            _pendingUnitId = string.Empty;
            _pendingAction = string.Empty;
            _pendingTargetNodeId = string.Empty;
            CurrentPreview = null;
            PreviewChanged?.Invoke();
        }

        public void ClearBuildPreview()
        {
            _pendingBuildPreviewRequestId = string.Empty;
            _pendingBuildPreviewNodeId = string.Empty;
            _pendingBuildPreviewBuildingTypeId = string.Empty;
            _pendingBuildPreviewCityId = string.Empty;
            CurrentBuildPreview = null;
            BuildPreviewChanged?.Invoke();
        }

        public void ClearRecipePreview()
        {
            _pendingRecipePreviewRequestId = string.Empty;
            _pendingRecipePreviewNodeId = string.Empty;
            _pendingRecipePreviewRecipeId = string.Empty;
            CurrentRecipePreview = null;
            RecipePreviewChanged?.Invoke();
        }

        public void ClearOrders()
        {
            SnapshotTurn = 0;
            SnapshotPhase = string.Empty;
            PlannedResearchTargetTechnologyId = string.Empty;
            PlannedNationalPolicyId = string.Empty;
            _plannedInstitutionIds.Clear();
            _ordersByUnitId.Clear();
            _buildOrders.Clear();
            _recipeSelections.Clear();
            _warZoneDirectives.Clear();
            _warZones.Clear();
            _ministerDrafts.Clear();
            OrdersChanged?.Invoke();
        }

        public void ClearAll()
        {
            ClearPreview();
            ClearBuildPreview();
            ClearRecipePreview();
            ClearOrders();
        }

        private static QueuedUnitOrderDto MapOrder(QueuedUnitOrder order)
        {
            return new QueuedUnitOrderDto
            {
                UnitId = order.UnitId,
                Action = order.Action,
                TargetNodeId = order.TargetNodeId,
                TargetUnitId = order.TargetUnitId,
                SecondaryNodeId = order.SecondaryNodeId,
                Params = order.Params != null ? order.Params.ToDictionary(pair => pair.Key, pair => pair.Value) : new Dictionary<string, string>(),
                PathNodeIds = order.PathNodeIds != null ? order.PathNodeIds.ToList() : new List<string>(),
                FirstTurnNodeId = order.FirstTurnNodeId,
                TotalTurns = order.TotalTurns,
                TurnStops = MapTurnStops(order.TurnStops)
            };
        }

        private static PathPreviewDto MapPreview(MsgPlanningPathPreviewResponse msg)
        {
            return new PathPreviewDto
            {
                RequestId = msg.RequestId,
                UnitId = msg.UnitId,
                Action = msg.Action,
                TargetNodeId = msg.TargetNodeId,
                Valid = msg.Valid,
                ErrorCode = msg.ErrorCode,
                PathNodeIds = msg.PathNodeIds != null ? msg.PathNodeIds.ToList() : new List<string>(),
                FirstTurnNodeId = msg.FirstTurnNodeId,
                TotalTurns = msg.TotalTurns,
                TurnStops = MapTurnStops(msg.TurnStops)
            };
        }

        private static BuildPreviewDto MapBuildPreview(MsgBuildStructurePreviewResponse msg)
        {
            return new BuildPreviewDto
            {
                RequestId = msg.RequestId,
                NodeId = msg.NodeId,
                BuildingTypeId = msg.BuildingTypeId,
                CityId = msg.CityId,
                Valid = msg.Valid,
                ErrorCode = msg.ErrorCode,
                Message = msg.FeedbackMessage,
                Details = MapFeedbackDetails(msg.FeedbackDetails)
            };
        }

        private static RecipePreviewDto MapRecipePreview(MsgSetBuildingRecipePreviewResponse msg)
        {
            return new RecipePreviewDto
            {
                RequestId = msg.RequestId,
                NodeId = msg.NodeId,
                RecipeId = msg.RecipeId,
                Valid = msg.Valid,
                ErrorCode = msg.ErrorCode,
                Message = msg.FeedbackMessage,
                Details = MapFeedbackDetails(msg.FeedbackDetails)
            };
        }

        private static Dictionary<string, string> MapFeedbackDetails(IEnumerable<FeedbackDetail> details)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (details == null)
            {
                return result;
            }

            foreach (var detail in details)
            {
                if (detail == null || string.IsNullOrWhiteSpace(detail.Key))
                {
                    continue;
                }

                result[detail.Key] = detail.Value ?? string.Empty;
            }

            return result;
        }

        private static List<MarchTurnStopDto> MapTurnStops(System.Collections.Generic.IEnumerable<MarchTurnStop> turnStops)
        {
            var result = new List<MarchTurnStopDto>();
            if (turnStops == null)
            {
                return result;
            }

            foreach (var stop in turnStops)
            {
                if (stop == null)
                {
                    continue;
                }

                result.Add(new MarchTurnStopDto
                {
                    TurnIndex = stop.TurnIndex,
                    NodeId = stop.NodeId
                });
            }

            return result;
        }
    }
}
