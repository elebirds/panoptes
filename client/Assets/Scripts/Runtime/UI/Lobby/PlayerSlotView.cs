using System;
using Panoptes.Protocol.V1;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Runtime.UI.Lobby
{
    public sealed class PlayerSlotView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI usernameText;
        [SerializeField] private GameObject hostBadge;
        [SerializeField] private GameObject readyBadge;
        [SerializeField] private GameObject botBadge;
        [SerializeField] private Button kickButton;

        private string _playerId;

        public void Setup(RoomPlayer player, bool showKickButton, Action<string> onKick)
        {
            _playerId = player != null ? player.PlayerId : string.Empty;

            if (usernameText != null)
            {
                usernameText.text = player != null ? player.Username : string.Empty;
            }

            if (hostBadge != null)
            {
                hostBadge.SetActive(player != null && player.IsHost);
            }

            if (readyBadge != null)
            {
                readyBadge.SetActive(player != null && player.IsReady);
            }

            if (botBadge != null)
            {
                botBadge.SetActive(player != null && player.IsBot);
            }

            if (kickButton != null)
            {
                kickButton.gameObject.SetActive(showKickButton);
                kickButton.onClick.RemoveAllListeners();
                if (showKickButton)
                {
                    kickButton.onClick.AddListener(() => onKick?.Invoke(_playerId));
                }
            }
        }
    }
}
