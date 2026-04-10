using Panoptes.Core.Application.Cache;
using Panoptes.Core.Infrastructure.Network;
using Panoptes.Protocol.V1;
using UnityEngine;

namespace Panoptes.Core.Application.Handler
{
    public sealed class LobbyMessageHandler : MonoBehaviour
    {
        private bool _registered;

        private void Awake()
        {
            RegisterHandlers();
        }

        private void OnDestroy()
        {
            UnregisterHandlers();
        }

        private void RegisterHandlers()
        {
            var dispatcher = MessageDispatcher.Instance;
            if (_registered || dispatcher == null)
            {
                return;
            }

            dispatcher.Register<MsgRoomCreated>("MsgRoomCreated", OnRoomCreated);
            dispatcher.Register<MsgRoomState>("MsgRoomState", OnRoomState);
            dispatcher.Register<MsgGameStarting>("MsgGameStarting", OnGameStarting);
            dispatcher.Register<MsgPlayerKicked>("MsgPlayerKicked", OnPlayerKicked);
            dispatcher.Register<MsgLobbyError>("MsgLobbyError", OnLobbyError);

            _registered = true;
        }

        private void UnregisterHandlers()
        {
            if (!_registered || MessageDispatcher.Instance == null)
            {
                return;
            }

            var dispatcher = MessageDispatcher.Instance;
            dispatcher.Unregister<MsgRoomCreated>("MsgRoomCreated", OnRoomCreated);
            dispatcher.Unregister<MsgRoomState>("MsgRoomState", OnRoomState);
            dispatcher.Unregister<MsgGameStarting>("MsgGameStarting", OnGameStarting);
            dispatcher.Unregister<MsgPlayerKicked>("MsgPlayerKicked", OnPlayerKicked);
            dispatcher.Unregister<MsgLobbyError>("MsgLobbyError", OnLobbyError);

            _registered = false;
        }

        private static void OnRoomCreated(MsgRoomCreated msg)
        {
            RoomCache.Instance?.PublishRoomCreated(msg);
        }

        private static void OnRoomState(MsgRoomState msg)
        {
            RoomCache.Instance?.Apply(msg);
        }

        private static void OnGameStarting(MsgGameStarting msg)
        {
            RoomCache.Instance?.PublishGameStarting(msg);
        }

        private static void OnPlayerKicked(MsgPlayerKicked msg)
        {
            RoomCache.Instance?.PublishPlayerKicked(msg);
        }

        private static void OnLobbyError(MsgLobbyError msg)
        {
            RoomCache.Instance?.PublishLobbyError(msg);
        }
    }
}
