using System;
using Holdem;

namespace SoloPokering.Gameplay
{
    public enum PokerPlayMode
    {
        Casual
    }

    [Serializable]
    public sealed class PokerMatchSettings
    {
        public const int RecommendedSeatCount = 7;

        public string HumanPlayerName = "You";
        public int StartingBank = 5000;
        public int SmallBlind = 5;
        public int BigBlind = 10;
        public int TurnTimerSeconds = 30;
        public int MinimumPlayers = 2;
        public int MaximumPlayers = RecommendedSeatCount;
        public PokerPlayMode PlayMode = PokerPlayMode.Casual;
        public DIFFICULTY DefaultBotDifficulty = DIFFICULTY.MEDIUM;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(HumanPlayerName))
                HumanPlayerName = "You";

            if (StartingBank <= 0)
                throw new InvalidOperationException("StartingBank must be greater than zero.");

            if (SmallBlind <= 0)
                throw new InvalidOperationException("SmallBlind must be greater than zero.");

            if (BigBlind <= SmallBlind)
                throw new InvalidOperationException("BigBlind must be greater than SmallBlind.");

            if (TurnTimerSeconds <= 0)
                throw new InvalidOperationException("TurnTimerSeconds must be greater than zero.");

            if (MinimumPlayers < 2)
                throw new InvalidOperationException("MinimumPlayers must be at least 2.");

            if (MaximumPlayers < MinimumPlayers)
                throw new InvalidOperationException("MaximumPlayers must be greater than or equal to MinimumPlayers.");
        }

        public PokerMatchSettings CreateCopy()
        {
            return new PokerMatchSettings
            {
                HumanPlayerName = HumanPlayerName,
                StartingBank = StartingBank,
                SmallBlind = SmallBlind,
                BigBlind = BigBlind,
                TurnTimerSeconds = TurnTimerSeconds,
                MinimumPlayers = MinimumPlayers,
                MaximumPlayers = MaximumPlayers,
                PlayMode = PlayMode,
                DefaultBotDifficulty = DefaultBotDifficulty
            };
        }
    }
}
