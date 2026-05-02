using System;
using Panoptes.Core.Application.Cache;

namespace Panoptes.Core.Application.Stores
{
    // Temporary adapter for static catalog bundle/section sync until that path
    // hydrates StaticCatalogStore directly from protocol/application services.
    public sealed class StaticCatalogLegacyHydrationBridge : IDisposable
    {
        private readonly StoreHydrationHelper _helper;
        private StaticCatalogCache _cache;

        public StaticCatalogLegacyHydrationBridge(StoreHydrationHelper helper)
        {
            _helper = helper ?? throw new ArgumentNullException(nameof(helper));
        }

        public void AttachToDefaultCache()
        {
            Attach(StaticCatalogCache.EnsureInstance());
        }

        public void Attach(StaticCatalogCache cache)
        {
            Detach();
            _cache = cache;

            if (_cache != null)
            {
                _cache.CatalogChanged += HydrateStaticCatalog;
            }

            HydrateStaticCatalog();
        }

        public void Dispose()
        {
            Detach();
        }

        private void Detach()
        {
            if (_cache != null)
            {
                _cache.CatalogChanged -= HydrateStaticCatalog;
            }

            _cache = null;
        }

        private void HydrateStaticCatalog()
        {
            _helper.HydrateStaticCatalog(StoreHydrationBootstrapSeeder.CaptureStaticCatalog(_cache));
        }
    }
}
