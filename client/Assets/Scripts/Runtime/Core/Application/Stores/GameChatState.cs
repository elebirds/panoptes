using System.Collections.Generic;
using Panoptes.Core.Domain;

namespace Panoptes.Core.Application.Stores
{
    public sealed class GameChatState
    {
        public GameChatState(IReadOnlyList<GameChatEntryDto> entries = null)
        {
            Entries = StoreSnapshotCloner.CloneGameChatEntries(entries);
        }

        public IReadOnlyList<GameChatEntryDto> Entries { get; }

        internal GameChatState Clone()
        {
            return new GameChatState(Entries);
        }
    }
}
