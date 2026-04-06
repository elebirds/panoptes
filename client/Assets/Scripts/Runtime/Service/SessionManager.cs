using UnityEngine;

namespace Panoptes.Runtime.Service
{
    public sealed class SessionManager : MonoBehaviour
    {
        public static SessionManager Instance { get; private set; }

        public string Token { get; private set; }
        public string PlayerID { get; private set; }
        public string Username { get; private set; }

        public bool IsLoggedIn => !string.IsNullOrWhiteSpace(Token);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void SetSession(string token, string playerID, string username)
        {
            Token = token;
            PlayerID = playerID;
            Username = username;
        }

        public void Clear()
        {
            Token = null;
            PlayerID = null;
            Username = null;
        }
    }
}