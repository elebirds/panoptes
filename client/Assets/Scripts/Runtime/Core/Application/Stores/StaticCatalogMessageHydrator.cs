using System;
using Panoptes.Core.Infrastructure.Network;
using Panoptes.Protocol.V1;

namespace Panoptes.Core.Application.Stores
{
    public sealed class StaticCatalogMessageHydrator : IDisposable
    {
        private readonly MessageDispatcher _dispatcher;
        private readonly StaticCatalogStore _staticCatalogStore;
        private bool _attached;

        public StaticCatalogMessageHydrator(
            MessageDispatcher dispatcher,
            StaticCatalogStore staticCatalogStore)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _staticCatalogStore = staticCatalogStore ?? throw new ArgumentNullException(nameof(staticCatalogStore));
        }

        public void Attach()
        {
            if (_attached)
            {
                return;
            }

            _dispatcher.Register<MsgStaticCatalogSnapshot>("MsgStaticCatalogSnapshot", HandleStaticCatalogSnapshot);
            _attached = true;
        }

        public void Dispose()
        {
            Detach();
        }

        public void Detach()
        {
            if (!_attached)
            {
                return;
            }

            _dispatcher.Unregister<MsgStaticCatalogSnapshot>("MsgStaticCatalogSnapshot", HandleStaticCatalogSnapshot);
            _attached = false;
        }

        public void HandleStaticCatalogSnapshot(MsgStaticCatalogSnapshot msg)
        {
            if (msg == null)
            {
                return;
            }

            _staticCatalogStore.Replace(StaticCatalogProtocolMapper.ToState(msg.Snapshot));
        }
    }
}
