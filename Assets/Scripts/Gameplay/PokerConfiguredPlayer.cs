using System;

namespace SoloPokering.Gameplay
{
    public sealed class PokerConfiguredPlayer
    {
        public PokerConfiguredPlayer(PokerPlayerDefinition definition, int startingChips)
        {
            if (definition == null)
                throw new ArgumentNullException("definition");

            if (startingChips < 0)
                throw new ArgumentOutOfRangeException("startingChips");

            Definition = definition.Clone();
            StartingChips = startingChips;
        }

        public PokerPlayerDefinition Definition { get; private set; }
        public int StartingChips { get; private set; }
    }
}
