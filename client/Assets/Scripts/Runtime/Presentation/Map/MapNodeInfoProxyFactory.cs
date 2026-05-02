using Panoptes.Core.Application.Cache;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Planning.Feedback;
using UnityEngine;

namespace Panoptes.Presentation.Map
{
    public sealed class MapNodeInfoProxyFactory
    {
        private UnitView _buildingInfoProxy;

        public void DestroyProxy()
        {
            if (_buildingInfoProxy == null)
            {
                return;
            }

            Object.Destroy(_buildingInfoProxy.gameObject);
            _buildingInfoProxy = null;
        }

        public UnitView GetOrCreate(
            NodeView nodeView,
            NodeDto nodeState,
            string normalizedBuildingType,
            bool isResourcePoint)
        {
            if (nodeView == null || nodeState == null)
            {
                return null;
            }

            if (_buildingInfoProxy == null)
            {
                var proxyGo = new GameObject("BuildingInfoProxy");
                _buildingInfoProxy = proxyGo.AddComponent<UnitView>();
                proxyGo.hideFlags = HideFlags.DontSave;
            }

            var infoType = normalizedBuildingType;
            if (string.IsNullOrEmpty(infoType) && isResourcePoint)
            {
                var resourceType = NormalizeToken(nodeState.ResourceType);
                infoType = string.IsNullOrEmpty(resourceType) ? "resource_point" : $"resource_{resourceType}";
            }

            if (string.IsNullOrEmpty(infoType))
            {
                return null;
            }

            var hp = nodeState.BuildingHp > 0 ? nodeState.BuildingHp : (isResourcePoint ? 1 : 100);
            var maxHp = ResolveMaxHp(nodeView, nodeState, infoType, hp, isResourcePoint);
            var unit = new UnitDto
            {
                Id = nodeState.Id ?? string.Empty,
                Type = infoType,
                Owner = !string.IsNullOrWhiteSpace(nodeState.Owner) ? nodeState.Owner : nodeState.TerritoryOwner,
                Q = nodeState.Q,
                R = nodeState.R,
                Hp = hp,
                MaxHp = maxHp
            };

            var worldPos = nodeView.BuildingAnchor != null
                ? nodeView.BuildingAnchor.position
                : nodeView.transform.position;

            _buildingInfoProxy.gameObject.SetActive(true);
            _buildingInfoProxy.Bind(unit, worldPos);
            _buildingInfoProxy.SetSelected(false);
            var collider = _buildingInfoProxy.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            _buildingInfoProxy.gameObject.SetActive(false);
            return _buildingInfoProxy;
        }

        public static bool TryGetInspectableNodeInfo(
            NodeDto nodeState,
            out string buildingType,
            out bool isResourcePoint)
        {
            buildingType = string.Empty;
            isResourcePoint = false;
            if (nodeState == null)
            {
                return false;
            }

            buildingType = NormalizeToken(nodeState.BuildingType);
            isResourcePoint = nodeState.IsResourcePoint;
            return !string.IsNullOrEmpty(buildingType) || isResourcePoint;
        }

        private static int ResolveMaxHp(
            NodeView nodeView,
            NodeDto nodeState,
            string infoType,
            int hp,
            bool isResourcePoint)
        {
            if (isResourcePoint)
            {
                return Mathf.Max(1, hp);
            }

            var maxHp = nodeState != null ? nodeState.BuildingMaxHp : 0;
            if (maxHp <= 0 && nodeView != null && nodeView.BuildingInstance != null)
            {
                maxHp = nodeView.BuildingInstance.MaxHitPoints;
            }

            if (maxHp <= 0)
            {
                var catalog = StaticCatalogCache.EnsureInstance();
                var normalizedType = NormalizeToken(infoType);
                if (catalog != null)
                {
                    if (string.Equals(normalizedType, "city_core", System.StringComparison.OrdinalIgnoreCase) &&
                        catalog.Rules != null &&
                        catalog.Rules.city_core_max_hp > 0)
                    {
                        maxHp = catalog.Rules.city_core_max_hp;
                    }
                    else if (catalog.TryGetBuilding(normalizedType, out var buildingEntry) && buildingEntry != null)
                    {
                        maxHp = buildingEntry.max_hp;
                    }
                }
            }

            return Mathf.Max(1, Mathf.Max(maxHp, hp));
        }

        private static string NormalizeToken(string value)
        {
            return MapInputTokens.Normalize(value);
        }
    }
}
