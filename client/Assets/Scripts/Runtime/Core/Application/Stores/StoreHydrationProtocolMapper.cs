using System;
using System.Collections.Generic;
using System.Linq;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Domain;
using Panoptes.Core.Infrastructure.Mapper;
using Panoptes.Protocol.V1;

namespace Panoptes.Core.Application.Stores
{
    public static class StoreHydrationProtocolMapper
    {
        private const string IndustryOutputPointKey = "industry_output";

        public static GameStateStoreState ToGameState(MsgGameInit msg)
        {
            if (msg == null)
            {
                return new GameStateStoreState();
            }

            return new GameStateStoreState(
                gameId: msg.GameId,
                activeGameSessionId: msg.GameId,
                myPlayerId: msg.YourPlayerId,
                turn: msg.Turn,
                phase: msg.Phase,
                mapWidth: msg.MapWidth,
                mapHeight: msg.MapHeight,
                isGameOver: false,
                tokensLeft: msg.MyPlayer?.TokensLeft ?? 0,
                nodes: MapNodes(msg.Nodes),
                units: MapUnits(msg.Units),
                myResources: MapResources(msg.MyPlayer));
        }

        public static GameStateStoreState ToGameState(GameStateCache cache)
        {
            if (cache == null)
            {
                return new GameStateStoreState();
            }

            var gameId = cache.GameID ?? string.Empty;
            var activeSessionId = string.IsNullOrWhiteSpace(cache.ActiveGameSessionID)
                ? gameId
                : cache.ActiveGameSessionID;

            return new GameStateStoreState(
                gameId: gameId,
                activeGameSessionId: activeSessionId,
                myPlayerId: cache.MyPlayerID,
                turn: cache.Turn,
                phase: cache.Phase,
                mapWidth: cache.MapWidth,
                mapHeight: cache.MapHeight,
                isGameOver: cache.IsGameOver,
                tokensLeft: cache.TokensLeft,
                nodes: cache.Nodes,
                units: cache.Units,
                myResources: MapResources(cache.MyPlayer));
        }

        public static GameStateStoreState MergePlanningStart(GameStateStoreState current, MsgPlanningStart msg)
        {
            var previous = current ?? new GameStateStoreState();
            if (msg == null)
            {
                return previous.Clone();
            }

            return new GameStateStoreState(
                gameId: previous.GameId,
                activeGameSessionId: previous.ActiveGameSessionId,
                myPlayerId: previous.MyPlayerId,
                turn: msg.Turn > 0 ? msg.Turn : previous.Turn,
                phase: string.IsNullOrWhiteSpace(msg.Phase) ? previous.Phase : msg.Phase,
                mapWidth: previous.MapWidth,
                mapHeight: previous.MapHeight,
                isGameOver: false,
                tokensLeft: msg.Tokens,
                nodes: msg.Nodes != null && msg.Nodes.Count > 0 ? MapNodes(msg.Nodes, previous.Nodes) : previous.Nodes,
                units: msg.Units != null && msg.Units.Count > 0 ? MapUnits(msg.Units, previous.Units) : previous.Units,
                myResources: msg.MyPlayer != null ? MapResources(msg.MyPlayer) : previous.MyResources);
        }

        public static GameStateStoreState MergeGameSync(GameStateStoreState current, MsgGameSync msg)
        {
            var previous = current ?? new GameStateStoreState();
            if (msg == null)
            {
                return previous.Clone();
            }

            return new GameStateStoreState(
                gameId: previous.GameId,
                activeGameSessionId: previous.ActiveGameSessionId,
                myPlayerId: previous.MyPlayerId,
                turn: msg.Turn > 0 ? msg.Turn : previous.Turn,
                phase: string.IsNullOrWhiteSpace(msg.Phase) ? previous.Phase : msg.Phase,
                mapWidth: previous.MapWidth,
                mapHeight: previous.MapHeight,
                isGameOver: previous.IsGameOver,
                tokensLeft: msg.MyPlayer != null ? msg.MyPlayer.TokensLeft : previous.TokensLeft,
                nodes: msg.Nodes != null && msg.Nodes.Count > 0 ? MapNodes(msg.Nodes, previous.Nodes) : previous.Nodes,
                units: msg.Units != null && msg.Units.Count > 0 ? MapUnits(msg.Units, previous.Units) : previous.Units,
                myResources: msg.MyPlayer != null ? MapResources(msg.MyPlayer) : previous.MyResources);
        }

        public static GameStateStoreState MergeTokenResult(GameStateStoreState current, MsgTokenResult msg)
        {
            var previous = current ?? new GameStateStoreState();
            if (msg == null || !msg.Success)
            {
                return previous.Clone();
            }

            return WithTokens(previous, msg.TokensLeft);
        }

        public static GameStateStoreState MergeRevealResult(GameStateStoreState current, MsgRevealResult msg)
        {
            var previous = current ?? new GameStateStoreState();
            if (msg == null)
            {
                return previous.Clone();
            }

            var nextNodes = CloneNodes(previous.Nodes);
            var revealed = MapNode(msg.TrueState, TryGetNode(previous.Nodes, msg.NodeId));
            if (revealed != null)
            {
                var key = string.IsNullOrWhiteSpace(revealed.Id) ? msg.NodeId : revealed.Id;
                if (!string.IsNullOrWhiteSpace(key))
                {
                    nextNodes[key] = revealed;
                }
            }

            return new GameStateStoreState(
                previous.GameId,
                previous.ActiveGameSessionId,
                previous.MyPlayerId,
                previous.Turn,
                previous.Phase,
                previous.MapWidth,
                previous.MapHeight,
                previous.IsGameOver,
                msg.TokensLeft > 0 ? msg.TokensLeft : previous.TokensLeft,
                nextNodes,
                previous.Units,
                previous.MyResources);
        }

        public static GameStateStoreState MergeGameOver(GameStateStoreState current)
        {
            var previous = current ?? new GameStateStoreState();
            return new GameStateStoreState(
                previous.GameId,
                previous.ActiveGameSessionId,
                previous.MyPlayerId,
                previous.Turn,
                previous.Phase,
                previous.MapWidth,
                previous.MapHeight,
                isGameOver: true,
                tokensLeft: previous.TokensLeft,
                nodes: previous.Nodes,
                units: previous.Units,
                myResources: previous.MyResources);
        }

        public static PlanningDraftState ToPlanningDraft(MsgPlanningSnapshot msg)
        {
            if (msg == null)
            {
                return new PlanningDraftState();
            }

            return new PlanningDraftState(
                snapshotTurn: msg.Turn,
                snapshotPhase: msg.Phase,
                unitOrders: MapUnitOrders(msg.UnitOrders),
                buildOrders: MapBuildOrders(msg.BuildOrders),
                recipeSelections: MapRecipeSelections(msg.RecipeSelections),
                warZoneDirectives: MapWarZoneDirectives(msg.WarZoneDirectives),
                warZones: MapWarZones(msg.WarZones),
                ministerDrafts: MapMinisterDrafts(msg.MinisterDrafts),
                plannedResearchTargetTechnologyId: msg.PlannedResearchTargetTechnologyId,
                plannedNationalPolicyId: msg.PlannedNationalPolicyId,
                plannedInstitutionPolicyIds: msg.PlannedInstitutionPolicyIds);
        }

        public static PlanningDraftState MergePathPreview(PlanningDraftState current, MsgPlanningPathPreviewResponse msg)
        {
            var previous = current ?? new PlanningDraftState();
            return new PlanningDraftState(
                previous.SnapshotTurn,
                previous.SnapshotPhase,
                previous.UnitOrders,
                previous.BuildOrders,
                previous.RecipeSelections,
                previous.WarZoneDirectives,
                previous.WarZones,
                previous.MinisterDrafts,
                msg != null ? MapPathPreview(msg) : previous.CurrentPreview,
                previous.CurrentBuildPreview,
                previous.CurrentRecipePreview,
                previous.PlannedResearchTargetTechnologyId,
                previous.PlannedNationalPolicyId,
                previous.PlannedInstitutionPolicyIds);
        }

        public static PlanningDraftState MergeBuildPreview(PlanningDraftState current, MsgBuildStructurePreviewResponse msg)
        {
            var previous = current ?? new PlanningDraftState();
            return new PlanningDraftState(
                previous.SnapshotTurn,
                previous.SnapshotPhase,
                previous.UnitOrders,
                previous.BuildOrders,
                previous.RecipeSelections,
                previous.WarZoneDirectives,
                previous.WarZones,
                previous.MinisterDrafts,
                previous.CurrentPreview,
                msg != null ? MapBuildPreview(msg) : previous.CurrentBuildPreview,
                previous.CurrentRecipePreview,
                previous.PlannedResearchTargetTechnologyId,
                previous.PlannedNationalPolicyId,
                previous.PlannedInstitutionPolicyIds);
        }

        public static PlanningDraftState MergeRecipePreview(PlanningDraftState current, MsgSetBuildingRecipePreviewResponse msg)
        {
            var previous = current ?? new PlanningDraftState();
            return new PlanningDraftState(
                previous.SnapshotTurn,
                previous.SnapshotPhase,
                previous.UnitOrders,
                previous.BuildOrders,
                previous.RecipeSelections,
                previous.WarZoneDirectives,
                previous.WarZones,
                previous.MinisterDrafts,
                previous.CurrentPreview,
                previous.CurrentBuildPreview,
                msg != null ? MapRecipePreview(msg) : previous.CurrentRecipePreview,
                previous.PlannedResearchTargetTechnologyId,
                previous.PlannedNationalPolicyId,
                previous.PlannedInstitutionPolicyIds);
        }

        public static TurnState ToTurn(MsgGameInit msg)
        {
            if (msg == null)
            {
                return new TurnState();
            }

            return new TurnState(
                msg.Turn,
                msg.Phase,
                msg.MyPlayer?.TokensLeft ?? 0,
                null,
                false,
                0,
                string.Empty,
                GamePhases.IsPlanning(msg.Phase));
        }

        public static TurnState ToTurn(GameStateCache cache)
        {
            if (cache == null)
            {
                return new TurnState();
            }

            return new TurnState(
                cache.Turn,
                cache.Phase,
                cache.TokensLeft,
                cache.LastPlanningStartEvents,
                cache.IsGameOver,
                0,
                string.Empty,
                GamePhases.IsPlanning(cache.Phase) && !cache.IsGameOver);
        }

        public static TurnState ToTurn(MsgPlanningStart msg)
        {
            if (msg == null)
            {
                return new TurnState();
            }

            return new TurnState(
                msg.Turn,
                msg.Phase,
                msg.Tokens,
                SettlementMapper.ToPlanningStartEvents(msg),
                false,
                msg.Timeout,
                string.Empty,
                GamePhases.IsPlanning(msg.Phase));
        }

        public static TurnState MergeTurn(TurnState current, MsgPlanningSnapshot msg)
        {
            var previous = current ?? new TurnState();
            if (msg == null)
            {
                return previous.Clone();
            }

            return new TurnState(
                msg.Turn > 0 ? msg.Turn : previous.Turn,
                string.IsNullOrWhiteSpace(msg.Phase) ? previous.Phase : msg.Phase,
                previous.TokensLeft,
                previous.PlanningStartEvents,
                previous.IsGameOver,
                previous.TimeoutSeconds,
                previous.NextPhase,
                previous.IsInteractive);
        }

        public static TurnState MergeTurn(TurnState current, MsgGameSync msg)
        {
            var previous = current ?? new TurnState();
            if (msg == null)
            {
                return previous.Clone();
            }

            return new TurnState(
                msg.Turn > 0 ? msg.Turn : previous.Turn,
                string.IsNullOrWhiteSpace(msg.Phase) ? previous.Phase : msg.Phase,
                msg.MyPlayer != null ? msg.MyPlayer.TokensLeft : previous.TokensLeft,
                previous.PlanningStartEvents,
                previous.IsGameOver,
                0,
                msg.NextPhase,
                GamePhases.IsPlanning(msg.Phase) && !previous.IsGameOver);
        }

        public static TurnState MergeTurn(TurnState current, MsgTokenResult msg)
        {
            var previous = current ?? new TurnState();
            return msg != null && msg.Success
                ? new TurnState(
                    previous.Turn,
                    previous.Phase,
                    msg.TokensLeft,
                    previous.PlanningStartEvents,
                    previous.IsGameOver,
                    previous.TimeoutSeconds,
                    previous.NextPhase,
                    previous.IsInteractive)
                : previous.Clone();
        }

        public static TurnState MergeTurn(TurnState current, MsgRevealResult msg)
        {
            var previous = current ?? new TurnState();
            return msg != null && msg.TokensLeft > 0
                ? new TurnState(
                    previous.Turn,
                    previous.Phase,
                    msg.TokensLeft,
                    previous.PlanningStartEvents,
                    previous.IsGameOver,
                    previous.TimeoutSeconds,
                    previous.NextPhase,
                    previous.IsInteractive)
                : previous.Clone();
        }

        public static TurnState MergeGameOver(TurnState current)
        {
            var previous = current ?? new TurnState();
            return new TurnState(
                previous.Turn,
                previous.Phase,
                previous.TokensLeft,
                previous.PlanningStartEvents,
                true,
                0,
                previous.NextPhase,
                false);
        }

        private static GameStateStoreState WithTokens(GameStateStoreState previous, int tokensLeft)
        {
            return new GameStateStoreState(
                previous.GameId,
                previous.ActiveGameSessionId,
                previous.MyPlayerId,
                previous.Turn,
                previous.Phase,
                previous.MapWidth,
                previous.MapHeight,
                previous.IsGameOver,
                tokensLeft,
                previous.Nodes,
                previous.Units,
                previous.MyResources);
        }

        private static Dictionary<string, NodeDto> MapNodes(IEnumerable<NodeView> views, IReadOnlyDictionary<string, NodeDto> existing = null)
        {
            var result = new Dictionary<string, NodeDto>(StringComparer.OrdinalIgnoreCase);
            if (views == null)
            {
                return result;
            }

            foreach (var view in views)
            {
                var node = MapNode(view, TryGetNode(existing, view?.Id));
                if (node != null && !string.IsNullOrWhiteSpace(node.Id))
                {
                    result[node.Id] = node;
                }
            }

            return result;
        }

        private static Dictionary<string, UnitDto> MapUnits(IEnumerable<UnitView> views, IReadOnlyDictionary<string, UnitDto> existing = null)
        {
            var result = new Dictionary<string, UnitDto>(StringComparer.OrdinalIgnoreCase);
            if (views == null)
            {
                return result;
            }

            foreach (var view in views)
            {
                var unit = MapUnit(view, TryGetUnit(existing, view?.Id));
                if (unit != null && !string.IsNullOrWhiteSpace(unit.Id))
                {
                    result[unit.Id] = unit;
                }
            }

            return result;
        }

        private static NodeDto MapNode(NodeView view, NodeDto existing = null)
        {
            if (view == null)
            {
                return null;
            }

            return new NodeDto
            {
                Id = view.Id,
                Q = view.Pos?.Q ?? existing?.Q ?? 0,
                R = view.Pos?.R ?? existing?.R ?? 0,
                Type = view.Terrain,
                Owner = view.ControllerPlayerId,
                TerritoryOwner = string.IsNullOrWhiteSpace(view.TerritoryOwnerPlayerId)
                    ? view.ControllerPlayerId
                    : view.TerritoryOwnerPlayerId,
                BuildingType = view.BuildingTypeId,
                BuildingHp = view.BuildingHp,
                BuildingMaxHp = existing?.BuildingMaxHp ?? 0,
                BuildingStatus = view.BuildingStatus,
                OperationSelectedRecipeId = view.Operation != null ? view.Operation.SelectedRecipeId : string.Empty,
                OperationCurrentProgress = view.Operation != null ? view.Operation.CurrentProgress : 0,
                OperationRequiredProgress = view.Operation != null ? view.Operation.RequiredProgress : 0,
                OperationBaseProgress = view.Operation != null ? view.Operation.BaseProgress : 0,
                OperationBlockedReason = view.Operation != null ? view.Operation.BlockedReason : string.Empty,
                OperationBlockedMessage = view.Operation != null ? view.Operation.BlockedMessage : string.Empty,
                CityId = view.CityId,
                ServiceCityId = view.ServiceCityId,
                TakeoverProgress = view.TakeoverProgress,
                TakeoverRequired = view.TakeoverRequired,
                IsCityCore = view.IsCityCore,
                IsVisible = view.IsCurrentlyVisible,
                IsMemory = view.IsMemory,
                LastObservedTurn = view.LastObservedTurn,
                HasRoad = view.HasRoad,
                Terrain = view.Terrain,
                IsResourcePoint = view.IsResourcePoint,
                ResourceType = view.ResourceType,
                IsSafeZone = view.IsSafeZone
            };
        }

        private static UnitDto MapUnit(UnitView view, UnitDto existing = null)
        {
            if (view == null)
            {
                return null;
            }

            var maxHp = view.MaxHp > 0 ? view.MaxHp : existing?.MaxHp ?? view.Hp;
            return new UnitDto
            {
                Id = view.Id,
                Type = view.UnitType,
                Owner = view.Faction,
                Q = view.Pos?.Q ?? existing?.Q ?? 0,
                R = view.Pos?.R ?? existing?.R ?? 0,
                Hp = view.Hp,
                MaxHp = maxHp
            };
        }

        private static ResourceDto MapResources(PlayerView player)
        {
            var resources = new ResourceDto
            {
                ResourceAmounts = MapResourceAmounts(player?.Resources),
                PointAmounts = MapPointAmounts(player?.Points)
            };
            ApplyFixedResourceFields(resources);
            return resources;
        }

        private static Dictionary<string, int> MapResourceAmounts(ResourceBag bag)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (bag?.Items == null)
            {
                return result;
            }

            foreach (var item in bag.Items)
            {
                AddAmount(result, item?.Key, item?.Amount ?? 0);
            }

            return result;
        }

        private static Dictionary<string, int> MapPointAmounts(PointBag points)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (points?.Items == null)
            {
                return result;
            }

            foreach (var item in points.Items)
            {
                AddAmount(result, item?.Key, item?.Amount ?? 0);
            }

            return result;
        }

        private static void AddAmount(Dictionary<string, int> values, string key, int amount)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            values[key.Trim()] = amount;
        }

        private static void ApplyFixedResourceFields(ResourceDto resources)
        {
            if (resources == null)
            {
                return;
            }

            resources.ResourceAmounts ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            resources.PointAmounts ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            resources.ResourceAmounts.TryGetValue(ResourceKeys.ResourceOre, out resources.Ore);
            resources.ResourceAmounts.TryGetValue(ResourceKeys.ResourceWood, out resources.Wood);
            resources.ResourceAmounts.TryGetValue(ResourceKeys.ResourceFood, out resources.Food);
            resources.PointAmounts.TryGetValue(IndustryOutputPointKey, out resources.IndustryOutput);
        }

        private static List<QueuedUnitOrderDto> MapUnitOrders(IEnumerable<QueuedUnitOrder> orders)
        {
            return orders == null
                ? new List<QueuedUnitOrderDto>()
                : orders
                    .Where(order => order != null && !string.IsNullOrWhiteSpace(order.UnitId))
                    .Select(order => new QueuedUnitOrderDto
                    {
                        UnitId = order.UnitId,
                        Action = order.Action,
                        TargetNodeId = order.TargetNodeId,
                        TargetUnitId = order.TargetUnitId,
                        SecondaryNodeId = order.SecondaryNodeId,
                        Params = order.Params != null
                            ? order.Params.ToDictionary(pair => pair.Key, pair => pair.Value)
                            : new Dictionary<string, string>(),
                        PathNodeIds = order.PathNodeIds != null ? order.PathNodeIds.ToList() : new List<string>(),
                        FirstTurnNodeId = order.FirstTurnNodeId,
                        TotalTurns = order.TotalTurns,
                        TurnStops = MapTurnStops(order.TurnStops)
                    })
                    .OrderBy(order => order.UnitId, StringComparer.OrdinalIgnoreCase)
                    .ToList();
        }

        private static List<QueuedBuildOrderDto> MapBuildOrders(IEnumerable<QueuedBuildOrder> orders)
        {
            return orders == null
                ? new List<QueuedBuildOrderDto>()
                : orders
                    .Where(order => order != null && !string.IsNullOrWhiteSpace(order.NodeId))
                    .Select(order => new QueuedBuildOrderDto
                    {
                        NodeId = order.NodeId,
                        BuildingTypeId = order.BuildingTypeId,
                        CityId = order.CityId
                    })
                    .ToList();
        }

        private static List<QueuedRecipeSelectionDto> MapRecipeSelections(IEnumerable<QueuedRecipeSelection> selections)
        {
            return selections == null
                ? new List<QueuedRecipeSelectionDto>()
                : selections
                    .Where(selection => selection != null && !string.IsNullOrWhiteSpace(selection.NodeId))
                    .Select(selection => new QueuedRecipeSelectionDto
                    {
                        NodeId = selection.NodeId,
                        RecipeId = selection.RecipeId
                    })
                    .ToList();
        }

        private static List<QueuedWarZoneDirectiveDto> MapWarZoneDirectives(IEnumerable<QueuedWarZoneDirective> directives)
        {
            return directives == null
                ? new List<QueuedWarZoneDirectiveDto>()
                : directives
                    .Where(directive => directive != null && !string.IsNullOrWhiteSpace(directive.ZoneId))
                    .Select(directive => new QueuedWarZoneDirectiveDto
                    {
                        ZoneId = directive.ZoneId,
                        Directive = directive.Directive,
                        TargetNode = directive.TargetNode
                    })
                    .ToList();
        }

        private static List<PlanningWarZoneDto> MapWarZones(IEnumerable<WarZone> zones)
        {
            return zones == null
                ? new List<PlanningWarZoneDto>()
                : zones
                    .Where(zone => zone != null && !string.IsNullOrWhiteSpace(zone.Id))
                    .Select(zone => new PlanningWarZoneDto
                    {
                        Id = zone.Id,
                        Name = zone.Name,
                        NodeIds = zone.NodeIds != null ? zone.NodeIds.ToList() : new List<string>(),
                        Directive = zone.Directive,
                        TargetNode = zone.TargetNode
                    })
                    .ToList();
        }

        private static List<MinisterDraftDto> MapMinisterDrafts(IEnumerable<MinisterDraftView> drafts)
        {
            var result = new List<MinisterDraftDto>();
            if (drafts == null)
            {
                return result;
            }

            foreach (var draft in drafts)
            {
                var mapped = MinisterMapper.ToDto(draft);
                if (mapped != null)
                {
                    result.Add(mapped);
                }
            }

            return result;
        }

        private static PathPreviewDto MapPathPreview(MsgPlanningPathPreviewResponse msg)
        {
            return new PathPreviewDto
            {
                RequestId = msg.RequestId,
                UnitId = msg.UnitId,
                Action = msg.Action,
                TargetNodeId = msg.TargetNodeId,
                Valid = msg.Valid,
                ErrorCode = msg.ErrorCode,
                PathNodeIds = msg.PathNodeIds != null ? msg.PathNodeIds.ToList() : new List<string>(),
                FirstTurnNodeId = msg.FirstTurnNodeId,
                TotalTurns = msg.TotalTurns,
                TurnStops = MapTurnStops(msg.TurnStops)
            };
        }

        private static BuildPreviewDto MapBuildPreview(MsgBuildStructurePreviewResponse msg)
        {
            return new BuildPreviewDto
            {
                RequestId = msg.RequestId,
                NodeId = msg.NodeId,
                BuildingTypeId = msg.BuildingTypeId,
                CityId = msg.CityId,
                Valid = msg.Valid,
                ErrorCode = msg.ErrorCode,
                Message = msg.FeedbackMessage,
                Details = MapFeedbackDetails(msg.FeedbackDetails)
            };
        }

        private static RecipePreviewDto MapRecipePreview(MsgSetBuildingRecipePreviewResponse msg)
        {
            return new RecipePreviewDto
            {
                RequestId = msg.RequestId,
                NodeId = msg.NodeId,
                RecipeId = msg.RecipeId,
                Valid = msg.Valid,
                ErrorCode = msg.ErrorCode,
                Message = msg.FeedbackMessage,
                Details = MapFeedbackDetails(msg.FeedbackDetails)
            };
        }

        private static List<MarchTurnStopDto> MapTurnStops(IEnumerable<MarchTurnStop> turnStops)
        {
            return turnStops == null
                ? new List<MarchTurnStopDto>()
                : turnStops
                    .Where(stop => stop != null)
                    .Select(stop => new MarchTurnStopDto
                    {
                        TurnIndex = stop.TurnIndex,
                        NodeId = stop.NodeId
                    })
                    .ToList();
        }

        private static Dictionary<string, string> MapFeedbackDetails(IEnumerable<FeedbackDetail> details)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (details == null)
            {
                return result;
            }

            foreach (var detail in details)
            {
                if (detail == null || string.IsNullOrWhiteSpace(detail.Key))
                {
                    continue;
                }

                result[detail.Key] = detail.Value ?? string.Empty;
            }

            return result;
        }

        private static Dictionary<string, NodeDto> CloneNodes(IReadOnlyDictionary<string, NodeDto> source)
        {
            var result = new Dictionary<string, NodeDto>(StringComparer.OrdinalIgnoreCase);
            if (source == null)
            {
                return result;
            }

            foreach (var pair in source)
            {
                if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value != null)
                {
                    result[pair.Key] = pair.Value;
                }
            }

            return result;
        }

        private static NodeDto TryGetNode(IReadOnlyDictionary<string, NodeDto> nodes, string nodeId)
        {
            if (nodes == null || string.IsNullOrWhiteSpace(nodeId))
            {
                return null;
            }

            return nodes.TryGetValue(nodeId, out var node) ? node : null;
        }

        private static UnitDto TryGetUnit(IReadOnlyDictionary<string, UnitDto> units, string unitId)
        {
            if (units == null || string.IsNullOrWhiteSpace(unitId))
            {
                return null;
            }

            return units.TryGetValue(unitId, out var unit) ? unit : null;
        }
    }
}
