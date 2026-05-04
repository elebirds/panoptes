using System;
using System.Collections.Generic;
using System.Linq;
using Panoptes.Core.Domain;

namespace Panoptes.Core.Application.Stores
{
    internal static class StoreSnapshotCloner
    {
        public static Dictionary<string, NodeDto> CloneNodes(IReadOnlyDictionary<string, NodeDto> source)
        {
            return CloneDictionary(source, CloneNode);
        }

        public static Dictionary<string, UnitDto> CloneUnits(IReadOnlyDictionary<string, UnitDto> source)
        {
            return CloneDictionary(source, CloneUnit);
        }

        public static Dictionary<string, CatalogBuildingDto> CloneCatalogBuildings(
            IReadOnlyDictionary<string, CatalogBuildingDto> source)
        {
            return CloneDictionary(source, CloneCatalogBuilding);
        }

        public static Dictionary<string, CatalogHudEntryDto> CloneCatalogHudEntries(
            IReadOnlyDictionary<string, CatalogHudEntryDto> source)
        {
            return CloneDictionary(source, CloneCatalogHudEntry);
        }

        public static Dictionary<string, CatalogRecipeDto> CloneCatalogRecipes(
            IReadOnlyDictionary<string, CatalogRecipeDto> source)
        {
            return CloneDictionary(source, CloneCatalogRecipe);
        }

        public static Dictionary<string, CatalogTechnologyDto> CloneCatalogTechnologies(
            IReadOnlyDictionary<string, CatalogTechnologyDto> source)
        {
            return CloneDictionary(source, CloneCatalogTechnology);
        }

        public static Dictionary<string, CatalogPolicyDto> CloneCatalogPolicies(
            IReadOnlyDictionary<string, CatalogPolicyDto> source)
        {
            return CloneDictionary(source, CloneCatalogPolicy);
        }

        public static Dictionary<string, CatalogUnitDto> CloneCatalogUnits(
            IReadOnlyDictionary<string, CatalogUnitDto> source)
        {
            return CloneDictionary(source, CloneCatalogUnit);
        }

        public static CatalogMapRuntimeBundleDto CloneCatalogMapRuntimeBundle(CatalogMapRuntimeBundleDto source)
        {
            if (source == null)
            {
                return null;
            }

            return new CatalogMapRuntimeBundleDto
            {
                Height = source.Height,
                Id = source.Id,
                Name = source.Name,
                Nodes = CloneCatalogMapRuntimeNodes(source.Nodes),
                Width = source.Width
            };
        }

        public static List<QueuedUnitOrderDto> CloneUnitOrders(IEnumerable<QueuedUnitOrderDto> source)
        {
            return CloneList(source, CloneQueuedUnitOrder);
        }

        public static List<QueuedBuildOrderDto> CloneBuildOrders(IEnumerable<QueuedBuildOrderDto> source)
        {
            return CloneList(source, CloneQueuedBuildOrder);
        }

        public static List<QueuedRecipeSelectionDto> CloneRecipeSelections(IEnumerable<QueuedRecipeSelectionDto> source)
        {
            return CloneList(source, CloneQueuedRecipeSelection);
        }

        public static List<QueuedWarZoneDirectiveDto> CloneWarZoneDirectives(IEnumerable<QueuedWarZoneDirectiveDto> source)
        {
            return CloneList(source, CloneWarZoneDirective);
        }

        public static List<PlanningWarZoneDto> CloneWarZones(IEnumerable<PlanningWarZoneDto> source)
        {
            return CloneList(source, CloneWarZone);
        }

        public static List<MinisterDraftDto> CloneMinisterDrafts(IEnumerable<MinisterDraftDto> source)
        {
            return CloneList(source, CloneMinisterDraft);
        }

        public static List<TurnEventDto> CloneTurnEvents(IEnumerable<TurnEventDto> source)
        {
            return CloneList(source, CloneTurnEvent);
        }

        public static TurnSettlementDto CloneTurnSettlement(TurnSettlementDto source)
        {
            if (source == null)
            {
                return null;
            }

            return new TurnSettlementDto
            {
                BuiltBuildings = CloneList(source.BuiltBuildings, CloneBuiltStructure),
                BuiltNodeIDs = CloneStrings(source.BuiltNodeIDs),
                CityCoreDamaged = source.CityCoreDamaged,
                DeadUnitIDs = CloneStrings(source.DeadUnitIDs),
                MovedUnitIDs = CloneStrings(source.MovedUnitIDs),
                NextPhase = source.NextPhase,
                Phase = source.Phase,
                Sections = CloneList(source.Sections, CloneSettlementSection)
            };
        }

        public static List<GameChatEntryDto> CloneGameChatEntries(IEnumerable<GameChatEntryDto> source)
        {
            return CloneList(source, CloneGameChatEntry);
        }

        public static List<string> CloneStrings(IEnumerable<string> source)
        {
            return source == null ? new List<string>() : source.Where(value => value != null).ToList();
        }

        public static ResourceDto CloneResource(ResourceDto source)
        {
            if (source == null)
            {
                return new ResourceDto();
            }

            return new ResourceDto
            {
                Food = source.Food,
                IndustryOutput = source.IndustryOutput,
                Ore = source.Ore,
                Wood = source.Wood,
                ResourceAmounts = CloneIntDictionary(source.ResourceAmounts),
                PointAmounts = CloneIntDictionary(source.PointAmounts)
            };
        }

        public static PathPreviewDto ClonePathPreview(PathPreviewDto source)
        {
            if (source == null)
            {
                return null;
            }

            return new PathPreviewDto
            {
                Action = source.Action,
                ErrorCode = source.ErrorCode,
                FirstTurnNodeId = source.FirstTurnNodeId,
                PathNodeIds = CloneStrings(source.PathNodeIds),
                RequestId = source.RequestId,
                TargetNodeId = source.TargetNodeId,
                TotalTurns = source.TotalTurns,
                TurnStops = CloneList(source.TurnStops, CloneMarchTurnStop),
                UnitId = source.UnitId,
                Valid = source.Valid
            };
        }

        public static BuildPreviewDto CloneBuildPreview(BuildPreviewDto source)
        {
            if (source == null)
            {
                return null;
            }

            return new BuildPreviewDto
            {
                BuildingTypeId = source.BuildingTypeId,
                CityId = source.CityId,
                Details = CloneStringDictionary(source.Details),
                ErrorCode = source.ErrorCode,
                Message = source.Message,
                NodeId = source.NodeId,
                RequestId = source.RequestId,
                Valid = source.Valid
            };
        }

        public static RecipePreviewDto CloneRecipePreview(RecipePreviewDto source)
        {
            if (source == null)
            {
                return null;
            }

            return new RecipePreviewDto
            {
                Details = CloneStringDictionary(source.Details),
                ErrorCode = source.ErrorCode,
                Message = source.Message,
                NodeId = source.NodeId,
                RecipeId = source.RecipeId,
                RequestId = source.RequestId,
                Valid = source.Valid
            };
        }

        private static Dictionary<string, TValue> CloneDictionary<TValue>(
            IReadOnlyDictionary<string, TValue> source,
            Func<TValue, TValue> clone)
        {
            var result = new Dictionary<string, TValue>(StringComparer.OrdinalIgnoreCase);
            if (source == null)
            {
                return result;
            }

            foreach (var pair in source)
            {
                if (pair.Key == null || pair.Value == null)
                {
                    continue;
                }

                result[pair.Key] = clone(pair.Value);
            }

            return result;
        }

        private static List<TValue> CloneList<TValue>(IEnumerable<TValue> source, Func<TValue, TValue> clone)
        {
            return source == null
                ? new List<TValue>()
                : source.Where(value => value != null).Select(clone).ToList();
        }

        private static NodeDto CloneNode(NodeDto source)
        {
            if (source == null)
            {
                return null;
            }

            return new NodeDto
            {
                BuildingHp = source.BuildingHp,
                BuildingMaxHp = source.BuildingMaxHp,
                BuildingStatus = source.BuildingStatus,
                BuildingType = source.BuildingType,
                CityId = source.CityId,
                HasRoad = source.HasRoad,
                Id = source.Id,
                IsCityCore = source.IsCityCore,
                IsMemory = source.IsMemory,
                IsResourcePoint = source.IsResourcePoint,
                IsSafeZone = source.IsSafeZone,
                IsVisible = source.IsVisible,
                LastObservedTurn = source.LastObservedTurn,
                OperationBaseProgress = source.OperationBaseProgress,
                OperationBlockedMessage = source.OperationBlockedMessage,
                OperationBlockedReason = source.OperationBlockedReason,
                OperationCurrentProgress = source.OperationCurrentProgress,
                OperationRequiredProgress = source.OperationRequiredProgress,
                OperationSelectedRecipeId = source.OperationSelectedRecipeId,
                Owner = source.Owner,
                Q = source.Q,
                R = source.R,
                ResourceType = source.ResourceType,
                ServiceCityId = source.ServiceCityId,
                TakeoverProgress = source.TakeoverProgress,
                TakeoverRequired = source.TakeoverRequired,
                Terrain = source.Terrain,
                TerritoryOwner = source.TerritoryOwner,
                Type = source.Type
            };
        }

        private static UnitDto CloneUnit(UnitDto source)
        {
            if (source == null)
            {
                return null;
            }

            return new UnitDto
            {
                Hp = source.Hp,
                Id = source.Id,
                MaxHp = source.MaxHp,
                Owner = source.Owner,
                Q = source.Q,
                R = source.R,
                Type = source.Type
            };
        }

        private static GameChatEntryDto CloneGameChatEntry(GameChatEntryDto source)
        {
            if (source == null)
            {
                return null;
            }

            return new GameChatEntryDto
            {
                Phase = source.Phase,
                Payload = CloneGameChatPayload(source.Payload),
                SenderPlayerId = source.SenderPlayerId,
                Sequence = source.Sequence,
                Turn = source.Turn
            };
        }

        private static SettlementSectionDto CloneSettlementSection(SettlementSectionDto source)
        {
            if (source == null)
            {
                return null;
            }

            return new SettlementSectionDto
            {
                Events = CloneTurnEvents(source.Events),
                Section = source.Section
            };
        }

        private static BuiltStructureDto CloneBuiltStructure(BuiltStructureDto source)
        {
            if (source == null)
            {
                return null;
            }

            return new BuiltStructureDto
            {
                BuildingHp = source.BuildingHp,
                BuildingType = source.BuildingType,
                CityId = source.CityId,
                NodeId = source.NodeId,
                OwnerId = source.OwnerId
            };
        }

        private static GameChatPayloadDto CloneGameChatPayload(GameChatPayloadDto source)
        {
            if (source == null)
            {
                return null;
            }

            return new GameChatPayloadDto
            {
                Emote = source.Emote,
                Kind = source.Kind,
                Text = source.Text
            };
        }

        private static QueuedUnitOrderDto CloneQueuedUnitOrder(QueuedUnitOrderDto source)
        {
            return new QueuedUnitOrderDto
            {
                Action = source.Action,
                FirstTurnNodeId = source.FirstTurnNodeId,
                Params = CloneStringDictionary(source.Params),
                PathNodeIds = CloneStrings(source.PathNodeIds),
                SecondaryNodeId = source.SecondaryNodeId,
                TargetNodeId = source.TargetNodeId,
                TargetUnitId = source.TargetUnitId,
                TotalTurns = source.TotalTurns,
                TurnStops = CloneList(source.TurnStops, CloneMarchTurnStop),
                UnitId = source.UnitId
            };
        }

        private static MarchTurnStopDto CloneMarchTurnStop(MarchTurnStopDto source)
        {
            return new MarchTurnStopDto
            {
                NodeId = source.NodeId,
                TurnIndex = source.TurnIndex
            };
        }

        private static QueuedBuildOrderDto CloneQueuedBuildOrder(QueuedBuildOrderDto source)
        {
            return new QueuedBuildOrderDto
            {
                BuildingTypeId = source.BuildingTypeId,
                CityId = source.CityId,
                NodeId = source.NodeId
            };
        }

        private static QueuedRecipeSelectionDto CloneQueuedRecipeSelection(QueuedRecipeSelectionDto source)
        {
            return new QueuedRecipeSelectionDto
            {
                NodeId = source.NodeId,
                RecipeId = source.RecipeId
            };
        }

        private static QueuedWarZoneDirectiveDto CloneWarZoneDirective(QueuedWarZoneDirectiveDto source)
        {
            return new QueuedWarZoneDirectiveDto
            {
                Directive = source.Directive,
                TargetNode = source.TargetNode,
                ZoneId = source.ZoneId
            };
        }

        private static PlanningWarZoneDto CloneWarZone(PlanningWarZoneDto source)
        {
            return new PlanningWarZoneDto
            {
                Directive = source.Directive,
                Id = source.Id,
                Name = source.Name,
                NodeIds = CloneStrings(source.NodeIds),
                TargetNode = source.TargetNode
            };
        }

        private static MinisterDraftDto CloneMinisterDraft(MinisterDraftDto source)
        {
            return new MinisterDraftDto
            {
                Available = source.Available,
                DraftId = source.DraftId,
                Kind = source.Kind,
                MinisterRole = source.MinisterRole,
                PlayerId = source.PlayerId,
                Rationale = source.Rationale,
                RiskNote = source.RiskNote,
                Source = source.Source,
                Status = source.Status,
                Summary = source.Summary,
                TargetId = source.TargetId,
                TargetLabel = source.TargetLabel,
                Title = source.Title,
                Turn = source.Turn
            };
        }

        private static TurnEventDto CloneTurnEvent(TurnEventDto source)
        {
            return new TurnEventDto
            {
                BlockedReasonMessage = source.BlockedReasonMessage,
                ConflictType = source.ConflictType,
                Damage = source.Damage,
                Data = CloneStringDictionary(source.Data),
                EnemyUnitId = source.EnemyUnitId,
                FromQ = source.FromQ,
                FromR = source.FromR,
                HpAfter = source.HpAfter,
                KillerId = source.KillerId,
                NodeId = source.NodeId,
                PosQ = source.PosQ,
                PosR = source.PosR,
                ReasonMessage = source.ReasonMessage,
                Section = source.Section,
                Sequence = source.Sequence,
                Source = source.Source,
                TargetUnitId = source.TargetUnitId,
                ToQ = source.ToQ,
                ToR = source.ToR,
                Type = source.Type,
                UnitId = source.UnitId
            };
        }

        private static CatalogBuildingDto CloneCatalogBuilding(CatalogBuildingDto source)
        {
            return new CatalogBuildingDto
            {
                BuildingScope = source.BuildingScope,
                DefaultRecipeId = source.DefaultRecipeId,
                Description = source.Description,
                IconKey = source.IconKey,
                Id = source.Id,
                MaxHp = source.MaxHp,
                Name = source.Name,
                PlacementKind = source.PlacementKind,
                PrefabKey = source.PrefabKey,
                RecipeIds = CloneStrings(source.RecipeIds),
                RequiredResourceType = source.RequiredResourceType,
                SortOrder = source.SortOrder,
                Tags = CloneStrings(source.Tags),
                TakeoverMode = source.TakeoverMode
            };
        }

        private static CatalogHudEntryDto CloneCatalogHudEntry(CatalogHudEntryDto source)
        {
            return new CatalogHudEntryDto
            {
                IconKey = source.IconKey,
                Key = source.Key,
                SortOrder = source.SortOrder,
                VisibleInHud = source.VisibleInHud
            };
        }

        private static CatalogRecipeDto CloneCatalogRecipe(CatalogRecipeDto source)
        {
            return new CatalogRecipeDto
            {
                BaseProgress = source.BaseProgress,
                BuildingId = source.BuildingId,
                Description = source.Description,
                IconKey = source.IconKey,
                Id = source.Id,
                Name = source.Name,
                Outputs = CloneCatalogRecipeOutputs(source.Outputs),
                PointInputs = CloneCatalogAmounts(source.PointInputs),
                ResourceInputs = CloneCatalogAmounts(source.ResourceInputs),
                SortOrder = source.SortOrder,
                Tags = CloneStrings(source.Tags),
                WorkAmount = source.WorkAmount
            };
        }

        private static CatalogTechnologyDto CloneCatalogTechnology(CatalogTechnologyDto source)
        {
            return new CatalogTechnologyDto
            {
                Branch = source.Branch,
                Description = source.Description,
                ExplicitEffects = CloneCatalogTechnologyEffects(source.ExplicitEffects),
                IconKey = source.IconKey,
                Id = source.Id,
                Name = source.Name,
                Prerequisites = CloneCatalogTechnologyPrerequisites(source.Prerequisites),
                ResearchCost = source.ResearchCost,
                SortOrder = source.SortOrder,
                Tags = CloneStrings(source.Tags),
                Tier = source.Tier
            };
        }

        private static CatalogPolicyDto CloneCatalogPolicy(CatalogPolicyDto source)
        {
            return new CatalogPolicyDto
            {
                ActivationTiming = source.ActivationTiming,
                BenefitDescription = source.BenefitDescription,
                Description = source.Description,
                IconKey = source.IconKey,
                Id = source.Id,
                Layer = source.Layer,
                Name = source.Name,
                NextActionDescription = source.NextActionDescription
            };
        }

        private static CatalogUnitDto CloneCatalogUnit(CatalogUnitDto source)
        {
            return new CatalogUnitDto
            {
                Attack = source.Attack,
                AttackRange = source.AttackRange,
                ChargeBonus = source.ChargeBonus,
                Class = source.Class,
                Description = source.Description,
                Flags = source.Flags == null ? null : new CatalogUnitFlagsDto
                {
                    CanAttackStructures = source.Flags.CanAttackStructures
                },
                IconKey = source.IconKey,
                Id = source.Id,
                MaxHp = source.MaxHp,
                MoveRange = source.MoveRange,
                Name = source.Name,
                PrefabKey = source.PrefabKey,
                RoadSpeedBonus = source.RoadSpeedBonus,
                Tags = CloneStrings(source.Tags),
                VisionRange = source.VisionRange
            };
        }

        private static List<CatalogMapRuntimeNodeDto> CloneCatalogMapRuntimeNodes(
            IEnumerable<CatalogMapRuntimeNodeDto> source)
        {
            return source == null
                ? new List<CatalogMapRuntimeNodeDto>()
                : source.Where(value => value != null)
                    .Select(value => new CatalogMapRuntimeNodeDto
                    {
                        BuildingHp = value.BuildingHp,
                        BuildingType = value.BuildingType,
                        HasRoad = value.HasRoad,
                        Id = value.Id,
                        IsResourcePoint = value.IsResourcePoint,
                        NodeName = value.NodeName,
                        Owner = value.Owner,
                        ResourceType = value.ResourceType,
                        Terrain = value.Terrain,
                        TerritoryOwner = value.TerritoryOwner,
                        X = value.X,
                        Y = value.Y
                    })
                    .ToList();
        }

        private static CatalogRecipeOutputsDto CloneCatalogRecipeOutputs(CatalogRecipeOutputsDto source)
        {
            if (source == null)
            {
                return null;
            }

            return new CatalogRecipeOutputsDto
            {
                PointProgress = CloneCatalogAmounts(source.PointProgress),
                Resources = CloneCatalogAmounts(source.Resources),
                StateChanges = CloneCatalogAmounts(source.StateChanges),
                Units = CloneStrings(source.Units)
            };
        }

        private static List<CatalogAmountDto> CloneCatalogAmounts(IEnumerable<CatalogAmountDto> source)
        {
            return source == null
                ? new List<CatalogAmountDto>()
                : source.Where(value => value != null)
                    .Select(value => new CatalogAmountDto
                    {
                        Amount = value.Amount,
                        Key = value.Key
                    })
                    .ToList();
        }

        private static List<CatalogTechnologyPrerequisiteDto> CloneCatalogTechnologyPrerequisites(
            IEnumerable<CatalogTechnologyPrerequisiteDto> source)
        {
            return source == null
                ? new List<CatalogTechnologyPrerequisiteDto>()
                : source.Where(value => value != null)
                    .Select(value => new CatalogTechnologyPrerequisiteDto
                    {
                        TargetId = value.TargetId,
                        Type = value.Type
                    })
                    .ToList();
        }

        private static List<CatalogTechnologyEffectDto> CloneCatalogTechnologyEffects(
            IEnumerable<CatalogTechnologyEffectDto> source)
        {
            return source == null
                ? new List<CatalogTechnologyEffectDto>()
                : source.Where(value => value != null)
                    .Select(value => new CatalogTechnologyEffectDto
                    {
                        InstitutionSlots = value.InstitutionSlots,
                        TargetId = value.TargetId,
                        Type = value.Type
                    })
                    .ToList();
        }

        private static Dictionary<string, string> CloneStringDictionary(IDictionary<string, string> source)
        {
            return source == null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(source, StringComparer.OrdinalIgnoreCase);
        }

        private static Dictionary<string, int> CloneIntDictionary(IDictionary<string, int> source)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (source == null)
            {
                return result;
            }

            foreach (var pair in source)
            {
                if (!string.IsNullOrWhiteSpace(pair.Key))
                {
                    result[pair.Key.Trim()] = pair.Value;
                }
            }

            return result;
        }
    }
}
