using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Google.Protobuf;
using NUnit.Framework;
using Panoptes.Core.Application.Intents;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Map;
using Panoptes.Presentation.UI.HUD;
using UnityEngine;
using MsgIssueUnitOrder = Panoptes.Protocol.V1.MsgIssueUnitOrder;

namespace Panoptes.Tests.EditMode.Presentation
{
    public sealed class SettlerUnitActionRegistrarTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            ActionLock.Release();
            if (_root != null)
            {
                UnityEngine.Object.DestroyImmediate(_root);
                _root = null;
            }
        }

        [Test]
        public void SettlerAction_ShouldSubmitExpandThroughPlanningIntentServiceWithStoreResolvedCenter()
        {
            var gameStateStore = new GameStateStore();
            var sender = new FakeMessageSender();
            var service = new PlanningIntentService(sender);
            var registry = CreateInjectedRegistry(gameStateStore, service, out var unit);

            gameStateStore.Replace(new GameStateStoreState(
                phase: "planning",
                nodes: new Dictionary<string, NodeDto>
                {
                    ["wrong_view_position"] = new NodeDto { Id = "wrong_view_position", Q = 0, R = 0 },
                    ["settle_center"] = new NodeDto { Id = "settle_center", Q = 3, R = -1 }
                },
                units: new Dictionary<string, UnitDto>
                {
                    ["unit-1"] = new UnitDto { Id = "unit-1", Type = "worker", Q = 3, R = -1 }
                }));
            unit.Bind(new UnitDto { Id = "unit-1", Type = "settler", Q = 0, R = 0 }, Vector3.zero);

            Assert.That(registry.TryResolve("settle_city", unit, out var handler, out _, out var isVisible), Is.True);
            Assert.That(isVisible, Is.True);

            handler(unit);

            Assert.That(sender.Last, Is.TypeOf<MsgIssueUnitOrder>());
            var order = (MsgIssueUnitOrder)sender.Last;
            Assert.That(order.UnitId, Is.EqualTo("unit-1"));
            Assert.That(order.Action, Is.EqualTo("settle_city"));
            Assert.That(order.TargetNodeId, Is.EqualTo("settle_center"));
        }

        [Test]
        public void SettlerAction_ShouldRejectNonPlanningPhase()
        {
            var gameStateStore = new GameStateStore();
            var sender = new FakeMessageSender();
            var service = new PlanningIntentService(sender);
            var registry = CreateInjectedRegistry(gameStateStore, service, out var unit);

            gameStateStore.Replace(new GameStateStoreState(
                phase: "resolving",
                nodes: new Dictionary<string, NodeDto>
                {
                    ["settle_center"] = new NodeDto { Id = "settle_center", Q = 1, R = 2 }
                },
                units: new Dictionary<string, UnitDto>
                {
                    ["unit-1"] = new UnitDto { Id = "unit-1", Q = 1, R = 2 }
                }));
            unit.Bind(new UnitDto { Id = "unit-1", Type = "settler" }, Vector3.zero);

            Assert.That(registry.TryResolve("settle_city", unit, out var handler, out _, out var isVisible), Is.True);
            Assert.That(isVisible, Is.False);

            handler(unit);

            Assert.That(sender.Sent, Is.Empty);
        }

        [Test]
        public void SettlerAction_ShouldStillSubmitWhenCenterNodeCannotBeResolved()
        {
            var gameStateStore = new GameStateStore();
            var sender = new FakeMessageSender();
            var service = new PlanningIntentService(sender);
            var registry = CreateInjectedRegistry(gameStateStore, service, out var unit);

            gameStateStore.Replace(new GameStateStoreState(
                phase: "planning",
                nodes: new Dictionary<string, NodeDto>
                {
                    ["other_node"] = new NodeDto { Id = "other_node", Q = 9, R = 9 }
                },
                units: new Dictionary<string, UnitDto>
                {
                    ["unit-1"] = new UnitDto { Id = "unit-1", Q = 1, R = 2 }
                }));
            unit.Bind(new UnitDto { Id = "unit-1", Type = "settler", Q = 1, R = 2 }, Vector3.zero);

            Assert.That(registry.TryResolve("settle_city", unit, out var handler, out _, out var isVisible), Is.True);
            Assert.That(isVisible, Is.True);

            handler(unit);

            Assert.That(sender.Last, Is.TypeOf<MsgIssueUnitOrder>());
            var order = (MsgIssueUnitOrder)sender.Last;
            Assert.That(order.UnitId, Is.EqualTo("unit-1"));
            Assert.That(order.Action, Is.EqualTo("settle_city"));
            Assert.That(order.TargetNodeId, Is.Empty);
        }

        [Test]
        public void SettlerRegistrarSource_ShouldStayOnFinalStoreAndServiceDependencies()
        {
            var source = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/UI/HUD/SettlerUnitActionRegistrar.cs"));
            var installer = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/Composition/ClientCompositionInstaller.cs"));
            var unitInfoPanel = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/UI/HUD/UnitInfoPanelController.cs"));
            var mapRenderer = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/Map/MapRenderer.cs"));
            var forbidden = new[]
            {
                "GameStateCache",
                "MapPlanningInputController",
                "MapPlanningInputController.Instance",
                "NetworkManager.Instance",
                "Panoptes.Protocol",
                ".Instance"
            };

            for (var i = 0; i < forbidden.Length; i++)
            {
                Assert.That(source, Does.Not.Contain(forbidden[i]));
            }

            Assert.That(source, Does.Contain("GameStateStore"));
            Assert.That(source, Does.Contain("PlanningIntentService"));
            Assert.That(source, Does.Contain(".ExpandTerritory("));
            Assert.That(installer, Does.Contain("RegisterComponentInHierarchy<SettlerUnitActionRegistrar>"));
            Assert.That(unitInfoPanel, Does.Not.Contain("AddComponent<SettlerUnitActionRegistrar>"));
            Assert.That(mapRenderer, Does.Not.Contain("AddComponent<SettlerUnitActionRegistrar>"));
            Assert.That(mapRenderer, Does.Not.Contain("typeof(SettlerUnitActionRegistrar)"));
        }

        private UnitInfoActionRegistry CreateInjectedRegistry(
            GameStateStore gameStateStore,
            PlanningIntentService planningIntentService,
            out UnitView unit)
        {
            _root = new GameObject("SettlerActionRegistrarTest");
            var registry = _root.AddComponent<UnitInfoActionRegistry>();
            var registrar = _root.AddComponent<SettlerUnitActionRegistrar>();
            unit = _root.AddComponent<UnitView>();
            InjectRegistrar(registrar, gameStateStore, planningIntentService);
            registrar.EnsureRegistered();
            return registry;
        }

        private static void InjectRegistrar(
            SettlerUnitActionRegistrar registrar,
            GameStateStore gameStateStore,
            PlanningIntentService planningIntentService)
        {
            var method = typeof(SettlerUnitActionRegistrar).GetMethod(
                "Construct",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method!.Invoke(registrar, new object[] { gameStateStore, planningIntentService });
        }

        private static string ResolveAssetPath(string assetRelativePath)
        {
            var localPath = Path.GetFullPath(Path.Combine("Assets", assetRelativePath));
            if (File.Exists(localPath) || Directory.Exists(localPath))
            {
                return localPath;
            }

            return Path.GetFullPath(Path.Combine("client", "Assets", assetRelativePath));
        }

        private sealed class FakeMessageSender : IClientMessageSender
        {
            public readonly List<IMessage> Sent = new();
            public IMessage Last => Sent.Count == 0 ? null : Sent[^1];

            public bool Send(IMessage message)
            {
                if (message == null)
                {
                    return false;
                }

                Sent.Add(message);
                return true;
            }
        }
    }
}
