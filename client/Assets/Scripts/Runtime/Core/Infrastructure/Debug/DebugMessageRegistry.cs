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
                ["MsgMinisterDirective"] = payload => Parse<MsgMinisterDirective>(payload),
                ["MsgTokenBuild"] = payload => Parse<MsgTokenBuild>(payload),
                ["MsgTokenReveal"] = payload => Parse<MsgTokenReveal>(payload),
                ["MsgTokenVeto"] = payload => Parse<MsgTokenVeto>(payload),
                ["MsgTokenAdjustFlow"] = payload => Parse<MsgTokenAdjustFlow>(payload),
                ["MsgBuildRoad"] = payload => Parse<MsgBuildRoad>(payload),
                ["MsgSubmitDomestic"] = payload => Parse<MsgSubmitDomestic>(payload),
                ["MsgSetWarZone"] = payload => Parse<MsgSetWarZone>(payload),
                ["MsgWarZoneDirective"] = payload => Parse<MsgWarZoneDirective>(payload),
                ["MsgTokenVetoCombat"] = payload => Parse<MsgTokenVetoCombat>(payload),
                ["MsgTokenMicro"] = payload => Parse<MsgTokenMicro>(payload),
                ["MsgCombatOrder"] = payload => Parse<MsgCombatOrder>(payload),
                ["MsgCombatPathPreviewRequest"] = payload => Parse<MsgCombatPathPreviewRequest>(payload),
                ["MsgSubmitCombat"] = payload => Parse<MsgSubmitCombat>(payload),
            };

        private static readonly string[] MessageTypes =
        {
            "MsgAddBot",
            "MsgBuildRoad",
            "MsgCombatOrder",
            "MsgCombatPathPreviewRequest",
            "MsgCreateRoom",
            "MsgJoinRoom",
            "MsgKickPlayer",
            "MsgLeaveRoom",
            "MsgMinisterDirective",
            "MsgReadyUp",
            "MsgSetPolicy",
            "MsgSetWarZone",
            "MsgStartGame",
            "MsgSubmitCombat",
            "MsgSubmitDomestic",
            "MsgTokenAdjustFlow",
            "MsgTokenBuild",
            "MsgTokenMicro",
            "MsgTokenReveal",
            "MsgTokenVeto",
            "MsgTokenVetoCombat",
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
                "MsgSetPolicy" => "{\n  \"policy\": \"ready_for_war\"\n}",
                "MsgTokenBuild" => "{\n  \"nodeId\": \"res_food\",\n  \"buildingType\": \"farm\"\n}",
                "MsgTokenReveal" => "{\n  \"nodeId\": \"res_food\"\n}",
                "MsgTokenAdjustFlow" => "{\n  \"fromNode\": \"node-a\",\n  \"toNode\": \"node-b\",\n  \"resourceType\": \"food\",\n  \"amount\": 1\n}",
                "MsgSetWarZone" => "{\n  \"zoneId\": \"zone1\",\n  \"name\": \"北线\",\n  \"nodeIds\": [\"res_ore\"]\n}",
                "MsgWarZoneDirective" => "{\n  \"zoneId\": \"zone1\",\n  \"directive\": \"attack\"\n}",
                "MsgTokenVetoCombat" => "{\n  \"unitId\": \"unit-1\"\n}",
                "MsgTokenMicro" => "{\n  \"unitId\": \"unit-1\",\n  \"targetNodeId\": \"node-b\"\n}",
                "MsgCombatOrder" => "{\n  \"unitId\": \"unit-1\",\n  \"action\": \"move\",\n  \"targetNodeId\": \"node-b\"\n}",
                "MsgCombatPathPreviewRequest" => "{\n  \"requestId\": \"preview-1\",\n  \"unitId\": \"unit-1\",\n  \"action\": \"move\",\n  \"targetNodeId\": \"node-b\"\n}",
                "MsgMinisterDirective" => "{\n  \"ministerRole\": \"domestic\",\n  \"content\": \"{}\"\n}",
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
