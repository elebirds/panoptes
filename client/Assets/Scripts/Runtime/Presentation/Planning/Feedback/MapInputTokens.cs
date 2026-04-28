/*************************************************
 * Project: Panoptes
 * File: MapInputTokens.cs
 * Author: Panoptes Team
 * Date: 2026-04-29
 * Description: Shared planning token normalization for presentation input and feedback.
 *************************************************/

using System;
using Panoptes.Core.Application.Cache;

namespace Panoptes.Presentation.Planning.Feedback
{
    /// <summary>
    /// Normalizes presentation-facing catalog and command tokens without making gameplay decisions.
    /// </summary>
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
