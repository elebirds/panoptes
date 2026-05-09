using System;
using System.Collections.Generic;
using System.Globalization;
using Panoptes.Core.Domain;
using Panoptes.Protocol.V1;
using UnityEngine;

namespace Panoptes.Core.Infrastructure.Mapper
{
    public static class MinisterMapper
    {
        public static MinisterProfileDto ToProfileDto(MinisterView view)
        {
            if (view == null)
            {
                return null;
            }

            return new MinisterProfileDto
            {
                MinisterId = view.MinisterId ?? string.Empty,
                Role = view.Role ?? string.Empty,
                Name = view.Name ?? string.Empty,
                IconKey = view.IconKey ?? string.Empty,
                PersonalityDesc = view.PersonalityDesc ?? string.Empty,
                Ability = view.Ability,
                Personality = view.Personality ?? string.Empty,
                Loyalty = view.Loyalty,
                Ambition = view.Ambition,
                Cautiousness = view.Cautiousness,
                Decisiveness = view.Decisiveness,
                LoyaltyTendency = view.LoyaltyTendency,
                AmbitionStyle = view.AmbitionStyle,
                IsVacant = view.Vacant
            };
        }

        public static MinisterProfileDto ToProfileDto(MinisterCandidateView view)
        {
            if (view == null)
            {
                return null;
            }

            return new MinisterProfileDto
            {
                MinisterId = view.MinisterId ?? string.Empty,
                Role = view.Role ?? string.Empty,
                Name = view.Name ?? string.Empty,
                IconKey = view.IconKey ?? string.Empty,
                PersonalityDesc = view.PersonalityDesc ?? string.Empty,
                Ability = view.Ability,
                Personality = view.Personality ?? string.Empty,
                Loyalty = view.Loyalty,
                Ambition = view.Ambition,
                Cautiousness = view.Cautiousness,
                Decisiveness = view.Decisiveness,
                LoyaltyTendency = view.LoyaltyTendency,
                AmbitionStyle = view.AmbitionStyle,
                IsCandidate = true
            };
        }

        public static MinisterMetricDto ToDto(MetricItem item)
        {
            if (item == null)
            {
                return null;
            }

            return new MinisterMetricDto
            {
                // Current protocol exposes label/value as strings.
                // Keep Key aligned with label for downstream UI grouping.
                Key = item.Label,
                Value = ParseMetricValue(item.Value),
                Label = item.Label,
            };
        }

        private static float ParseMetricValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0f;
            }

            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            if (float.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed))
            {
                return parsed;
            }

            return 0f;
        }

        public static List<MinisterMetricDto> ToDtoList(IEnumerable<MetricItem> items)
        {
            var result = new List<MinisterMetricDto>();
            if (items == null)
            {
                return result;
            }

            foreach (var item in items)
            {
                var dto = ToDto(item);
                if (dto != null)
                {
                    result.Add(dto);
                }
            }

            return result;
        }

        public static MinisterDraftDto ToDto(MinisterDraftView view)
        {
            if (view == null || string.IsNullOrWhiteSpace(view.JsonPayload))
            {
                return null;
            }

            MinisterDraftPayload payload;
            try
            {
                payload = JsonUtility.FromJson<MinisterDraftPayload>(view.JsonPayload);
            }
            catch (ArgumentException)
            {
                return null;
            }

            if (payload == null || string.IsNullOrWhiteSpace(payload.draft_id))
            {
                return null;
            }

            return PayloadToDto(payload, view.MinisterRole, view.Available);
        }

        public static MinisterDraftDto ToDto(MinisterProposalView view)
        {
            if (view == null)
            {
                return null;
            }

            MinisterDraftPayload payload = null;
            if (!string.IsNullOrWhiteSpace(view.RawJson))
            {
                try
                {
                    payload = JsonUtility.FromJson<MinisterDraftPayload>(view.RawJson);
                }
                catch (ArgumentException)
                {
                    payload = null;
                }
            }

            var dto = payload != null && !string.IsNullOrWhiteSpace(payload.draft_id)
                ? PayloadToDto(payload, view.MinisterRole, true)
                : new MinisterDraftDto
                {
                    DraftId = view.ProposalId ?? string.Empty,
                    MinisterRole = view.MinisterRole ?? string.Empty,
                    Kind = view.Kind ?? string.Empty,
                    Title = view.Title ?? string.Empty,
                    Summary = view.Summary ?? string.Empty,
                    Rationale = view.Rationale ?? string.Empty,
                    RiskNote = view.RiskNote ?? string.Empty,
                    Status = "pending",
                    Available = true,
                    Source = "minister_proposal",
                    OperationId = view.OperationId ?? string.Empty,
                    Objective = view.Objective ?? string.Empty
                };

            dto.DraftId = string.IsNullOrWhiteSpace(dto.DraftId) ? view.ProposalId ?? string.Empty : dto.DraftId;
            dto.MinisterRole = string.IsNullOrWhiteSpace(dto.MinisterRole) ? view.MinisterRole ?? string.Empty : dto.MinisterRole;
            dto.Kind = string.IsNullOrWhiteSpace(dto.Kind) ? view.Kind ?? string.Empty : dto.Kind;
            dto.Title = string.IsNullOrWhiteSpace(dto.Title) ? view.Title ?? string.Empty : dto.Title;
            dto.Summary = string.IsNullOrWhiteSpace(dto.Summary) ? view.Summary ?? string.Empty : dto.Summary;
            dto.Rationale = string.IsNullOrWhiteSpace(dto.Rationale) ? view.Rationale ?? string.Empty : dto.Rationale;
            dto.RiskNote = string.IsNullOrWhiteSpace(dto.RiskNote) ? view.RiskNote ?? string.Empty : dto.RiskNote;
            dto.OperationId = string.IsNullOrWhiteSpace(dto.OperationId) ? view.OperationId ?? string.Empty : dto.OperationId;
            dto.Objective = string.IsNullOrWhiteSpace(dto.Objective) ? view.Objective ?? string.Empty : dto.Objective;
            var typedCommands = MapOperationCommands(view.OperationCommands);
            if (typedCommands.Count > 0)
            {
                dto.OperationCommands = typedCommands;
            }
            return string.IsNullOrWhiteSpace(dto.DraftId) ? null : dto;
        }

        private static MinisterDraftDto PayloadToDto(MinisterDraftPayload payload, string roleFallback, bool availableFallback)
        {
            return new MinisterDraftDto
            {
                DraftId = payload.draft_id ?? string.Empty,
                PlayerId = payload.player_id ?? string.Empty,
                MinisterRole = !string.IsNullOrWhiteSpace(payload.minister_role)
                    ? payload.minister_role
                    : (roleFallback ?? string.Empty),
                Kind = payload.kind ?? string.Empty,
                TargetId = payload.target_id ?? string.Empty,
                TargetLabel = payload.target_label ?? string.Empty,
                Title = payload.title ?? string.Empty,
                Summary = payload.summary ?? string.Empty,
                Rationale = payload.rationale ?? string.Empty,
                RiskNote = payload.risk_note ?? string.Empty,
                Status = payload.status ?? string.Empty,
                Available = availableFallback && payload.available,
                Turn = payload.turn,
                Source = payload.source ?? string.Empty,
                InstitutionIds = payload.institution_ids ?? Array.Empty<string>(),
                NodeId = payload.node_id ?? string.Empty,
                BuildingTypeId = payload.building_type_id ?? string.Empty,
                CityId = payload.city_id ?? string.Empty,
                RecipeId = payload.recipe_id ?? string.Empty,
                UnitId = payload.unit_id ?? string.Empty,
                Action = payload.action ?? string.Empty,
                TargetNodeId = payload.target_node_id ?? string.Empty,
                TargetUnitId = payload.target_unit_id ?? string.Empty,
                SecondaryNodeId = payload.secondary_node_id ?? string.Empty,
                OperationId = payload.operation_id ?? string.Empty,
                Objective = payload.objective ?? string.Empty,
                OperationCommands = MapOperationSteps(payload.operation_steps)
            };
        }

        private static List<MinisterOperationCommandDto> MapOperationSteps(IEnumerable<MinisterDraftPayload> steps)
        {
            var result = new List<MinisterOperationCommandDto>();
            if (steps == null)
            {
                return result;
            }

            foreach (var step in steps)
            {
                if (step == null)
                {
                    continue;
                }

                result.Add(new MinisterOperationCommandDto
                {
                    Label = !string.IsNullOrWhiteSpace(step.target_label) ? step.target_label : step.title ?? string.Empty,
                    Kind = step.kind ?? string.Empty,
                    NodeId = step.node_id ?? string.Empty,
                    BuildingTypeId = step.building_type_id ?? string.Empty,
                    CityId = step.city_id ?? string.Empty,
                    RecipeId = step.recipe_id ?? string.Empty,
                    UnitId = step.unit_id ?? string.Empty,
                    Action = step.action ?? string.Empty,
                    TargetNodeId = step.target_node_id ?? string.Empty,
                    TargetUnitId = step.target_unit_id ?? string.Empty,
                    SecondaryNodeId = step.secondary_node_id ?? string.Empty
                });
            }

            return result;
        }

        private static List<MinisterOperationCommandDto> MapOperationCommands(IEnumerable<MinisterOperationCommandView> commands)
        {
            var result = new List<MinisterOperationCommandDto>();
            if (commands == null)
            {
                return result;
            }

            foreach (var command in commands)
            {
                var mapped = MapOperationCommand(command);
                if (mapped != null)
                {
                    result.Add(mapped);
                }
            }

            return result;
        }

        private static MinisterOperationCommandDto MapOperationCommand(MinisterOperationCommandView view)
        {
            if (view == null)
            {
                return null;
            }

            MinisterDraftPayload payload = null;
            if (!string.IsNullOrWhiteSpace(view.RawJson))
            {
                try
                {
                    payload = JsonUtility.FromJson<MinisterDraftPayload>(view.RawJson);
                }
                catch (ArgumentException)
                {
                    payload = null;
                }
            }

            var dto = payload != null
                ? new MinisterOperationCommandDto
                {
                    Label = !string.IsNullOrWhiteSpace(payload.target_label) ? payload.target_label : payload.title ?? string.Empty,
                    Kind = payload.kind ?? string.Empty,
                    RawJson = view.RawJson ?? string.Empty,
                    NodeId = payload.node_id ?? string.Empty,
                    BuildingTypeId = payload.building_type_id ?? string.Empty,
                    CityId = payload.city_id ?? string.Empty,
                    RecipeId = payload.recipe_id ?? string.Empty,
                    UnitId = payload.unit_id ?? string.Empty,
                    Action = payload.action ?? string.Empty,
                    TargetNodeId = payload.target_node_id ?? string.Empty,
                    TargetUnitId = payload.target_unit_id ?? string.Empty,
                    SecondaryNodeId = payload.secondary_node_id ?? string.Empty
                }
                : new MinisterOperationCommandDto
                {
                    Label = view.Label ?? string.Empty,
                    Kind = view.Kind ?? string.Empty,
                    RawJson = view.RawJson ?? string.Empty
                };

            dto.Label = string.IsNullOrWhiteSpace(dto.Label) ? view.Label ?? string.Empty : dto.Label;
            dto.Kind = string.IsNullOrWhiteSpace(dto.Kind) ? view.Kind ?? string.Empty : dto.Kind;
            ApplyCommandEnvelope(dto, view.Command);
            return dto;
        }

        private static void ApplyCommandEnvelope(MinisterOperationCommandDto dto, CommandEnvelope envelope)
        {
            if (dto == null || envelope == null)
            {
                return;
            }

            switch (envelope.BodyCase)
            {
                case CommandEnvelope.BodyOneofCase.BuildStructure:
                    dto.Kind = string.IsNullOrWhiteSpace(dto.Kind) ? "build" : dto.Kind;
                    dto.NodeId = string.IsNullOrWhiteSpace(dto.NodeId) ? envelope.BuildStructure?.NodeId ?? string.Empty : dto.NodeId;
                    dto.BuildingTypeId = string.IsNullOrWhiteSpace(dto.BuildingTypeId) ? envelope.BuildStructure?.BuildingTypeId ?? string.Empty : dto.BuildingTypeId;
                    dto.CityId = string.IsNullOrWhiteSpace(dto.CityId) ? envelope.BuildStructure?.CityId ?? string.Empty : dto.CityId;
                    break;
                case CommandEnvelope.BodyOneofCase.SetBuildingRecipe:
                    dto.Kind = string.IsNullOrWhiteSpace(dto.Kind) ? "recipe" : dto.Kind;
                    dto.NodeId = string.IsNullOrWhiteSpace(dto.NodeId) ? envelope.SetBuildingRecipe?.NodeId ?? string.Empty : dto.NodeId;
                    dto.RecipeId = string.IsNullOrWhiteSpace(dto.RecipeId) ? envelope.SetBuildingRecipe?.RecipeId ?? string.Empty : dto.RecipeId;
                    break;
                case CommandEnvelope.BodyOneofCase.IssueUnitOrder:
                    dto.Kind = string.IsNullOrWhiteSpace(dto.Kind) ? "unit_order" : dto.Kind;
                    dto.UnitId = string.IsNullOrWhiteSpace(dto.UnitId) ? envelope.IssueUnitOrder?.UnitId ?? string.Empty : dto.UnitId;
                    dto.Action = string.IsNullOrWhiteSpace(dto.Action) ? envelope.IssueUnitOrder?.Action ?? string.Empty : dto.Action;
                    dto.TargetNodeId = string.IsNullOrWhiteSpace(dto.TargetNodeId) ? envelope.IssueUnitOrder?.TargetNodeId ?? string.Empty : dto.TargetNodeId;
                    dto.TargetUnitId = string.IsNullOrWhiteSpace(dto.TargetUnitId) ? envelope.IssueUnitOrder?.TargetUnitId ?? string.Empty : dto.TargetUnitId;
                    dto.SecondaryNodeId = string.IsNullOrWhiteSpace(dto.SecondaryNodeId) ? envelope.IssueUnitOrder?.SecondaryNodeId ?? string.Empty : dto.SecondaryNodeId;
                    break;
            }
        }

        [Serializable]
        private sealed class MinisterDraftPayload
        {
            public string draft_id;
            public string player_id;
            public string minister_role;
            public string kind;
            public string target_id;
            public string target_label;
            public string title;
            public string summary;
            public string rationale;
            public string risk_note;
            public string status;
            public bool available;
            public int turn;
            public string source;
            public string[] institution_ids;
            public string node_id;
            public string building_type_id;
            public string city_id;
            public string recipe_id;
            public string unit_id;
            public string action;
            public string target_node_id;
            public string target_unit_id;
            public string secondary_node_id;
            public string operation_id;
            public string objective;
            public MinisterDraftPayload[] operation_steps;
        }
    }
}
