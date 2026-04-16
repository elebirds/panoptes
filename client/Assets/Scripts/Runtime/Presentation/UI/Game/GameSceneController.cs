using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Intents;
using Panoptes.Core.Domain;
using Panoptes.Core.Events;
using Panoptes.Presentation.Map;
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

            ShowToast(MapGameError(evt.Code), false);
        }

        private void OnTokenResult(TokenResultEvent evt)
        {
            if (evt == null || evt.Success || string.IsNullOrWhiteSpace(evt.ErrorCode))
            {
                return;
            }

            ShowToast(MapGameError(evt.ErrorCode), false);
        }

        private void OnGameOver(GameOverEvent _)
        {
            if (statusText != null)
            {
                statusText.gameObject.SetActive(false);
            }
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
            EnsureComponent<TokenHUD>(canvas.transform, "TokenHUD");
            EnsureComponent<SettlementTimeline>(canvas.transform, "SettlementTimeline");
            EnsureComponent<TurnReportPanel>(canvas.transform, "TurnReportPanel");
            EnsureComponent<ResourceHUD>(canvas.transform, "ResourcePanel");
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
            var existing = UnityEngine.Object.FindAnyObjectByType<T>();
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

        private static string MapGameError(string code)
        {
            return code switch
            {
                "phase_mismatch" => "当前阶段不支持此操作",
                "game_not_found" => "当前对局不存在",
                "invalid_request" => "请求格式错误",
                "unauthorized" => "请重新登录",
                "internal_error" => "服务器错误，请稍后重试",
                _ => string.IsNullOrWhiteSpace(code) ? "未知错误" : code
            };
        }
    }
}
