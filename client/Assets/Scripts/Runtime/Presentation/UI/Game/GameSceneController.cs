using System.Collections.Generic;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Feedback;
using Panoptes.Core.Application.Intents;
using Panoptes.Core.Domain;
using Panoptes.Core.Events;
using Panoptes.Presentation.Map;
using Panoptes.Presentation.Common;
using Panoptes.Presentation.UI.Common;
using Panoptes.Presentation.UI.HUD;
using Panoptes.Presentation.UI.Turn;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Presentation.UI.Game
{
    public sealed class GameSceneController : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private bool hideFullscreenBackgroundOnGameScene = true;
        [SerializeField] private string fullscreenBackgroundObjectName = "Background";

        private GameStateCache _cache;

        private void Awake()
        {
            _cache = GameStateCache.Instance;
            HideFullscreenBackgroundIfNeeded();
            EnsurePresentationHelpers();
        }

        private void OnEnable()
        {
            _cache = GameStateCache.Instance;

            if (_cache != null)
            {
                GameIntents.Initialize(_cache);
                _cache.OnStateChanged += RefreshFromCache;
                _cache.OnGameError += OnGameError;
                _cache.OnTokenResult += OnTokenResult;
                _cache.OnTurnSettled += OnTurnSettled;
                _cache.OnGameOver += OnGameOver;
            }
        }

        private void Start()
        {
            RefreshFromCache();
        }

        private void OnDisable()
        {
            if (_cache != null)
            {
                _cache.OnStateChanged -= RefreshFromCache;
                _cache.OnGameError -= OnGameError;
                _cache.OnTokenResult -= OnTokenResult;
                _cache.OnTurnSettled -= OnTurnSettled;
                _cache.OnGameOver -= OnGameOver;
            }

            GameIntents.Dispose();
        }

        public void RefreshFromCache()
        {
            if (_cache == null || string.IsNullOrWhiteSpace(_cache.GameID) || string.IsNullOrWhiteSpace(_cache.MyPlayerID))
            {
                if (statusText != null)
                {
                    statusText.text = "等待游戏初始化...";
                }
                return;
            }

            var summary = $"Game {_cache.GameID}\n玩家 {_cache.MyPlayerID}\n回合 {_cache.Turn} / {GamePhases.ToDisplayText(_cache.Phase)}\n地图 {_cache.MapWidth}x{_cache.MapHeight}";
            if (statusText != null)
            {
                statusText.text = summary;
            }

            if (Debug.isDebugBuild)
            {
                Debug.Log($"[GameScene] {summary}");
            }
        }

        private void OnGameError(GameErrorEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            ShowToast(GameplayFeedbackText.ResolveMessage(evt.Message, evt.Code), false);
        }

        private void OnTokenResult(TokenResultEvent evt)
        {
            if (evt == null || evt.Success || string.IsNullOrWhiteSpace(evt.ErrorCode))
            {
                return;
            }

            ShowToast(GameplayFeedbackText.ResolveMessage(string.Empty, evt.ErrorCode), false);
        }

        private void OnGameOver(GameOverEvent _)
        {
            if (statusText != null)
            {
                statusText.gameObject.SetActive(false);
            }
        }

        private void OnTurnSettled(TurnSettledEvent evt)
        {
            var completedTechnologyNames = CollectCompletedTechnologyNames(evt);
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

        private void EnsurePresentationHelpers()
        {
            var canvas = statusText != null ? statusText.canvas : GetComponentInChildren<Canvas>(true);
            if (canvas == null)
            {
                return;
            }

            EnsureComponent<TurnHUD>(canvas.transform, "TurnHUD");
            EnsureComponent<ResourceHUD>(canvas.transform, "ResourcePanel");
            EnsureComponent<SettlementTimeline>(canvas.transform, "SettlementTimeline");
            EnsureComponent<TurnReportPanel>(canvas.transform, "TurnReportPanel");
            EnsurePrefabComponent<GameOverOverlay>(canvas.transform, "GameOverOverlay", "Prefabs/UI/GameOverOverlay");
            EnsureRuntimeComponent<SettlementPlaybackController>("SettlementPlaybackController");
        }

        private static void EnsureComponent<T>(Transform parent, string objectName) where T : Component
        {
            var existing = parent.Find(objectName);
            if (existing != null && existing.GetComponent<T>() != null)
            {
                return;
            }

            var go = existing != null ? existing.gameObject : new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            if (go.GetComponent<T>() == null)
            {
                go.AddComponent<T>();
            }
        }

        private static void EnsureRuntimeComponent<T>(string objectName) where T : Component
        {
            var existing = SceneObjectFinder.FindFirstSceneObject<T>();
            if (existing != null)
            {
                return;
            }

            var go = new GameObject(objectName);
            go.AddComponent<T>();
        }

        private static void EnsurePrefabComponent<T>(Transform parent, string objectName, string resourcesPath) where T : Component
        {
            var existing = parent.Find(objectName);
            if (existing != null && existing.GetComponent<T>() != null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(resourcesPath))
            {
                var prefab = Resources.Load<GameObject>(resourcesPath.Trim());
                if (prefab != null)
                {
                    var instance = Object.Instantiate(prefab, parent, false);
                    instance.name = objectName;
                    if (instance.GetComponent<T>() != null)
                    {
                        return;
                    }
                }
            }

            EnsureComponent<T>(parent, objectName);
        }

        private List<string> CollectCompletedTechnologyNames(TurnSettledEvent evt)
        {
            var result = new List<string>();
            if (_cache == null || evt?.Settlement?.Sections == null || string.IsNullOrWhiteSpace(_cache.MyPlayerID))
            {
                return result;
            }

            var seenTechnologyIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            for (var sectionIndex = 0; sectionIndex < evt.Settlement.Sections.Count; sectionIndex++)
            {
                var section = evt.Settlement.Sections[sectionIndex];
                if (section?.Events == null)
                {
                    continue;
                }

                for (var eventIndex = 0; eventIndex < section.Events.Count; eventIndex++)
                {
                    var turnEvent = section.Events[eventIndex];
                    if (!IsOwnedTechnologyCompletion(turnEvent, _cache.MyPlayerID))
                    {
                        continue;
                    }

                    var technologyId = ReadEventData(turnEvent, "technology_id");
                    if (string.IsNullOrWhiteSpace(technologyId) || !seenTechnologyIds.Add(technologyId.Trim()))
                    {
                        continue;
                    }

                    result.Add(ResolveTechnologyDisplayName(technologyId));
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

        private static string ResolveTechnologyDisplayName(string technologyId)
        {
            var normalizedTechnologyId = technologyId?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedTechnologyId))
            {
                return string.Empty;
            }

            var catalog = StaticCatalogCache.EnsureInstance();
            if (catalog != null &&
                catalog.TryGetTechnology(normalizedTechnologyId, out var technology) &&
                technology != null &&
                !string.IsNullOrWhiteSpace(technology.name))
            {
                return technology.name.Trim();
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

        private static void ShowToast(string message, bool success)
        {
            if (ErrorToast.Instance != null)
            {
                ErrorToast.Instance.Show(message, success);
                return;
            }

            if (success)
            {
                Debug.Log($"[GameScene] {message}");
                return;
            }

            Debug.LogWarning($"[GameScene] {message}");
        }
    }
}
