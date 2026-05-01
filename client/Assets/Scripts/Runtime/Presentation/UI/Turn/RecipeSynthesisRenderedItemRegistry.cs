using System;
using System.Collections.Generic;

namespace Panoptes.Presentation.UI.Domestic
{
    internal sealed class RecipeSynthesisRenderedItemRegistry
    {
        private readonly List<RecipeSynthesisItemView> _items = new();
        private readonly Dictionary<string, RecipeSynthesisItemView> _byRecipeId = new(StringComparer.OrdinalIgnoreCase);

        public void Register(
            string recipeId,
            RecipeSynthesisItemView item,
            Action<RecipeSynthesisItemView, bool> activationChanged,
            Action<RecipeSynthesisItemView> hoverEntered,
            Action<RecipeSynthesisItemView> hoverExited)
        {
            if (item == null)
            {
                return;
            }

            _items.Add(item);

            var normalizedRecipeId = NormalizeToken(recipeId);
            if (!string.IsNullOrWhiteSpace(normalizedRecipeId))
            {
                _byRecipeId[normalizedRecipeId] = item;
            }

            if (activationChanged != null)
            {
                item.ActivationChanged -= activationChanged;
                item.ActivationChanged += activationChanged;
            }

            if (hoverEntered != null)
            {
                item.HoverEntered -= hoverEntered;
                item.HoverEntered += hoverEntered;
            }

            if (hoverExited != null)
            {
                item.HoverExited -= hoverExited;
                item.HoverExited += hoverExited;
            }
        }

        public bool TryGetRecipe(string recipeId, out RecipeSynthesisItemView item)
        {
            return _byRecipeId.TryGetValue(NormalizeToken(recipeId), out item);
        }

        public bool ContainsRecipe(string recipeId)
        {
            return _byRecipeId.ContainsKey(NormalizeToken(recipeId));
        }

        public void SetActiveRecipe(string selectedRecipeId)
        {
            var normalizedSelectedRecipeId = NormalizeToken(selectedRecipeId);
            foreach (var pair in _byRecipeId)
            {
                var item = pair.Value;
                if (item == null)
                {
                    continue;
                }

                item.SetActiveState(
                    !string.IsNullOrWhiteSpace(normalizedSelectedRecipeId) &&
                    string.Equals(pair.Key, normalizedSelectedRecipeId, StringComparison.Ordinal),
                    notify: false);
            }
        }

        public void Clear(
            Action<RecipeSynthesisItemView> destroyItem,
            Action<RecipeSynthesisItemView, bool> activationChanged,
            Action<RecipeSynthesisItemView> hoverEntered,
            Action<RecipeSynthesisItemView> hoverExited)
        {
            for (var i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (item == null)
                {
                    continue;
                }

                if (activationChanged != null)
                {
                    item.ActivationChanged -= activationChanged;
                }

                if (hoverEntered != null)
                {
                    item.HoverEntered -= hoverEntered;
                }

                if (hoverExited != null)
                {
                    item.HoverExited -= hoverExited;
                }

                destroyItem?.Invoke(item);
            }

            _items.Clear();
            _byRecipeId.Clear();
        }

        private static string NormalizeToken(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }
    }
}
