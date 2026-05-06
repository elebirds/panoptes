using System;
using System.Collections.Generic;
using Panoptes.Core.Application.Feedback;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using Panoptes.Presentation.UI.Common;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Panoptes.Presentation.UI.Game
{
    public sealed class GameSceneController : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private bool hideFullscreenBackgroundOnGameScene = true;
        [SerializeField] private string fullscreenBackgroundObjectName = "Background";

        private GameStateStore _gameStateStore;
        private SettlementStore _settlementStore;
        private GameOverStore _gameOverStore;
        private GameplayFeedbackStore _feedbackStore;
        private StaticCatalogStore _staticCatalogStore;
        private ErrorToast _errorToast;
        private IDisposable _gameStateSubscription;
        private IDisposable _settlementSubscription;
        private IDisposable _gameOverSubscription;
        private IDisposable _feedbackSubscription;

        [Inject]
        private void Construct(
            GameStateStore gameStateStore,
            SettlementStore settlementStore,
            GameOverStore gameOverStore,
            GameplayFeedbackStore feedbackStore,
            StaticCatalogStore staticCatalogStore,
            ErrorToast errorToast)
        {
            _gameStateStore = gameStateStore;
            _settlementStore = settlementStore;
            _gameOverStore = gameOverStore;
            _feedbackStore = feedbackStore;
            _staticCatalogStore = staticCatalogStore;
            _errorToast = errorToast;
        }

        private void Awake()
        {
            HideFullscreenBackgroundIfNeeded();
        }

        private void OnEnable()
        {
            _gameStateSubscription?.Dispose();
            _gameStateSubscription = _gameStateStore?.State.Subscribe(this, static (state, self) => self.RefreshFromState(state));
            _settlementSubscription?.Dispose();
            _settlementSubscription = _settlementStore?.State.Subscribe(this, static (state, self) => self.OnSettlementChanged(state));
            _gameOverSubscription?.Dispose();
            _gameOverSubscription = _gameOverStore?.State.Subscribe(this, static (state, self) => self.OnGameOverChanged(state));
            _feedbackSubscription?.Dispose();
            _feedbackSubscription = _feedbackStore?.State.Subscribe(this, static (state, self) => self.OnFeedbackChanged(state));
        }

        private void Start()
        {
            RefreshFromState(_gameStateStore?.Snapshot);
        }

        private void OnDisable()
        {
            _gameStateSubscription?.Dispose();
            _gameStateSubscription = null;
            _settlementSubscription?.Dispose();
            _settlementSubscription = null;
            _gameOverSubscription?.Dispose();
            _gameOverSubscription = null;
            _feedbackSubscription?.Dispose();
            _feedbackSubscription = null;
        }

        public void RefreshFromState(GameStateStoreState state)
        {
            if (state == null || string.IsNullOrWhiteSpace(state.GameId) || string.IsNullOrWhiteSpace(state.MyPlayerId))
            {
                if (statusText != null)
                {
                    statusText.text = "等待游戏初始化...";
                }
                return;
            }

            var summary = $"Game {state.GameId}\n玩家 {state.MyPlayerId}\n回合 {state.Turn} / {GamePhases.ToDisplayText(state.Phase)}\n地图 {state.MapWidth}x{state.MapHeight}";
            if (statusText != null)
            {
                statusText.text = summary;
            }

            if (Debug.isDebugBuild)
            {
                PanoptesLog.Log($"[GameScene] {summary}");
            }
        }

        public void RefreshFromCache()
        {
            RefreshFromState(_gameStateStore?.Snapshot);
        }

        private void OnFeedbackChanged(GameplayFeedbackState state)
        {
            if (state == null || !state.HasFeedback)
            {
                return;
            }

            ShowToast(GameplayFeedbackText.ResolveMessage(state.Message, state.Code), state.Success);
        }

        private void OnGameOverChanged(GameOverState state)
        {
            if (state == null || !state.IsGameOver || statusText == null)
            {
                return;
            }

            statusText.gameObject.SetActive(false);
        }

        private void OnSettlementChanged(SettlementState state)
        {
            var completedTechnologyNames = CollectCompletedTechnologyNames(state?.Settlement);
            if (completedTechnologyNames.Count == 0)
            {
                return;
            }

            ShowToast(BuildTechnologyCompletedToastMessage(completedTechnologyNames), true);
        }

        private void HideFullscreenBackgroundIfNeeded()
        {
            if (!hideFullscreenBackgroundOnGameScene)
            {
                return;
            }

            var canvas = statusText != null ? statusText.canvas : null;
            var root = canvas != null ? canvas.transform : transform;
            if (root == null)
            {
                return;
            }

            var target = root.Find(fullscreenBackgroundObjectName);
            if (target == null)
            {
                return;
            }

            var image = target.GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = false;
            }

            target.gameObject.SetActive(false);
        }

        private List<string> CollectCompletedTechnologyNames(TurnSettlementDto settlement)
        {
            var result = new List<string>();
            var myPlayerId = _gameStateStore?.Snapshot.MyPlayerId ?? string.Empty;
            if (settlement?.Sections == null || string.IsNullOrWhiteSpace(myPlayerId))
            {
                return result;
            }

            var seenTechnologyIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            for (var sectionIndex = 0; sectionIndex < settlement.Sections.Count; sectionIndex++)
            {
                var section = settlement.Sections[sectionIndex];
                if (section?.Events == null)
                {
                    continue;
                }

                for (var eventIndex = 0; eventIndex < section.Events.Count; eventIndex++)
                {
                    var turnEvent = section.Events[eventIndex];
                    if (!IsOwnedTechnologyCompletion(turnEvent, myPlayerId))
                    {
                        continue;
                    }

                    var technologyId = ReadEventData(turnEvent, "technology_id");
                    if (string.IsNullOrWhiteSpace(technologyId) || !seenTechnologyIds.Add(technologyId.Trim()))
                    {
                        continue;
                    }

                    result.Add(ResolveTechnologyDisplayName(technologyId, _staticCatalogStore?.Snapshot));
                }
            }

            return result;
        }

        private static bool IsOwnedTechnologyCompletion(TurnEventDto evt, string playerId)
        {
            if (evt == null ||
                !string.Equals(evt.Type, "technology_completed", System.StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(playerId))
            {
                return false;
            }

            var eventPlayerId = ReadEventData(evt, "player_id");
            if (string.IsNullOrWhiteSpace(eventPlayerId))
            {
                eventPlayerId = evt.Source;
            }

            return string.Equals(eventPlayerId?.Trim(), playerId.Trim(), System.StringComparison.Ordinal);
        }

        private static string ResolveTechnologyDisplayName(string technologyId, StaticCatalogState catalog)
        {
            var normalizedTechnologyId = technologyId?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedTechnologyId))
            {
                return string.Empty;
            }

            if (catalog?.Technologies != null &&
                catalog.Technologies.TryGetValue(normalizedTechnologyId, out var technology) &&
                technology != null &&
                !string.IsNullOrWhiteSpace(technology.Name))
            {
                return technology.Name.Trim();
            }

            return normalizedTechnologyId;
        }

        private static string BuildTechnologyCompletedToastMessage(IReadOnlyList<string> technologyNames)
        {
            if (technologyNames == null || technologyNames.Count == 0)
            {
                return string.Empty;
            }

            return technologyNames.Count == 1
                ? $"科技研究完成：{technologyNames[0]}"
                : $"科技研究完成：{string.Join("、", technologyNames)}";
        }

        private static string ReadEventData(TurnEventDto evt, params string[] keys)
        {
            if (evt?.Data == null || keys == null)
            {
                return string.Empty;
            }

            for (var i = 0; i < keys.Length; i++)
            {
                var key = keys[i];
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (evt.Data.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        private void ShowToast(string message, bool success)
        {
            if (_errorToast != null)
            {
                _errorToast.Show(message, success);
                return;
            }

            if (success)
            {
                PanoptesLog.Log($"[GameScene] {message}");
                return;
            }

            PanoptesLog.Warning($"[GameScene] {message}");
        }
    }
}
