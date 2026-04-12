using System;
using System.Collections.Generic;
using Google.Protobuf;
using Panoptes.Protocol.V1;
using Panoptes.Core.Infrastructure.Network;
using UnityEngine;

namespace Panoptes.DebugTools
{
    public sealed class MessageLogger : MonoBehaviour
    {
        [Serializable]
        public struct LogEntry
        {
            public string Direction;
            public string Timestamp;
            public string MsgType;
            public string Summary;
        }

        public static MessageLogger Instance { get; private set; }

        public List<LogEntry> Entries { get; } = new();
        public event Action OnNewEntry;

        private const int MaxEntries = 100;
        [SerializeField] private bool mirrorEntriesToUnityConsole = true;
        private readonly JsonParser _jsonParser =
            new(JsonParser.Settings.Default.WithIgnoreUnknownFields(true));

        private bool _senderHooked;
        private MessageDispatcher _dispatcher;
        private NetworkManager _network;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null)
            {
                return;
            }

            var go = new GameObject("MessageLogger");
            DontDestroyOnLoad(go);
            go.AddComponent<MessageLogger>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            HookSender();
            TryHookRuntimeObjects();
        }

        private void Update()
        {
            TryHookRuntimeObjects();
        }

        private void OnDestroy()
        {
            if (_senderHooked)
            {
                MessageSender.OnSendIntercepted -= OnSendIntercepted;
                _senderHooked = false;
            }

            if (_dispatcher != null)
            {
                _dispatcher.OnDispatching -= OnDispatching;
                _dispatcher = null;
            }

            if (_network != null)
            {
                _network.OnError -= OnNetworkError;
                _network = null;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void HookSender()
        {
            if (_senderHooked)
            {
                return;
            }

            MessageSender.OnSendIntercepted += OnSendIntercepted;
            _senderHooked = true;
        }

        private void TryHookRuntimeObjects()
        {
            var dispatcher = MessageDispatcher.Instance;
            if (dispatcher != null && dispatcher != _dispatcher)
            {
                if (_dispatcher != null)
                {
                    _dispatcher.OnDispatching -= OnDispatching;
                }

                _dispatcher = dispatcher;
                _dispatcher.OnDispatching += OnDispatching;
            }

            var network = NetworkManager.Instance;
            if (network != null && network != _network)
            {
                if (_network != null)
                {
                    _network.OnError -= OnNetworkError;
                }

                _network = network;
                _network.OnError += OnNetworkError;
            }
        }

        private void OnSendIntercepted(string msgType, IMessage message)
        {
            AddEntry("OUT", msgType, BuildOutgoingSummary(msgType, message));
        }

        private void OnDispatching(Envelope envelope)
        {
            if (envelope == null)
            {
                return;
            }

            AddEntry("IN", envelope.Type, BuildIncomingSummary(envelope));
        }

        private void OnNetworkError(string err)
        {
            AddEntry("ERR", "NetworkError", string.IsNullOrWhiteSpace(err) ? "unknown_error" : err);
        }

        private void AddEntry(string direction, string msgType, string summary)
        {
            var timestamp = Time.time.ToString("F2");
            Entries.Add(new LogEntry
            {
                Direction = direction,
                Timestamp = timestamp,
                MsgType = msgType,
                Summary = summary
            });

            if (Entries.Count > MaxEntries)
            {
                Entries.RemoveAt(0);
            }

            MirrorToUnityConsole(direction, timestamp, msgType, summary);
            OnNewEntry?.Invoke();
        }

        private void MirrorToUnityConsole(string direction, string timestamp, string msgType, string summary)
        {
            if (!mirrorEntriesToUnityConsole)
            {
                return;
            }

            var text = $"[DebugPanel/{direction}] t={timestamp} type={msgType} summary={summary}";
            switch (direction)
            {
                case "ERR":
                    Debug.LogError(text);
                    break;
                case "OUT":
                    Debug.Log(text);
                    break;
                default:
                    Debug.Log(text);
                    break;
            }
        }

        private string BuildIncomingSummary(Envelope envelope)
        {
            var payload = envelope.Payload ?? "{}";
            switch (envelope.Type)
            {
                case "MsgDomesticPhaseStart":
                    if (TryParse(payload, out MsgDomesticPhaseStart domesticStart))
                    {
                        return $"turn={domesticStart.Turn} timeout={domesticStart.Timeout} tokens={domesticStart.Tokens}";
                    }
                    break;
                case "MsgCombatPhaseStart":
                    if (TryParse(payload, out MsgCombatPhaseStart combatStart))
                    {
                        return $"turn=0 timeout={combatStart.Timeout}";
                    }
                    break;
                case "MsgTokenResult":
                    if (TryParse(payload, out MsgTokenResult tokenResult))
                    {
                        return $"success={tokenResult.Success} tokens_left={tokenResult.TokensLeft} error={tokenResult.ErrorCode}";
                    }
                    break;
                case "MsgRevealResult":
                    if (TryParse(payload, out MsgRevealResult reveal))
                    {
                        var owner = reveal.TrueState != null ? reveal.TrueState.Owner : string.Empty;
                        var buildingType = reveal.TrueState != null ? reveal.TrueState.BuildingType : string.Empty;
                        return $"node={reveal.NodeId} owner={owner} building={buildingType}";
                    }
                    break;
                case "MsgMinisterReportChunk":
                    if (TryParse(payload, out MsgMinisterReportChunk reportChunk))
                    {
                        var chunkLen = reportChunk.Chunk == null ? 0 : reportChunk.Chunk.Length;
                        return $"role={reportChunk.MinisterRole} chunk_len={chunkLen} final={reportChunk.IsFinal}";
                    }
                    break;
                case "MsgMinisterMetrics":
                    if (TryParse(payload, out MsgMinisterMetrics metrics))
                    {
                        return $"role={metrics.MinisterRole} metrics_count={metrics.Metrics.Count}";
                    }
                    break;
                case "MsgMinisterAction":
                    if (TryParse(payload, out MsgMinisterAction action))
                    {
                        return $"role={action.Minister} actions_count={action.Actions.Count}";
                    }
                    break;
                case "MsgDomesticSettlement":
                    if (TryParse(payload, out MsgDomesticSettlement domesticSettlement))
                    {
                        return $"changes_count={domesticSettlement.Changes.Count}";
                    }
                    break;
                case "MsgCombatSettlement":
                    if (TryParse(payload, out MsgCombatSettlement combatSettlement))
                    {
                        return $"events_count={combatSettlement.Events.Count}";
                    }
                    break;
                case "MsgGameOver":
                    if (TryParse(payload, out MsgGameOver gameOver))
                    {
                        return $"winner={gameOver.WinnerId} reason={gameOver.Reason}";
                    }
                    break;
            }

            return envelope.Type;
        }

        private static string BuildOutgoingSummary(string msgType, IMessage message)
        {
            switch (message)
            {
                case MsgSetPolicy setPolicy:
                    return $"policy={setPolicy.Policy}";
                case MsgTokenBuild tokenBuild:
                    return $"node={tokenBuild.NodeId} building={tokenBuild.BuildingType}";
                case MsgTokenReveal tokenReveal:
                    return $"node={tokenReveal.NodeId}";
                case MsgSetWarZone setWarZone:
                    return $"zone={setWarZone.ZoneId} nodes={setWarZone.NodeIds.Count}";
                case MsgWarZoneDirective directive:
                    return $"zone={directive.ZoneId} directive={directive.Directive}";
                default:
                    return msgType;
            }
        }

        private bool TryParse<T>(string payload, out T message)
            where T : IMessage<T>, new()
        {
            try
            {
                message = _jsonParser.Parse<T>(payload);
                return true;
            }
            catch
            {
                message = default;
                return false;
            }
        }
    }
}
