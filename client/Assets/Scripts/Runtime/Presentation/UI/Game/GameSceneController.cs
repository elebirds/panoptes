using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Intents;
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
        }

        private void OnEnable()
        {
            _cache = GameStateCache.Instance;

            if (_cache != null)
            {
                GameIntents.Initialize(_cache);
                _cache.OnStateChanged += RefreshFromCache;
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

            var summary = $"Game {_cache.GameID}\n玩家 {_cache.MyPlayerID}\n回合 {_cache.Turn} / {_cache.Phase}\n地图 {_cache.MapWidth}x{_cache.MapHeight}";
            if (statusText != null)
            {
                statusText.text = summary;
            }

            if (Debug.isDebugBuild)
            {
                Debug.Log($"[GameScene] {summary}");
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
    }
}
