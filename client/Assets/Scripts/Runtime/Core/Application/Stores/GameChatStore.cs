using System.Collections.Generic;
using Panoptes.Core.Domain;

namespace Panoptes.Core.Application.Stores
{
    public sealed class GameChatStore : ReactiveStore<GameChatState>
    {
        public GameChatStore()
            : base(new GameChatState())
        {
        }

        internal void Replace(IReadOnlyList<GameChatEntryDto> entries)
        {
            Publish(new GameChatState(entries));
        }

        internal void Append(GameChatEntryDto entry)
        {
            if (entry == null)
            {
                return;
            }

            var entries = new List<GameChatEntryDto>(Snapshot.Entries) { entry };
            Publish(new GameChatState(entries));
        }

        internal void Clear()
        {
            Publish(new GameChatState());
        }

        protected override GameChatState CloneState(GameChatState state)
        {
            return state == null ? new GameChatState() : state.Clone();
        }
    }
}
