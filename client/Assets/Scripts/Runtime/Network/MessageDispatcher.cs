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
using UnityEngine;

namespace Panoptes.Runtime.Network
{
    public class MessageDispatcher : MonoBehaviour
    {
        public static MessageDispatcher Instance { get; private set; }

        // type string → handler
        private readonly Dictionary<string, Action<ByteString>> _handlers = new();

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

        // 注册 Handler
        // 用法：Register<MsgGameInit>("MsgGameInit", OnGameInit)
        public void Register<T>(string messageType, Action<T> handler)
            where T : IMessage<T>, new()
        {
            _handlers[messageType] = payload =>
            {
                try
                {
                    var msg = new T();
                    msg = (T)msg.Descriptor.Parser.ParseFrom(payload);
                    handler(msg);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Dispatcher] Failed to parse {messageType}: {e.Message}");
                }
            };
        }

        // 取消注册
        public void Unregister(string messageType)
        {
            _handlers.Remove(messageType);
        }

        // 由 NetworkManager 调用，已在主线程
        public void Dispatch(Envelope envelope)
        {
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