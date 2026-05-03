/*************************************************
 * Project: Panoptes
 * File: MapInputTokens.cs
 * Author: Panoptes Team
 * Date: 2026-04-29
 * Description: Shared planning token normalization for presentation input and feedback.
 *************************************************/

using System;
using System.Collections.Generic;

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

        public static bool HasTag(IEnumerable<string> tags, string tag)
        {
            if (tags == null || string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            var normalized = Normalize(tag);
            foreach (var item in tags)
            {
                if (string.Equals(Normalize(item), normalized, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
