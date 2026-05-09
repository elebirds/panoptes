using System;
using System.Collections.Generic;
using System.Linq;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using Panoptes.Core.Events;
using R3;
using UnityEngine;
using VContainer;

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
        private readonly HashSet<string> _locallyActivatedSkillKeys = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<IDisposable> _subscriptions = new();
        private readonly System.Random _affectionRandom = new();
        private readonly GameStateStore _gameStateStore;
        private readonly MinisterCommandService _ministerCommandService;
        private readonly PlanningDraftStore _planningDraftStore;
        private GameStateCache _gameStateCache;
        private StaticCatalogCache _staticCatalogCache;
        private int _lastObservedGameTurn = -1;
        private string _affectionGameKey = string.Empty;
        private bool _gameStateCacheSubscribed;
        private bool _staticCatalogCacheSubscribed;
        private bool _disposed;
        private MinisterReportState _current;
        private string _activeRole = string.Empty;
        private long _messageSequence;
        private static Dictionary<string, MinisterTabSource> s_resourceMinisterProfiles;

        public MinisterReportViewModel(PlanningDraftStore planningDraftStore)
            : this(planningDraftStore, null, null, null, null)
        {
        }

        [Inject]
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
            EnsureRuntimeSourceSubscriptions();

            _current = Project();
            _state = new BehaviorSubject<MinisterReportState>(_current);

            _subscriptions.Add(_planningDraftStore.State.Subscribe(this, static (state, self) => self.OnPlanningDraftStateChanged(state)));
            if (_gameStateStore != null)
            {
                _subscriptions.Add(_gameStateStore.State.Subscribe(this, static (state, self) => self.OnGameStateStoreChanged(state)));
            }

            OnGameStateStoreChanged(_gameStateStore?.Snapshot);
            OnPlanningDraftStateChanged(_planningDraftStore.Snapshot);
            Publish();
        }

        public MinisterReportState Current => _current;
        public Observable<MinisterReportState> State => _state;

        public void RefreshFromRuntimeSources()
        {
            EnsureRuntimeSourceSubscriptions();
            Publish();
        }

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

        public void ChooseSkill(MinisterSkillCardState skill)
        {
            if (skill == null || string.IsNullOrWhiteSpace(skill.Id))
            {
                return;
            }

            var role = NormalizeRole(skill.MinisterRole);
            if (_ministerCommandService != null &&
                !_ministerCommandService.ActivateSkill(role, skill.Id))
            {
                AddPlayerMessage(role, "技能暂时无法释放。");
                Publish();
                return;
            }

            _locallyActivatedSkillKeys.Add(SkillKey(role, skill.Id));
            AddPlayerMessage(role, "释放技能：" + Clean(skill.Name, skill.Id));
            _activeRole = role;
            Publish();
        }

        public void FireMinister(string role)
        {
            role = NormalizeRole(role);
            if (string.IsNullOrWhiteSpace(role))
            {
                return;
            }

            if (_ministerCommandService != null && !_ministerCommandService.FireMinister(role))
            {
                AddPlayerMessage(role, "辞退指令暂时无法发送。");
                Publish();
                return;
            }

            _activeRole = role;
            Publish();
        }

        public void HireMinister(string role, string candidateId)
        {
            SendMinisterRosterAction(role, candidateId, false);
        }

        public void ReplaceMinister(string role, string candidateId)
        {
            SendMinisterRosterAction(role, candidateId, true);
        }

        public void RefreshMinisterCandidates(string role = null)
        {
            role = NormalizeRole(role);
            if (_ministerCommandService != null && !_ministerCommandService.RefreshCandidates(role))
            {
                AddPlayerMessage(role, "候选刷新暂时无法发送。");
                Publish();
                return;
            }

            _activeRole = role;
            Publish();
        }

        private void SendMinisterRosterAction(string role, string candidateId, bool replace)
        {
            role = NormalizeRole(role);
            candidateId = candidateId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(candidateId))
            {
                return;
            }

            var sent = _ministerCommandService == null
                ? false
                : (replace
                    ? _ministerCommandService.ReplaceMinister(role, candidateId)
                    : _ministerCommandService.HireMinister(role, candidateId));
            if (!sent)
            {
                AddPlayerMessage(role, replace ? "替换指令暂时无法发送。" : "雇佣指令暂时无法发送。");
                Publish();
                return;
            }

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

        private void EnsureRuntimeSourceSubscriptions()
        {
            if (_gameStateCache == null)
            {
                _gameStateCache = GameStateCache.Instance;
            }

            if (_gameStateCache != null && !_gameStateCacheSubscribed)
            {
                var cache = _gameStateCache;
                cache.OnStateChanged += OnGameStateChanged;
                cache.OnMinisterChunk += OnMinisterChunk;
                cache.OnMinisterMetrics += OnMinisterMetrics;
                _gameStateCacheSubscribed = true;
                _subscriptions.Add(new CleanupSubscription(() =>
                {
                    cache.OnStateChanged -= OnGameStateChanged;
                    cache.OnMinisterChunk -= OnMinisterChunk;
                    cache.OnMinisterMetrics -= OnMinisterMetrics;
                }));
            }

            if (_staticCatalogCache == null)
            {
                _staticCatalogCache = StaticCatalogCache.Instance;
            }

            if (_staticCatalogCache != null && !_staticCatalogCacheSubscribed)
            {
                var cache = _staticCatalogCache;
                cache.CatalogChanged += OnCatalogChanged;
                _staticCatalogCacheSubscribed = true;
                _subscriptions.Add(new CleanupSubscription(() => cache.CatalogChanged -= OnCatalogChanged));
            }
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
            EnsureRuntimeSourceSubscriptions();
            var tabs = BuildTabs();
            var candidates = BuildCandidates();
            var activeRole = ResolveActiveRole(tabs);
            var messages = _messagesByRole.TryGetValue(activeRole, out var roleMessages)
                ? roleMessages.Select(CloneMessage).ToList()
                : new List<MinisterChatMessageState>();
            var options = BuildOptions(activeRole);
            var skills = BuildSkills(activeRole);

            return new MinisterReportState(
                "大臣汇报",
                activeRole,
                tabs,
                candidates,
                messages,
                options,
                skills,
                BuildLegacyGroups());
        }

        private List<MinisterTabState> BuildTabs()
        {
            var roles = new Dictionary<string, MinisterTabSource>(StringComparer.OrdinalIgnoreCase);
            var catalogMinisters = _staticCatalogCache?.Ministers;
            if (catalogMinisters != null)
            {
                for (var i = 0; i < catalogMinisters.Count; i++)
                {
                    var minister = catalogMinisters[i];
                    if (minister == null)
                    {
                        continue;
                    }

                    var role = NormalizeRole(minister.role);
                    roles[role] = MinisterTabSource.FromCatalog(role, minister);
                }
            }

            foreach (var pair in LoadResourceMinisterProfiles())
            {
                if (!roles.ContainsKey(pair.Key))
                {
                    roles[pair.Key] = pair.Value;
                }
                else if (!HasAttributes(roles[pair.Key].Attributes))
                {
                    roles[pair.Key] = pair.Value.WithNameIconFallback(roles[pair.Key]);
                }
            }

            var roster = _gameStateCache?.Ministers;
            if (roster != null)
            {
                for (var i = 0; i < roster.Count; i++)
                {
                    var minister = roster[i];
                    if (minister == null)
                    {
                        continue;
                    }

                    var role = NormalizeRole(minister.Role);
                    if (roles.TryGetValue(role, out var existing))
                    {
                        roles[role] = existing.WithProfile(minister);
                    }
                    else
                    {
                        roles[role] = MinisterTabSource.FromProfile(role, minister, IconResourceFor(string.Empty, role));
                    }
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
                        roles[role] = ResourceOrEmpty(role);
                    }
                }
            }

            foreach (var pair in _messagesByRole)
            {
                var role = NormalizeRole(pair.Key);
                if (!roles.ContainsKey(role))
                {
                    roles[role] = ResourceOrEmpty(role);
                }
            }

            if (roles.Count == 0)
            {
                roles[DefaultRole] = ResourceOrEmpty(DefaultRole);
            }

            return roles
                .OrderBy(pair => RoleSortKey(pair.Key), StringComparer.OrdinalIgnoreCase)
                .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => new MinisterTabState(
                    pair.Key,
                    pair.Value.MinisterId,
                    pair.Value.Name,
                    pair.Value.Title,
                    pair.Value.IconResource,
                    AvatarText(pair.Value.Name, pair.Key),
                    string.Equals(pair.Key, _activeRole, StringComparison.OrdinalIgnoreCase),
                    pair.Value.IsVacant,
                    pair.Value.IsVacant,
                    AffectionFor(pair.Key),
                    AffectionPulseSequenceFor(pair.Key),
                    AffectionPulseDeltaFor(pair.Key),
                    pair.Value.Attributes,
                    pair.Value.PersonalityText))
                .ToList();
        }

        private List<MinisterTabState> BuildCandidates()
        {
            var candidates = _gameStateCache?.MinisterCandidates;
            if (candidates == null || candidates.Count == 0)
            {
                return new List<MinisterTabState>();
            }

            var roster = new Dictionary<string, MinisterProfileDto>(StringComparer.OrdinalIgnoreCase);
            var ministers = _gameStateCache?.Ministers;
            if (ministers != null)
            {
                for (var i = 0; i < ministers.Count; i++)
                {
                    var minister = ministers[i];
                    if (minister == null || string.IsNullOrWhiteSpace(minister.Role))
                    {
                        continue;
                    }

                    roster[NormalizeRole(minister.Role)] = minister;
                }
            }

            return candidates
                .Where(candidate => candidate != null && !string.IsNullOrWhiteSpace(candidate.Role))
                .OrderBy(candidate => RoleSortKey(candidate.Role), StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.Role, StringComparer.OrdinalIgnoreCase)
                .Select(candidate =>
                {
                    var role = NormalizeRole(candidate.Role);
                    roster.TryGetValue(role, out var currentMinister);
                    var source = MinisterTabSource.FromProfile(role, candidate, IconResourceFor(candidate.IconKey, role));
                    var roleVacant = currentMinister == null || currentMinister.IsVacant;
                    return new MinisterTabState(
                        role,
                        source.MinisterId,
                        source.Name,
                        source.Title,
                        source.IconResource,
                        AvatarText(source.Name, role),
                        false,
                        false,
                        roleVacant,
                        AffectionFor(role),
                        AffectionPulseSequenceFor(role),
                        AffectionPulseDeltaFor(role),
                        source.Attributes,
                        source.PersonalityText);
                })
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

        private List<MinisterSkillCardState> BuildSkills(string activeRole)
        {
            activeRole = NormalizeRole(activeRole);
            var cards = _staticCatalogCache?.MinisterSkillCards;
            if (cards == null || cards.Count == 0)
            {
                return new List<MinisterSkillCardState>();
            }

            var result = new List<MinisterSkillCardState>();
            for (var i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (card == null || string.IsNullOrWhiteSpace(card.id) || !RoleMatches(card.role_tags, activeRole))
                {
                    continue;
                }

                var cardId = card.id.Trim();
                result.Add(new MinisterSkillCardState(
                    cardId,
                    activeRole,
                    Clean(card.name, cardId),
                    Clean(card.description, "暂无技能说明。"),
                    BuildSkillTiming(card.delay_turns, card.duration_turns),
                    !_locallyActivatedSkillKeys.Contains(SkillKey(activeRole, cardId))));
            }

            return result
                .OrderBy(skill => skill.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(skill => skill.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
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

        private static bool RoleMatches(string[] roleTags, string role)
        {
            if (roleTags == null || roleTags.Length == 0)
            {
                return false;
            }

            role = NormalizeRole(role);
            for (var i = 0; i < roleTags.Length; i++)
            {
                if (string.Equals(NormalizeRole(roleTags[i]), role, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildSkillTiming(int delayTurns, int durationTurns)
        {
            var timing = delayTurns <= 0 ? "本回合生效" : delayTurns == 1 ? "下一回合生效" : delayTurns + " 回合后生效";
            var duration = durationTurns <= 1 ? "持续 1 回合" : "持续 " + durationTurns + " 回合";
            return timing + "，" + duration;
        }

        private static string SkillKey(string role, string skillId)
        {
            return NormalizeRole(role) + ":" + (skillId ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string RoleTitle(string role)
        {
            return NormalizeRole(role) switch
            {
                "works" => "工务大臣",
                "defense" => "军备大臣",
                "command" => "军令大臣",
                "frontier" => "边务大臣",
                "military" => "军务大臣",
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

        private static string ProfileIconResourceFor(MinisterProfileDto minister, string role, string iconResource)
        {
            if (minister == null)
            {
                return Clean(iconResource, IconResourceFor(string.Empty, role));
            }

            var normalizedRole = NormalizeRole(role);
            var rawIconKey = minister.IconKey ?? string.Empty;
            if (IsGeneratedMinister(minister) && IconKeyFallsBackToRole(rawIconKey, normalizedRole))
            {
                return IconResourceFor(CandidateAvatarKey(minister.MinisterId), normalizedRole);
            }

            return Clean(iconResource, IconResourceFor(rawIconKey, normalizedRole));
        }

        private static bool IsGeneratedMinister(MinisterProfileDto minister)
        {
            return minister != null &&
                   !string.IsNullOrWhiteSpace(minister.MinisterId) &&
                   minister.MinisterId.Trim().StartsWith("cand:", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IconKeyFallsBackToRole(string iconKey, string role)
        {
            var key = Clean(iconKey, role).Trim();
            return string.IsNullOrWhiteSpace(key) ||
                   string.Equals(key, role, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(key, "military", StringComparison.OrdinalIgnoreCase) && string.Equals(role, "command", StringComparison.OrdinalIgnoreCase);
        }

        private static string CandidateAvatarKey(string ministerId)
        {
            unchecked
            {
                var hash = 2166136261u;
                var source = ministerId ?? string.Empty;
                for (var i = 0; i < source.Length; i++)
                {
                    hash ^= source[i];
                    hash *= 16777619u;
                }

                return "candidate_" + ((hash % 10u) + 1u).ToString("00");
            }
        }

        private static string RoleSortKey(string role)
        {
            return NormalizeRole(role) switch
            {
                "domestic" => "00",
                "works" => "01",
                "defense" => "02",
                "command" => "03",
                "frontier" => "04",
                "military" => "05",
                "finance" => "06",
                "agriculture" => "07",
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

        private static string BuildPersonalityText(string description, string key, string fallback = "")
        {
            if (!string.IsNullOrWhiteSpace(description))
            {
                return description.Trim();
            }

            if (!string.IsNullOrWhiteSpace(key))
            {
                return key.Trim();
            }

            return fallback ?? string.Empty;
        }

        private static IReadOnlyList<MinisterAttributeState> BuildAttributes(
            int ability,
            int loyalty,
            int ambition,
            int cautiousness,
            int decisiveness,
            int loyaltyTendency,
            int ambitionStyle)
        {
            return new[]
            {
                new MinisterAttributeState("ability", "能力", ability),
                new MinisterAttributeState("loyalty", "忠诚", loyalty),
                new MinisterAttributeState("ambition", "野心", ambition),
                new MinisterAttributeState("cautiousness", "谨慎", cautiousness),
                new MinisterAttributeState("decisiveness", "果断", decisiveness),
                new MinisterAttributeState("loyalty_tendency", "忠诚倾向", loyaltyTendency),
                new MinisterAttributeState("ambition_style", "野心表现", ambitionStyle)
            };
        }

        private static bool HasAttributes(IReadOnlyList<MinisterAttributeState> attributes)
        {
            return attributes != null && attributes.Count > 0;
        }

        private static bool HasAuthoritativeProfileAttributes(MinisterProfileDto minister)
        {
            return minister != null &&
                   (minister.Loyalty != 0 ||
                    minister.Ambition != 0 ||
                    minister.Cautiousness != 0 ||
                    minister.Decisiveness != 0 ||
                    minister.LoyaltyTendency != 0 ||
                    minister.AmbitionStyle != 0);
        }

        private static MinisterTabSource ResourceOrEmpty(string role)
        {
            role = NormalizeRole(role);
            return LoadResourceMinisterProfiles().TryGetValue(role, out var source)
                ? source
                : MinisterTabSource.Empty(role);
        }

        private static IReadOnlyDictionary<string, MinisterTabSource> LoadResourceMinisterProfiles()
        {
            if (s_resourceMinisterProfiles != null)
            {
                return s_resourceMinisterProfiles;
            }

            var result = new Dictionary<string, MinisterTabSource>(StringComparer.OrdinalIgnoreCase);
            var asset = Resources.Load<TextAsset>("Data/sections/ministers");
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
            {
                s_resourceMinisterProfiles = result;
                return s_resourceMinisterProfiles;
            }

            try
            {
                var section = JsonUtility.FromJson<ResourceMinistersSection>(asset.text);
                var ministers = section?.ministers;
                if (ministers != null)
                {
                    for (var i = 0; i < ministers.Length; i++)
                    {
                        var minister = ministers[i];
                        if (minister == null)
                        {
                            continue;
                        }

                        var role = NormalizeRole(minister.role);
                        if (string.IsNullOrWhiteSpace(role))
                        {
                            continue;
                        }

                        result[role] = MinisterTabSource.FromResource(role, minister);
                    }
                }
            }
            catch (ArgumentException)
            {
                result.Clear();
            }

            s_resourceMinisterProfiles = result;
            return s_resourceMinisterProfiles;
        }

        [Serializable]
        private sealed class ResourceMinistersSection
        {
            public ResourceMinisterProfile[] ministers;
        }

        [Serializable]
        private sealed class ResourceMinisterProfile
        {
            public string name;
            public string role;
            public string icon_key;
            public string personality;
            public string personality_desc;
            public int ability;
            public int loyalty;
            public int ambition;
            public int cautiousness;
            public int decisiveness;
            public int loyalty_tendency;
            public int ambition_style;
        }

        private sealed class MinisterTabSource
        {
            public string MinisterId;
            public string Name;
            public string Title;
            public string IconResource;
            public string PersonalityText;
            public bool IsVacant;
            public IReadOnlyList<MinisterAttributeState> Attributes = Array.Empty<MinisterAttributeState>();

            public static MinisterTabSource Empty(string role)
            {
                return new MinisterTabSource
                {
                    MinisterId = string.Empty,
                    Name = RoleTitle(role),
                    Title = RoleTitle(role),
                    IconResource = IconResourceFor(string.Empty, role),
                    PersonalityText = string.Empty,
                    IsVacant = true
                };
            }

            public static MinisterTabSource FromCatalog(string role, StaticCatalogCache.MinisterJson minister)
            {
                return new MinisterTabSource
                {
                    MinisterId = string.Empty,
                    Name = Clean(minister.name, RoleTitle(role)),
                    Title = RoleTitle(role),
                    IconResource = IconResourceFor(minister.icon_key, role),
                    PersonalityText = BuildPersonalityText(minister.personality_desc, minister.personality),
                    IsVacant = false,
                    Attributes = BuildAttributes(
                        minister.ability,
                        minister.loyalty,
                        minister.ambition,
                        minister.cautiousness,
                        minister.decisiveness,
                        minister.loyalty_tendency,
                        minister.ambition_style)
                };
            }

            public static MinisterTabSource FromResource(string role, ResourceMinisterProfile minister)
            {
                return new MinisterTabSource
                {
                    MinisterId = string.Empty,
                    Name = Clean(minister.name, RoleTitle(role)),
                    Title = RoleTitle(role),
                    IconResource = IconResourceFor(minister.icon_key, role),
                    PersonalityText = BuildPersonalityText(minister.personality_desc, minister.personality),
                    IsVacant = false,
                    Attributes = BuildAttributes(
                        minister.ability,
                        minister.loyalty,
                        minister.ambition,
                        minister.cautiousness,
                        minister.decisiveness,
                        minister.loyalty_tendency,
                        minister.ambition_style)
                };
            }

            public static MinisterTabSource FromProfile(string role, MinisterProfileDto minister, string iconResource)
            {
                return new MinisterTabSource
                {
                    MinisterId = minister != null && minister.IsVacant ? string.Empty : Clean(minister?.MinisterId, string.Empty),
                    Name = minister != null && minister.IsVacant ? "空缺" : Clean(minister?.Name, RoleTitle(role)),
                    Title = RoleTitle(role),
                    IconResource = ProfileIconResourceFor(minister, role, iconResource),
                    PersonalityText = minister != null && minister.IsVacant
                        ? string.Empty
                        : BuildPersonalityText(minister?.PersonalityDesc, minister?.Personality),
                    IsVacant = minister != null && minister.IsVacant,
                    Attributes = BuildAttributes(
                        minister?.Ability ?? 0,
                        minister?.Loyalty ?? 0,
                        minister?.Ambition ?? 0,
                        minister?.Cautiousness ?? 0,
                        minister?.Decisiveness ?? 0,
                        minister?.LoyaltyTendency ?? 0,
                        minister?.AmbitionStyle ?? 0)
                };
            }

            public MinisterTabSource WithProfile(MinisterProfileDto minister)
            {
                var profileAttributes = BuildAttributes(
                    minister.Ability,
                    minister.Loyalty,
                    minister.Ambition,
                    minister.Cautiousness,
                    minister.Decisiveness,
                    minister.LoyaltyTendency,
                    minister.AmbitionStyle);

                return new MinisterTabSource
                {
                    MinisterId = minister != null && minister.IsVacant ? string.Empty : Clean(minister?.MinisterId, MinisterId),
                    Name = minister != null && minister.IsVacant ? "空缺" : Clean(minister?.Name, Name),
                    Title = Title,
                    IconResource = IconResource,
                    PersonalityText = minister != null && minister.IsVacant
                        ? string.Empty
                        : BuildPersonalityText(minister?.PersonalityDesc, minister?.Personality, PersonalityText),
                    IsVacant = minister != null && minister.IsVacant,
                    Attributes = HasAuthoritativeProfileAttributes(minister) || !HasAttributes(Attributes)
                        ? profileAttributes
                        : Attributes
                };
            }

            public MinisterTabSource WithNameIconFallback(MinisterTabSource fallback)
            {
                return new MinisterTabSource
                {
                    MinisterId = MinisterId,
                    Name = Clean(Name, fallback?.Name),
                    Title = Clean(Title, fallback?.Title),
                    IconResource = Clean(IconResource, fallback?.IconResource),
                    PersonalityText = Clean(PersonalityText, fallback?.PersonalityText),
                    IsVacant = IsVacant,
                    Attributes = Attributes
                };
            }
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
            IReadOnlyList<MinisterTabState> candidateMinisters,
            IReadOnlyList<MinisterChatMessageState> messages,
            IReadOnlyList<MinisterReplyOptionState> options,
            IReadOnlyList<MinisterSkillCardState> skills = null,
            IReadOnlyList<ManagementPanelGroupState> groups = null)
        {
            Title = string.IsNullOrWhiteSpace(title) ? "大臣汇报" : title.Trim();
            ActiveRole = activeRole ?? string.Empty;
            Groups = groups != null ? new List<ManagementPanelGroupState>(groups) : new List<ManagementPanelGroupState>();
            Ministers = ministers != null ? new List<MinisterTabState>(ministers) : new List<MinisterTabState>();
            CandidateMinisters = candidateMinisters != null ? new List<MinisterTabState>(candidateMinisters) : new List<MinisterTabState>();
            Messages = messages != null ? new List<MinisterChatMessageState>(messages) : new List<MinisterChatMessageState>();
            Options = options != null ? new List<MinisterReplyOptionState>(options) : new List<MinisterReplyOptionState>();
            Skills = skills != null ? new List<MinisterSkillCardState>(skills) : new List<MinisterSkillCardState>();
        }

        public string ActiveRole { get; }
        public IReadOnlyList<ManagementPanelGroupState> Groups { get; }
        public bool HasMessages => Messages.Count > 0;
        public bool HasMinisters => Ministers.Count > 0;
        public bool HasSkills => Skills.Count > 0;
        public IReadOnlyList<MinisterChatMessageState> Messages { get; }
        public IReadOnlyList<MinisterTabState> Ministers { get; }
        public IReadOnlyList<MinisterTabState> CandidateMinisters { get; }
        public IReadOnlyList<MinisterReplyOptionState> Options { get; }
        public IReadOnlyList<MinisterSkillCardState> Skills { get; }
        public string Title { get; }
    }

    public sealed class MinisterSkillCardState
    {
        public MinisterSkillCardState(
            string id,
            string ministerRole,
            string name,
            string description,
            string timing,
            bool isAvailable)
        {
            Description = description ?? string.Empty;
            Id = id ?? string.Empty;
            IsAvailable = isAvailable;
            MinisterRole = ministerRole ?? string.Empty;
            Name = name ?? string.Empty;
            Timing = timing ?? string.Empty;
        }

        public string Description { get; }
        public string Id { get; }
        public bool IsAvailable { get; }
        public string MinisterRole { get; }
        public string Name { get; }
        public string Timing { get; }
    }

    public sealed class MinisterTabState
    {
        public MinisterTabState(
            string role,
            string ministerId,
            string name,
            string title,
            string iconResource,
            string avatarText,
            bool selected,
            bool vacant,
            bool roleVacant = false,
            int affection = 0,
            int affectionPulseSequence = 0,
            int affectionPulseDelta = 0,
            IReadOnlyList<MinisterAttributeState> attributes = null,
            string personalityText = "")
        {
            Affection = Math.Clamp(affection, 0, 100);
            AffectionPulseDelta = affectionPulseDelta;
            AffectionPulseSequence = Math.Max(0, affectionPulseSequence);
            Attributes = attributes != null ? new List<MinisterAttributeState>(attributes) : new List<MinisterAttributeState>();
            AvatarText = avatarText ?? string.Empty;
            IconResource = iconResource ?? string.Empty;
            IsSelected = selected;
            IsVacant = vacant;
            RoleVacant = roleVacant || vacant;
            MinisterId = ministerId ?? string.Empty;
            Name = name ?? string.Empty;
            PersonalityText = personalityText ?? string.Empty;
            Role = role ?? string.Empty;
            Title = title ?? string.Empty;
        }

        public int Affection { get; }
        public int AffectionPulseDelta { get; }
        public int AffectionPulseSequence { get; }
        public IReadOnlyList<MinisterAttributeState> Attributes { get; }
        public string AvatarText { get; }
        public string MinisterId { get; }
        public string IconResource { get; }
        public bool IsSelected { get; }
        public bool IsVacant { get; }
        public bool RoleVacant { get; }
        public string Name { get; }
        public string PersonalityText { get; }
        public string Role { get; }
        public string Title { get; }
    }

    public sealed class MinisterAttributeState
    {
        public MinisterAttributeState(string key, string label, int value)
        {
            Key = key ?? string.Empty;
            Label = label ?? string.Empty;
            Value = value;
        }

        public string Key { get; }
        public string Label { get; }
        public int Value { get; }
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
