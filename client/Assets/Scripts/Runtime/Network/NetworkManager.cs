/*************************************************
 * Project: Panoptes
 * File: NetworkManager.cs
 * Author: Panoptes Team
 * Date: 2026-04-04
 * Description: WebSocket connection lifecycle manager placeholder.
 *************************************************/

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Protobuf;
using NativeWebSocket;
using Panoptes.Runtime.Protocol;
using UnityEngine;

namespace Panoptes.Runtime.Network
{
    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance { get; private set; }

        [Header("Config")]
        [SerializeField] private string serverUrl = "ws://localhost:8080/ws";

        public bool IsConnected => _ws?.State == WebSocketState.Open;

        private WebSocket _ws;
        private readonly Queue<byte[]> _receiveQueue = new();
        private readonly object _queueLock = new();

        // 连接状态事件
        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string> OnError;

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
            // NativeWebSocket 需要在主线程 dispatch
#if !UNITY_WEBGL || UNITY_EDITOR
            _ws?.DispatchMessageQueue();
#endif

            // 处理接收队列（已在主线程）
            lock (_queueLock)
            {
                while (_receiveQueue.Count > 0)
                {
                    var data = _receiveQueue.Dequeue();
                    ProcessMessage(data);
                }
            }
        }

        public async Task ConnectAsync(string url = null)
        {
            var targetUrl = url ?? serverUrl;

            _ws = new WebSocket(targetUrl);

            _ws.OnOpen += () =>
            {
                Debug.Log("[Network] Connected");
                OnConnected?.Invoke();
            };

            _ws.OnClose += code =>
            {
                Debug.Log($"[Network] Disconnected: {code}");
                OnDisconnected?.Invoke();
            };

            _ws.OnError += err =>
            {
                Debug.LogError($"[Network] Error: {err}");
                OnError?.Invoke(err);
            };

            _ws.OnMessage += data =>
            {
                // WebSocket 回调可能在非主线程，放进队列
                lock (_queueLock)
                {
                    _receiveQueue.Enqueue(data);
                }
            };

            await _ws.Connect();
        }

        public void Disconnect()
        {
            _ws?.Close();
        }

        // 发送任意 proto 消息，自动包装进 Envelope
        public void Send<T>(T message) where T : IMessage<T>
        {
            if (!IsConnected)
            {
                Debug.LogWarning("[Network] Not connected, dropping message");
                return;
            }

            var envelope = new Envelope
            {
                Type = typeof(T).Name,
                Payload = message.ToByteString()
            };

            var bytes = envelope.ToByteArray();
            _ws.Send(bytes);
        }

        private void ProcessMessage(byte[] data)
        {
            try
            {
                var envelope = Envelope.Parser.ParseFrom(data);
                MessageDispatcher.Instance.Dispatch(envelope);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Network] Failed to parse message: {e.Message}");
            }
        }

        async void OnApplicationQuit()
        {
            await _ws?.Close();
        }
    }
}