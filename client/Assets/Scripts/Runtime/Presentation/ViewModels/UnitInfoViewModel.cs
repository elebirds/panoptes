using System;
using System.Collections.Generic;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using R3;

namespace Panoptes.Presentation.ViewModels
{
    public sealed class UnitInfoViewModel : IViewModel<UnitInfoState>, IDisposable
    {
        private readonly GameStateStore _gameStateStore;
        private readonly PlanningDraftStore _planningDraftStore;
        private readonly SelectionService _selectionService;
        private readonly SelectionStore _selectionStore;
        private readonly StaticCatalogStore _staticCatalogStore;
        private readonly BehaviorSubject<UnitInfoState> _state;
        private readonly List<IDisposable> _subscriptions = new();
        private bool _actionLocked;
        private bool _disposed;
        private UnitInfoState _current;

        public UnitInfoViewModel(
            GameStateStore gameStateStore,
            SelectionStore selectionStore,
            StaticCatalogStore staticCatalogStore,
            PlanningDraftStore planningDraftStore,
            SelectionService selectionService)
        {
            _gameStateStore = gameStateStore ?? throw new ArgumentNullException(nameof(gameStateStore));
            _selectionStore = selectionStore ?? throw new ArgumentNullException(nameof(selectionStore));
            _staticCatalogStore = staticCatalogStore ?? throw new ArgumentNullException(nameof(staticCatalogStore));
            _planningDraftStore = planningDraftStore ?? throw new ArgumentNullException(nameof(planningDraftStore));
            _selectionService = selectionService ?? throw new ArgumentNullException(nameof(selectionService));
            _current = Project();
            _state = new BehaviorSubject<UnitInfoState>(_current);
            SubscribeStores();
        }

        public UnitInfoState Current => _current;
        public Observable<UnitInfoState> State => _state;

        public void ClearSelection()
        {
            _selectionService.Clear();
        }

        public void SelectUnit(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                ClearSelection();
                return;
            }

            _selectionService.SelectUnit(unitId);
        }

        public void SetActionLocked(bool actionLocked)
        {
            if (_actionLocked == actionLocked)
            {
                return;
            }

            _actionLocked = actionLocked;
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

        private void SubscribeStores()
        {
            _subscriptions.Add(_gameStateStore.State.Subscribe(this, static (_, self) => self.Publish()));
            _subscriptions.Add(_selectionStore.State.Subscribe(this, static (_, self) => self.Publish()));
            _subscriptions.Add(_staticCatalogStore.State.Subscribe(this, static (_, self) => self.Publish()));
            _subscriptions.Add(_planningDraftStore.State.Subscribe(this, static (_, self) => self.Publish()));
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

        private UnitInfoState Project()
        {
            var selection = _selectionStore.Snapshot;
            if (selection == null || string.IsNullOrWhiteSpace(selection.SelectedUnitId))
            {
                return new UnitInfoState(actionLocked: _actionLocked);
            }

            var selectedId = selection.SelectedUnitId.Trim();
            var game = _gameStateStore.Snapshot;
            var catalog = _staticCatalogStore.Snapshot;
            var planning = _planningDraftStore.Snapshot;
            var selected = ResolveSelectedUnit(selectedId, game, catalog);
            if (!selected.HasSelection)
            {
                return new UnitInfoState(actionLocked: _actionLocked);
            }

            ResolveCatalogText(catalog, selected.UnitType, selected.IsBuildingOrResource, out var displayName, out var description);
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = BuildDisplayName(selected.UnitId, selected.UnitType);
            }

            var directOrder = ResolveDirectOrderState(catalog, selected.UnitType, selected.IsBuildingOrResource);
            var interactivePlanning = GamePhases.IsPlanning(game.Phase) && !game.IsGameOver;
            var controllable = !string.IsNullOrWhiteSpace(game.MyPlayerId) &&
                               string.Equals(game.MyPlayerId, selected.OwnerId, StringComparison.Ordinal);
            var showDirectOrderButtons = interactivePlanning &&
                                         controllable &&
                                         (directOrder.CanMove || directOrder.IsMilitaryUnit);

            return new UnitInfoState(
                hasSelection: true,
                unitId: selected.UnitId,
                unitType: selected.UnitType,
                ownerId: selected.OwnerId,
                displayName: displayName,
                description: description,
                hp: selected.Hp,
                maxHp: selected.MaxHp,
                planningSummary: ResolvePlanningSummary(planning, selected.UnitId),
                showDirectOrderButtons: showDirectOrderButtons,
                canMove: directOrder.CanMove,
                isMilitaryUnit: directOrder.IsMilitaryUnit,
                canAttack: directOrder.CanAttack,
                canCharge: directOrder.CanCharge,
                actionLocked: _actionLocked);
        }

        private static SelectedUnitProjection ResolveSelectedUnit(
            string selectedId,
            GameStateStoreState game,
            StaticCatalogState catalog)
        {
            if (game?.Units != null &&
                game.Units.TryGetValue(selectedId, out var unit) &&
                unit != null)
            {
                return new SelectedUnitProjection(
                    true,
                    unit.Id,
                    unit.Type,
                    unit.Owner,
                    unit.Hp,
                    unit.MaxHp,
                    isBuildingOrResource: false);
            }

            if (game?.Nodes != null &&
                game.Nodes.TryGetValue(selectedId, out var node) &&
                node != null &&
                (!string.IsNullOrWhiteSpace(node.BuildingType) || node.IsResourcePoint))
            {
                var type = !string.IsNullOrWhiteSpace(node.BuildingType) ? node.BuildingType : node.ResourceType;
                var owner = !string.IsNullOrWhiteSpace(node.Owner) ? node.Owner : node.TerritoryOwner;
                var maxHp = ResolveBuildingMaxHp(node, type, catalog);
                var hp = node.BuildingHp > 0 ? node.BuildingHp : maxHp;
                return new SelectedUnitProjection(
                    true,
                    selectedId,
                    type,
                    owner,
                    hp,
                    maxHp,
                    isBuildingOrResource: true);
            }

            return default;
        }

        private static int ResolveBuildingMaxHp(
            NodeDto node,
            string buildingType,
            StaticCatalogState catalog)
        {
            if (node == null || node.IsResourcePoint)
            {
                return 1;
            }

            var maxHp = node.BuildingMaxHp;
            var normalizedType = NormalizeToken(buildingType);
            if (maxHp <= 0 &&
                !string.IsNullOrWhiteSpace(normalizedType) &&
                catalog?.Buildings != null &&
                catalog.Buildings.TryGetValue(normalizedType, out var building) &&
                building != null)
            {
                maxHp = building.MaxHp;
            }

            if (maxHp <= 0)
            {
                maxHp = node.BuildingHp > 0 ? node.BuildingHp : 100;
            }

            return Math.Max(1, maxHp);
        }

        private static void ResolveCatalogText(
            StaticCatalogState catalog,
            string unitType,
            bool isBuildingOrResource,
            out string displayName,
            out string description)
        {
            displayName = string.Empty;
            description = string.Empty;
            var normalizedType = NormalizeToken(unitType);
            if (string.IsNullOrWhiteSpace(normalizedType) || catalog == null)
            {
                return;
            }

            if (!isBuildingOrResource &&
                catalog.Units != null &&
                catalog.Units.TryGetValue(normalizedType, out var unit) &&
                unit != null)
            {
                displayName = unit.Name?.Trim() ?? string.Empty;
                description = unit.Description?.Trim() ?? string.Empty;
                return;
            }

            if (catalog.Buildings != null &&
                catalog.Buildings.TryGetValue(normalizedType, out var building) &&
                building != null)
            {
                displayName = building.Name?.Trim() ?? string.Empty;
                description = building.Description?.Trim() ?? string.Empty;
            }
        }

        private static DirectOrderProjection ResolveDirectOrderState(
            StaticCatalogState catalog,
            string unitType,
            bool isBuildingOrResource)
        {
            var normalizedType = NormalizeToken(unitType);
            if (string.IsNullOrWhiteSpace(normalizedType) ||
                isBuildingOrResource ||
                IsResourceType(normalizedType) ||
                catalog?.Units == null ||
                !catalog.Units.TryGetValue(normalizedType, out var unit) ||
                unit == null)
            {
                return default;
            }

            var isMilitaryUnit = !HasTag(unit.Tags, "civilian");
            return new DirectOrderProjection(
                canMove: true,
                isMilitaryUnit: isMilitaryUnit,
                canAttack: isMilitaryUnit,
                canCharge: isMilitaryUnit && HasTag(unit.Tags, "charge"));
        }

        private static string ResolvePlanningSummary(PlanningDraftState planning, string unitId)
        {
            if (planning?.UnitOrders == null || string.IsNullOrWhiteSpace(unitId))
            {
                return string.Empty;
            }

            var normalizedUnitId = NormalizeToken(unitId);
            for (var i = 0; i < planning.UnitOrders.Count; i++)
            {
                var order = planning.UnitOrders[i];
                if (order == null ||
                    !string.Equals(NormalizeToken(order.UnitId), normalizedUnitId, StringComparison.Ordinal))
                {
                    continue;
                }

                var action = LocalizeOrderAction(order.Action);
                var target = !string.IsNullOrWhiteSpace(order.TargetNodeId)
                    ? order.TargetNodeId.Trim()
                    : order.TargetUnitId?.Trim();
                return string.IsNullOrWhiteSpace(target) ? $"已规划：{action}" : $"已规划：{action} -> {target}";
            }

            return string.Empty;
        }

        private static string BuildDisplayName(string unitId, string unitType)
        {
            var type = NormalizeToken(unitType);
            if (string.IsNullOrEmpty(type))
            {
                return $"单位 {unitId}";
            }

            return $"{LocalizeUnitType(type)} [{unitId}]";
        }

        private static string LocalizeOrderAction(string action)
        {
            return NormalizeToken(action) switch
            {
                "move" => "移动",
                "attack" => "攻击",
                "hold" => "待命",
                "charge" => "冲锋",
                "build" => "建造",
                "" => "指令",
                _ => action?.Trim() ?? "指令"
            };
        }

        private static string LocalizeUnitType(string unitType)
        {
            return NormalizeToken(unitType) switch
            {
                "city_core" => "城市核心",
                "settler" => "开拓者",
                "infantry" => "步兵",
                "archer" => "弓手",
                "cavalry" => "骑兵",
                "spearman" => "枪兵",
                "resource_point" => "资源点",
                _ => unitType
            };
        }

        private static bool HasTag(IReadOnlyList<string> tags, string tag)
        {
            if (tags == null || string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            for (var i = 0; i < tags.Count; i++)
            {
                if (string.Equals(tags[i], tag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsResourceType(string normalizedType)
        {
            return string.Equals(normalizedType, "resource_point", StringComparison.Ordinal) ||
                   normalizedType.StartsWith("resource_", StringComparison.Ordinal);
        }

        private static string NormalizeToken(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
        }

        private readonly struct DirectOrderProjection
        {
            public readonly bool CanAttack;
            public readonly bool CanCharge;
            public readonly bool CanMove;
            public readonly bool IsMilitaryUnit;

            public DirectOrderProjection(bool canMove, bool isMilitaryUnit, bool canAttack, bool canCharge)
            {
                CanAttack = canAttack;
                CanCharge = canCharge;
                CanMove = canMove;
                IsMilitaryUnit = isMilitaryUnit;
            }
        }

        private readonly struct SelectedUnitProjection
        {
            public readonly bool HasSelection;
            public readonly int Hp;
            public readonly bool IsBuildingOrResource;
            public readonly int MaxHp;
            public readonly string OwnerId;
            public readonly string UnitId;
            public readonly string UnitType;

            public SelectedUnitProjection(
                bool hasSelection,
                string unitId,
                string unitType,
                string ownerId,
                int hp,
                int maxHp,
                bool isBuildingOrResource)
            {
                HasSelection = hasSelection;
                Hp = hp;
                IsBuildingOrResource = isBuildingOrResource;
                MaxHp = maxHp <= 0 ? 1 : maxHp;
                OwnerId = ownerId ?? string.Empty;
                UnitId = unitId ?? string.Empty;
                UnitType = unitType ?? string.Empty;
            }
        }
    }
}
