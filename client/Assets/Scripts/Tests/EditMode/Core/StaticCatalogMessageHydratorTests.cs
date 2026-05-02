using NUnit.Framework;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Infrastructure.Network;
using Panoptes.Protocol.V1;
using UnityEngine;

namespace Panoptes.Tests.EditMode.Core
{
    public sealed class StaticCatalogMessageHydratorTests
    {
        private GameObject _dispatcherObject;
        private StaticCatalogMessageHydrator _hydrator;

        [TearDown]
        public void TearDown()
        {
            _hydrator?.Dispose();
            _hydrator = null;

            if (_dispatcherObject != null)
            {
                Object.DestroyImmediate(_dispatcherObject);
                _dispatcherObject = null;
            }
        }

        [Test]
        public void HandleStaticCatalogSnapshot_ShouldHydrateStore()
        {
            var store = new StaticCatalogStore();
            var dispatcher = CreateDispatcher();
            _hydrator = new StaticCatalogMessageHydrator(dispatcher, store);

            _hydrator.HandleStaticCatalogSnapshot(new MsgStaticCatalogSnapshot
            {
                Snapshot = new StaticCatalogSnapshot
                {
                    Buildings =
                    {
                        new BuildingCatalogEntry { Id = "farm", Name = "Farm" }
                    }
                }
            });

            Assert.That(store.Snapshot.Buildings["farm"].Name, Is.EqualTo("Farm"));
        }

        [Test]
        public void AttachAndDispose_ShouldRegisterAndUnregisterDispatcherHandler()
        {
            var store = new StaticCatalogStore();
            var dispatcher = CreateDispatcher();
            _hydrator = new StaticCatalogMessageHydrator(dispatcher, store);
            _hydrator.Attach();

            dispatcher.Dispatch(CatalogFrame("farm", "Farm"));

            Assert.That(store.Snapshot.Buildings["farm"].Name, Is.EqualTo("Farm"));

            _hydrator.Dispose();
            dispatcher.Dispatch(CatalogFrame("mill", "Mill"));

            Assert.That(store.Snapshot.Buildings.ContainsKey("mill"), Is.False);
        }

        [Test]
        public void StaticCatalogSnapshot_ShouldPassSessionGateWithoutActiveGameSession()
        {
            var store = new StaticCatalogStore();
            var dispatcher = CreateDispatcher();
            _hydrator = new StaticCatalogMessageHydrator(dispatcher, store);
            _hydrator.Attach();

            dispatcher.Dispatch(CatalogFrame("farm", "Farm"));

            Assert.That(store.Snapshot.Buildings.ContainsKey("farm"), Is.True);
        }

        private MessageDispatcher CreateDispatcher()
        {
            _dispatcherObject = new GameObject("MessageDispatcher");
            return _dispatcherObject.AddComponent<MessageDispatcher>();
        }

        private static ServerFrame CatalogFrame(string id, string name)
        {
            return new ServerFrame
            {
                Game = new GameEvent
                {
                    StaticCatalogSnapshot = new MsgStaticCatalogSnapshot
                    {
                        Snapshot = new StaticCatalogSnapshot
                        {
                            Buildings =
                            {
                                new BuildingCatalogEntry { Id = id, Name = name }
                            }
                        }
                    }
                }
            };
        }
    }
}
