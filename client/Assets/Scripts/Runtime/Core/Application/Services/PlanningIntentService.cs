using System;
using Google.Protobuf;
using Panoptes.Core.Application.Intents;
using Panoptes.Protocol.V1;

namespace Panoptes.Core.Application.Services
{
    public sealed class PlanningIntentService
    {
        private readonly IClientMessageSender _sender;

        public PlanningIntentService(IClientMessageSender sender)
        {
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        }

        public bool SetBuildingRecipe(string nodeId, string recipeId)
        {
            return SendIfUnlocked(new MsgSetBuildingRecipe
            {
                NodeId = nodeId ?? string.Empty,
                RecipeId = recipeId ?? string.Empty
            });
        }

        public bool CancelBuildingRecipe(string nodeId)
        {
            return SendIfUnlocked(new MsgCancelBuildingRecipe
            {
                NodeId = nodeId ?? string.Empty
            });
        }

        public bool BuildToken(string nodeId, string buildingTypeId, string cityId = null)
        {
            return SendIfUnlocked(new MsgBuildStructure
            {
                NodeId = nodeId ?? string.Empty,
                BuildingTypeId = buildingTypeId ?? string.Empty,
                CityId = cityId ?? string.Empty
            });
        }

        public bool DemolishBuilding(string nodeId)
        {
            return SendIfUnlocked(new MsgDemolishBuilding
            {
                NodeId = nodeId ?? string.Empty
            });
        }

        public bool PreviewBuild(string requestId, string nodeId, string buildingTypeId, string cityId)
        {
            return SendIfUnlocked(new MsgBuildStructurePreviewRequest
            {
                RequestId = requestId ?? string.Empty,
                NodeId = nodeId ?? string.Empty,
                BuildingTypeId = buildingTypeId ?? string.Empty,
                CityId = cityId ?? string.Empty
            });
        }

        public bool PreviewRecipe(string requestId, string nodeId, string recipeId)
        {
            return SendIfUnlocked(new MsgSetBuildingRecipePreviewRequest
            {
                RequestId = requestId ?? string.Empty,
                NodeId = nodeId ?? string.Empty,
                RecipeId = recipeId ?? string.Empty
            });
        }

        public bool ExpandTerritory(string unitId, string centerNodeId = null)
        {
            return IssueUnitOrder(unitId, "settle_city", centerNodeId, null, null);
        }

        public bool DeployTerritoryUnit(string unitId, string centerNodeId = null)
        {
            return IssueUnitOrder(unitId, "settle_city", centerNodeId, null, null);
        }

        public bool MoveUnit(string unitId, string targetNodeId)
        {
            return IssueUnitOrder(unitId, "move", targetNodeId, null, null);
        }

        public bool PreviewMove(string requestId, string unitId, string targetNodeId)
        {
            return SendIfUnlocked(new MsgPlanningPathPreviewRequest
            {
                RequestId = requestId ?? string.Empty,
                UnitId = unitId ?? string.Empty,
                Action = "move",
                TargetNodeId = targetNodeId ?? string.Empty
            });
        }

        public bool MicroUnit(string unitId, string targetNodeId)
        {
            return MoveUnit(unitId, targetNodeId);
        }

        public bool AttackUnit(string unitId, string targetUnitId, string secondaryNodeId = null)
        {
            return IssueUnitOrder(unitId, "attack", null, targetUnitId, secondaryNodeId);
        }

        public bool AttackNode(string unitId, string targetNodeId, string secondaryNodeId = null)
        {
            return IssueUnitOrder(unitId, "attack", targetNodeId, null, secondaryNodeId);
        }

        public bool DestroyRoad(string unitId, string fromNodeId, string toNodeId)
        {
            return IssueUnitOrder(unitId, "destroy_road", fromNodeId, null, toNodeId);
        }

        public bool HoldUnit(string unitId)
        {
            return IssueUnitOrder(unitId, "hold", null, null, null);
        }

        public bool ChargeUnit(string unitId, string targetNodeId, string targetUnitId = null)
        {
            return IssueUnitOrder(unitId, "charge", targetNodeId, targetUnitId, null);
        }

        public bool CancelUnitOrder(string unitId)
        {
            return SendIfUnlocked(new MsgCancelUnitOrder
            {
                UnitId = unitId ?? string.Empty
            });
        }

        private bool IssueUnitOrder(
            string unitId,
            string action,
            string targetNodeId,
            string targetUnitId,
            string secondaryNodeId)
        {
            return SendIfUnlocked(new MsgIssueUnitOrder
            {
                UnitId = unitId ?? string.Empty,
                Action = action ?? string.Empty,
                TargetNodeId = targetNodeId ?? string.Empty,
                TargetUnitId = targetUnitId ?? string.Empty,
                SecondaryNodeId = secondaryNodeId ?? string.Empty
            });
        }

        private bool SendIfUnlocked(IMessage message)
        {
            return !ActionLock.IsLocked && _sender.Send(message);
        }
    }
}
