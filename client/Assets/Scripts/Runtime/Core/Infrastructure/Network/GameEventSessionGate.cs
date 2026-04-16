/*************************************************
 * Project: Panoptes
 * File: GameEventSessionGate.cs
 * Author: Panoptes Team
 * Date: 2026-04-17
 * Description: Filters cross-session game events before they reach runtime handlers.
 *************************************************/

using System;
using Panoptes.Core.Application.Cache;
using Panoptes.Protocol.V1;
using UnityEngine;

namespace Panoptes.Core.Infrastructure.Network
{
    internal static class GameEventSessionGate
    {
        public static bool ShouldDispatch(MessageDispatcher.DispatchEntry entry)
        {
            if (entry.Frame == null || entry.Frame.TargetCase != ServerFrame.TargetOneofCase.Game)
            {
                return true;
            }

            var gameEvent = entry.Frame.Game;
            if (gameEvent == null)
            {
                return true;
            }

            var incomingSessionID = entry.Frame.Meta != null
                ? (entry.Frame.Meta.GameSessionId ?? string.Empty).Trim()
                : string.Empty;

            if (gameEvent.BodyCase == GameEvent.BodyOneofCase.GameInit)
            {
                GameStateCache.Instance?.SetActiveGameSession(incomingSessionID);
                return true;
            }

            var activeSessionID = GameStateCache.Instance != null
                ? (GameStateCache.Instance.ActiveGameSessionID ?? string.Empty).Trim()
                : string.Empty;

            if (string.IsNullOrWhiteSpace(activeSessionID))
            {
                Debug.LogWarning(
                    $"[Dispatcher] Dropping {entry.MessageType} because no active game session is established. incoming_session={incomingSessionID}");
                return false;
            }

            if (string.Equals(incomingSessionID, activeSessionID, StringComparison.Ordinal))
            {
                return true;
            }

            Debug.LogWarning(
                $"[Dispatcher] Dropping cross-session game event {entry.MessageType}. incoming_session={incomingSessionID} active_session={activeSessionID}");
            return false;
        }
    }
}
