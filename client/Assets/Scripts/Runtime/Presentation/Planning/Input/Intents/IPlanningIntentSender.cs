/*************************************************
 * Project: Panoptes
 * File: IPlanningIntentSender.cs
 * Author: Panoptes Team
 * Date: 2026-04-29
 * Description: Planning intent port that forwards player input without client-side legality checks.
 *************************************************/

namespace Panoptes.Presentation.Planning.Input.Intents
{
    /// <summary>
    /// Sends planning intents through the existing application path while preserving server authority.
    /// </summary>
    public interface IPlanningIntentSender
    {
        void PreviewMove(string requestId, string unitId, string targetNodeId);
        void MoveUnit(string unitId, string targetNodeId);
        void HoldUnit(string unitId);
        void AttackUnit(string attackerUnitId, string targetUnitId, string plannedMoveTargetNodeId);
        void AttackNode(string attackerUnitId, string nodeId, string plannedMoveTargetNodeId);
        void ChargeUnit(string unitId, string targetNodeId, string targetUnitId);
        void PreviewBuild(string requestId, string nodeId, string buildingType, string cityId);
        void BuildToken(string nodeId, string buildingType, string cityId);
        void ExpandTerritory(string unitId, string centerNodeId);
    }
}
