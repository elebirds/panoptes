/*************************************************
 * Project: Panoptes
 * File: SquadUnitRenderBudgetPresenter.cs
 * Author: Panoptes Team
 * Date: 2026-05-01
 * Description: Renderer budget helper for squad unit visuals.
 *************************************************/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Panoptes.Presentation.Map
{
    public static class SquadUnitRenderBudgetPresenter
    {
        public readonly struct Options
        {
            public Options(
                int maxVisibleRenderers,
                bool disableCastShadows,
                bool disableReceiveShadows,
                string[] rendererPriorityKeywords)
            {
                MaxVisibleRenderers = maxVisibleRenderers;
                DisableCastShadows = disableCastShadows;
                DisableReceiveShadows = disableReceiveShadows;
                RendererPriorityKeywords = rendererPriorityKeywords;
            }

            public int MaxVisibleRenderers { get; }

            public bool DisableCastShadows { get; }

            public bool DisableReceiveShadows { get; }

            public string[] RendererPriorityKeywords { get; }
        }

        public static void Apply(Transform memberRoot, Options options)
        {
            if (memberRoot == null)
            {
                return;
            }

            var renderers = memberRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return;
            }

            var rendererCap = Mathf.Max(1, options.MaxVisibleRenderers);
            var scored = new List<(Renderer renderer, int score)>(renderers.Length);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (renderer.gameObject == null || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var score = CalculateRendererScore(renderer, options.RendererPriorityKeywords);
                scored.Add((renderer, score));
            }

            scored.Sort((a, b) => b.score.CompareTo(a.score));
            var keepSet = new HashSet<Renderer>();
            for (var i = 0; i < scored.Count && i < rendererCap; i++)
            {
                var renderer = scored[i].renderer;
                if (renderer != null)
                {
                    keepSet.Add(renderer);
                }
            }

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                var keep = keepSet.Contains(renderer);
                renderer.enabled = keep;

                if (!keep)
                {
                    continue;
                }

                if (options.DisableCastShadows)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }

                if (options.DisableReceiveShadows)
                {
                    renderer.receiveShadows = false;
                }
            }
        }

        public static bool IsOverThreshold(Transform memberRoot, int threshold)
        {
            if (memberRoot == null)
            {
                return false;
            }

            var renderers = memberRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return false;
            }

            var resolvedThreshold = Mathf.Max(1, threshold);
            var activeRendererCount = 0;
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer != null && renderer.gameObject != null && renderer.gameObject.activeInHierarchy)
                {
                    activeRendererCount++;
                }
            }

            return activeRendererCount > resolvedThreshold;
        }

        private static int CalculateRendererScore(Renderer renderer, string[] rendererPriorityKeywords)
        {
            if (renderer == null)
            {
                return int.MinValue;
            }

            var score = 0;
            var normalizedName = NormalizeToken(renderer.name);
            var goName = renderer.gameObject != null ? NormalizeToken(renderer.gameObject.name) : string.Empty;
            if (rendererPriorityKeywords != null)
            {
                for (var i = 0; i < rendererPriorityKeywords.Length; i++)
                {
                    var key = NormalizeToken(rendererPriorityKeywords[i]);
                    if (string.IsNullOrEmpty(key))
                    {
                        continue;
                    }

                    if (normalizedName.Contains(key) || goName.Contains(key))
                    {
                        score += 100 - i;
                    }
                }
            }

            if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
            {
                score += Mathf.Clamp(skinned.sharedMesh.vertexCount / 500, 0, 120);
            }

            return score;
        }

        private static string NormalizeToken(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }
    }
}
