using System;
using Panoptes.Protocol.V1;
using UnityEngine;

namespace Panoptes.Runtime.Cache
{
    public sealed class ClientRuntimeConfigCache : MonoBehaviour
    {
        public static ClientRuntimeConfigCache Instance { get; private set; }

        public bool DevMode { get; private set; }

        public event Action OnConfigChanged;

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

        public void Apply(MsgClientRuntimeConfig msg)
        {
            DevMode = msg != null && msg.DevMode;
            OnConfigChanged?.Invoke();
        }

        public void Clear()
        {
            DevMode = false;
            OnConfigChanged?.Invoke();
        }
    }
}
