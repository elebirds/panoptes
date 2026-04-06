using Panoptes.Runtime.Cache;
using TMPro;
using UnityEngine;

namespace Panoptes.Runtime.UI.Game
{
    public sealed class GameSceneController : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI statusText;

        private GameStateCache _cache;

        private void Awake()
        {
            _cache = GameStateCache.Instance;
        }

        private void OnEnable()
        {
            if (_cache != null)
            {
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

            Debug.Log($"[GameScene] {summary}");
        }
    }
}
