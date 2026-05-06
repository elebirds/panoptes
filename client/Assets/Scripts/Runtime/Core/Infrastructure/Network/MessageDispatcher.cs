/*************************************************
 * Project: Panoptes
 * File: MessageDispatcher.cs
 * Author: Panoptes Team
 * Date: 2026-04-04
 * Description: Typed ServerFrame message dispatcher.
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
        public readonly struct DispatchEntry
        {
            public DispatchEntry(ServerFrame frame, string messageType, IMessage message, string payloadJson)
            {
                Frame = frame;
                MessageType = messageType ?? string.Empty;
                Message = message;
                PayloadJson = payloadJson ?? "{}";
            }

            public ServerFrame Frame { get; }
            public string MessageType { get; }
            public IMessage Message { get; }
            public string PayloadJson { get; }
        }

        public static MessageDispatcher Instance { get; private set; }
        public event Action<DispatchEntry> OnDispatching;
        private GameEventSessionGate _gameEventSessionGate;

        private readonly Dictionary<string, List<Action<IMessage>>> _typedHandlers =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<Delegate, Action<IMessage>>> _typedHandlerWrappers =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<Action<string>>> _rawHandlers =
            new(StringComparer.Ordinal);

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

        public void UseGameEventSessionGate(GameEventSessionGate gameEventSessionGate)
        {
            _gameEventSessionGate = gameEventSessionGate;
        }

        public void Register<T>(string messageType, Action<T> handler)
            where T : IMessage<T>, new()
        {
            if (string.IsNullOrWhiteSpace(messageType) || handler == null)
            {
                return;
            }

            Action<IMessage> wrapper = message =>
            {
                if (message is T typed)
                {
                    handler(typed);
                    return;
                }

                PanoptesLog.Error($"[Dispatcher] Message type mismatch for {messageType}: {message?.GetType().Name ?? "null"}");
            };

            if (!_typedHandlerWrappers.TryGetValue(messageType, out var wrappers))
            {
                wrappers = new Dictionary<Delegate, Action<IMessage>>();
                _typedHandlerWrappers[messageType] = wrappers;
            }

            wrappers[handler] = wrapper;
            RegisterTyped(messageType, wrapper);
        }

        private void RegisterTyped(string messageType, Action<IMessage> handler)
        {
            if (string.IsNullOrWhiteSpace(messageType) || handler == null)
            {
                return;
            }

            if (!_typedHandlers.TryGetValue(messageType, out var list))
            {
                list = new List<Action<IMessage>>();
                _typedHandlers[messageType] = list;
            }

            list.Add(handler);
        }

        public void RegisterRaw(string messageType, Action<string> handler)
        {
            if (string.IsNullOrWhiteSpace(messageType))
            {
                PanoptesLog.Warning("[Dispatcher] RegisterRaw failed: messageType is empty.");
                return;
            }

            if (handler == null)
            {
                PanoptesLog.Warning($"[Dispatcher] RegisterRaw failed: handler is null for {messageType}.");
                return;
            }

            if (!_rawHandlers.TryGetValue(messageType, out var list))
            {
                list = new List<Action<string>>();
                _rawHandlers[messageType] = list;
            }

            list.Add(handler);
        }

        public void Unregister(string messageType)
        {
            if (string.IsNullOrWhiteSpace(messageType))
            {
                return;
            }

            _typedHandlers.Remove(messageType);
            _rawHandlers.Remove(messageType);
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

            UnregisterTyped(messageType, wrapper);
            wrappers.Remove(handler);
            if (wrappers.Count == 0)
            {
                _typedHandlerWrappers.Remove(messageType);
            }
        }

        private void UnregisterTyped(string messageType, Action<IMessage> handler)
        {
            if (string.IsNullOrWhiteSpace(messageType) || handler == null)
            {
                return;
            }

            if (!_typedHandlers.TryGetValue(messageType, out var list))
            {
                return;
            }

            list.Remove(handler);
            if (list.Count == 0)
            {
                _typedHandlers.Remove(messageType);
            }
        }

        public void UnregisterRaw(string messageType, Action<string> handler)
        {
            if (string.IsNullOrWhiteSpace(messageType) || handler == null)
            {
                return;
            }

            if (!_rawHandlers.TryGetValue(messageType, out var list))
            {
                return;
            }

            list.Remove(handler);
            if (list.Count == 0)
            {
                _rawHandlers.Remove(messageType);
            }
        }

        public void Dispatch(ServerFrame frame)
        {
            if (frame == null)
            {
                PanoptesLog.Warning("[Dispatcher] Received null server frame.");
                return;
            }

            if (!TransportFrames.TryExtract(frame, out var message, out var messageType, out var payloadJson))
            {
                PanoptesLog.Warning("[Dispatcher] Failed to extract payload from ServerFrame.");
                return;
            }

            var entry = new DispatchEntry(frame, messageType, message, payloadJson);
            OnDispatching?.Invoke(entry);
            var gameEventSessionGate = _gameEventSessionGate;
            if (gameEventSessionGate == null && Panoptes.Core.Application.Cache.GameStateCache.Instance != null)
            {
                gameEventSessionGate = new GameEventSessionGate(Panoptes.Core.Application.Cache.GameStateCache.Instance);
                _gameEventSessionGate = gameEventSessionGate;
            }

            if (gameEventSessionGate != null && !gameEventSessionGate.ShouldDispatch(entry))
            {
                return;
            }

            var hasRawHandlers = _rawHandlers.TryGetValue(messageType, out var rawHandlers) &&
                                 rawHandlers != null &&
                                 rawHandlers.Count > 0;
            if (hasRawHandlers)
            {
                var snapshot = rawHandlers.ToArray();
                for (int i = 0; i < snapshot.Length; i++)
                {
                    try
                    {
                        snapshot[i]?.Invoke(payloadJson);
                    }
                    catch (Exception e)
                    {
                        PanoptesLog.Error($"[Dispatcher] Raw handler failed for {messageType}: {e}");
                    }
                }
            }

            if (_typedHandlers.TryGetValue(messageType, out var typedHandlers) && typedHandlers != null && typedHandlers.Count > 0)
            {
                var snapshot = typedHandlers.ToArray();
                for (int i = 0; i < snapshot.Length; i++)
                {
                    try
                    {
                        snapshot[i]?.Invoke(message);
                    }
                    catch (Exception e)
                    {
                        PanoptesLog.Error($"[Dispatcher] Typed handler failed for {messageType}: {e}");
                    }
                }
                return;
            }

            if (!hasRawHandlers)
            {
                PanoptesLog.Warning($"[Dispatcher] No handler for: {messageType}");
            }
        }
    }
}
