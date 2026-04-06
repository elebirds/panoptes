/*************************************************
 * Project: Panoptes
 * File: ResourceHUD.cs
 * Author: Panoptes Team
 * Date: 2026-04-04
 * Description: Resource HUD placeholder.
 *************************************************/

using UnityEngine;
using TMPro;
using Panoptes.Runtime.Cache;
using Panoptes.Protocol.V1;
using System.Collections.Generic;

namespace Panoptes.Runtime.UI.HUD
{
    public sealed class ResourceHUD : MonoBehaviour
    {
        [SerializeField] private TMP_Text[] resourceLines;
        [SerializeField] private string lineFormat = "{0}: {1}";

        private void OnEnable()
        {
            if (GameStateCache.Instance != null)
            {
                GameStateCache.Instance.OnStateChanged += Refresh;
            }

            if (StaticCatalogCache.Instance != null)
            {
                StaticCatalogCache.Instance.CatalogChanged += Refresh;
            }

            Refresh();
        }

        private void OnDisable()
        {
            if (GameStateCache.Instance != null)
            {
                GameStateCache.Instance.OnStateChanged -= Refresh;
            }

            if (StaticCatalogCache.Instance != null)
            {
                StaticCatalogCache.Instance.CatalogChanged -= Refresh;
            }
        }

        private void Refresh()
        {
            if (resourceLines == null || resourceLines.Length == 0)
            {
                return;
            }

            var player = GameStateCache.Instance != null ? GameStateCache.Instance.MyPlayer : null;
            var items = player != null && player.Resources != null ? player.Resources.Items : null;
            var valuesByKey = new Dictionary<string, int>();
            if (items != null)
            {
                for (var i = 0; i < items.Count; i++)
                {
                    ResourceValue item = items[i];
                    if (item == null || string.IsNullOrWhiteSpace(item.Key))
                    {
                        continue;
                    }

                    valuesByKey[item.Key] = item.Amount;
                }
            }

            var entries = StaticCatalogCache.Instance != null
                ? StaticCatalogCache.Instance.Resources
                : null;

            var lineIndex = 0;
            if (entries != null)
            {
                foreach (var pair in entries)
                {
                    if (lineIndex >= resourceLines.Length)
                    {
                        break;
                    }

                    var line = resourceLines[lineIndex];
                    if (line != null)
                    {
                        var entry = pair.Value;
                        var amount = valuesByKey.TryGetValue(entry.key, out var value) ? value : 0;
                        line.text = string.Format(lineFormat, entry.display_name, amount);
                    }

                    lineIndex++;
                }
            }

            for (; lineIndex < resourceLines.Length; lineIndex++)
            {
                if (resourceLines[lineIndex] != null)
                {
                    resourceLines[lineIndex].text = string.Empty;
                }
            }
        }
    }
}
