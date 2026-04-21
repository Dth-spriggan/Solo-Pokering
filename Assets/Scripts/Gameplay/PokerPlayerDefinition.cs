using System;
using Holdem;

namespace SoloPokering.Gameplay
{
    public sealed class PokerPlayerDefinition
    {
        public string Name { get; private set; }
        public bool IsHuman { get; private set; }
        public DIFFICULTY Difficulty { get; private set; }
        public PLAYINGSTYLE PlayingStyle { get; private set; }

        private PokerPlayerDefinition()
        {
        }

        public static PokerPlayerDefinition Human(string name)
        {
            return new PokerPlayerDefinition
            {
                Name = string.IsNullOrWhiteSpace(name) ? "You" : name.Trim(),
                IsHuman = true,
                Difficulty = DIFFICULTY.MEDIUM,
                PlayingStyle = PLAYINGSTYLE.TIGHT
            };
        }

        public static PokerPlayerDefinition Bot(PLAYINGSTYLE playingStyle, DIFFICULTY difficulty, string customName = null)
        {
            return new PokerPlayerDefinition
            {
                Name = string.IsNullOrWhiteSpace(customName) ? GetDefaultBotName(playingStyle) : customName.Trim(),
                IsHuman = false,
                Difficulty = difficulty,
                PlayingStyle = playingStyle
            };
        }

        internal Player BuildPlayer(int buyInAmount)
        {
            if (IsHuman)
                return new Player(Name, buyInAmount);

            AIPlayer bot = new AIPlayer(buyInAmount, Difficulty, PlayingStyle);
            if (!string.IsNullOrWhiteSpace(Name))
                bot.Name = Name;

            return bot;
        }

        public PokerPlayerDefinition Clone()
        {
            return IsHuman ? Human(Name) : Bot(PlayingStyle, Difficulty, Name);
        }

        private static string GetDefaultBotName(PLAYINGSTYLE style)
        {
            switch (style)
            {
                case PLAYINGSTYLE.BLUFFER:
                    return "Stewie";
                case PLAYINGSTYLE.AGRESSIVE:
                    return "Rachel";
                default:
                    return "Ray";
            }
        }
    }
}
