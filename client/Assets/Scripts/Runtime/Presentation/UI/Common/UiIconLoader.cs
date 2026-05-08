using UnityEngine;

namespace Panoptes.Presentation.UI.Common
{
    public static class UiIconLoader
    {
        public static Sprite LoadSprite(string iconKey, string fallbackId, params string[] roots)
        {
            var sprite = LoadSpriteByKey(iconKey, roots);
            return sprite != null ? sprite : LoadSpriteByKey(fallbackId, roots);
        }

        public static Sprite LoadSpriteByKey(string key, params string[] roots)
        {
            key = NormalizeIconKey(key);
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            var direct = Resources.Load<Sprite>(key);
            if (direct != null)
            {
                return direct;
            }

            if (roots == null)
            {
                return null;
            }

            for (var i = 0; i < roots.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(roots[i]))
                {
                    continue;
                }

                var root = roots[i].Trim().TrimEnd('/');
                var sprite = Resources.Load<Sprite>($"{root}/{key}");
                if (sprite != null)
                {
                    return sprite;
                }
            }

            return null;
        }

        public static string NormalizeIconKey(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();
        }
    }
}
