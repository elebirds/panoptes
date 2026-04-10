/*************************************************
 * Project: Panoptes
 * File: MessageDispatcher.cs
 * Author: Panoptes Team
 * Date: 2026-04-04
 * Description: Envelope message dispatcher.
 *************************************************/

using System;
using System.Collections.Generic;
using Google.Protobuf;
using Panoptes.Protocol.V1;
using UnityEngine;

namespace Panoptes.Core.Infrastructure.Network
{
    public class MessageDispatcher : MonoBehaviour
    {
        public static MessageDispatcher Instance { get; private set; }
        public event Action<Envelope> OnDispatching;

        // message type -> handlers
        private readonly Dictionary<string, List<Action<string>>> _handlers =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<Delegate, Action<string>>> _typedHandlerWrappers =
            new(StringComparer.Ordinal);
        private readonly JsonParser _jsonParser =
            new(JsonParser.Settings.Default.WithIgnoreUnknownFields(true));

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

        public void Register<T>(string messageType, Action<T> handler)
            where T : IMessage<T>, new()
        {
            if (string.IsNullOrWhiteSpace(messageType) || handler == null)
            {
                return;
            }

            Action<string> wrapper = payloadJson =>
            {
                try
                {
                    var json = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson;
                    var msg = _jsonParser.Parse<T>(json);
                    handler(msg);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Dispatcher] Failed to parse {messageType}: {e}");
                }
            };

            if (!_typedHandlerWrappers.TryGetValue(messageType, out var wrappers))
            {
                wrappers = new Dictionary<Delegate, Action<string>>();
                _typedHandlerWrappers[messageType] = wrappers;
            }

            wrappers[handler] = wrapper;
            RegisterRaw(messageType, wrapper);
        }

        // Register raw payload handler (Envelope.Payload JSON string)
        public void RegisterRaw(string messageType, Action<string> handler)
        {
            if (string.IsNullOrWhiteSpace(messageType))
            {
                Debug.LogWarning("[Dispatcher] RegisterRaw failed: messageType is empty.");
                return;
            }

            if (handler == null)
            {
                Debug.LogWarning($"[Dispatcher] RegisterRaw failed: handler is null for {messageType}.");
                return;
            }

            if (!_handlers.TryGetValue(messageType, out var list))
            {
                list = new List<Action<string>>();
                _handlers[messageType] = list;
            }

            list.Add(handler);
        }

        public void Unregister(string messageType)
        {
            if (string.IsNullOrWhiteSpace(messageType))
            {
                return;
            }

            _handlers.Remove(messageType);
            _typedHandlerWrappers.Remove(messageType);
        }

        public void Unregister<T>(string messageType, Action<T> handler)
            where T : IMessage<T>, new()
        {
            if (string.IsNullOrWhiteSpace(messageType) || handler == null)
            {
                return;
            }

            if (!_typedHandlerWrappers.TryGetValue(messageType, out var wrappers))
            {
                return;
            }

            if (!wrappers.TryGetValue(handler, out var wrapper))
            {
                return;
            }

            UnregisterRaw(messageType, wrapper);
            wrappers.Remove(handler);
            if (wrappers.Count == 0)
            {
                _typedHandlerWrappers.Remove(messageType);
            }
        }

        public void UnregisterRaw(string messageType, Action<string> handler)
        {
            if (string.IsNullOrWhiteSpace(messageType) || handler == null)
            {
                return;
            }

            if (!_handlers.TryGetValue(messageType, out var list))
            {
                return;
            }

            list.Remove(handler);
            if (list.Count == 0)
            {
                _handlers.Remove(messageType);
            }
        }

        public void Dispatch(Envelope envelope)
        {
            if (envelope == null)
            {
                Debug.LogWarning("[Dispatcher] Received null envelope.");
                return;
            }

            OnDispatching?.Invoke(envelope);

            if (_handlers.TryGetValue(envelope.Type, out var handlers) && handlers != null && handlers.Count > 0)
            {
                var snapshot = handlers.ToArray();
                for (int i = 0; i < snapshot.Length; i++)
                {
                    try
                    {
                        snapshot[i]?.Invoke(envelope.Payload);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[Dispatcher] Handler failed for {envelope.Type}: {e}");
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[Dispatcher] No handler for: {envelope.Type}");
            }
        }
    }
}
