/*************************************************
 * Project: Panoptes
 * File: NetworkManager.cs
 * Author: Panoptes Team
 * Date: 2026-04-04
 * Description: WebSocket connection lifecycle manager placeholder.
 *************************************************/

using System;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf;
using NativeWebSocket;
using Panoptes.Protocol.V1;
using UnityEngine;

namespace Panoptes.Runtime.Network
{
    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance { get; private set; }

        [Header("Config")]
        [SerializeField] private string serverUrl = "ws://localhost:8080/ws";

        public bool IsConnected => _ws != null && _ws.State == WebSocketState.Open;
        public bool IsConnecting { get; private set; }

        private WebSocket _ws;
        private readonly JsonParser _jsonParser =
            new(JsonParser.Settings.Default.WithIgnoreUnknownFields(true));

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string> OnError;
        public event Action<Envelope> OnEnvelopeReceived;
        public event Action<Envelope> OnEnvelopeSent;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Update()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            _ws?.DispatchMessageQueue();
#endif
        }

        public async Task ConnectAsync(string url)
        {
            var targetUrl = string.IsNullOrWhiteSpace(url) ? serverUrl : url;
            await DisconnectInternalAsync();
            IsConnecting = true;

            _ws = new WebSocket(targetUrl);

            _ws.OnOpen += () =>
            {
                IsConnecting = false;
                Debug.Log("[Network] Connected");
                OnConnected?.Invoke();
            };

            _ws.OnClose += code =>
            {
                IsConnecting = false;
                Debug.Log($"[Network] Disconnected: {code}");
                OnDisconnected?.Invoke();
            };

            _ws.OnError += err =>
            {
                IsConnecting = false;
                Debug.LogError($"[Network] Error: {err}");
                OnError?.Invoke(err);
            };

            _ws.OnMessage += ProcessMessage;

            try
            {
                await _ws.Connect();
            }
            catch (Exception e)
            {
                IsConnecting = false;
                OnError?.Invoke(e.Message);
                throw;
            }
        }

        public void Disconnect()
        {
            _ = DisconnectInternalAsync();
        }

        public void Send<T>(T message) where T : IMessage<T>
        {
            if (!IsConnected)
            {
                Debug.LogWarning("[Network] Not connected, dropping message");
                return;
            }

            var envelope = new Envelope
            {
                Type = message.Descriptor.Name,
                Payload = JsonFormatter.Default.Format(message)
            };

            var envelopeJson = JsonFormatter.Default.Format(envelope);
            var bytes = Encoding.UTF8.GetBytes(envelopeJson);
            _ws.Send(bytes);
            OnEnvelopeSent?.Invoke(envelope);
        }

        private void ProcessMessage(byte[] data)
        {
            try
            {
                var rawJson = Encoding.UTF8.GetString(data);
                var envelope = _jsonParser.Parse<Envelope>(rawJson);
                OnEnvelopeReceived?.Invoke(envelope);

                if (MessageDispatcher.Instance == null)
                {
                    Debug.LogWarning("[Network] MessageDispatcher is not ready.");
                    return;
                }

                MessageDispatcher.Instance.Dispatch(envelope);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Network] Failed to parse message: {e}");
                OnError?.Invoke("invalid_message");
            }
        }

        private async Task DisconnectInternalAsync()
        {
            if (_ws == null)
            {
                return;
            }

            var socket = _ws;
            _ws = null;

            try
            {
                if (socket.State == WebSocketState.Open || socket.State == WebSocketState.Connecting)
                {
                    await socket.Close();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Network] Close failed: {e.Message}");
            }
        }

        async void OnApplicationQuit()
        {
            await DisconnectInternalAsync();
        }
    }
}