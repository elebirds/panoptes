using Panoptes.Protocol.V1;
using Panoptes.Core.Application.Services;

namespace Panoptes.Core.Infrastructure.Service
{
    public sealed class LobbyService
    {
        private readonly IClientMessageSender _messageSender;

        public LobbyService(IClientMessageSender messageSender)
        {
            _messageSender = messageSender;
        }

        public void CreateRoom(string name, int maxPlayers)
        {
            Send(new MsgCreateRoom
            {
                Name = name,
                MaxPlayers = maxPlayers
            });
        }

        public void JoinRoom(string roomCode)
        {
            Send(new MsgJoinRoom
            {
                RoomCode = roomCode
            });
        }

        public void LeaveRoom()
        {
            Send(new MsgLeaveRoom());
        }

        public void ReadyUp()
        {
            Send(new MsgReadyUp());
        }

        public void AddBot()
        {
            Send(new MsgAddBot());
        }

        public void StartGame()
        {
            Send(new MsgStartGame());
        }

        public void KickPlayer(string playerId)
        {
            Send(new MsgKickPlayer
            {
                PlayerId = playerId
            });
        }

        private void Send(Google.Protobuf.IMessage message)
        {
            _messageSender?.Send(message);
        }
    }
}
