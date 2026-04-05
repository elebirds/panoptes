/*************************************************
 * Project: Panoptes
 * File: MessageDispatcher.cs
 * Author: Panoptes Team
 * Date: 2026-04-04
 * Description: Envelope message dispatch placeholder.
 *************************************************/

using System;
using System.Collections.Generic;
using Google.Protobuf;
using Panoptes.Protocol.V1;
using UnityEngine;

namespace Panoptes.Runtime.Network
{
    public class MessageDispatcher : MonoBehaviour
    {
        public static MessageDispatcher Instance { get; private set; }

        private readonly Dictionary<string, Action<string>> _handlers =
            new(StringComparer.Ordinal);

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

        public void Register<T>(string messageType, Action<T> handler)
            where T : IMessage<T>, new()
        {
            if (string.IsNullOrWhiteSpace(messageType))
            {
                Debug.LogError("[Dispatcher] Message type is required.");
                return;
            }

            if (handler == null)
            {
                Debug.LogError($"[Dispatcher] Handler for {messageType} is null.");
                return;
            }

            _handlers[messageType] = payloadJson =>
            {
                try
                {
                    var json = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson;
                    var msg = JsonParser.Default.Parse<T>(json);
                    handler(msg);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Dispatcher] Failed to parse {messageType}: {e}");
                }
            };
        }

        public void Unregister(string messageType)
        {
            if (string.IsNullOrWhiteSpace(messageType))
            {
                return;
            }

            _handlers.Remove(messageType);
        }

        public void Dispatch(Envelope envelope)
        {
            if (envelope == null)
            {
                Debug.LogWarning("[Dispatcher] Received null envelope.");
                return;
            }

            if (_handlers.TryGetValue(envelope.Type, out var handler))
            {
                handler(envelope.Payload);
            }
            else
            {
                Debug.LogWarning($"[Dispatcher] No handler for: {envelope.Type}");
            }
        }
    }
}