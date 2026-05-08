using System;
using System.Collections.Generic;
using System.Linq;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using Panoptes.Core.Events;
using R3;

namespace Panoptes.Presentation.ViewModels
{
    public sealed class MinisterReportViewModel : IViewModel<MinisterReportState>, IDisposable
    {
        private const string DefaultRole = "domestic";

        private readonly BehaviorSubject<MinisterReportState> _state;
        private readonly Dictionary<string, List<MinisterChatMessageState>> _messagesByRole = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _streamingMessageByRole = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _affectionByRole = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _affectionPulseDeltaByRole = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _affectionPulseSequenceByRole = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _announcedDraftIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _locallyResolvedDraftIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<IDisposable> _subscriptions = new();
        private readonly Random _affectionRandom = new();
        private readonly GameStateCache _gameStateCache;
        private readonly GameStateStore _gameStateStore;
        private readonly MinisterCommandService _ministerCommandService;
        private readonly PlanningDraftStore _planningDraftStore;
        private readonly StaticCatalogCache _staticCatalogCache;
        private int _lastObservedGameTurn = -1;
        private string _affectionGameKey = string.Empty;
        private bool _disposed;
        private MinisterReportState _current;
        private string _activeRole = string.Empty;
        private long _messageSequence;

        public MinisterReportViewModel(PlanningDraftStore planningDraftStore)
            : this(planningDraftStore, null, null, null, null)
        {
        }

        public MinisterReportViewModel(
            PlanningDraftStore planningDraftStore,
            GameStateCache gameStateCache,
            StaticCatalogCache staticCatalogCache,
            MinisterCommandService ministerCommandService,
            GameStateStore gameStateStore = null)
        {
            _planningDraftStore = planningDraftStore ?? throw new ArgumentNullException(nameof(planningDraftStore));
            _gameStateCache = gameStateCache;
            _gameStateStore = gameStateStore;
            _staticCatalogCache = staticCatalogCache;
            _ministerCommandService = ministerCommandService;

            _current = Project();
            _state = new BehaviorSubject<MinisterReportState>(_current);

            _subscriptions.Add(_planningDraftStore.State.Subscribe(this, static (state, self) => self.OnPlanningDraftStateChanged(state)));
            if (_gameStateStore != null)
            {
                _subscriptions.Add(_gameStateStore.State.Subscribe(this, static (state, self) => self.OnGameStateStoreChanged(state)));
            }

            if (_gameStateCache != null)
            {
                _gameStateCache.OnStateChanged += OnGameStateChanged;
                _gameStateCache.OnMinisterChunk += OnMinisterChunk;
                _gameStateCache.OnMinisterMetrics += OnMinisterMetrics;
                _subscriptions.Add(new CleanupSubscription(() =>
                {
                    _gameStateCache.OnStateChanged -= OnGameStateChanged;
                    _gameStateCache.OnMinisterChunk -= OnMinisterChunk;
                    _gameStateCache.OnMinisterMetrics -= OnMinisterMetrics;
                }));
            }

            if (_staticCatalogCache != null)
            {
                _staticCatalogCache.CatalogChanged += OnCatalogChanged;
                _subscriptions.Add(new CleanupSubscription(() => _staticCatalogCache.CatalogChanged -= OnCatalogChanged));
            }

            OnGameStateStoreChanged(_gameStateStore?.Snapshot);
            OnPlanningDraftStateChanged(_planningDraftStore.Snapshot);
            Publish();
        }

        public MinisterReportState Current => _current;
        public Observable<MinisterReportState> State => _state;

        public void SelectMinister(string role)
        {
            var normalized = NormalizeRole(role);
            if (string.Equals(_activeRole, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _activeRole = normalized;
            Publish();
        }

        public void ChooseOption(MinisterReplyOptionState option)
        {
            if (option == null)
            {
                return;
            }

            var role = NormalizeRole(option.MinisterRole);
            if (_ministerCommandService == null)
            {
                if (option.Accept)
                {
                    IncreaseAffection(role);
                }

                MarkRoleDraftsLocallyResolved(role);
                AddPlayerMessage(role, option.PlayerText);
                _activeRole = role;
                Publish();
                return;
            }

            var sent = option.Accept
                ? _ministerCommandService.AcceptRole(role)
                : _ministerCommandService.RejectRole(role);

            if (!sent)
            {
                AddPlayerMessage(role, "Command is currently unavailable.");
                Publish();
                return;
            }

            if (option.Accept)
            {
                IncreaseAffection(role);
            }

            MarkRoleDraftsLocallyResolved(role);
            AddPlayerMessage(role, option.PlayerText);
            _activeRole = role;
            Publish();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            for (var i = 0; i < _subscriptions.Count; i++)
            {
                _subscriptions[i]?.Dispose();
            }

            _subscriptions.Clear();
            _state.Dispose();
        }

        private void OnGameStateChanged()
        {
            Publish();
        }

        private void OnGameStateStoreChanged(GameStateStoreState state)
        {
            var gameKey = (state?.ActiveGameSessionId ?? string.Empty) + "|" + (state?.GameId ?? string.Empty);
            var turn = state?.Turn ?? 0;
            var gameChanged = !string.Equals(_affectionGameKey, gameKey, StringComparison.Ordinal);
            var restarted = turn <= 1 && _lastObservedGameTurn > 1;
            _affectionGameKey = gameKey;
            _lastObservedGameTurn = turn;

            if (!gameChanged && !restarted)
            {
                return;
            }

            _affectionByRole.Clear();
            _affectionPulseDeltaByRole.Clear();
            _affectionPulseSequenceByRole.Clear();
            Publish();
        }

        private void OnCatalogChanged()
        {
            Publish();
        }

        private void OnMinisterMetrics(MinisterMetricsEvent evt)
        {
            if (evt == null || evt.Metrics == null || evt.Metrics.Count == 0)
            {
                return;
            }

            var role = NormalizeRole(evt.MinisterRole);
            var parts = new List<string>();
            for (var i = 0; i < evt.Metrics.Count; i++)
            {
                var metric = evt.Metrics[i];
                if (metric == null || string.IsNullOrWhiteSpace(metric.Label))
                {
                    continue;
                }

                parts.Add(metric.Label + ": " + metric.Value.ToString("0.##"));
            }

            if (parts.Count == 0)
            {
                return;
            }

            AddMinisterMessage(role, string.Join("\n", parts), false);
            Publish();
        }

        private void OnMinisterChunk(MinisterChunkEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            var role = NormalizeRole(evt.MinisterRole);
            if (!_streamingMessageByRole.TryGetValue(role, out var messageId) ||
                !TryFindMessage(role, messageId, out var message))
            {
                message = AddMinisterMessage(role, string.Empty, true);
                messageId = message.Id;
                _streamingMessageByRole[role] = messageId;
            }

            if (!string.IsNullOrEmpty(evt.Chunk))
            {
                message.Text += evt.Chunk;
            }

            if (evt.IsFinal)
            {
                message.IsStreaming = false;
                _streamingMessageByRole.Remove(role);
            }

            if (string.IsNullOrWhiteSpace(_activeRole))
            {
                _activeRole = role;
            }

            Publish();
        }

        private void OnPlanningDraftStateChanged(PlanningDraftState state)
        {
            if (state?.MinisterDrafts == null)
            {
                Publish();
                return;
            }

            for (var i = 0; i < state.MinisterDrafts.Count; i++)
            {
                var draft = state.MinisterDrafts[i];
                if (draft == null || string.IsNullOrWhiteSpace(draft.DraftId) || _announcedDraftIds.Contains(draft.DraftId))
                {
                    continue;
                }

                _announcedDraftIds.Add(draft.DraftId);
                AddMinisterMessage(NormalizeRole(draft.MinisterRole), BuildDraftMessage(draft), false);
            }

            Publish();
        }

        private MinisterReportState Project()
        {
            var tabs = BuildTabs();
            var activeRole = ResolveActiveRole(tabs);
            var messages = _messagesByRole.TryGetValue(activeRole, out var roleMessages)
                ? roleMessages.Select(CloneMessage).ToList()
                : new List<MinisterChatMessageState>();
            var options = BuildOptions(activeRole);

            return new MinisterReportState(
                "大臣汇报",
                activeRole,
                tabs,
                messages,
                options,
                BuildLegacyGroups());
        }

        private List<MinisterTabState> BuildTabs()
        {
            var roles = new Dictionary<string, (string Name, string Title, string IconResource)>(StringComparer.OrdinalIgnoreCase);
            var ministers = _staticCatalogCache?.Ministers;
            if (ministers != null)
            {
                for (var i = 0; i < ministers.Count; i++)
                {
                    var minister = ministers[i];
                    if (minister == null)
                    {
                        continue;
                    }

                    var role = NormalizeRole(minister.role);
                    roles[role] = (
                        Clean(minister.name, RoleTitle(role)),
                        RoleTitle(role),
                        IconResourceFor(minister.icon_key, role));
                }
            }

            var drafts = _planningDraftStore.Snapshot?.MinisterDrafts;
            if (drafts != null)
            {
                for (var i = 0; i < drafts.Count; i++)
                {
                    var role = NormalizeRole(drafts[i]?.MinisterRole);
                    if (!roles.ContainsKey(role))
                    {
                        roles[role] = (RoleTitle(role), RoleTitle(role), IconResourceFor(string.Empty, role));
                    }
                }
            }

            foreach (var pair in _messagesByRole)
            {
                var role = NormalizeRole(pair.Key);
                if (!roles.ContainsKey(role))
                {
                    roles[role] = (RoleTitle(role), RoleTitle(role), IconResourceFor(string.Empty, role));
                }
            }

            if (roles.Count == 0)
            {
                roles[DefaultRole] = (RoleTitle(DefaultRole), RoleTitle(DefaultRole), IconResourceFor(string.Empty, DefaultRole));
            }

            return roles
                .OrderBy(pair => RoleSortKey(pair.Key), StringComparer.OrdinalIgnoreCase)
                .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => new MinisterTabState(
                    pair.Key,
                    pair.Value.Name,
                    pair.Value.Title,
                    pair.Value.IconResource,
                    AvatarText(pair.Value.Name, pair.Key),
                    string.Equals(pair.Key, _activeRole, StringComparison.OrdinalIgnoreCase),
                    AffectionFor(pair.Key),
                    AffectionPulseSequenceFor(pair.Key),
                    AffectionPulseDeltaFor(pair.Key)))
                .ToList();
        }

        private string ResolveActiveRole(IReadOnlyList<MinisterTabState> tabs)
        {
            if (!string.IsNullOrWhiteSpace(_activeRole) &&
                tabs.Any(tab => string.Equals(tab.Role, _activeRole, StringComparison.OrdinalIgnoreCase)))
            {
                return _activeRole;
            }

            _activeRole = tabs.Count > 0 ? tabs[0].Role : DefaultRole;
            return _activeRole;
        }

        private List<MinisterReplyOptionState> BuildOptions(string activeRole)
        {
            var drafts = _planningDraftStore.Snapshot?.MinisterDrafts;
            if (drafts == null || drafts.Count == 0)
            {
                return new List<MinisterReplyOptionState>();
            }

            return BuildRoleBatchOptions(activeRole, drafts);
        }

        private List<MinisterReplyOptionState> BuildRoleBatchOptions(string activeRole, IReadOnlyList<MinisterDraftDto> drafts)
        {
            var pendingCount = 0;
            for (var i = 0; i < drafts.Count; i++)
            {
                var draft = drafts[i];
                if (draft == null ||
                    !draft.IsInteractive ||
                    _locallyResolvedDraftIds.Contains(draft.DraftId) ||
                    !string.Equals(NormalizeRole(draft.MinisterRole), activeRole, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                pendingCount++;
            }

            if (pendingCount == 0)
            {
                return new List<MinisterReplyOptionState>();
            }

            var roleTitle = RoleTitle(activeRole);
            return new List<MinisterReplyOptionState>
            {
                new MinisterReplyOptionState(
                    activeRole + ":accept-role",
                    string.Empty,
                    activeRole,
                    "采纳全部",
                    "采纳" + roleTitle + "的全部建议。",
                    true),
                new MinisterReplyOptionState(
                    activeRole + ":reject-role",
                    string.Empty,
                    activeRole,
                    "暂不采纳",
                    "暂不采纳" + roleTitle + "的建议。",
                    false)
            };
        }

        private void MarkRoleDraftsLocallyResolved(string role)
        {
            var drafts = _planningDraftStore.Snapshot?.MinisterDrafts;
            if (drafts == null)
            {
                return;
            }

            role = NormalizeRole(role);
            for (var i = 0; i < drafts.Count; i++)
            {
                var draft = drafts[i];
                if (draft == null ||
                    string.IsNullOrWhiteSpace(draft.DraftId) ||
                    !draft.IsInteractive ||
                    !string.Equals(NormalizeRole(draft.MinisterRole), role, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                _locallyResolvedDraftIds.Add(draft.DraftId);
            }
        }

        private void IncreaseAffection(string role)
        {
            role = NormalizeRole(role);
            var delta = _affectionRandom.Next(5, 11);
            var current = AffectionFor(role);
            var next = Math.Min(100, current + delta);
            _affectionByRole[role] = next;
            _affectionPulseDeltaByRole[role] = next - current;
            _affectionPulseSequenceByRole.TryGetValue(role, out var sequence);
            _affectionPulseSequenceByRole[role] = sequence + 1;
        }

        private int AffectionFor(string role)
        {
            return _affectionByRole.TryGetValue(NormalizeRole(role), out var value) ? Math.Clamp(value, 0, 100) : 0;
        }

        private int AffectionPulseSequenceFor(string role)
        {
            return _affectionPulseSequenceByRole.TryGetValue(NormalizeRole(role), out var value) ? value : 0;
        }

        private int AffectionPulseDeltaFor(string role)
        {
            return _affectionPulseDeltaByRole.TryGetValue(NormalizeRole(role), out var value) ? value : 0;
        }

        private IReadOnlyList<ManagementPanelGroupState> BuildLegacyGroups()
        {
            var drafts = _planningDraftStore.Snapshot?.MinisterDrafts;
            if (drafts == null || drafts.Count == 0)
            {
                return Array.Empty<ManagementPanelGroupState>();
            }

            var groups = new Dictionary<string, List<ManagementPanelRowState>>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < drafts.Count; i++)
            {
                var draft = drafts[i];
                if (draft == null)
                {
                    continue;
                }

                var role = NormalizeRole(draft.MinisterRole);
                if (!groups.TryGetValue(role, out var rows))
                {
                    rows = new List<ManagementPanelRowState>();
                    groups[role] = rows;
                }

                rows.Add(new ManagementPanelRowState(
                    draft.DraftId,
                    draft.Title,
                    draft.Summary,
                    BuildDraftDetail(draft),
                    draft.DisplayStatus,
                    draft.IsInteractive ? "Review" : string.Empty));
            }

            return groups
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => new ManagementPanelGroupState(pair.Key, RoleTitle(pair.Key), pair.Value))
                .ToList();
        }

        private MinisterChatMessageState AddMinisterMessage(string role, string text, bool streaming)
        {
            role = NormalizeRole(role);
            var roster = BuildTabs().FirstOrDefault(tab => string.Equals(tab.Role, role, StringComparison.OrdinalIgnoreCase));
            var message = new MinisterChatMessageState(
                NextMessageId(),
                role,
                roster?.Name ?? RoleTitle(role),
                roster?.Title ?? RoleTitle(role),
                roster?.IconResource ?? IconResourceFor(string.Empty, role),
                roster?.AvatarText ?? AvatarText(string.Empty, role),
                text ?? string.Empty,
                false,
                streaming);
            MessagesFor(role).Add(message);
            return message;
        }

        private void AddPlayerMessage(string role, string text)
        {
            role = NormalizeRole(role);
            MessagesFor(role).Add(new MinisterChatMessageState(
                NextMessageId(),
                role,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                text ?? string.Empty,
                true,
                false));
        }

        private List<MinisterChatMessageState> MessagesFor(string role)
        {
            role = NormalizeRole(role);
            if (!_messagesByRole.TryGetValue(role, out var messages))
            {
                messages = new List<MinisterChatMessageState>();
                _messagesByRole[role] = messages;
            }

            return messages;
        }

        private bool TryFindMessage(string role, string messageId, out MinisterChatMessageState message)
        {
            message = null;
            if (!_messagesByRole.TryGetValue(NormalizeRole(role), out var messages))
            {
                return false;
            }

            for (var i = 0; i < messages.Count; i++)
            {
                if (string.Equals(messages[i].Id, messageId, StringComparison.Ordinal))
                {
                    message = messages[i];
                    return true;
                }
            }

            return false;
        }

        private void Publish()
        {
            if (_disposed)
            {
                return;
            }

            _current = Project();
            _state.OnNext(_current);
        }

        private string NextMessageId()
        {
            _messageSequence++;
            return "minister-message-" + _messageSequence;
        }

        private static MinisterChatMessageState CloneMessage(MinisterChatMessageState message)
        {
            return new MinisterChatMessageState(
                message.Id,
                message.MinisterRole,
                message.MinisterName,
                message.MinisterTitle,
                message.IconResource,
                message.AvatarText,
                message.Text,
                message.IsPlayer,
                message.IsStreaming);
        }

        private static string BuildDraftMessage(MinisterDraftDto draft)
        {
            var lines = new List<string>();
            AddLine(lines, draft.Title);
            AddLine(lines, draft.Summary);
            AddLine(lines, draft.Rationale);
            AddLine(lines, BuildOperationSummary(draft));
            AddLine(lines, draft.RiskNote);
            return lines.Count > 0 ? string.Join("\n", lines) : "有一条新的建议等待定夺。";
        }

        private static string BuildDraftDetail(MinisterDraftDto draft)
        {
            var lines = new List<string>();
            AddLine(lines, draft.Rationale);
            AddLine(lines, BuildOperationSummary(draft));
            return string.Join("\n", lines);
        }

        private static string BuildOperationSummary(MinisterDraftDto draft)
        {
            if (draft == null || !string.Equals(draft.Kind, "operation", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            var lines = new List<string>();
            AddLine(lines, string.IsNullOrWhiteSpace(draft.Objective) ? string.Empty : "目标：" + draft.Objective.Trim());
            var commands = draft.OperationCommands;
            if (commands == null || commands.Count == 0)
            {
                return string.Join("\n", lines);
            }

            lines.Add("行动批次：");
            for (var i = 0; i < commands.Count; i++)
            {
                var command = commands[i];
                if (command == null)
                {
                    continue;
                }

                var summary = BuildOperationCommandSummary(command);
                if (!string.IsNullOrWhiteSpace(summary))
                {
                    lines.Add((i + 1).ToString() + ". " + summary);
                }
            }

            return string.Join("\n", lines);
        }

        private static string BuildOperationCommandSummary(MinisterOperationCommandDto command)
        {
            var label = Clean(command.Label, string.Empty);
            if (!string.IsNullOrWhiteSpace(label))
            {
                return label;
            }

            var kind = Clean(command.Kind, string.Empty).ToLowerInvariant();
            return kind switch
            {
                "build" => JoinParts("建造", command.BuildingTypeId, command.NodeId),
                "recipe" => JoinParts("调整生产", command.RecipeId, command.NodeId),
                "unit_order" => JoinParts("调动部队", command.UnitId, command.Action, command.TargetNodeId, command.TargetUnitId),
                "research" => JoinParts("研究", command.Label),
                "policy" => JoinParts("国策", command.Label),
                "institution" => JoinParts("制度", command.Label),
                _ => JoinParts(kind, command.UnitId, command.Action, command.TargetNodeId, command.NodeId)
            };
        }

        private static string JoinParts(params string[] parts)
        {
            return string.Join(" ",
                parts.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part.Trim()));
        }

        private static void AddLine(List<string> lines, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                lines.Add(value.Trim());
            }
        }

        private static string NormalizeRole(string role)
        {
            return string.IsNullOrWhiteSpace(role) ? DefaultRole : role.Trim().ToLowerInvariant();
        }

        private static string RoleTitle(string role)
        {
            return NormalizeRole(role) switch
            {
                "military" => "军事大臣",
                "finance" => "财政大臣",
                "agriculture" => "农政大臣",
                "domestic" => "内政大臣",
                _ => Clean(role, "Minister")
            };
        }

        private static string IconResourceFor(string iconKey, string role)
        {
            var key = Clean(iconKey, NormalizeRole(role));
            return string.IsNullOrWhiteSpace(key) ? string.Empty : "Icons/Ministers/" + key.Trim();
        }

        private static string RoleSortKey(string role)
        {
            return NormalizeRole(role) switch
            {
                "domestic" => "00",
                "military" => "01",
                "finance" => "02",
                "agriculture" => "03",
                _ => "99-" + role
            };
        }

        private static string AvatarText(string name, string role)
        {
            var source = Clean(name, role);
            return string.IsNullOrEmpty(source) ? "M" : source.Substring(0, 1).ToUpperInvariant();
        }

        private static string Clean(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? (fallback ?? string.Empty) : value.Trim();
        }

        private sealed class CleanupSubscription : IDisposable
        {
            private Action _cleanup;

            public CleanupSubscription(Action cleanup)
            {
                _cleanup = cleanup;
            }

            public void Dispose()
            {
                var cleanup = _cleanup;
                _cleanup = null;
                cleanup?.Invoke();
            }
        }
    }

    public sealed class MinisterReportState
    {
        public MinisterReportState(
            string title,
            string activeRole,
            IReadOnlyList<MinisterTabState> ministers,
            IReadOnlyList<MinisterChatMessageState> messages,
            IReadOnlyList<MinisterReplyOptionState> options,
            IReadOnlyList<ManagementPanelGroupState> groups = null)
        {
            Title = string.IsNullOrWhiteSpace(title) ? "大臣汇报" : title.Trim();
            ActiveRole = activeRole ?? string.Empty;
            Groups = groups != null ? new List<ManagementPanelGroupState>(groups) : new List<ManagementPanelGroupState>();
            Ministers = ministers != null ? new List<MinisterTabState>(ministers) : new List<MinisterTabState>();
            Messages = messages != null ? new List<MinisterChatMessageState>(messages) : new List<MinisterChatMessageState>();
            Options = options != null ? new List<MinisterReplyOptionState>(options) : new List<MinisterReplyOptionState>();
        }

        public string ActiveRole { get; }
        public IReadOnlyList<ManagementPanelGroupState> Groups { get; }
        public bool HasMessages => Messages.Count > 0;
        public bool HasMinisters => Ministers.Count > 0;
        public IReadOnlyList<MinisterChatMessageState> Messages { get; }
        public IReadOnlyList<MinisterTabState> Ministers { get; }
        public IReadOnlyList<MinisterReplyOptionState> Options { get; }
        public string Title { get; }
    }

    public sealed class MinisterTabState
    {
        public MinisterTabState(
            string role,
            string name,
            string title,
            string iconResource,
            string avatarText,
            bool selected,
            int affection = 0,
            int affectionPulseSequence = 0,
            int affectionPulseDelta = 0)
        {
            Affection = Math.Clamp(affection, 0, 100);
            AffectionPulseDelta = affectionPulseDelta;
            AffectionPulseSequence = Math.Max(0, affectionPulseSequence);
            AvatarText = avatarText ?? string.Empty;
            IconResource = iconResource ?? string.Empty;
            IsSelected = selected;
            Name = name ?? string.Empty;
            Role = role ?? string.Empty;
            Title = title ?? string.Empty;
        }

        public int Affection { get; }
        public int AffectionPulseDelta { get; }
        public int AffectionPulseSequence { get; }
        public string AvatarText { get; }
        public string IconResource { get; }
        public bool IsSelected { get; }
        public string Name { get; }
        public string Role { get; }
        public string Title { get; }
    }

    public sealed class MinisterChatMessageState
    {
        public MinisterChatMessageState(
            string id,
            string ministerRole,
            string ministerName,
            string ministerTitle,
            string iconResource,
            string avatarText,
            string text,
            bool isPlayer,
            bool isStreaming)
        {
            AvatarText = avatarText ?? string.Empty;
            Id = id ?? string.Empty;
            IconResource = iconResource ?? string.Empty;
            IsPlayer = isPlayer;
            IsStreaming = isStreaming;
            MinisterName = ministerName ?? string.Empty;
            MinisterRole = ministerRole ?? string.Empty;
            MinisterTitle = ministerTitle ?? string.Empty;
            Text = text ?? string.Empty;
        }

        public string AvatarText { get; }
        public string IconResource { get; }
        public string Id { get; }
        public bool IsPlayer { get; }
        public bool IsStreaming { get; set; }
        public string MinisterName { get; }
        public string MinisterRole { get; }
        public string MinisterTitle { get; }
        public string Text { get; set; }
    }

    public sealed class MinisterReplyOptionState
    {
        public MinisterReplyOptionState(
            string id,
            string draftId,
            string ministerRole,
            string label,
            string playerText,
            bool accept)
        {
            Accept = accept;
            DraftId = draftId ?? string.Empty;
            Id = id ?? string.Empty;
            Label = label ?? string.Empty;
            MinisterRole = ministerRole ?? string.Empty;
            PlayerText = playerText ?? string.Empty;
        }

        public bool Accept { get; }
        public string DraftId { get; }
        public string Id { get; }
        public string Label { get; }
        public string MinisterRole { get; }
        public string PlayerText { get; }
    }
}
