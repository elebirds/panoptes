#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
            public string PayloadJson;
            public bool CanReplay;
            public string Error;
        }

        public static MessageLogger Instance { get; private set; }

        public List<LogEntry> Entries { get; } = new();
        public event Action OnNewEntry;

        private const int MaxEntries = 100;
        private const string ColorPhaseStart = "#6EEB83";
        private const string ColorOutgoing = "#8FD3FF";
        private const string ColorIncoming = "#FFD166";
        private const string ColorError = "#FF7A7A";
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
            var payloadJson = JsonFormatter.Default.Format(message);
            AddEntry(
                "OUT",
                msgType,
                BuildOutgoingSummary(msgType, message),
                payloadJson,
                DebugMessageRegistry.SupportsMessageType(msgType),
                string.Empty);
        }

        private void OnDispatching(Envelope envelope)
        {
            if (envelope == null)
            {
                return;
            }

            AddEntry(
                "IN",
                envelope.Type,
                BuildIncomingSummary(envelope),
                envelope.Payload ?? "{}",
                DebugMessageRegistry.SupportsMessageType(envelope.Type),
                string.Empty);
        }

        private void OnNetworkError(string err)
        {
            var message = string.IsNullOrWhiteSpace(err) ? "unknown_error" : err;
            AddEntry("ERR", "NetworkError", message, string.Empty, false, message);
        }

        private void AddEntry(string direction, string msgType, string summary, string payloadJson, bool canReplay, string error)
        {
            var timestamp = Time.time.ToString("F2");
            Entries.Add(new LogEntry
            {
                Direction = direction,
                Timestamp = timestamp,
                MsgType = msgType,
                Summary = summary,
                PayloadJson = payloadJson ?? string.Empty,
                CanReplay = canReplay,
                Error = error ?? string.Empty
            });

            if (Entries.Count > MaxEntries)
            {
                Entries.RemoveAt(0);
            }

            MirrorToUnityConsole(direction, timestamp, msgType, summary, error);
            OnNewEntry?.Invoke();
        }

        private void MirrorToUnityConsole(string direction, string timestamp, string msgType, string summary, string error)
        {
            if (!mirrorEntriesToUnityConsole)
            {
                return;
            }

            var text = $"[DebugPanel/{direction}] t={timestamp} type={msgType} summary={summary}";
            if (!string.IsNullOrWhiteSpace(error))
            {
                text = $"{text} error={error}";
            }

            switch (direction)
            {
                case "ERR":
                    Debug.LogError(WrapColor(text, ColorError));
                    break;
                case "OUT":
                    Debug.Log(WrapColor(text, ColorOutgoing));
                    break;
                case "IN":
                    Debug.Log(WrapColor(text, ColorIncoming));
                    break;
                default:
                    Debug.Log(text);
                    break;
            }
        }

        public static string WrapPhaseStartColor(string text)
        {
            return WrapColor(text, ColorPhaseStart);
        }

        private static string WrapColor(string text, string colorHex)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(colorHex))
            {
                return text ?? string.Empty;
            }

            return $"<color={colorHex}>{text}</color>";
        }

        private string BuildIncomingSummary(Envelope envelope)
        {
            var payload = envelope.Payload ?? "{}";
            switch (envelope.Type)
            {
                case "MsgPlanningStart":
                    if (TryParse(payload, out MsgPlanningStart planningStart))
                    {
                        return $"turn={planningStart.Turn} timeout={planningStart.Timeout} tokens={planningStart.Tokens}";
                    }
                    break;
                case "MsgPlanningSnapshot":
                    if (TryParse(payload, out MsgPlanningSnapshot planningSnapshot))
                    {
                        return $"turn={planningSnapshot.Turn} unit_orders={planningSnapshot.UnitOrders.Count}";
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
                case "MsgTurnSettlement":
                    if (TryParse(payload, out MsgTurnSettlement turnSettlement))
                    {
                        return $"turn={turnSettlement.Turn} sections={turnSettlement.Sections.Count} next_phase={turnSettlement.NextPhase}";
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
                case MsgBuildStructure buildStructure:
                    return $"node={buildStructure.NodeId} building={buildStructure.BuildingType}";
                case MsgRevealNode revealNode:
                    return $"node={revealNode.NodeId}";
                case MsgSetWarZone setWarZone:
                    return $"zone={setWarZone.ZoneId} nodes={setWarZone.NodeIds.Count}";
                case MsgWarZoneDirective directive:
                    return $"zone={directive.ZoneId} directive={directive.Directive}";
                case MsgIssueUnitOrder issueOrder:
                    return $"unit={issueOrder.UnitId} action={issueOrder.Action} node={issueOrder.TargetNodeId}";
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
#endif
