/*************************************************
 * Project: Panoptes
 * File: GamePlanningIntentSender.cs
 * Author: Panoptes Team
 * Date: 2026-04-29
 * Description: Adapter from planning presentation input to existing GameIntents dispatch.
 *************************************************/

using Panoptes.Core.Application.Intents;

namespace Panoptes.Presentation.Planning.Input.Intents
{
    /// <summary>
    /// Forwards planning input to GameIntents without adding local gameplay validation.
    /// </summary>
    public sealed class GamePlanningIntentSender : IPlanningIntentSender
    {
        public void PreviewMove(string requestId, string unitId, string targetNodeId)
        {
            GameIntents.PreviewMove(requestId, unitId, targetNodeId);
        }

        public void MoveUnit(string unitId, string targetNodeId)
        {
            GameIntents.MoveUnit(unitId, targetNodeId);
        }

        public void HoldUnit(string unitId)
        {
            GameIntents.HoldUnit(unitId);
        }

        public void AttackUnit(string attackerUnitId, string targetUnitId, string plannedMoveTargetNodeId)
        {
            GameIntents.AttackUnit(attackerUnitId, targetUnitId, plannedMoveTargetNodeId);
        }

        public void AttackNode(string attackerUnitId, string nodeId, string plannedMoveTargetNodeId)
        {
            GameIntents.AttackNode(attackerUnitId, nodeId, plannedMoveTargetNodeId);
        }

        public void ChargeUnit(string unitId, string targetNodeId, string targetUnitId)
        {
            GameIntents.ChargeUnit(unitId, targetNodeId, targetUnitId);
        }

        public void PreviewBuild(string requestId, string nodeId, string buildingType, string cityId)
        {
            GameIntents.PreviewBuild(requestId, nodeId, buildingType, cityId);
        }

        public void BuildToken(string nodeId, string buildingType, string cityId)
        {
            GameIntents.BuildToken(nodeId, buildingType, cityId);
        }

        public void ExpandTerritory(string unitId, string centerNodeId)
        {
            GameIntents.ExpandTerritory(unitId, centerNodeId);
        }
    }
}
