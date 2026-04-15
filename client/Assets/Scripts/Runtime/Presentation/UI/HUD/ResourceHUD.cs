/*************************************************
 * Project: Panoptes
 * File: ResourceHUD.cs
 * Author: Panoptes Team
 * Date: 2026-04-04
 * Description: Resource HUD placeholder.
 *************************************************/

using System;
using TMPro;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Domain;
using UnityEngine;

namespace Panoptes.Presentation.UI.HUD
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

            var resources = GameStateCache.Instance != null
                ? GameStateCache.Instance.GetMyResources()
                : new ResourceDto();
            var items = new[]
            {
                ("木材", resources.Wood),
                ("粮食", resources.Food),
                ("矿石", resources.Ore),
                ("工业产出", resources.IndustryOutput)
            };

            var format = string.IsNullOrWhiteSpace(lineFormat) ? "{0}: {1}" : lineFormat;
            for (var i = 0; i < resourceLines.Length; i++)
            {
                var line = resourceLines[i];
                if (line == null)
                {
                    continue;
                }

                if (i >= items.Length)
                {
                    line.text = string.Empty;
                    continue;
                }

                line.text = string.Format(format, items[i].Item1, items[i].Item2);
            }
        }
    }
}
