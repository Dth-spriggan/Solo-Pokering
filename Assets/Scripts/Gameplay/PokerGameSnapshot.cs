using System;
using System.Collections.Generic;

namespace SoloPokering.Gameplay
{
    public enum PokerGamePhase
    {
        NotStarted,
        PostingBlinds,
        PreFlop,
        Flop,
        Turn,
        River,
        Showdown,
        HandComplete,
        MatchComplete
    }

    public enum PokerActionType
    {
        Fold,
        CheckOrCall,
        RaiseOrBet,
        AllIn
    }

    [Serializable]
    public sealed class PokerCardSnapshot
    {
        public string ResourceKey { get; set; }
        public string DisplayName { get; set; }
        public bool IsFaceUp { get; set; }
        public bool IsHighlighted { get; set; }
    }

    [Serializable]
    public sealed class PokerActionOptions
    {
        public bool CanFold { get; set; }
        public bool CanCheckOrCall { get; set; }
        public bool CanRaiseOrBet { get; set; }
        public bool CanAllIn { get; set; }
        public int AmountToCall { get; set; }
        public int MinimumRaise { get; set; }
        public int MaximumRaise { get; set; }
        public string CheckOrCallLabel { get; set; }
        public string RaiseOrBetLabel { get; set; }
    }

    [Serializable]
    public sealed class PokerPlayerSnapshot
    {
        public int SeatIndex { get; set; }
        public string Name { get; set; }
        public bool IsHuman { get; set; }
        public bool IsDealer { get; set; }
        public bool IsCurrentTurn { get; set; }
        public bool IsFolded { get; set; }
        public bool IsBusted { get; set; }
        public int ChipStack { get; set; }
        public int AmountInPot { get; set; }
        public string ActionText { get; set; }
        public string BestHandText { get; set; }
        public List<PokerCardSnapshot> HoleCards { get; set; }
    }

    [Serializable]
    public sealed class PokerGameSnapshot
    {
        public PokerGameSnapshot()
        {
            CommunityCards = new List<PokerCardSnapshot>();
            Players = new List<PokerPlayerSnapshot>();
            ActionLog = new List<string>();
            HumanActionOptions = new PokerActionOptions();
            CurrentPlayerIndex = -1;
        }

        public PokerGamePhase Phase { get; set; }
        public int CurrentPlayerIndex { get; set; }
        public int PotAmount { get; set; }
        public int SmallBlind { get; set; }
        public int BigBlind { get; set; }
        public bool IsWaitingForHumanInput { get; set; }
        public string BannerMessage { get; set; }
        public string WinnerMessage { get; set; }
        public PokerActionOptions HumanActionOptions { get; set; }
        public List<PokerCardSnapshot> CommunityCards { get; set; }
        public List<PokerPlayerSnapshot> Players { get; set; }
        public List<string> ActionLog { get; set; }
    }
}
