#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Google.Protobuf;
using Panoptes.Protocol.V1;

namespace Panoptes.DebugTools
{
    public static class DebugMessageRegistry
    {
        private static readonly JsonParser Parser =
            new(JsonParser.Settings.Default.WithIgnoreUnknownFields(true));

        private static readonly Dictionary<string, Func<string, IMessage>> Factories =
            new(StringComparer.Ordinal)
            {
                ["MsgCreateRoom"] = payload => Parse<MsgCreateRoom>(payload),
                ["MsgJoinRoom"] = payload => Parse<MsgJoinRoom>(payload),
                ["MsgLeaveRoom"] = payload => Parse<MsgLeaveRoom>(payload),
                ["MsgReadyUp"] = payload => Parse<MsgReadyUp>(payload),
                ["MsgAddBot"] = payload => Parse<MsgAddBot>(payload),
                ["MsgStartGame"] = payload => Parse<MsgStartGame>(payload),
                ["MsgKickPlayer"] = payload => Parse<MsgKickPlayer>(payload),
                ["MsgSetPolicy"] = payload => Parse<MsgSetPolicy>(payload),
                ["MsgSetInstitutionLoadout"] = payload => Parse<MsgSetInstitutionLoadout>(payload),
                ["MsgSetMinisterDirective"] = payload => Parse<MsgSetMinisterDirective>(payload),
                ["MsgSetResearchTarget"] = payload => Parse<MsgSetResearchTarget>(payload),
                ["MsgSetBuildingRecipe"] = payload => Parse<MsgSetBuildingRecipe>(payload),
                ["MsgBuildStructure"] = payload => Parse<MsgBuildStructure>(payload),
                ["MsgRevealNode"] = payload => Parse<MsgRevealNode>(payload),
                ["MsgSubmitTurn"] = payload => Parse<MsgSubmitTurn>(payload),
                ["MsgSetWarZone"] = payload => Parse<MsgSetWarZone>(payload),
                ["MsgWarZoneDirective"] = payload => Parse<MsgWarZoneDirective>(payload),
                ["MsgIssueUnitOrder"] = payload => Parse<MsgIssueUnitOrder>(payload),
                ["MsgCancelUnitOrder"] = payload => Parse<MsgCancelUnitOrder>(payload),
                ["MsgPlanningPathPreviewRequest"] = payload => Parse<MsgPlanningPathPreviewRequest>(payload),
            };

        private static readonly string[] MessageTypes =
        {
            "MsgAddBot",
            "MsgBuildStructure",
            "MsgCancelUnitOrder",
            "MsgCreateRoom",
            "MsgIssueUnitOrder",
            "MsgJoinRoom",
            "MsgKickPlayer",
            "MsgLeaveRoom",
            "MsgPlanningPathPreviewRequest",
            "MsgReadyUp",
            "MsgRevealNode",
            "MsgSetBuildingRecipe",
            "MsgSetMinisterDirective",
            "MsgSetPolicy",
            "MsgSetInstitutionLoadout",
            "MsgSetResearchTarget",
            "MsgSetWarZone",
            "MsgStartGame",
            "MsgSubmitTurn",
            "MsgWarZoneDirective",
        };

        public static IReadOnlyList<string> SupportedMessageTypes => MessageTypes;

        public static bool SupportsMessageType(string messageType)
        {
            return !string.IsNullOrWhiteSpace(messageType) && Factories.ContainsKey(messageType.Trim());
        }

        public static bool TryCreateMessage(string messageType, string payloadJson, out IMessage message, out string error)
        {
            message = null;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(messageType))
            {
                error = "message_type_required";
                return false;
            }

            if (!Factories.TryGetValue(messageType.Trim(), out var factory))
            {
                error = $"unknown_message_type:{messageType.Trim()}";
                return false;
            }

            try
            {
                message = factory(string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson);
                return true;
            }
            catch (Exception e)
            {
                error = $"parse_error:{e.Message}";
                return false;
            }
        }

        public static string CreateDefaultPayload(string messageType)
        {
            return messageType switch
            {
                "MsgCreateRoom" => "{\n  \"name\": \"debug-room\",\n  \"maxPlayers\": 2\n}",
                "MsgJoinRoom" => "{\n  \"roomCode\": \"ABCD12\"\n}",
                "MsgKickPlayer" => "{\n  \"playerId\": \"player-2\"\n}",
                "MsgSetPolicy" => "{\n  \"nationalPolicyId\": \"war_preparedness\"\n}",
                "MsgSetInstitutionLoadout" => "{\n  \"policyIds\": [\"academy_charter\"]\n}",
                "MsgSetMinisterDirective" => "{\n  \"ministerRole\": \"domestic\",\n  \"content\": \"{}\"\n}",
                "MsgSetResearchTarget" => "{\n  \"technologyId\": \"tech_masonry\"\n}",
                "MsgSetBuildingRecipe" => "{\n  \"nodeId\": \"node-a\",\n  \"recipeId\": \"recipe_swordsman\"\n}",
                "MsgBuildStructure" => "{\n  \"nodeId\": \"res_food\",\n  \"buildingType\": \"farm\"\n}",
                "MsgRevealNode" => "{\n  \"nodeId\": \"res_food\"\n}",
                "MsgSetWarZone" => "{\n  \"zoneId\": \"zone1\",\n  \"name\": \"北线\",\n  \"nodeIds\": [\"res_ore\"]\n}",
                "MsgWarZoneDirective" => "{\n  \"zoneId\": \"zone1\",\n  \"directive\": \"attack\"\n}",
                "MsgIssueUnitOrder" => "{\n  \"unitId\": \"unit-1\",\n  \"action\": \"move\",\n  \"targetNodeId\": \"node-b\"\n}",
                "MsgCancelUnitOrder" => "{\n  \"unitId\": \"unit-1\"\n}",
                "MsgPlanningPathPreviewRequest" => "{\n  \"requestId\": \"preview-1\",\n  \"unitId\": \"unit-1\",\n  \"action\": \"move\",\n  \"targetNodeId\": \"node-b\"\n}",
                _ => "{}"
            };
        }

        private static IMessage Parse<T>(string payloadJson)
            where T : IMessage<T>, new()
        {
            return Parser.Parse<T>(payloadJson);
        }
    }
}
#endif
