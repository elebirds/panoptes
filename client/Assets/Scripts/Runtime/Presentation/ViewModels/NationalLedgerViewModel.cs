using System;
using System.Collections.Generic;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using R3;

namespace Panoptes.Presentation.ViewModels
{
    public sealed class NationalLedgerViewModel : ManagementPanelViewModelBase
    {
        private readonly GameStateStore _gameStateStore;
        private readonly StaticCatalogStore _staticCatalogStore;

        public NationalLedgerViewModel(GameStateStore gameStateStore, StaticCatalogStore staticCatalogStore)
        {
            _gameStateStore = gameStateStore ?? throw new ArgumentNullException(nameof(gameStateStore));
            _staticCatalogStore = staticCatalogStore ?? throw new ArgumentNullException(nameof(staticCatalogStore));
            AddSubscription(_gameStateStore.State.Subscribe(this, static (_, self) => self.Publish()));
            AddSubscription(_staticCatalogStore.State.Subscribe(this, static (_, self) => self.Publish()));
            Publish();
        }

        protected override ManagementPanelState Project()
        {
            var game = _gameStateStore.Snapshot;
            var groups = new List<ManagementPanelGroupState>
            {
                new ManagementPanelGroupState("overview", "Overview", BuildOverviewRows(game)),
                new ManagementPanelGroupState("resources", "Resources", BuildResourceRows(game.MyResources)),
                new ManagementPanelGroupState("catalog", "Catalog", BuildCatalogRows(_staticCatalogStore.Snapshot))
            };

            return new ManagementPanelState("National Ledger", groups);
        }

        private static IReadOnlyList<ManagementPanelRowState> BuildOverviewRows(GameStateStoreState game)
        {
            return new List<ManagementPanelRowState>
            {
                new("turn", "Turn", game.Turn.ToString(), game.Phase),
                new("tokens", "Tokens", game.TokensLeft.ToString(), "Remaining planning tokens"),
                new("map", "Map", $"{game.MapWidth} x {game.MapHeight}", "Known world size"),
                new("nodes", "Nodes", (game.Nodes?.Count ?? 0).ToString(), "Visible/read model nodes"),
                new("units", "Units", (game.Units?.Count ?? 0).ToString(), "Known units")
            };
        }

        private static IReadOnlyList<ManagementPanelRowState> BuildResourceRows(ResourceDto resources)
        {
            var rows = new List<ManagementPanelRowState>();
            if (resources == null)
            {
                return rows;
            }

            AddResource(rows, "food", "Food", resources.Food);
            AddResource(rows, "wood", "Wood", resources.Wood);
            AddResource(rows, "ore", "Ore", resources.Ore);
            AddResource(rows, "industry_output", "Industry Output", resources.IndustryOutput);
            return rows;
        }

        private static IReadOnlyList<ManagementPanelRowState> BuildCatalogRows(StaticCatalogState catalog)
        {
            return new List<ManagementPanelRowState>
            {
                new("buildings", "Buildings", (catalog.Buildings?.Count ?? 0).ToString(), "Known building definitions"),
                new("recipes", "Recipes", (catalog.Recipes?.Count ?? 0).ToString(), "Known production recipes"),
                new("tech", "Technologies", (catalog.Technologies?.Count ?? 0).ToString(), "Known technologies"),
                new("policies", "Policies", (catalog.Policies?.Count ?? 0).ToString(), "Known policies"),
                new("institutions", "Institutions", (catalog.Institutions?.Count ?? 0).ToString(), "Known institutions"),
                new("units", "Units", (catalog.Units?.Count ?? 0).ToString(), "Known unit definitions")
            };
        }

        private static void AddResource(List<ManagementPanelRowState> rows, string id, string title, int amount)
        {
            rows.Add(new ManagementPanelRowState(id, title, amount.ToString(), "Stockpile"));
        }
    }
}
