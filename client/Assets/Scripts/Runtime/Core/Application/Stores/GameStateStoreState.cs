using System.Collections.Generic;
using Panoptes.Core.Domain;

namespace Panoptes.Core.Application.Stores
{
    public sealed class GameStateStoreState
    {
        public GameStateStoreState(
            string gameId = "",
            string activeGameSessionId = "",
            string myPlayerId = "",
            int turn = 0,
            string phase = "",
            int mapWidth = 0,
            int mapHeight = 0,
            bool isGameOver = false,
            int tokensLeft = 0,
            IReadOnlyDictionary<string, NodeDto> nodes = null,
            IReadOnlyDictionary<string, UnitDto> units = null,
            ResourceDto myResources = null)
        {
            ActiveGameSessionId = activeGameSessionId ?? string.Empty;
            GameId = gameId ?? string.Empty;
            IsGameOver = isGameOver;
            MapHeight = mapHeight;
            MapWidth = mapWidth;
            MyPlayerId = myPlayerId ?? string.Empty;
            MyResources = StoreSnapshotCloner.CloneResource(myResources);
            Nodes = StoreSnapshotCloner.CloneNodes(nodes);
            Phase = phase ?? string.Empty;
            TokensLeft = tokensLeft;
            Turn = turn;
            Units = StoreSnapshotCloner.CloneUnits(units);
        }

        public string ActiveGameSessionId { get; }
        public string GameId { get; }
        public bool IsGameOver { get; }
        public int MapHeight { get; }
        public int MapWidth { get; }
        public string MyPlayerId { get; }
        public ResourceDto MyResources { get; }
        public IReadOnlyDictionary<string, NodeDto> Nodes { get; }
        public string Phase { get; }
        public int TokensLeft { get; }
        public int Turn { get; }
        public IReadOnlyDictionary<string, UnitDto> Units { get; }

        internal GameStateStoreState Clone()
        {
            return new GameStateStoreState(
                GameId,
                ActiveGameSessionId,
                MyPlayerId,
                Turn,
                Phase,
                MapWidth,
                MapHeight,
                IsGameOver,
                TokensLeft,
                Nodes,
                Units,
                MyResources);
        }
    }
}
