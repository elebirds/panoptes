using Panoptes.Protocol.V1;
using Panoptes.Runtime.Network;

namespace Panoptes.Runtime.Service
{
    public sealed class LobbyService
    {
        public void CreateRoom(string name, int maxPlayers)
        {
            MessageSender.Send(new MsgCreateRoom
            {
                Name = name,
                MaxPlayers = maxPlayers
            });
        }

        public void JoinRoom(string roomCode)
        {
            MessageSender.Send(new MsgJoinRoom
            {
                RoomCode = roomCode
            });
        }

        public void LeaveRoom()
        {
            MessageSender.Send(new MsgLeaveRoom());
        }

        public void ReadyUp()
        {
            MessageSender.Send(new MsgReadyUp());
        }

        public void AddBot()
        {
            MessageSender.Send(new MsgAddBot());
        }

        public void StartGame()
        {
            MessageSender.Send(new MsgStartGame());
        }

        public void KickPlayer(string playerId)
        {
            MessageSender.Send(new MsgKickPlayer
            {
                PlayerId = playerId
            });
        }
    }
}
