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

        private void OnDispatching(MessageDispatcher.DispatchEntry entry)
        {
            if (entry.Message == null)
            {
                return;
            }

            AddEntry(
                "IN",
                entry.MessageType,
                BuildIncomingSummary(entry),
                entry.PayloadJson,
                DebugMessageRegistry.SupportsMessageType(entry.MessageType),
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

        private static string BuildIncomingSummary(MessageDispatcher.DispatchEntry entry)
        {
            switch (entry.Message)
            {
                case MsgPlanningStart planningStart:
                    return $"turn={planningStart.Turn} timeout={planningStart.Timeout} tokens={planningStart.Tokens}";
                case MsgPlanningSnapshot planningSnapshot:
                    return $"turn={planningSnapshot.Turn} unit_orders={planningSnapshot.UnitOrders.Count}";
                case MsgPlanningPathPreviewResponse preview:
                    return $"request={preview.RequestId} valid={preview.Valid} path_nodes={preview.PathNodeIds.Count}";
                case MsgTokenResult tokenResult:
                    return $"success={tokenResult.Success} tokens_left={tokenResult.TokensLeft} error={tokenResult.ErrorCode}";
                case MsgRevealResult reveal:
                    var owner = reveal.TrueState != null ? reveal.TrueState.Owner : string.Empty;
                    var buildingType = reveal.TrueState != null ? reveal.TrueState.BuildingType : string.Empty;
                    return $"node={reveal.NodeId} owner={owner} building={buildingType}";
                case MsgResearchResult research:
                    return $"success={research.Success} tech={research.TechnologyId} error={research.ErrorCode}";
                case MsgSetBuildingRecipeResult recipe:
                    return $"success={recipe.Success} node={recipe.NodeId} recipe={recipe.RecipeId}";
                case MsgMinisterReportChunk reportChunk:
                    var chunkLen = reportChunk.Chunk == null ? 0 : reportChunk.Chunk.Length;
                    return $"role={reportChunk.MinisterRole} chunk_len={chunkLen} final={reportChunk.IsFinal}";
                case MsgMinisterMetrics metrics:
                    return $"role={metrics.MinisterRole} metrics_count={metrics.Metrics.Count}";
                case MsgTurnReport turnReport:
                    return $"turn={turnReport.Turn} summary_len={(turnReport.Summary ?? string.Empty).Length}";
                case MsgTurnSettlement turnSettlement:
                    return $"turn={turnSettlement.Turn} sections={turnSettlement.Sections.Count} next_phase={turnSettlement.NextPhase}";
                case MsgGameOver gameOver:
                    return $"winner={gameOver.WinnerId} reason={gameOver.Reason}";
                case Problem problem:
                    return $"code={problem.Code} message={problem.Message}";
            }

            return entry.MessageType;
        }

        private static string BuildOutgoingSummary(string msgType, IMessage message)
        {
            return message switch
            {
                MsgSetPolicy setPolicy => $"policy={setPolicy.Policy}",
                MsgBuildStructure buildStructure =>
                    $"node={buildStructure.NodeId} building={buildStructure.BuildingType}",
                MsgRevealNode revealNode => $"node={revealNode.NodeId}",
                MsgSetWarZone setWarZone => $"zone={setWarZone.ZoneId} nodes={setWarZone.NodeIds.Count}",
                MsgWarZoneDirective warZoneDirective =>
                    $"zone={warZoneDirective.ZoneId} directive={warZoneDirective.Directive}",
                MsgIssueUnitOrder issueUnitOrder => $"unit={issueUnitOrder.UnitId} action={issueUnitOrder.Action}",
                MsgPlanningPathPreviewRequest planningPreview =>
                    $"request={planningPreview.RequestId} unit={planningPreview.UnitId} action={planningPreview.Action}",
                MsgSetMinisterDirective setMinisterDirective => $"minister={setMinisterDirective.MinisterRole}",
                _ => msgType
            };
        }
    }
}
#endif
