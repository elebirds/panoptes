/*************************************************
 * Project: Panoptes
 * File: UnitCache.cs
 * Author: Panoptes Team
 * Date: 2026-04-06
 * Description: Runtime unit-view registry.
 *************************************************/

using System.Collections.Generic;
using UnityEngine;

namespace Panoptes.Presentation.Map
{
    public sealed class UnitCache : MonoBehaviour
    {
        public static UnitCache Instance { get; private set; }

        private readonly Dictionary<string, UnitView> _unitViews = new();

        public IReadOnlyDictionary<string, UnitView> UnitViews => _unitViews;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public void Register(UnitView unitView)
        {
            if (unitView == null || string.IsNullOrEmpty(unitView.UnitId))
            {
                return;
            }

            _unitViews[unitView.UnitId] = unitView;
        }

        public void Unregister(UnitView unitView)
        {
            if (unitView == null || string.IsNullOrEmpty(unitView.UnitId))
            {
                return;
            }

            if (_unitViews.TryGetValue(unitView.UnitId, out var existing) && existing == unitView)
            {
                _unitViews.Remove(unitView.UnitId);
            }
        }

        public UnitView GetView(string unitId)
        {
            if (string.IsNullOrEmpty(unitId))
            {
                return null;
            }

            _unitViews.TryGetValue(unitId, out var view);
            return view;
        }

        public void Clear()
        {
            _unitViews.Clear();
        }
    }
}
