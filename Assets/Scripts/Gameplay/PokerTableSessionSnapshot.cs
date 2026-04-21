using System;
using System.Collections.Generic;

namespace SoloPokering.Gameplay
{
    public enum PokerSeatVisualPosition
    {
        BottomCenter,
        BottomLeft,
        LeftMid,
        TopLeft,
        TopRight,
        RightMid,
        BottomRight
    }

    [Serializable]
    public sealed class BotAvatarProfileSnapshot
    {
        public string Id;
        public string DisplayName;
        public string Initials;
        public string Title;
        public string Description;
        public string AccentHex;
        public string SecondaryHex;
        public string DifficultyLabel;
        public string PlayingStyleLabel;
        public bool CanAdd;
        public bool IsSeated;
        public bool IsQueued;
        public bool IsMarkedForLeave;
        public bool WillReplaceSeat;
        public string AvailabilityLabel;
        public int CurrentSeatIndex;
        public int TargetSeatIndex;
    }

    [Serializable]
    public sealed class PokerTableSeatSnapshot
    {
        public int SeatIndex;
        public PokerSeatVisualPosition Position;
        public float AnchorX;
        public float AnchorY;
        public bool IsHuman;
        public bool IsOccupied;
        public bool IsAvailable;
        public bool IsBot;
        public bool IsBusted;
        public bool IsDealer;
        public bool IsCurrentTurn;
        public bool IsFolded;
        public bool IsPendingJoin;
        public bool IsPendingLeave;
        public bool CanToggleKick;
        public bool CanQueueBot;
        public bool HasQueuedReplacement;
        public string PendingStatus;
        public string SeatStatusLabel;
        public string DisplayName;
        public string AvatarProfileId;
        public string AvatarInitials;
        public string AvatarTitle;
        public string AvatarDescription;
        public string AvatarAccentHex;
        public string AvatarSecondaryHex;
        public string DifficultyLabel;
        public string PlayingStyleLabel;
        public string QueuedReplacementName;
        public int ChipStack;
        public int AmountInPot;
        public string ActionText;
        public string BestHandText;
        public List<PokerCardSnapshot> HoleCards;
    }

    [Serializable]
    public sealed class PokerTableSessionSnapshot
    {
        public PokerTableSessionSnapshot()
        {
            Seats = new List<PokerTableSeatSnapshot>();
            BotQueryResults = new List<BotAvatarProfileSnapshot>();
            HandSnapshot = new PokerGameSnapshot();
        }

        public string HumanPlayerName;
        public int StartingBank;
        public int SmallBlind;
        public int BigBlind;
        public int TurnTimerSeconds;
        public bool IsHandRunning;
        public bool IsShowingResolvedHand;
        public bool CanStartGame;
        public bool CanApplyPendingSeatChanges;
        public bool HasPendingSeatChanges;
        public bool SettingsApplyToNextHand;
        public int OccupiedSeatCount;
        public int EligiblePlayerCount;
        public int AvailableSeatCount;
        public int PendingJoinCount;
        public int PendingLeaveCount;
        public string BotQuery;
        public string BannerMessage;
        public string PendingSeatSummary;
        public string SettingsStatusLabel;
        public PokerGameSnapshot HandSnapshot;
        public List<PokerTableSeatSnapshot> Seats;
        public List<BotAvatarProfileSnapshot> BotQueryResults;
    }
}
