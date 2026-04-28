using System;
using Panoptes.Core.Application.Cache;

namespace Panoptes.Presentation.Map
{
    public static class MapInputTokens
    {
        public static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        public static bool HasTag(StaticCatalogCache.UnitEntryJson entry, string tag)
        {
            if (entry == null || entry.tags == null || string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            var normalized = Normalize(tag);
            for (var i = 0; i < entry.tags.Length; i++)
            {
                if (string.Equals(Normalize(entry.tags[i]), normalized, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
