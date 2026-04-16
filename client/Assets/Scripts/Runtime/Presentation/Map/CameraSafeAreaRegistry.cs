using System.Collections.Generic;
using UnityEngine;

namespace Panoptes.Presentation.Map
{
    public static class CameraSafeAreaRegistry
    {
        private static readonly HashSet<CameraSafeAreaSource> Sources = new();

        public static void Register(CameraSafeAreaSource source)
        {
            if (source != null)
            {
                Sources.Add(source);
            }
        }

        public static void Unregister(CameraSafeAreaSource source)
        {
            if (source != null)
            {
                Sources.Remove(source);
            }
        }

        public static Rect GetSafeViewportRect()
        {
            CollectLiveSources();

            var left = 0f;
            var right = 0f;
            var top = 0f;
            var bottom = 0f;

            Sources.RemoveWhere(source => source == null);

            foreach (var source in Sources)
            {
                if (!source.TryGetContribution(out var edge, out var normalizedSize))
                {
                    continue;
                }

                switch (edge)
                {
                    case CameraSafeAreaEdge.Left:
                        left += normalizedSize;
                        break;
                    case CameraSafeAreaEdge.Right:
                        right += normalizedSize;
                        break;
                    case CameraSafeAreaEdge.Top:
                        top += normalizedSize;
                        break;
                    case CameraSafeAreaEdge.Bottom:
                        bottom += normalizedSize;
                        break;
                }
            }

            left = Mathf.Clamp(left, 0f, 0.8f);
            right = Mathf.Clamp(right, 0f, 0.8f);
            top = Mathf.Clamp(top, 0f, 0.8f);
            bottom = Mathf.Clamp(bottom, 0f, 0.8f);

            var width = Mathf.Max(0.05f, 1f - left - right);
            var height = Mathf.Max(0.05f, 1f - top - bottom);
            return new Rect(left, bottom, width, height);
        }

        private static void CollectLiveSources()
        {
            var liveSources = Object.FindObjectsByType<CameraSafeAreaSource>(FindObjectsInactive.Include);
            for (var i = 0; i < liveSources.Length; i++)
            {
                if (liveSources[i] != null)
                {
                    Sources.Add(liveSources[i]);
                }
            }
        }
    }
}
