/*************************************************
 * Project: Panoptes
 * File: NetworkManager.cs
 * Author: Panoptes Team
 * Date: 2026-04-04
 * Description: WebSocket connection lifecycle manager placeholder.
 *************************************************/

using System;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf;
using NativeWebSocket;
using Panoptes.Protocol.V1;
using Panoptes.Core.Application.App;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Infrastructure.Service;
using UnityEngine;
using UnityEngine.Networking;

namespace Panoptes.Core.Infrastructure.Network
{
    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance { get; private set; }

        [Header("Config")]
        [SerializeField] private string serverUrl = "ws://localhost:8080/ws";

        public string ServerUrl => serverUrl;
        public bool IsConnected => _ws != null && _ws.State == WebSocketState.Open;
        public bool IsConnecting { get; private set; }

        private WebSocket _ws;
        private string _lastConnectedUrl = string.Empty;
        private bool _manualDisconnectRequested;
        private bool _isReconnecting;
        private readonly JsonParser _jsonParser =
            new(JsonParser.Settings.Default.WithIgnoreUnknownFields(true));

        public event System.Action OnConnected;
        public event System.Action OnDisconnected;
        public event System.Action<string> OnError;
        public event System.Action<ServerFrame> OnFrameReceived;
        public event System.Action<ClientFrame> OnFrameSent;

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
            _lastConnectedUrl = targetUrl;
            _manualDisconnectRequested = false;
            await DisconnectInternalAsync();
            IsConnecting = true;

            _ws = new WebSocket(targetUrl);

            _ws.OnOpen += () =>
            {
                IsConnecting = false;
                Debug.Log("[Network] Connected");
                HideLoadingOverlay();
                OnConnected?.Invoke();
            };

            _ws.OnClose += code =>
            {
                IsConnecting = false;
                Debug.Log($"[Network] Disconnected: {code}");
                _ = HandleDisconnectAsync();
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

        public Task ConnectWithSessionAsync()
        {
            var token = SessionManager.Instance != null ? SessionManager.Instance.Token : string.Empty;
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException("Session token is missing.");
            }

            var delimiter = serverUrl.Contains("?") ? "&" : "?";
            var url = $"{serverUrl}{delimiter}token={UnityWebRequest.EscapeURL(token)}";
            return ConnectAsync(url);
        }

        public void Disconnect()
        {
            _manualDisconnectRequested = true;
            _ = DisconnectInternalAsync();
        }

        public void Send(IMessage message)
        {
            if (!IsConnected)
            {
                Debug.LogWarning("[Network] Not connected, dropping message");
                return;
            }

            if (!TransportFrames.TryCreateClientFrame(message, out var frame, out var error))
            {
                Debug.LogError($"[Network] Failed to wrap outbound message {message.Descriptor.Name}: {error}");
                OnError?.Invoke("invalid_request");
                return;
            }

            var frameJson = JsonFormatter.Default.Format(frame);
            var bytes = Encoding.UTF8.GetBytes(frameJson);
            _ws.Send(bytes);
            OnFrameSent?.Invoke(frame);
        }

        public void Send<T>(T message) where T : IMessage<T>
        {
            Send((IMessage)message);
        }

        private void ProcessMessage(byte[] data)
        {
            try
            {
                var rawJson = Encoding.UTF8.GetString(data);
                var frame = _jsonParser.Parse<ServerFrame>(rawJson);
                OnFrameReceived?.Invoke(frame);

                if (MessageDispatcher.Instance == null)
                {
                    Debug.LogWarning("[Network] MessageDispatcher is not ready.");
                    return;
                }

                MessageDispatcher.Instance.Dispatch(frame);
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

        private async Task HandleDisconnectAsync()
        {
            OnDisconnected?.Invoke();

            if (_manualDisconnectRequested)
            {
                _manualDisconnectRequested = false;
                return;
            }

            if (RoomCache.Instance != null && !string.IsNullOrWhiteSpace(RoomCache.Instance.RoomID))
            {
                RoomCache.Instance.Clear();
            }

            ShowLoadingOverlay("连接断开，正在重连...");

            if (_isReconnecting || string.IsNullOrWhiteSpace(_lastConnectedUrl))
            {
                return;
            }

            _isReconnecting = true;
            try
            {
                for (var retry = 0; retry < 5; retry++)
                {
                    await Task.Delay(2000);

                    try
                    {
                        await ConnectAsync(_lastConnectedUrl);
                        HideLoadingOverlay();
                        return;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[Network] Reconnect failed ({retry + 1}/5): {e.Message}");
                    }
                }
            }
            finally
            {
                _isReconnecting = false;
            }

            HideLoadingOverlay();
            ClientRuntimeConfigCache.Instance?.Clear();
            ConfigCache.Instance?.Clear();
            GameStateCache.Instance?.Clear();
            if (AppManager.Instance != null)
            {
                AppManager.Instance.TransitionTo(AppState.Login);
            }
        }

        private static void ShowLoadingOverlay(string message)
        {
            InvokeLoadingOverlay("Show", message);
        }

        private static void HideLoadingOverlay()
        {
            InvokeLoadingOverlay("Hide", null);
        }

        private static void InvokeLoadingOverlay(string methodName, string message)
        {
            var overlayType = Type.GetType("Panoptes.Presentation.UI.Common.LoadingOverlay, Panoptes.Presentation");
            if (overlayType == null)
            {
                return;
            }

            var instanceProperty = overlayType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            var instance = instanceProperty?.GetValue(null);
            if (instance == null)
            {
                return;
            }

            MethodInfo method;
            object[] args;
            if (message == null)
            {
                method = overlayType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                args = Array.Empty<object>();
            }
            else
            {
                method = overlayType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(string) }, null);
                args = new object[] { message };
            }

            method?.Invoke(instance, args);
        }

        async void OnApplicationQuit()
        {
            await DisconnectInternalAsync();
        }
    }
}
