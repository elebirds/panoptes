using System;
using System.Reflection;
using Google.Protobuf;
using NUnit.Framework;
using Panoptes.Protocol.V1;

namespace Panoptes.Tests.EditMode.Core
{
    public sealed class GameChatTransportTests
    {
        [Test]
        public void TryCreateClientFrame_ShouldWrapMsgSendGameChatUnderGameChat()
        {
            var transportFramesType = Type.GetType("Panoptes.Core.Infrastructure.Network.TransportFrames, Panoptes.Core");
            Assert.That(transportFramesType, Is.Not.Null, "缺少 TransportFrames。");

            var method = transportFramesType!.GetMethod("TryCreateClientFrame", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "TransportFrames 必须提供 TryCreateClientFrame。");

            var message = new MsgSendGameChat
            {
                Payload = new ChatPayload
                {
                    Emote = ChatEmote.ThumbsUp
                }
            };

            var args = new object[] { message, null, null };
            var result = (bool)(method!.Invoke(null, args) ?? false);

            Assert.That(result, Is.True);
            var frame = args[1] as ClientFrame;
            Assert.That(frame, Is.Not.Null);
            Assert.That(frame!.TargetCase, Is.EqualTo(ClientFrame.TargetOneofCase.Game));
            Assert.That(frame.Game, Is.Not.Null);
            Assert.That(frame.Game.BodyCase, Is.EqualTo(GameCommand.BodyOneofCase.Chat));
            Assert.That(frame.Game.Chat, Is.Not.Null);
            Assert.That(frame.Game.Chat.BodyCase, Is.EqualTo(ChatCommand.BodyOneofCase.SendGameChat));
            Assert.That(frame.Game.Chat.SendGameChat.Payload.Emote, Is.EqualTo(ChatEmote.ThumbsUp));
        }

        [Test]
        public void TryExtract_ShouldReturnMsgGameChatPosted()
        {
            var transportFramesType = Type.GetType("Panoptes.Core.Infrastructure.Network.TransportFrames, Panoptes.Core");
            Assert.That(transportFramesType, Is.Not.Null, "缺少 TransportFrames。");

            var method = transportFramesType!.GetMethod("TryExtract", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "TransportFrames 必须提供 TryExtract。");

            var frame = new ServerFrame
            {
                Game = new GameEvent
                {
                    GameChatPosted = new MsgGameChatPosted
                    {
                        Entry = new ChatEntry
                        {
                            Sequence = 7,
                            SenderPlayerId = "player-2",
                            Turn = 3,
                            Phase = "planning",
                            Payload = new ChatPayload
                            {
                                Emote = ChatEmote.Warning
                            }
                        }
                    }
                }
            };

            var args = new object[] { frame, null, null, null };
            var result = (bool)(method!.Invoke(null, args) ?? false);

            Assert.That(result, Is.True);
            Assert.That(args[1], Is.TypeOf<MsgGameChatPosted>());
            Assert.That(args[2] as string, Is.EqualTo("MsgGameChatPosted"));
            StringAssert.Contains("senderPlayerId", args[3] as string ?? string.Empty);
        }

        [Test]
        public void TryExtract_ShouldReturnMsgGameChatSync()
        {
            var transportFramesType = Type.GetType("Panoptes.Core.Infrastructure.Network.TransportFrames, Panoptes.Core");
            Assert.That(transportFramesType, Is.Not.Null, "缺少 TransportFrames。");

            var method = transportFramesType!.GetMethod("TryExtract", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "TransportFrames 必须提供 TryExtract。");

            var frame = new ServerFrame
            {
                Game = new GameEvent
                {
                    GameChatSync = new MsgGameChatSync
                    {
                        Entries =
                        {
                            new ChatEntry
                            {
                                Sequence = 11,
                                SenderPlayerId = "player-1",
                                Turn = 4,
                                Phase = "resolving",
                                Payload = new ChatPayload
                                {
                                    Emote = ChatEmote.Gg
                                }
                            }
                        }
                    }
                }
            };

            var args = new object[] { frame, null, null, null };
            var result = (bool)(method!.Invoke(null, args) ?? false);

            Assert.That(result, Is.True);
            Assert.That(args[1], Is.TypeOf<MsgGameChatSync>());
            Assert.That(args[2] as string, Is.EqualTo("MsgGameChatSync"));
            StringAssert.Contains("entries", args[3] as string ?? string.Empty);
        }
    }
}
