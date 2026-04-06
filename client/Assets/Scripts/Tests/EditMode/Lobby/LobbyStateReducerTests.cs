using System;
using NUnit.Framework;
using Panoptes.Runtime.Service;
using UnityEngine;

namespace Panoptes.Tests.EditMode.Lobby
{
    public sealed class LobbyStateReducerTests
    {
        private GameObject _sessionObject;
        private SessionManager _sessionManager;

        [SetUp]
        public void SetUp()
        {
            _sessionObject = new GameObject("SessionManager");
            _sessionManager = _sessionObject.AddComponent<SessionManager>();
            _sessionManager.SetSession("token", "player-2", "bob");
        }

        [TearDown]
        public void TearDown()
        {
            if (_sessionObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_sessionObject);
            }

            var roomCacheType = Type.GetType("Panoptes.Runtime.Cache.RoomCache, Panoptes.Runtime");
            if (roomCacheType == null)
            {
                return;
            }

            var existing = UnityEngine.Object.FindAnyObjectByType(roomCacheType) as Component;
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }
        }

        [Test]
        public void Apply_ShouldMirrorRoomStateAndDetectHost()
        {
            var roomCacheType = RequireRoomCacheType();
            var roomObject = new GameObject("RoomCache");
            var roomCache = roomObject.AddComponent(roomCacheType);

            var changedCount = 0;
            var changedEvent = roomCacheType.GetEvent("OnRoomStateChanged");
            var handler = new Action(() => changedCount++);
            changedEvent?.AddEventHandler(roomCache, handler);

            var msg = CreateRoomStateMessage(
                roomId: "room-1",
                roomCode: "ABC123",
                roomName: "测试房间",
                status: "waiting",
                maxPlayers: 4,
                CreatePlayer("player-1", "alice", isReady: true, isHost: true),
                CreatePlayer("player-2", "bob", isReady: false, isHost: false));

            roomCacheType.GetMethod("Apply")?.Invoke(roomCache, new object[] { msg });

            Assert.That(GetProperty<string>(roomCache, roomCacheType, "RoomID"), Is.EqualTo("room-1"));
            Assert.That(GetProperty<string>(roomCache, roomCacheType, "RoomCode"), Is.EqualTo("ABC123"));
            Assert.That(GetProperty<string>(roomCache, roomCacheType, "RoomName"), Is.EqualTo("测试房间"));
            Assert.That(GetProperty<string>(roomCache, roomCacheType, "Status"), Is.EqualTo("waiting"));
            Assert.That(GetProperty<int>(roomCache, roomCacheType, "MaxPlayers"), Is.EqualTo(4));
            Assert.That(GetProperty<bool>(roomCache, roomCacheType, "IsHost"), Is.False);

            var players = GetProperty<System.Collections.IList>(roomCache, roomCacheType, "Players");
            Assert.That(players, Is.Not.Null);
            Assert.That(players.Count, Is.EqualTo(2));
            Assert.That(changedCount, Is.EqualTo(1));
        }

        [Test]
        public void Clear_ShouldResetAllFieldsAndRaiseChangeEvent()
        {
            var roomCacheType = RequireRoomCacheType();
            var roomObject = new GameObject("RoomCache");
            var roomCache = roomObject.AddComponent(roomCacheType);

            var changedCount = 0;
            var changedEvent = roomCacheType.GetEvent("OnRoomStateChanged");
            var handler = new Action(() => changedCount++);
            changedEvent?.AddEventHandler(roomCache, handler);

            var msg = CreateRoomStateMessage(
                roomId: "room-2",
                roomCode: "ROOM66",
                roomName: "待清理房间",
                status: "ready",
                maxPlayers: 6,
                CreatePlayer("player-2", "bob", isReady: true, isHost: true));

            roomCacheType.GetMethod("Apply")?.Invoke(roomCache, new object[] { msg });
            roomCacheType.GetMethod("Clear")?.Invoke(roomCache, Array.Empty<object>());

            Assert.That(GetProperty<string>(roomCache, roomCacheType, "RoomID"), Is.Empty);
            Assert.That(GetProperty<string>(roomCache, roomCacheType, "RoomCode"), Is.Empty);
            Assert.That(GetProperty<string>(roomCache, roomCacheType, "RoomName"), Is.Empty);
            Assert.That(GetProperty<string>(roomCache, roomCacheType, "Status"), Is.Empty);
            Assert.That(GetProperty<int>(roomCache, roomCacheType, "MaxPlayers"), Is.Zero);
            Assert.That(GetProperty<bool>(roomCache, roomCacheType, "IsHost"), Is.False);

            var players = GetProperty<System.Collections.IList>(roomCache, roomCacheType, "Players");
            Assert.That(players, Is.Not.Null);
            Assert.That(players.Count, Is.Zero);
            Assert.That(changedCount, Is.EqualTo(2));
        }

        private static Type RequireRoomCacheType()
        {
            return Type.GetType("Panoptes.Runtime.Cache.RoomCache, Panoptes.Runtime")
                   ?? throw new AssertionException("RoomCache 类型不存在。");
        }

        private static object CreateRoomStateMessage(
            string roomId,
            string roomCode,
            string roomName,
            string status,
            int maxPlayers,
            params object[] players)
        {
            var roomStateType = Type.GetType("Panoptes.Protocol.V1.MsgRoomState, Panoptes.Runtime")
                                ?? throw new AssertionException("MsgRoomState 类型不存在。");
            var roomState = Activator.CreateInstance(roomStateType)
                            ?? throw new AssertionException("无法创建 MsgRoomState。");

            roomStateType.GetProperty("RoomId")?.SetValue(roomState, roomId);
            roomStateType.GetProperty("RoomCode")?.SetValue(roomState, roomCode);
            roomStateType.GetProperty("Name")?.SetValue(roomState, roomName);
            roomStateType.GetProperty("Status")?.SetValue(roomState, status);
            roomStateType.GetProperty("MaxPlayers")?.SetValue(roomState, maxPlayers);

            var playersCollection = roomStateType.GetProperty("Players")?.GetValue(roomState);
            var addMethod = playersCollection?.GetType().GetMethod("Add", new[] { players[0].GetType() });
            foreach (var player in players)
            {
                addMethod?.Invoke(playersCollection, new[] { player });
            }

            return roomState;
        }

        private static object CreatePlayer(string playerId, string username, bool isReady, bool isHost)
        {
            var roomPlayerType = Type.GetType("Panoptes.Protocol.V1.RoomPlayer, Panoptes.Runtime")
                                 ?? throw new AssertionException("RoomPlayer 类型不存在。");
            var player = Activator.CreateInstance(roomPlayerType)
                         ?? throw new AssertionException("无法创建 RoomPlayer。");

            roomPlayerType.GetProperty("PlayerId")?.SetValue(player, playerId);
            roomPlayerType.GetProperty("Username")?.SetValue(player, username);
            roomPlayerType.GetProperty("IsReady")?.SetValue(player, isReady);
            roomPlayerType.GetProperty("IsHost")?.SetValue(player, isHost);
            return player;
        }

        private static T GetProperty<T>(Component instance, Type type, string propertyName)
        {
            var property = type.GetProperty(propertyName)
                           ?? throw new AssertionException($"缺少属性 {propertyName}");
            return (T)property.GetValue(instance);
        }
    }
}
