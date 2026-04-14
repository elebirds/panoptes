using System;
using Google.Protobuf;
using Panoptes.Protocol.V1;
using AuthMessages = Panoptes.Protocol.V1.Auth;

namespace Panoptes.Core.Infrastructure.Network
{
    internal static class TransportFrames
    {
        private static readonly JsonFormatter Formatter = JsonFormatter.Default;

        public static bool TryCreateClientFrame(IMessage command, out ClientFrame frame, out string error)
        {
            frame = null;
            error = string.Empty;

            if (command == null)
            {
                error = "command is null";
                return false;
            }

            switch (command)
            {
                case AuthMessages.MsgRegister register:
                    frame = new ClientFrame
                    {
                        Meta = BuildMeta(),
                        Auth = new AuthCommand { Register = register }
                    };
                    return true;
                case AuthMessages.MsgLogin login:
                    frame = new ClientFrame
                    {
                        Meta = BuildMeta(),
                        Auth = new AuthCommand { Login = login }
                    };
                    return true;
                case MsgCreateRoom createRoom:
                    frame = LobbyFrame(new LobbyCommand { CreateRoom = createRoom });
                    return true;
                case MsgJoinRoom joinRoom:
                    frame = LobbyFrame(new LobbyCommand { JoinRoom = joinRoom });
                    return true;
                case MsgLeaveRoom leaveRoom:
                    frame = LobbyFrame(new LobbyCommand { LeaveRoom = leaveRoom });
                    return true;
                case MsgReadyUp readyUp:
                    frame = LobbyFrame(new LobbyCommand { ReadyUp = readyUp });
                    return true;
                case MsgAddBot addBot:
                    frame = LobbyFrame(new LobbyCommand { AddBot = addBot });
                    return true;
                case MsgStartGame startGame:
                    frame = LobbyFrame(new LobbyCommand { StartGame = startGame });
                    return true;
                case MsgKickPlayer kickPlayer:
                    frame = LobbyFrame(new LobbyCommand { KickPlayer = kickPlayer });
                    return true;
                case MsgSetPolicy setPolicy:
                    frame = PlanningFrame(new PlanningCommand { SetPolicy = setPolicy });
                    return true;
                case MsgSetResearchTarget setResearchTarget:
                    frame = PlanningFrame(new PlanningCommand { SetResearchTarget = setResearchTarget });
                    return true;
                case MsgResearchTechnology researchTechnology:
                    frame = PlanningFrame(new PlanningCommand
                    {
                        SetResearchTarget = new MsgSetResearchTarget
                        {
                            TechnologyId = researchTechnology.TechnologyId ?? string.Empty
                        }
                    });
                    return true;
                case MsgSetBuildingRecipe setBuildingRecipe:
                    frame = PlanningFrame(new PlanningCommand { SetBuildingRecipe = setBuildingRecipe });
                    return true;
                case MsgBuildStructure buildStructure:
                    frame = PlanningFrame(new PlanningCommand { BuildStructure = buildStructure });
                    return true;
                case MsgTokenBuild tokenBuild:
                    frame = PlanningFrame(new PlanningCommand
                    {
                        BuildStructure = new MsgBuildStructure
                        {
                            NodeId = tokenBuild.NodeId ?? string.Empty,
                            BuildingType = tokenBuild.BuildingType ?? string.Empty,
                            CastleId = tokenBuild.CastleId ?? string.Empty
                        }
                    });
                    return true;
                case MsgRevealNode revealNode:
                    frame = PlanningFrame(new PlanningCommand { RevealNode = revealNode });
                    return true;
                case MsgTokenReveal tokenReveal:
                    frame = PlanningFrame(new PlanningCommand
                    {
                        RevealNode = new MsgRevealNode
                        {
                            NodeId = tokenReveal.NodeId ?? string.Empty
                        }
                    });
                    return true;
                case MsgSetWarZone setWarZone:
                    frame = PlanningFrame(new PlanningCommand { SetWarZone = setWarZone });
                    return true;
                case MsgWarZoneDirective warZoneDirective:
                    frame = PlanningFrame(new PlanningCommand { WarZoneDirective = warZoneDirective });
                    return true;
                case MsgSetMinisterDirective setMinisterDirective:
                    frame = PlanningFrame(new PlanningCommand { SetMinisterDirective = setMinisterDirective });
                    return true;
                case MsgMinisterDirective ministerDirective:
                    frame = PlanningFrame(new PlanningCommand
                    {
                        SetMinisterDirective = new MsgSetMinisterDirective
                        {
                            MinisterRole = ministerDirective.MinisterRole ?? string.Empty,
                            Content = ministerDirective.Content ?? string.Empty
                        }
                    });
                    return true;
                case MsgIssueUnitOrder issueUnitOrder:
                    frame = PlanningFrame(new PlanningCommand { IssueUnitOrder = issueUnitOrder });
                    return true;
                case MsgCancelUnitOrder cancelUnitOrder:
                    frame = PlanningFrame(new PlanningCommand { CancelUnitOrder = cancelUnitOrder });
                    return true;
                case MsgTokenVetoCombat vetoCombat:
                    frame = PlanningFrame(new PlanningCommand
                    {
                        CancelUnitOrder = new MsgCancelUnitOrder
                        {
                            UnitId = vetoCombat.UnitId ?? string.Empty
                        }
                    });
                    return true;
                case MsgCombatOrder combatOrder:
                    frame = PlanningFrame(new PlanningCommand
                    {
                        IssueUnitOrder = new MsgIssueUnitOrder
                        {
                            UnitId = combatOrder.UnitId ?? string.Empty,
                            Action = combatOrder.Action ?? string.Empty,
                            TargetNodeId = combatOrder.TargetNodeId ?? string.Empty,
                            TargetUnitId = combatOrder.TargetUnitId ?? string.Empty
                        }
                    });
                    return true;
                case MsgTokenMicro tokenMicro:
                    frame = PlanningFrame(new PlanningCommand
                    {
                        IssueUnitOrder = new MsgIssueUnitOrder
                        {
                            UnitId = tokenMicro.UnitId ?? string.Empty,
                            Action = "move",
                            TargetNodeId = tokenMicro.TargetNode ?? string.Empty
                        }
                    });
                    return true;
                case MsgTokenExpandTerritory expandTerritory:
                    frame = PlanningFrame(new PlanningCommand
                    {
                        IssueUnitOrder = new MsgIssueUnitOrder
                        {
                            UnitId = expandTerritory.UnitId ?? string.Empty,
                            Action = "settle_city",
                            TargetNodeId = expandTerritory.CenterNodeId ?? string.Empty
                        }
                    });
                    return true;
                case MsgPlanningPathPreviewRequest planningPreview:
                    frame = PlanningFrame(new PlanningCommand { PlanningPathPreviewRequest = planningPreview });
                    return true;
                case MsgCombatPathPreviewRequest combatPreview:
                    frame = PlanningFrame(new PlanningCommand
                    {
                        PlanningPathPreviewRequest = new MsgPlanningPathPreviewRequest
                        {
                            RequestId = combatPreview.RequestId ?? string.Empty,
                            UnitId = combatPreview.UnitId ?? string.Empty,
                            Action = combatPreview.Action ?? string.Empty,
                            TargetNodeId = combatPreview.TargetNodeId ?? string.Empty
                        }
                    });
                    return true;
                case MsgSubmitTurn submitTurn:
                    frame = PlanningFrame(new PlanningCommand { SubmitTurn = submitTurn });
                    return true;
                case MsgSubmitCombat:
                    frame = PlanningFrame(new PlanningCommand { SubmitTurn = new MsgSubmitTurn() });
                    return true;
                default:
                    error = $"unsupported outbound message type: {command.Descriptor.Name}";
                    return false;
            }
        }

        public static bool TryExtract(ServerFrame frame, out IMessage message, out string messageType, out string payloadJson)
        {
            message = null;
            messageType = string.Empty;
            payloadJson = "{}";

            if (frame == null)
            {
                return false;
            }

            switch (frame.TargetCase)
            {
                case ServerFrame.TargetOneofCase.Problem:
                    message = frame.Problem;
                    break;
                case ServerFrame.TargetOneofCase.Auth:
                    message = ExtractAuth(frame.Auth);
                    break;
                case ServerFrame.TargetOneofCase.Lobby:
                    message = ExtractLobby(frame.Lobby);
                    break;
                case ServerFrame.TargetOneofCase.Game:
                    message = ExtractGame(frame.Game);
                    break;
                default:
                    return false;
            }

            if (message == null)
            {
                return false;
            }

            messageType = message.Descriptor.Name;
            payloadJson = Formatter.Format(message);
            return true;
        }

        private static ClientFrame LobbyFrame(LobbyCommand command)
        {
            return new ClientFrame
            {
                Meta = BuildMeta(),
                Lobby = command
            };
        }

        private static ClientFrame PlanningFrame(PlanningCommand command)
        {
            return new ClientFrame
            {
                Meta = BuildMeta(),
                Game = new GameCommand
                {
                    Planning = command
                }
            };
        }

        private static CommandMeta BuildMeta()
        {
            return new CommandMeta
            {
                RequestId = Guid.NewGuid().ToString("N")
            };
        }

        private static IMessage ExtractAuth(AuthEvent evt)
        {
            if (evt == null)
            {
                return null;
            }

            return evt.BodyCase switch
            {
                AuthEvent.BodyOneofCase.LoginSuccess => evt.LoginSuccess,
                AuthEvent.BodyOneofCase.AuthError => evt.AuthError,
                AuthEvent.BodyOneofCase.ClientRuntimeConfig => evt.ClientRuntimeConfig,
                _ => null
            };
        }

        private static IMessage ExtractLobby(LobbyEvent evt)
        {
            if (evt == null)
            {
                return null;
            }

            return evt.BodyCase switch
            {
                LobbyEvent.BodyOneofCase.RoomCreated => evt.RoomCreated,
                LobbyEvent.BodyOneofCase.RoomState => evt.RoomState,
                LobbyEvent.BodyOneofCase.GameStarting => evt.GameStarting,
                LobbyEvent.BodyOneofCase.PlayerKicked => evt.PlayerKicked,
                LobbyEvent.BodyOneofCase.LobbyError => evt.LobbyError,
                _ => null
            };
        }

        private static IMessage ExtractGame(GameEvent evt)
        {
            if (evt == null)
            {
                return null;
            }

            return evt.BodyCase switch
            {
                GameEvent.BodyOneofCase.StaticCatalogManifest => evt.StaticCatalogManifest,
                GameEvent.BodyOneofCase.StaticCatalogSnapshot => evt.StaticCatalogSnapshot,
                GameEvent.BodyOneofCase.GameInit => evt.GameInit,
                GameEvent.BodyOneofCase.PlanningStart => evt.PlanningStart,
                GameEvent.BodyOneofCase.PlanningSnapshot => evt.PlanningSnapshot,
                GameEvent.BodyOneofCase.PlanningPathPreviewResponse => evt.PlanningPathPreviewResponse,
                GameEvent.BodyOneofCase.TokenResult => evt.TokenResult,
                GameEvent.BodyOneofCase.RevealResult => evt.RevealResult,
                GameEvent.BodyOneofCase.ResearchResult => evt.ResearchResult,
                GameEvent.BodyOneofCase.SetBuildingRecipeResult => evt.SetBuildingRecipeResult,
                GameEvent.BodyOneofCase.TurnReport => evt.TurnReport,
                GameEvent.BodyOneofCase.TurnSettlement => evt.TurnSettlement,
                GameEvent.BodyOneofCase.GameOver => evt.GameOver,
                GameEvent.BodyOneofCase.MinisterReportChunk => evt.MinisterReportChunk,
                GameEvent.BodyOneofCase.MinisterMetrics => evt.MinisterMetrics,
                _ => null
            };
        }
    }
}
