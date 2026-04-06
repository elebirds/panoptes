/*************************************************
 * Project: Panoptes
 * File: ConfigMessageBridge.cs
 * Author: Panoptes Team
 * Date: 2026-04-06
 * Description: Receives server config push messages and stores them to ConfigCache.
 *************************************************/

using System.Collections.Generic;
using Panoptes.Runtime.Cache;
using UnityEngine;

namespace Panoptes.Runtime.Network
{
    public sealed class ConfigMessageBridge : MonoBehaviour
    {
        [Header("Wrapped Message Types")]
        [Tooltip("Payload is utf8 JSON wrapper. Example: {\"key\":\"buildconfig\",\"json\":\"...\"}")]
        [SerializeField] private string singleConfigMessageType = "MsgConfigPushJson";

        [Tooltip("Payload is utf8 JSON wrapper. Example: {\"configs\":[{\"key\":\"buildconfig\",\"json\":\"...\"}]}")]
        [SerializeField] private string batchConfigMessageType = "MsgConfigBatchJson";

        [Header("Direct Message Types")]
        [Tooltip("Payload is raw buildconfig JSON text.")]
        [SerializeField] private string buildConfigMessageType = "MsgBuildConfig";

        [Tooltip("Payload is raw armyconfig JSON text.")]
        [SerializeField] private string armyConfigMessageType = "MsgArmyConfig";

        [Tooltip("Payload is raw maptileconfig JSON text.")]
        [SerializeField] private string mapTileConfigMessageType = "MsgMapTileConfig";

        [Tooltip("Payload is raw map JSON text.")]
        [SerializeField] private string mapConfigMessageType = "MsgMapConfig";

        [Header("Debug")]
        [SerializeField] private bool logUnknownPayloadWarning = true;

        private readonly List<string> _registeredTypes = new();
        private bool _handlersRegistered;

        private void OnEnable()
        {
            TryRegisterHandlers();
        }

        private void Update()
        {
            if (!_handlersRegistered && MessageDispatcher.Instance != null)
            {
                TryRegisterHandlers();
            }
        }

        private void OnDisable()
        {
            UnregisterHandlers();
        }

        private void TryRegisterHandlers()
        {
            if (_handlersRegistered || MessageDispatcher.Instance == null)
            {
                return;
            }

            RegisterWrapped(singleConfigMessageType);
            RegisterWrapped(batchConfigMessageType);

            RegisterDirect(buildConfigMessageType, "buildconfig");
            RegisterDirect(armyConfigMessageType, "armyconfig");
            RegisterDirect(mapTileConfigMessageType, "maptileconfig");
            RegisterDirect(mapConfigMessageType, "mapconfig");

            _handlersRegistered = _registeredTypes.Count > 0;
        }

        private void RegisterWrapped(string messageType)
        {
            if (string.IsNullOrWhiteSpace(messageType) || MessageDispatcher.Instance == null)
            {
                return;
            }

            var normalized = messageType.Trim();
            if (_registeredTypes.Contains(normalized))
            {
                return;
            }

            MessageDispatcher.Instance.RegisterRaw(normalized, HandleWrappedPayload);
            _registeredTypes.Add(normalized);
        }

        private void RegisterDirect(string messageType, string configKey)
        {
            if (string.IsNullOrWhiteSpace(messageType) || string.IsNullOrWhiteSpace(configKey) || MessageDispatcher.Instance == null)
            {
                return;
            }

            var normalized = messageType.Trim();
            if (_registeredTypes.Contains(normalized))
            {
                return;
            }

            var key = configKey.Trim().ToLowerInvariant();
            MessageDispatcher.Instance.RegisterRaw(normalized, payload => HandleDirectPayload(payload, key));
            _registeredTypes.Add(normalized);
        }

        private void UnregisterHandlers()
        {
            if (!_handlersRegistered || MessageDispatcher.Instance == null)
            {
                _registeredTypes.Clear();
                _handlersRegistered = false;
                return;
            }

            for (var i = 0; i < _registeredTypes.Count; i++)
            {
                MessageDispatcher.Instance.Unregister(_registeredTypes[i]);
            }

            _registeredTypes.Clear();
            _handlersRegistered = false;
        }

        private void HandleWrappedPayload(string payloadJson)
        {
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                if (logUnknownPayloadWarning)
                {
                    Debug.LogWarning("[ConfigMessageBridge] Received empty wrapped config payload.");
                }
                return;
            }

            var cache = ConfigCache.EnsureInstance();
            if (!cache.ApplyPushJson(payloadJson) && logUnknownPayloadWarning)
            {
                Debug.LogWarning("[ConfigMessageBridge] Unsupported wrapped config payload format.");
            }
        }

        private void HandleDirectPayload(string payloadJson, string configKey)
        {
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                if (logUnknownPayloadWarning)
                {
                    Debug.LogWarning($"[ConfigMessageBridge] Received empty direct payload for '{configKey}'.");
                }
                return;
            }

            var cache = ConfigCache.EnsureInstance();
            cache.SetJson(configKey, payloadJson);
        }
    }
}
