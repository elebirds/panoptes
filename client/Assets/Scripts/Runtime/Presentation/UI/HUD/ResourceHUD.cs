/*************************************************
 * Project: Panoptes
 * File: ResourceHUD.cs
 * Author: Panoptes Team
 * Date: 2026-04-04
 * Description: Resource HUD placeholder.
 *************************************************/

using UnityEngine;
using TMPro;
using Panoptes.Core.Application.Cache;
using System.Collections.Generic;

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
            // TODO: Replace with real resource data when available.
        }
    }
}
