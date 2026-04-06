using Panoptes.Protocol.V1;
using TMPro;
using UnityEngine;

namespace Panoptes.Runtime.UI.Lobby
{
    public sealed class PlayerSlotView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI usernameText;
        [SerializeField] private GameObject hostBadge;
        [SerializeField] private GameObject readyBadge;

        public void Setup(RoomPlayer player, bool isHost)
        {
            if (usernameText != null)
            {
                usernameText.text = player != null ? player.Username : string.Empty;
            }

            if (hostBadge != null)
            {
                hostBadge.SetActive(isHost);
            }

            if (readyBadge != null)
            {
                readyBadge.SetActive(player != null && player.IsReady);
            }
        }
    }
}
