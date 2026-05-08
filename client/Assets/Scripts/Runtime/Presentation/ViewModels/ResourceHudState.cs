using System;
using System.Collections.Generic;
using System.Linq;

namespace Panoptes.Presentation.ViewModels
{
    public sealed class ResourceHudRowState
    {
        public ResourceHudRowState(
            string key,
            int amount,
            string iconKey = "",
            bool isPoint = false,
            string displayName = "",
            string description = "")
        {
            Key = string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim();
            Amount = amount;
            IconKey = string.IsNullOrWhiteSpace(iconKey) ? string.Empty : iconKey.Trim();
            IsPoint = isPoint;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Key : displayName.Trim();
            Description = string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
        }

        public int Amount { get; }
        public string Description { get; }
        public string DisplayName { get; }
        public string IconKey { get; }
        public bool IsPoint { get; }
        public string Key { get; }
    }

    public sealed class ResourceHudState
    {
        public ResourceHudState(IEnumerable<ResourceHudRowState> rows = null)
        {
            Rows = rows == null
                ? Array.Empty<ResourceHudRowState>()
                : rows.Where(row => row != null).ToArray();
        }

        public IReadOnlyList<ResourceHudRowState> Rows { get; }
    }
}
