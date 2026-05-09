using System;
using Panoptes.Core.Application.Stores;

namespace Panoptes.Presentation.ViewModels
{
    internal static class RecipeSynthesisSelectionResolver
    {
        public static string ResolveSelectedRecipeId(PlanningDraftState draft, GameStateStoreState game, string contextNodeId)
        {
            return TryResolvePlannedRecipeId(draft, contextNodeId, out var plannedRecipeId)
                ? plannedRecipeId
                : ResolveActiveRecipeId(game, contextNodeId);
        }

        public static string ResolvePlannedRecipeId(PlanningDraftState draft, string contextNodeId)
        {
            return TryResolvePlannedRecipeId(draft, contextNodeId, out var plannedRecipeId)
                ? plannedRecipeId
                : string.Empty;
        }

        public static bool TryResolvePlannedRecipeId(PlanningDraftState draft, string contextNodeId, out string recipeId)
        {
            var selections = draft?.RecipeSelections;
            contextNodeId = Normalize(contextNodeId);
            if (selections == null || string.IsNullOrWhiteSpace(contextNodeId))
            {
                recipeId = string.Empty;
                return false;
            }

            for (var i = 0; i < selections.Count; i++)
            {
                if (!string.Equals(Normalize(selections[i]?.NodeId), contextNodeId, StringComparison.Ordinal))
                {
                    continue;
                }

                recipeId = Normalize(selections[i]?.RecipeId);
                return true;
            }

            recipeId = string.Empty;
            return false;
        }

        public static string ResolveActiveRecipeId(GameStateStoreState game, string contextNodeId)
        {
            var nodes = game?.Nodes;
            contextNodeId = Normalize(contextNodeId);
            if (nodes == null || string.IsNullOrEmpty(contextNodeId))
            {
                return string.Empty;
            }

            if (nodes.TryGetValue(contextNodeId, out var node) ||
                nodes.TryGetValue(contextNodeId.ToUpperInvariant(), out node))
            {
                return Normalize(node?.OperationSelectedRecipeId);
            }

            foreach (var candidate in nodes.Values)
            {
                if (string.Equals(Normalize(candidate?.Id), contextNodeId, StringComparison.Ordinal))
                {
                    return Normalize(candidate?.OperationSelectedRecipeId);
                }
            }

            return string.Empty;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
        }
    }
}
