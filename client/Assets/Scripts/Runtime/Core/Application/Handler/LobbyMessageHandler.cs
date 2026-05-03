using Panoptes.Core.Application.Cache;
using Panoptes.Core.Infrastructure.Network;
using Panoptes.Protocol.V1;
using UnityEngine;

namespace Panoptes.Core.Application.Handler
{
    public sealed class LobbyMessageHandler : MonoBehaviour
    {
        private bool _registered;
        private MessageDispatcher _dispatcher;
        private RoomCache _roomCache;

        public void UseProjectServices(MessageDispatcher dispatcher, RoomCache roomCache)
        {
            _dispatcher = dispatcher;
            _roomCache = roomCache;
            RegisterHandlers();
        }

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
            var dispatcher = _dispatcher;
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
            if (!_registered || _dispatcher == null)
            {
                return;
            }

            var dispatcher = _dispatcher;
            dispatcher.Unregister<MsgRoomCreated>("MsgRoomCreated", OnRoomCreated);
            dispatcher.Unregister<MsgRoomState>("MsgRoomState", OnRoomState);
            dispatcher.Unregister<MsgGameStarting>("MsgGameStarting", OnGameStarting);
            dispatcher.Unregister<MsgPlayerKicked>("MsgPlayerKicked", OnPlayerKicked);
            dispatcher.Unregister<MsgLobbyError>("MsgLobbyError", OnLobbyError);

            _registered = false;
        }

        private void OnRoomCreated(MsgRoomCreated msg)
        {
            _roomCache?.PublishRoomCreated(msg);
        }

        private void OnRoomState(MsgRoomState msg)
        {
            _roomCache?.Apply(msg);
        }

        private void OnGameStarting(MsgGameStarting msg)
        {
            _roomCache?.PublishGameStarting(msg);
        }

        private void OnPlayerKicked(MsgPlayerKicked msg)
        {
            _roomCache?.PublishPlayerKicked(msg);
        }

        private void OnLobbyError(MsgLobbyError msg)
        {
            _roomCache?.PublishLobbyError(msg);
        }
    }
}
