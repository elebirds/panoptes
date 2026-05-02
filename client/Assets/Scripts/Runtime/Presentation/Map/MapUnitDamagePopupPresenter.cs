using System.Collections.Generic;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Common;
using Panoptes.Presentation.Planning.Feedback;
using UnityEngine;

namespace Panoptes.Presentation.Map
{
    public sealed class MapUnitDamagePopupPresenter
    {
        private readonly Dictionary<string, int> _knownUnitHpByUnitId = new();
        private readonly Dictionary<string, float> _lastDamagePopupTimeByUnitId = new();

        public int KnownUnitCount => _knownUnitHpByUnitId.Count;

        public void Clear()
        {
            _knownUnitHpByUnitId.Clear();
            _lastDamagePopupTimeByUnitId.Clear();
        }

        public void ForgetUnit(string unitId)
        {
            var normalizedUnitId = Normalize(unitId);
            if (string.IsNullOrEmpty(normalizedUnitId))
            {
                return;
            }

            _knownUnitHpByUnitId.Remove(normalizedUnitId);
            _lastDamagePopupTimeByUnitId.Remove(normalizedUnitId);
        }

        public void RememberUnit(UnitDto unit)
        {
            if (unit == null)
            {
                return;
            }

            var unitId = Normalize(unit.Id);
            if (!string.IsNullOrEmpty(unitId))
            {
                _knownUnitHpByUnitId[unitId] = Mathf.Max(0, unit.Hp);
            }
        }

        public void TrackMovedUnit(
            UnitDto moved,
            bool allowFallbackPopup,
            float repeatCooldownSeconds,
            ref DamageNumberPopupController popupController)
        {
            if (moved == null || string.IsNullOrWhiteSpace(moved.Id))
            {
                return;
            }

            var unitId = moved.Id.Trim();
            var hpAfter = Mathf.Max(0, moved.Hp);

            if (!_knownUnitHpByUnitId.TryGetValue(unitId, out var hpBefore))
            {
                var map = MapRenderer.Instance;
                if (map != null && map.TryGetUnitView(unitId, out var unitView) && unitView != null)
                {
                    hpBefore = Mathf.Max(0, unitView.HitPoints);
                }
                else
                {
                    hpBefore = hpAfter;
                }
            }

            _knownUnitHpByUnitId[unitId] = hpAfter;

            var damage = hpBefore - hpAfter;
            if (allowFallbackPopup && damage > 0)
            {
                TryShowUnitDamagePopup(unitId, damage, repeatCooldownSeconds, ref popupController);
            }
        }

        private void TryShowUnitDamagePopup(
            string unitId,
            int damage,
            float repeatCooldownSeconds,
            ref DamageNumberPopupController popupController)
        {
            if (string.IsNullOrWhiteSpace(unitId) || damage <= 0)
            {
                return;
            }

            var normalizedUnitId = unitId.Trim();
            if (_lastDamagePopupTimeByUnitId.TryGetValue(normalizedUnitId, out var lastPopupAt))
            {
                var cooldown = Mathf.Max(0f, repeatCooldownSeconds);
                if (Time.unscaledTime - lastPopupAt < cooldown)
                {
                    return;
                }
            }

            var map = MapRenderer.Instance;
            if (map == null || !map.TryGetUnitView(normalizedUnitId, out var unitView) || unitView == null)
            {
                return;
            }

            if (popupController == null)
            {
                popupController = SceneObjectFinder.FindFirstSceneObject<DamageNumberPopupController>();
            }

            if (popupController == null)
            {
                var popupRoot = new GameObject("DamageNumberPopupController_Fallback");
                popupController = popupRoot.AddComponent<DamageNumberPopupController>();
            }

            popupController.ShowDamage(unitView.transform, damage, isBuilding: false);
            _lastDamagePopupTimeByUnitId[normalizedUnitId] = Time.unscaledTime;
        }

        private static string Normalize(string value)
        {
            return MapInputTokens.Normalize(value);
        }
    }
}
