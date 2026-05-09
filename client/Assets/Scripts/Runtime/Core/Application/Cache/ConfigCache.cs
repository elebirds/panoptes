/*************************************************
 * Project: Panoptes
 * File: ConfigCache.cs
 * Author: Panoptes Team
 * Date: 2026-04-17
 * Description: Session-scoped runtime JSON config cache.
 *************************************************/

using System;
using System.Collections.Generic;
using Panoptes.Protocol.V1;
using UnityEngine;

namespace Panoptes.Core.Application.Cache
{
    public sealed class ConfigCache : MonoBehaviour
    {
#pragma warning disable CS0649
        [Serializable]
        private sealed class ConfigPushPayload
        {
            public string key;
            public string json;
            public string config_key;
            public string config_json;
            public string value;
        }

        [Serializable]
        private sealed class ConfigBatchPayload
        {
            public ConfigPushPayload[] configs;
        }

        [Serializable]
        private sealed class KnownConfigBundlePayload
        {
            public string buildconfig;
            public string armyconfig;
            public string maptileconfig;
            public string mapconfig;
            public string map;
        }
#pragma warning restore CS0649

        public static ConfigCache Instance { get; private set; }

        [SerializeField] private bool logUpdates = true;

        private readonly Dictionary<string, string> _jsonByKey = new(StringComparer.OrdinalIgnoreCase);

        public event Action<string> ConfigUpdated;

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

        public bool TryGetJson(string key, out string json)
        {
            json = string.Empty;
            key = NormalizeKey(key);
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            return _jsonByKey.TryGetValue(key, out json) && !string.IsNullOrWhiteSpace(json);
        }

        public void SetJson(string key, string json, bool notify = true)
        {
            key = NormalizeKey(key);
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                _jsonByKey.Remove(key);
                if (notify)
                {
                    ConfigUpdated?.Invoke(key);
                }
                return;
            }

            _jsonByKey[key] = json;
            if (logUpdates)
            {
                PanoptesLog.Log($"[ConfigCache] Updated config '{key}', length={json.Length}.");
            }

            if (notify)
            {
                ConfigUpdated?.Invoke(key);
            }
        }

        public void ApplyBatch(MsgConfigBatchJson msg)
        {
            if (msg == null || msg.Configs == null || msg.Configs.Count == 0)
            {
                return;
            }

            for (var i = 0; i < msg.Configs.Count; i++)
            {
                ApplyEntry(msg.Configs[i]);
            }
        }

        public void Clear()
        {
            if (_jsonByKey.Count == 0)
            {
                return;
            }

            var clearedKeys = new List<string>(_jsonByKey.Keys);
            _jsonByKey.Clear();

            for (var i = 0; i < clearedKeys.Count; i++)
            {
                ConfigUpdated?.Invoke(clearedKeys[i]);
            }
        }

        public bool ApplyPushJson(string payloadJson)
        {
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return false;
            }

            if (TryApplySingle(payloadJson))
            {
                return true;
            }

            if (TryApplyBatch(payloadJson))
            {
                return true;
            }

            if (TryApplyKnownBundle(payloadJson))
            {
                return true;
            }

            return false;
        }

        private void ApplyEntry(ConfigJsonEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            SetJson(entry.Key, entry.Json);
        }

        private bool TryApplySingle(string payloadJson)
        {
            ConfigPushPayload parsed;
            try
            {
                parsed = JsonUtility.FromJson<ConfigPushPayload>(payloadJson);
            }
            catch
            {
                return false;
            }

            return TryApplySinglePayload(parsed);
        }

        private bool TryApplyBatch(string payloadJson)
        {
            ConfigBatchPayload parsed;
            try
            {
                parsed = JsonUtility.FromJson<ConfigBatchPayload>(payloadJson);
            }
            catch
            {
                return false;
            }

            if (parsed?.configs == null || parsed.configs.Length == 0)
            {
                return false;
            }

            var anyApplied = false;
            for (var i = 0; i < parsed.configs.Length; i++)
            {
                anyApplied |= TryApplySinglePayload(parsed.configs[i]);
            }

            return anyApplied;
        }

        private bool TryApplyKnownBundle(string payloadJson)
        {
            KnownConfigBundlePayload parsed;
            try
            {
                parsed = JsonUtility.FromJson<KnownConfigBundlePayload>(payloadJson);
            }
            catch
            {
                return false;
            }

            if (parsed == null)
            {
                return false;
            }

            var anyApplied = false;
            anyApplied |= TryApplyKnownKey("buildconfig", parsed.buildconfig);
            anyApplied |= TryApplyKnownKey("armyconfig", parsed.armyconfig);
            anyApplied |= TryApplyKnownKey("maptileconfig", parsed.maptileconfig);
            anyApplied |= TryApplyKnownKey("mapconfig", parsed.mapconfig);
            anyApplied |= TryApplyKnownKey("map", parsed.map);
            return anyApplied;
        }

        private bool TryApplyKnownKey(string key, string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            SetJson(key, json);
            return true;
        }

        private bool TryApplySinglePayload(ConfigPushPayload payload)
        {
            if (payload == null)
            {
                return false;
            }

            var key = NormalizeKey(payload.key);
            if (string.IsNullOrEmpty(key))
            {
                key = NormalizeKey(payload.config_key);
            }

            var json = payload.json;
            if (string.IsNullOrWhiteSpace(json))
            {
                json = payload.config_json;
            }
            if (string.IsNullOrWhiteSpace(json))
            {
                json = payload.value;
            }

            if (string.IsNullOrEmpty(key) || string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            SetJson(key, json);
            return true;
        }

        private static string NormalizeKey(string key)
        {
            return (key ?? string.Empty).Trim().ToLowerInvariant();
        }
    }
}
