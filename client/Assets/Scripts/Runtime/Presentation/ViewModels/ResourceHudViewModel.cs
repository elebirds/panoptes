using System;
using System.Collections.Generic;
using System.Linq;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using R3;

namespace Panoptes.Presentation.ViewModels
{
    public sealed class ResourceHudViewModel : IViewModel<ResourceHudState>, IDisposable
    {
        private const string IndustryOutputPointKey = "industry_output";

        private readonly GameStateStore _gameStateStore;
        private readonly StaticCatalogStore _staticCatalogStore;
        private readonly BehaviorSubject<ResourceHudState> _state;
        private readonly List<IDisposable> _subscriptions = new();
        private ResourceHudState _current;
        private bool _disposed;
        private bool _includePoints = true;

        public ResourceHudViewModel(GameStateStore gameStateStore, StaticCatalogStore staticCatalogStore)
        {
            _gameStateStore = gameStateStore ?? throw new ArgumentNullException(nameof(gameStateStore));
            _staticCatalogStore = staticCatalogStore ?? throw new ArgumentNullException(nameof(staticCatalogStore));
            _current = Project();
            _state = new BehaviorSubject<ResourceHudState>(_current);
            _subscriptions.Add(_gameStateStore.State.Subscribe(this, static (_, self) => self.Publish()));
            _subscriptions.Add(_staticCatalogStore.State.Subscribe(this, static (_, self) => self.Publish()));
        }

        public ResourceHudState Current => _current;
        public Observable<ResourceHudState> State => _state;

        public void SetIncludePoints(bool includePoints)
        {
            if (_includePoints == includePoints)
            {
                return;
            }

            _includePoints = includePoints;
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

        private void Publish()
        {
            if (_disposed)
            {
                return;
            }

            _current = Project();
            _state.OnNext(_current);
        }

        private ResourceHudState Project()
        {
            var resources = BuildResourceAmounts(_gameStateStore.Snapshot?.MyResources);
            var points = BuildPointAmounts(_gameStateStore.Snapshot?.MyResources);
            var catalog = _staticCatalogStore.Snapshot;
            var rows = new List<ResourceHudRowState>();

            AddCatalogRows(rows, catalog?.Resources, resources, false);
            if (_includePoints)
            {
                AddCatalogRows(rows, catalog?.Points, points, true);
            }

            if (rows.Count == 0)
            {
                AddFallbackRows(rows, resources, false);
                if (_includePoints)
                {
                    AddFallbackRows(rows, points, true);
                }
            }

            return new ResourceHudState(rows);
        }

        private static void AddCatalogRows(
            List<ResourceHudRowState> rows,
            IReadOnlyDictionary<string, CatalogHudEntryDto> catalogEntries,
            IReadOnlyDictionary<string, int> amounts,
            bool isPoint)
        {
            if (catalogEntries == null || catalogEntries.Count == 0)
            {
                return;
            }

            foreach (var entry in catalogEntries.Values
                         .Where(entry => entry != null && entry.VisibleInHud)
                         .OrderBy(entry => entry.SortOrder)
                         .ThenBy(entry => NormalizeKey(entry.Key), StringComparer.OrdinalIgnoreCase))
            {
                var key = NormalizeKey(entry.Key);
                amounts.TryGetValue(key, out var amount);
                rows.Add(new ResourceHudRowState(
                    key,
                    amount,
                    entry.IconKey,
                    isPoint,
                    entry.Name,
                    entry.Description));
            }
        }

        private static void AddFallbackRows(
            List<ResourceHudRowState> rows,
            IReadOnlyDictionary<string, int> amounts,
            bool isPoint)
        {
            foreach (var pair in amounts.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                rows.Add(new ResourceHudRowState(pair.Key, pair.Value, string.Empty, isPoint));
            }
        }

        private static Dictionary<string, int> BuildResourceAmounts(ResourceDto source)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (source == null)
            {
                return result;
            }

            result[ResourceKeys.ResourceFood] = source.Food;
            result[ResourceKeys.ResourceOre] = source.Ore;
            result[ResourceKeys.ResourceWood] = source.Wood;
            CopyAmountsInto(result, source.ResourceAmounts);
            return result;
        }

        private static Dictionary<string, int> BuildPointAmounts(ResourceDto source)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (source == null)
            {
                return result;
            }

            result[IndustryOutputPointKey] = source.IndustryOutput;
            CopyAmountsInto(result, source.PointAmounts);
            return result;
        }

        private static void CopyAmountsInto(
            Dictionary<string, int> result,
            IReadOnlyDictionary<string, int> source)
        {
            if (result == null || source == null)
            {
                return;
            }

            foreach (var pair in source)
            {
                var key = NormalizeKey(pair.Key);
                if (!string.IsNullOrEmpty(key))
                {
                    result[key] = pair.Value;
                }
            }
        }

        private static string NormalizeKey(string key)
        {
            return string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim();
        }
    }
}
