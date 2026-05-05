using System;
using System.Collections.Generic;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using R3;

namespace Panoptes.Presentation.ViewModels
{
    public sealed class RecipeSynthesisViewModel : ManagementPanelViewModelBase
    {
        private readonly RecipeSynthesisContextStore _contextStore;
        private readonly GameStateStore _gameStateStore;
        private readonly PlanningDraftStore _planningDraftStore;
        private readonly StaticCatalogStore _staticCatalogStore;

        public RecipeSynthesisViewModel(
            StaticCatalogStore staticCatalogStore,
            PlanningDraftStore planningDraftStore,
            RecipeSynthesisContextStore contextStore,
            GameStateStore gameStateStore)
        {
            _staticCatalogStore = staticCatalogStore ?? throw new ArgumentNullException(nameof(staticCatalogStore));
            _planningDraftStore = planningDraftStore ?? throw new ArgumentNullException(nameof(planningDraftStore));
            _contextStore = contextStore ?? throw new ArgumentNullException(nameof(contextStore));
            _gameStateStore = gameStateStore ?? throw new ArgumentNullException(nameof(gameStateStore));
            AddSubscription(_staticCatalogStore.State.Subscribe(this, static (_, self) => self.Publish()));
            AddSubscription(_planningDraftStore.State.Subscribe(this, static (_, self) => self.Publish()));
            AddSubscription(_contextStore.State.Subscribe(this, static (_, self) => self.Publish()));
            AddSubscription(_gameStateStore.State.Subscribe(this, static (_, self) => self.Publish()));
            Publish();
        }

        protected override ManagementPanelState Project()
        {
            var context = _contextStore.Current;
            if (context == null || !context.HasContext)
            {
                return new ManagementPanelState("配方");
            }

            var catalog = _staticCatalogStore.Snapshot;
            if (catalog?.Recipes == null || catalog.Recipes.Count == 0)
            {
                return new ManagementPanelState("配方");
            }

            var contextNodeId = Normalize(context.NodeId);
            var contextBuildingTypeId = Normalize(context.BuildingTypeId);
            var draft = _planningDraftStore.Snapshot;
            var game = _gameStateStore.Snapshot;
            var selectedRecipeId = ResolveSelectedRecipeId(draft, game, contextNodeId);
            var preview = ResolvePreview(draft, contextNodeId);
            var recipes = new List<CatalogRecipeDto>(catalog.Recipes.Values);
            recipes.Sort(CompareRecipes);

            var groups = new Dictionary<string, List<ManagementPanelRowState>>(StringComparer.Ordinal);
            for (var i = 0; i < recipes.Count; i++)
            {
                var recipe = recipes[i];
                if (recipe == null || string.IsNullOrWhiteSpace(recipe.Id))
                {
                    continue;
                }

                var buildingId = Normalize(recipe.BuildingId);
                if (!string.Equals(buildingId, contextBuildingTypeId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!groups.TryGetValue(buildingId, out var rows))
                {
                    rows = new List<ManagementPanelRowState>();
                    groups[buildingId] = rows;
                }

                var recipeId = Normalize(recipe.Id);
                rows.Add(new ManagementPanelRowState(
                    recipeId,
                    recipe.Name,
                    ResolveRecipeSummary(recipe),
                    recipe.Description,
                    ResolveStatus(recipeId, selectedRecipeId, preview),
                    "选择",
                    recipe.IconKey,
                    prerequisiteIds: null,
                    costs: BuildRecipeCosts(recipe, catalog),
                    outputs: BuildRecipeOutputs(recipe, catalog),
                    emptyCostsLabel: "无消耗"));
            }

            return new ManagementPanelState("配方", BuildGroups(groups));
        }

        private static RecipePreviewDto ResolvePreview(PlanningDraftState draft, string contextNodeId)
        {
            var preview = draft?.CurrentRecipePreview;
            if (preview == null || !string.Equals(Normalize(preview.NodeId), contextNodeId, StringComparison.Ordinal))
            {
                return null;
            }

            return preview;
        }

        private static string ResolveSelectedRecipeId(
            PlanningDraftState draft,
            GameStateStoreState game,
            string contextNodeId)
        {
            var draftSelection = ResolveDraftSelectedRecipeId(draft, contextNodeId);
            return !string.IsNullOrEmpty(draftSelection)
                ? draftSelection
                : ResolveOperationSelectedRecipeId(game, contextNodeId);
        }

        private static string ResolveDraftSelectedRecipeId(PlanningDraftState draft, string contextNodeId)
        {
            var selections = draft?.RecipeSelections;
            if (selections == null)
            {
                return string.Empty;
            }

            for (var i = 0; i < selections.Count; i++)
            {
                if (!string.Equals(Normalize(selections[i]?.NodeId), contextNodeId, StringComparison.Ordinal))
                {
                    continue;
                }

                var recipeId = Normalize(selections[i]?.RecipeId);
                if (!string.IsNullOrEmpty(recipeId))
                {
                    return recipeId;
                }
            }

            return string.Empty;
        }

        private static string ResolveOperationSelectedRecipeId(GameStateStoreState game, string contextNodeId)
        {
            var nodes = game?.Nodes;
            if (nodes == null || string.IsNullOrEmpty(contextNodeId))
            {
                return string.Empty;
            }

            if (nodes.TryGetValue(contextNodeId, out var node))
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

        private static string ResolveStatus(string recipeId, string selectedRecipeId, RecipePreviewDto preview)
        {
            if (string.Equals(recipeId, selectedRecipeId, StringComparison.Ordinal))
            {
                return "已选择";
            }

            if (preview == null || !string.Equals(recipeId, Normalize(preview.RecipeId), StringComparison.Ordinal))
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(preview.ErrorCode))
            {
                return $"预览失败：{preview.ErrorCode.Trim()}";
            }

            return preview.Valid ? "预览可用" : "预览中";
        }

        private static string ResolveRecipeSummary(CatalogRecipeDto recipe)
        {
            if (recipe == null)
            {
                return string.Empty;
            }

            return $"工作量：{recipe.WorkAmount}，基础进度：{recipe.BaseProgress}";
        }

        private static IReadOnlyList<ManagementPanelAmountState> BuildRecipeCosts(
            CatalogRecipeDto recipe,
            StaticCatalogState catalog)
        {
            var result = new List<ManagementPanelAmountState>();
            AddAmounts(result, recipe?.ResourceInputs, catalog?.Resources, "resource_");
            AddAmounts(result, recipe?.PointInputs, catalog?.Points, "point_");
            return result;
        }

        private static IReadOnlyList<ManagementPanelAmountState> BuildRecipeOutputs(
            CatalogRecipeDto recipe,
            StaticCatalogState catalog)
        {
            var result = new List<ManagementPanelAmountState>();
            AddAmounts(result, recipe?.Outputs?.Resources, catalog?.Resources, "resource_");
            AddAmounts(result, recipe?.Outputs?.PointProgress, catalog?.Points, "point_");
            AddStringOutputs(result, recipe?.Outputs?.Units, catalog?.Units);
            return result;
        }

        private static void AddAmounts(
            List<ManagementPanelAmountState> result,
            IReadOnlyList<CatalogAmountDto> amounts,
            IReadOnlyDictionary<string, CatalogHudEntryDto> catalog,
            string fallbackPrefix)
        {
            if (result == null || amounts == null)
            {
                return;
            }

            for (var i = 0; i < amounts.Count; i++)
            {
                var amount = amounts[i];
                if (amount == null || string.IsNullOrWhiteSpace(amount.Key))
                {
                    continue;
                }

                var key = Normalize(amount.Key);
                CatalogHudEntryDto entry = null;
                catalog?.TryGetValue(key, out entry);
                result.Add(new ManagementPanelAmountState(
                    key,
                    LocalizeAmountName(key),
                    amount.Amount,
                    !string.IsNullOrWhiteSpace(entry?.IconKey) ? entry.IconKey : fallbackPrefix + key));
            }
        }

        private static void AddStringOutputs(
            List<ManagementPanelAmountState> result,
            IReadOnlyList<string> unitIds,
            IReadOnlyDictionary<string, CatalogUnitDto> units)
        {
            if (result == null || unitIds == null)
            {
                return;
            }

            for (var i = 0; i < unitIds.Count; i++)
            {
                var key = Normalize(unitIds[i]);
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                CatalogUnitDto unit = null;
                units?.TryGetValue(key, out unit);
                result.Add(new ManagementPanelAmountState(
                    key,
                    string.IsNullOrWhiteSpace(unit?.Name) ? LocalizeAmountName(key) : unit.Name,
                    1,
                    !string.IsNullOrWhiteSpace(unit?.IconKey) ? unit.IconKey : "unit_" + key));
            }
        }

        private static string LocalizeAmountName(string key)
        {
            return Normalize(key) switch
            {
                "food" => "粮食",
                "wood" => "木材",
                "ore" => "矿石",
                "research_output" => "科研",
                "industry_output" => "工业",
                "settler" => "开拓者",
                "infantry" => "步兵",
                "archer" => "弓手",
                "cavalry" => "骑兵",
                "spearman" => "枪兵",
                _ => key
            };
        }

        private static IReadOnlyList<ManagementPanelGroupState> BuildGroups(Dictionary<string, List<ManagementPanelRowState>> groups)
        {
            var keys = new List<string>(groups.Keys);
            keys.Sort(StringComparer.OrdinalIgnoreCase);
            var result = new List<ManagementPanelGroupState>(keys.Count);
            for (var i = 0; i < keys.Count; i++)
            {
                result.Add(new ManagementPanelGroupState(keys[i], keys[i], groups[keys[i]]));
            }

            return result;
        }

        private static int CompareRecipes(CatalogRecipeDto left, CatalogRecipeDto right)
        {
            var buildingCompare = string.Compare(left?.BuildingId, right?.BuildingId, StringComparison.OrdinalIgnoreCase);
            if (buildingCompare != 0)
            {
                return buildingCompare;
            }

            var sortCompare = (left?.SortOrder ?? 0).CompareTo(right?.SortOrder ?? 0);
            return sortCompare != 0
                ? sortCompare
                : string.Compare(left?.Id, right?.Id, StringComparison.OrdinalIgnoreCase);
        }
    }
}
