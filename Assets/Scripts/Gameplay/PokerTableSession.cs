using System;
using System.Collections.Generic;
using Holdem;

namespace SoloPokering.Gameplay
{
    /// <summary>
    /// High-level table orchestration for an 8-seat offline table.
    /// It owns lobby state, avatar query results, seat reservations, and deferred add/kick operations.
    /// </summary>
    public sealed class PokerTableSession
    {
        private const int HumanSeatIndex = 0;
        private const string HumanAccentHex = "#6AB1FF";
        private const string HumanSecondaryHex = "#10253E";

        private static readonly PokerSeatVisualPosition[] SeatPositions =
        {
            PokerSeatVisualPosition.BottomCenter,
            PokerSeatVisualPosition.BottomLeft,
            PokerSeatVisualPosition.LeftMid,
            PokerSeatVisualPosition.TopLeft,
            PokerSeatVisualPosition.TopCenter,
            PokerSeatVisualPosition.TopRight,
            PokerSeatVisualPosition.RightMid,
            PokerSeatVisualPosition.BottomRight
        };

        // Fill top and side seats first so the table looks balanced as bots are added.
        private static readonly int[] SeatJoinPriority = { 4, 3, 5, 2, 6, 1, 7 };
        private static readonly float[] SeatAnchorX = { 0.50f, 0.20f, 0.15f, 0.25f, 0.50f, 0.75f, 0.85f, 0.80f };
        private static readonly float[] SeatAnchorY = { 0.18f, 0.32f, 0.52f, 0.80f, 0.86f, 0.80f, 0.52f, 0.32f };

        private readonly PokerMatchSettings settings;
        private readonly SeatRuntimeState[] seats;
        private readonly Dictionary<int, int> activePlayerSeatMap;
        private readonly Random sessionRandom;

        private OfflinePokerGame currentGame;
        private PokerGameSnapshot currentHandSnapshot;
        private string botQuery;
        private string sessionMessage;
        private int completedHandCount;
        private int lastDealerSeatIndex;
        private bool handRunning;

        private sealed class QueuedBotReservation
        {
            public BotAvatarProfile Profile;
            public DIFFICULTY Difficulty;
        }

        private readonly List<QueuedBotReservation> waitingRoom = new List<QueuedBotReservation>();

        private sealed class SeatRuntimeState
        {
            public int SeatIndex;
            public PokerSeatVisualPosition Position;
            public bool IsHuman;
            public PokerPlayerDefinition PlayerDefinition;
            public int ChipStack;
            public BotAvatarProfile BotProfile;
            public BotAvatarProfile PendingJoinProfile;
            public DIFFICULTY AssignedDifficulty;
            public DIFFICULTY PendingJoinDifficulty;
            public bool PendingLeave;

            public bool IsOccupied
            {
                get { return PlayerDefinition != null; }
            }

            public bool IsBot
            {
                get { return IsOccupied && !IsHuman; }
            }

            public bool IsBusted
            {
                get { return IsOccupied && ChipStack <= 0; }
            }

            public void SetHuman(string playerName, int chipStack)
            {
                IsHuman = true;
                PlayerDefinition = PokerPlayerDefinition.Human(playerName);
                ChipStack = chipStack;
                BotProfile = null;
                PendingJoinProfile = null;
                AssignedDifficulty = DIFFICULTY.MEDIUM;
                PendingJoinDifficulty = DIFFICULTY.MEDIUM;
                PendingLeave = false;
            }

            public void SetBot(BotAvatarProfile profile, int chipStack, DIFFICULTY difficulty)
            {
                IsHuman = false;
                PlayerDefinition = PokerPlayerDefinition.Bot(profile.PlayingStyle, difficulty, profile.DisplayName);
                ChipStack = chipStack;
                BotProfile = profile;
                PendingJoinProfile = null;
                AssignedDifficulty = difficulty;
                PendingJoinDifficulty = difficulty;
                PendingLeave = false;
            }

            public void QueueBot(BotAvatarProfile profile, DIFFICULTY difficulty)
            {
                PendingJoinProfile = profile;
                PendingJoinDifficulty = difficulty;
            }

            public void ClearBot()
            {
                if (IsHuman)
                    return;

                PlayerDefinition = null;
                ChipStack = 0;
                BotProfile = null;
                PendingJoinProfile = null;
                AssignedDifficulty = DIFFICULTY.MEDIUM;
                PendingJoinDifficulty = DIFFICULTY.MEDIUM;
                PendingLeave = false;
            }
        }

        public PokerTableSession(PokerMatchSettings initialSettings = null)
        {
            settings = (initialSettings ?? new PokerMatchSettings()).CreateCopy();
            settings.MaximumPlayers = PokerMatchSettings.RecommendedSeatCount;
            settings.MinimumPlayers = Math.Min(settings.MinimumPlayers, settings.MaximumPlayers);
            settings.Validate();

            seats = new SeatRuntimeState[settings.MaximumPlayers];
            activePlayerSeatMap = new Dictionary<int, int>();
            sessionRandom = new Random();

            for (int i = 0; i < seats.Length; i++)
            {
                seats[i] = new SeatRuntimeState
                {
                    SeatIndex = i,
                    Position = SeatPositions[i]
                };
            }

            seats[HumanSeatIndex].SetHuman(settings.HumanPlayerName, settings.StartingBank);
            botQuery = string.Empty;
            currentHandSnapshot = new PokerGameSnapshot();
            sessionMessage = "Table ready. Add bots, tweak the settings, then start the hand.";
            lastDealerSeatIndex = -1;
        }

        public PokerMatchSettings Settings
        {
            get { return settings; }
        }

        public void RenameHuman(string playerName)
        {
            settings.HumanPlayerName = string.IsNullOrWhiteSpace(playerName) ? "You" : playerName.Trim();
            settings.Validate();
            seats[HumanSeatIndex].SetHuman(settings.HumanPlayerName, seats[HumanSeatIndex].ChipStack <= 0 ? settings.StartingBank : seats[HumanSeatIndex].ChipStack);
            sessionMessage = "Player name updated to " + settings.HumanPlayerName + ".";
        }

        public void SetStartingBank(int amount)
        {
            settings.StartingBank = Math.Max(1, amount);

            if (!handRunning && completedHandCount == 0)
            {
                for (int i = 0; i < seats.Length; i++)
                {
                    if (seats[i].IsOccupied)
                        seats[i].ChipStack = settings.StartingBank;
                }
            }

            sessionMessage = completedHandCount == 0
                ? "Starting bank set to " + settings.StartingBank + "."
                : "Starting bank updated for future joins and fresh matches.";
        }

        public void SetTurnTimer(int seconds)
        {
            settings.TurnTimerSeconds = Math.Max(5, seconds);
            sessionMessage = "Turn timer set to " + settings.TurnTimerSeconds + " seconds.";
        }

        public void SetBlindValues(int smallBlind, int bigBlind)
        {
            settings.SmallBlind = Math.Max(1, smallBlind);
            settings.BigBlind = Math.Max(settings.SmallBlind + 1, bigBlind);
            sessionMessage = "Blinds updated to " + settings.SmallBlind + "/" + settings.BigBlind + ".";
        }

        public void SetBotMode(PokerBotMode mode)
        {
            settings.BotMode = mode;
            switch (mode)
            {
                case PokerBotMode.Easy:
                    settings.DefaultBotDifficulty = DIFFICULTY.EASY;
                    break;
                case PokerBotMode.Hard:
                    settings.DefaultBotDifficulty = DIFFICULTY.HARD;
                    break;
                case PokerBotMode.Random:
                    settings.DefaultBotDifficulty = DIFFICULTY.MEDIUM;
                    break;
                default:
                    settings.DefaultBotDifficulty = DIFFICULTY.MEDIUM;
                    break;
            }

            sessionMessage = "Bot mode set to " + mode + ".";
        }

        public void SetBotQuery(string query)
        {
            botQuery = string.IsNullOrWhiteSpace(query) ? string.Empty : query.Trim();
        }

        public bool QueueAddBot(string profileId, out string feedback)
        {
            BotAvatarProfile profile = BotAvatarProfile.GetById(profileId);
            if (profile == null)
            {
                feedback = "Bot profile not found.";
                return false;
            }

    // Kiểm tra xem Bot này đã ngồi ở ghế hoặc đã nằm trong hàng chờ chưa
            bool isAlreadyInWaitingRoom = waitingRoom.Exists(reservation => reservation.Profile.Id == profile.Id);
            if (IsProfileAlreadyClaimed(profile.Id) || isAlreadyInWaitingRoom)
            {
                feedback = profile.DisplayName + " is already in line.";
                sessionMessage = feedback;
                return false;
            }

            DIFFICULTY difficulty = ResolveDifficultyForNewBot();
            int targetSeatIndex = FindBestSeatForJoin();
    
    // NẾU HẾT GHẾ TRỐNG -> ĐẨY VÀO HÀNG CHỜ VÔ HẠN
    if (targetSeatIndex < 0)
    {
        waitingRoom.Add(new QueuedBotReservation
        {
            Profile = profile,
            Difficulty = difficulty
        });

        feedback = profile.DisplayName + " added to the waiting room.";
    }
    else
    {
        // Vẫn còn chỗ (hoặc chỗ đang chờ trống) -> Đặt chỗ vào ghế cụ thể
        SeatRuntimeState targetSeat = seats[targetSeatIndex];
        if (handRunning)
            targetSeat.QueueBot(profile, difficulty);
        else
            targetSeat.SetBot(profile, settings.StartingBank, difficulty);

        feedback = profile.DisplayName + " reserved seat " + targetSeatIndex + ".";
    }

    sessionMessage = feedback;
    return true;
}

        public bool ToggleKickBot(int seatIndex, out string feedback)
        {
            if (seatIndex <= HumanSeatIndex || seatIndex >= seats.Length)
            {
                feedback = "That seat cannot be removed.";
                sessionMessage = feedback;
                return false;
            }

            SeatRuntimeState seat = seats[seatIndex];

            if (seat.PendingJoinProfile != null && !seat.IsOccupied)
            {
                string pendingName = seat.PendingJoinProfile.DisplayName;
                seat.PendingJoinProfile = null;
                seat.PendingJoinDifficulty = DIFFICULTY.MEDIUM;
                feedback = "Cancelled queued join for " + pendingName + ".";
                sessionMessage = feedback;
                return true;
            }

            if (!seat.IsOccupied || seat.IsHuman)
            {
                feedback = "There is no removable bot in that seat.";
                sessionMessage = feedback;
                return false;
            }

            if (handRunning)
            {
                seat.PendingLeave = !seat.PendingLeave;
                feedback = seat.PendingLeave
                    ? seat.PlayerDefinition.Name + " is marked to leave after this hand."
                    : "Cancelled pending kick for " + seat.PlayerDefinition.Name + ".";
            }
            else
            {
                string botName = seat.PlayerDefinition.Name;
                seat.ClearBot();
                feedback = "Removed " + botName + " from seat " + seatIndex + ".";
            }

            sessionMessage = feedback;
            return true;
        }

        public bool ApplyPendingSeatChangesNow(out string feedback)
        {
            if (handRunning)
            {
                feedback = "Seat changes unlock after the current hand reaches showdown.";
                sessionMessage = feedback;
                return false;
            }

            if (CountPendingJoins() == 0 && CountPendingLeaves() == 0)
            {
                feedback = "There are no queued seat changes to apply.";
                sessionMessage = feedback;
                return false;
            }

            ApplyPendingSeatChangesInternal();
            feedback = "Queued seat changes have been applied to the table.";
            sessionMessage = feedback;
            return true;
        }

        public bool StartNextHand(out string feedback)
        {
            feedback = string.Empty;

            if (handRunning)
            {
                feedback = "The current hand is still running.";
                sessionMessage = feedback;
                return false;
            }

            ApplyPendingSeatChangesInternal();

            if (CountEligiblePlayers() < settings.MinimumPlayers)
            {
                feedback = "At least 2 active players are required to start a hand.";
                sessionMessage = feedback;
                return false;
            }

            PokerMatchSettings handSettings = settings.CreateCopy();
            currentGame = new OfflinePokerGame(handSettings);

            int dealerPlayerIndex;
            currentGame.ConfigurePlayers(BuildConfiguredPlayersForNextHand(out dealerPlayerIndex));
            currentGame.SetNextDealerIndex(dealerPlayerIndex);

            currentHandSnapshot = currentGame.StartNewHand();
            handRunning = !IsHandResolved(currentHandSnapshot);
            feedback = currentHandSnapshot.BannerMessage;
            sessionMessage = feedback;

            if (!handRunning)
                FinalizeResolvedHand();

            return true;
        }

        public PokerTableSessionSnapshot HumanFold()
        {
            return ExecuteHumanAction(delegate { return currentGame.HumanFold(); });
        }

        public PokerTableSessionSnapshot HumanCheckOrCall()
        {
            return ExecuteHumanAction(delegate { return currentGame.HumanCheckOrCall(); });
        }

        public PokerTableSessionSnapshot HumanRaiseOrBet(int amount)
        {
            return ExecuteHumanAction(delegate { return currentGame.HumanRaiseOrBet(amount); });
        }

        public PokerTableSessionSnapshot HumanAllIn()
        {
            return ExecuteHumanAction(delegate { return currentGame.HumanAllIn(); });
        }

        public PokerTableSessionSnapshot AdvanceAutoPlay()
        {
            if (currentGame == null || !handRunning)
                return GetSnapshot();

            currentHandSnapshot = currentGame.AdvanceOneStep();
            if (IsHandResolved(currentHandSnapshot))
                FinalizeResolvedHand();
            else
                sessionMessage = currentHandSnapshot.BannerMessage;

            return GetSnapshot();
        }

        public PokerTableSessionSnapshot GetSnapshot()
        {
            PokerTableSessionSnapshot snapshot = new PokerTableSessionSnapshot
            {
                HumanPlayerName = settings.HumanPlayerName,
                StartingBank = settings.StartingBank,
                SmallBlind = settings.SmallBlind,
                BigBlind = settings.BigBlind,
                TurnTimerSeconds = settings.TurnTimerSeconds,
                IsHandRunning = handRunning,
                IsShowingResolvedHand = !handRunning && IsHandResolved(currentHandSnapshot),
                CanStartGame = !handRunning && CountProjectedEligiblePlayers() >= settings.MinimumPlayers,
                CanApplyPendingSeatChanges = !handRunning && (CountPendingJoins() > 0 || CountPendingLeaves() > 0),
                HasPendingSeatChanges = CountPendingJoins() > 0 || CountPendingLeaves() > 0,
                SettingsApplyToNextHand = handRunning || completedHandCount > 0,
                OccupiedSeatCount = CountOccupiedSeats(),
                EligiblePlayerCount = CountEligiblePlayers(),
                AvailableSeatCount = Math.Max(0, settings.MaximumPlayers - GetProjectedOccupiedSeatCount()),
                PendingJoinCount = CountPendingJoins(),
                PendingLeaveCount = CountPendingLeaves(),
                BotQuery = botQuery,
                BannerMessage = sessionMessage,
                PendingSeatSummary = BuildPendingSeatSummary(),
                SettingsStatusLabel = BuildSettingsStatusLabel(),
                HandSnapshot = currentHandSnapshot ?? new PokerGameSnapshot()
            };

            for (int i = 0; i < seats.Length; i++)
                snapshot.Seats.Add(BuildSeatSnapshot(i));

            List<BotAvatarProfileSnapshot> botResults = BuildBotQueryResults();
            for (int i = 0; i < botResults.Count; i++)
                snapshot.BotQueryResults.Add(botResults[i]);

            return snapshot;
        }

        private PokerTableSessionSnapshot ExecuteHumanAction(Func<PokerGameSnapshot> action)
        {
            if (currentGame == null || !handRunning)
                return GetSnapshot();

            currentHandSnapshot = action();
            if (IsHandResolved(currentHandSnapshot))
                FinalizeResolvedHand();
            else
                sessionMessage = currentHandSnapshot.BannerMessage;

            return GetSnapshot();
        }

        private void FinalizeResolvedHand()
        {
            SyncSeatChipStacksFromResolvedHand();
            RememberDealerSeatFromResolvedHand();
            handRunning = false;
            currentGame = null;
            completedHandCount++;
            sessionMessage = !string.IsNullOrWhiteSpace(currentHandSnapshot.WinnerMessage)
                ? currentHandSnapshot.WinnerMessage
                : currentHandSnapshot.BannerMessage;

            if (seats[HumanSeatIndex].ChipStack <= 0)
                sessionMessage = "Game over. You are out of chips.";
        }

        private void SyncSeatChipStacksFromResolvedHand()
        {
            if (currentHandSnapshot == null)
                return;

            for (int playerIndex = 0; playerIndex < currentHandSnapshot.Players.Count; playerIndex++)
            {
                int seatIndex;
                if (!activePlayerSeatMap.TryGetValue(playerIndex, out seatIndex))
                    continue;

                seats[seatIndex].ChipStack = currentHandSnapshot.Players[playerIndex].ChipStack;
            }
        }

        private void RememberDealerSeatFromResolvedHand()
        {
            if (currentHandSnapshot == null)
                return;

            for (int playerIndex = 0; playerIndex < currentHandSnapshot.Players.Count; playerIndex++)
            {
                PokerPlayerSnapshot player = currentHandSnapshot.Players[playerIndex];
                if (!player.IsDealer)
                    continue;

                int seatIndex;
                if (activePlayerSeatMap.TryGetValue(playerIndex, out seatIndex))
                    lastDealerSeatIndex = seatIndex;

                return;
            }
        }

        private int ResolveDealerSeatIndexForNextHand()
        {
            if (lastDealerSeatIndex < 0)
                return -1;

            for (int offset = 1; offset <= seats.Length; offset++)
            {
                int seatIndex = (lastDealerSeatIndex + offset) % seats.Length;
                if (seats[seatIndex].IsOccupied && seats[seatIndex].ChipStack > 0)
                    return seatIndex;
            }

            return -1;
        }

        private DIFFICULTY ResolveDifficultyForNewBot()
        {
            switch (settings.BotMode)
            {
                case PokerBotMode.Easy:
                    return DIFFICULTY.EASY;
                case PokerBotMode.Hard:
                    return DIFFICULTY.HARD;
                case PokerBotMode.Random:
                    return (DIFFICULTY)sessionRandom.Next(0, 3);
                default:
                    return DIFFICULTY.MEDIUM;
            }
        }

        private List<PokerConfiguredPlayer> BuildConfiguredPlayersForNextHand(out int dealerPlayerIndex)
        {
            List<PokerConfiguredPlayer> configuredPlayers = new List<PokerConfiguredPlayer>();
            activePlayerSeatMap.Clear();
            dealerPlayerIndex = -1;
            int dealerSeatIndex = ResolveDealerSeatIndexForNextHand();

            int activeIndex = 0;
            for (int seatIndex = 0; seatIndex < seats.Length; seatIndex++)
            {
                SeatRuntimeState seat = seats[seatIndex];
                if (!seat.IsOccupied || seat.ChipStack <= 0)
                    continue;

                configuredPlayers.Add(new PokerConfiguredPlayer(seat.PlayerDefinition, seat.ChipStack));
                activePlayerSeatMap.Add(activeIndex, seatIndex);
                if (seatIndex == dealerSeatIndex)
                    dealerPlayerIndex = activeIndex;
                activeIndex++;
            }

            return configuredPlayers;
        }

        private void ApplyPendingSeatChangesInternal()
        {
            for (int i = 1; i < seats.Length; i++)
            {
                if (seats[i].IsOccupied && seats[i].ChipStack <= 0)
                    seats[i].ClearBot();
            }
            // Xóa những đứa đã đánh dấu rời bàn
            for (int i = 1; i < seats.Length; i++)
            {
                if (seats[i].PendingLeave)
                    seats[i].ClearBot();
            }

            // Nhét những đứa đã chốt ghế từ trước vào
            for (int i = 1; i < seats.Length; i++)
            {
                if (seats[i].PendingJoinProfile != null)
                {
                    BotAvatarProfile profile = seats[i].PendingJoinProfile;
                    DIFFICULTY difficulty = seats[i].PendingJoinDifficulty;
                    seats[i].SetBot(profile, settings.StartingBank, difficulty);
                }
            }

            // BỐC TỪ HÀNG CHỜ VÔ HẠN VÀO CÁC GHẾ CÒN TRỐNG
            while (waitingRoom.Count > 0)
            {
                int emptySeat = FindBestSeatForJoin();
                if (emptySeat < 0) break; // Hết ghế trống thì ngưng

                QueuedBotReservation reservation = waitingRoom[0];
                seats[emptySeat].SetBot(reservation.Profile, settings.StartingBank, reservation.Difficulty);
                waitingRoom.RemoveAt(0); // Xóa khỏi hàng chờ
            }
        }

        private PokerTableSeatSnapshot BuildSeatSnapshot(int seatIndex)
        {
            SeatRuntimeState seat = seats[seatIndex];
            PokerPlayerSnapshot handPlayer = FindHandPlayerForSeat(seatIndex);

            PokerTableSeatSnapshot snapshot = new PokerTableSeatSnapshot
            {
                SeatIndex = seatIndex,
                Position = seat.Position,
                AnchorX = SeatAnchorX[seatIndex],
                AnchorY = SeatAnchorY[seatIndex],
                IsHuman = seat.IsHuman,
                IsOccupied = seat.IsOccupied,
                IsAvailable = !seat.IsOccupied && seat.PendingJoinProfile == null,
                IsBot = seat.IsBot,
                IsBusted = seat.IsBusted,
                IsPendingJoin = seat.PendingJoinProfile != null,
                IsPendingLeave = seat.PendingLeave,
                CanToggleKick = seatIndex > HumanSeatIndex && (seat.IsOccupied || seat.PendingJoinProfile != null),
                CanQueueBot = seatIndex > HumanSeatIndex && (!seat.IsOccupied || seat.PendingLeave) && seat.PendingJoinProfile == null,
                HasQueuedReplacement = seat.IsOccupied && seat.PendingJoinProfile != null,
                PendingStatus = BuildSeatPendingStatus(seat),
                SeatStatusLabel = BuildSeatStatusLabel(seat, handPlayer),
                AvatarDescription = string.Empty,
                DifficultyLabel = string.Empty,
                PlayingStyleLabel = string.Empty,
                QueuedReplacementName = seat.PendingJoinProfile != null ? seat.PendingJoinProfile.DisplayName : string.Empty,
                HoleCards = new List<PokerCardSnapshot>()
            };

            if (seat.IsHuman)
            {
                snapshot.DisplayName = seat.PlayerDefinition.Name;
                snapshot.AvatarInitials = BuildInitials(seat.PlayerDefinition.Name);
                snapshot.AvatarTitle = "Human";
                snapshot.AvatarDescription = "Local player";
                snapshot.AvatarAccentHex = HumanAccentHex;
                snapshot.AvatarSecondaryHex = HumanSecondaryHex;
            }
            else if (seat.IsOccupied && seat.BotProfile != null)
            {
                snapshot.DisplayName = seat.PlayerDefinition.Name;
                snapshot.AvatarProfileId = seat.BotProfile.Id;
                snapshot.AvatarInitials = seat.BotProfile.Initials;
                snapshot.AvatarTitle = seat.BotProfile.Title;
                snapshot.AvatarDescription = seat.BotProfile.Description;
                snapshot.AvatarAccentHex = seat.BotProfile.AccentHex;
                snapshot.AvatarSecondaryHex = seat.BotProfile.SecondaryHex;
                snapshot.DifficultyLabel = seat.AssignedDifficulty.ToString();
                snapshot.PlayingStyleLabel = seat.BotProfile.PlayingStyle.ToString();
            }
            else if (seat.PendingJoinProfile != null)
            {
                snapshot.DisplayName = "Reserved";
                snapshot.AvatarProfileId = seat.PendingJoinProfile.Id;
                snapshot.AvatarInitials = seat.PendingJoinProfile.Initials;
                snapshot.AvatarTitle = seat.PendingJoinProfile.Title;
                snapshot.AvatarDescription = seat.PendingJoinProfile.Description;
                snapshot.AvatarAccentHex = seat.PendingJoinProfile.AccentHex;
                snapshot.AvatarSecondaryHex = seat.PendingJoinProfile.SecondaryHex;
                snapshot.DifficultyLabel = seat.PendingJoinDifficulty.ToString();
                snapshot.PlayingStyleLabel = seat.PendingJoinProfile.PlayingStyle.ToString();
            }

            if (seat.IsOccupied)
            {
                snapshot.ChipStack = handPlayer != null ? handPlayer.ChipStack : seat.ChipStack;
                snapshot.IsDealer = handPlayer != null && handPlayer.IsDealer;
                snapshot.IsSmallBlind = handPlayer != null && handPlayer.IsSmallBlind;
                snapshot.IsBigBlind = handPlayer != null && handPlayer.IsBigBlind;
                snapshot.IsCurrentTurn = handPlayer != null && handPlayer.IsCurrentTurn;
                snapshot.IsFolded = handPlayer != null && handPlayer.IsFolded;
                snapshot.AmountInPot = handPlayer != null ? handPlayer.AmountInPot : 0;
                snapshot.ActionText = handPlayer != null ? handPlayer.ActionText : string.Empty;
                snapshot.BestHandText = handPlayer != null ? handPlayer.BestHandText : string.Empty;

                if (handPlayer != null && handPlayer.HoleCards != null)
                {
                    for (int i = 0; i < handPlayer.HoleCards.Count; i++)
                        snapshot.HoleCards.Add(CloneCardSnapshot(handPlayer.HoleCards[i]));
                }
            }

            return snapshot;
        }

        private List<BotAvatarProfileSnapshot> BuildBotQueryResults()
        {
            List<BotAvatarProfileSnapshot> results = new List<BotAvatarProfileSnapshot>();
            IReadOnlyList<BotAvatarProfile> catalog = BotAvatarProfile.GetCatalog();

            for (int i = 0; i < catalog.Count; i++)
            {
                BotAvatarProfile profile = catalog[i];
                if (!profile.Matches(botQuery))
                    continue;

                int targetSeat = FindBestSeatForJoin();
                string availabilityLabel = "Ready";
                bool canAdd = true;
                bool isSeated = false;
                bool isQueued = false;
                bool isMarkedForLeave = false;

                // Kiểm tra xem nó có đang rớt vào list phòng chờ không
                bool isWaitingInRoom = waitingRoom.Exists(reservation => reservation.Profile.Id == profile.Id);

                int seatedIndex = FindSeatByProfileId(profile.Id);
                if (seatedIndex >= 0)
                {
                    isSeated = true;
                    isMarkedForLeave = seats[seatedIndex].PendingLeave;
                    canAdd = false;
                    if (isMarkedForLeave)
                        availabilityLabel = "Seat " + seatedIndex + " leaves after showdown";
                    else
                        availabilityLabel = "Already at seat " + seatedIndex;
                }
                else
                {
                    int queuedIndex = FindQueuedSeatByProfileId(profile.Id);
                    if (queuedIndex >= 0)
                    {
                        isQueued = true;
                        canAdd = false;
                        availabilityLabel = "Queued for seat " + queuedIndex;
                        targetSeat = queuedIndex;
                    }
                    else if (isWaitingInRoom)
                    {
                        // Đang nằm trong hàng chờ vô hạn
                        isQueued = true;
                        canAdd = false;
                        availabilityLabel = "In waiting room";
                    }
                    else if (targetSeat < 0)
                    {
                        // Bàn đã đầy -> Vẫn cho phép bấm Add để ném vào Waiting Room
                        canAdd = true;
                        availabilityLabel = "Join waiting room";
                    }
                    else if (handRunning)
                    {
                        availabilityLabel = "Join seat " + targetSeat + " after showdown";
                    }
                    else
                    {
                        availabilityLabel = "Add to seat " + targetSeat;
                    }
                }

                results.Add(new BotAvatarProfileSnapshot
                {
                    Id = profile.Id,
                    DisplayName = profile.DisplayName,
                    Initials = profile.Initials,
                    Title = profile.Title,
                    Description = profile.Description,
                    AccentHex = profile.AccentHex,
                    SecondaryHex = profile.SecondaryHex,
                    DifficultyLabel = profile.Difficulty.ToString(),
                    PlayingStyleLabel = profile.PlayingStyle.ToString(),
                    CanAdd = canAdd,
                    IsSeated = isSeated,
                    IsQueued = isQueued,
                    IsMarkedForLeave = isMarkedForLeave,
                    WillReplaceSeat = targetSeat >= 0 && (targetSeat < seats.Length) && seats[targetSeat].PendingLeave,
                    AvailabilityLabel = availabilityLabel,
                    CurrentSeatIndex = isSeated ? seatedIndex : (isQueued ? targetSeat : -1),
                    TargetSeatIndex = targetSeat
                });
            }

            return results;
        }
        private PokerPlayerSnapshot FindHandPlayerForSeat(int seatIndex)
        {
            if (currentHandSnapshot == null)
                return null;

            for (int i = 0; i < currentHandSnapshot.Players.Count; i++)
            {
                int mappedSeatIndex;
                if (activePlayerSeatMap.TryGetValue(i, out mappedSeatIndex) && mappedSeatIndex == seatIndex)
                    return currentHandSnapshot.Players[i];
            }

            return null;
        }

        private bool IsProfileAlreadyClaimed(string profileId)
        {
            return FindSeatByProfileId(profileId) >= 0 || FindQueuedSeatByProfileId(profileId) >= 0;
        }

        private int FindBestSeatForJoin()
        {
            for (int index = 0; index < SeatJoinPriority.Length; index++)
            {
                int seatIndex = SeatJoinPriority[index];
                if (!seats[seatIndex].IsOccupied && seats[seatIndex].PendingJoinProfile == null)
                    return seatIndex;
            }

            for (int index = 0; index < SeatJoinPriority.Length; index++)
            {
                int seatIndex = SeatJoinPriority[index];
                if (seats[seatIndex].PendingLeave && seats[seatIndex].PendingJoinProfile == null)
                    return seatIndex;
            }

            return -1;
        }

        private int FindSeatByProfileId(string profileId)
        {
            for (int i = 1; i < seats.Length; i++)
            {
                if (seats[i].BotProfile != null && string.Equals(seats[i].BotProfile.Id, profileId, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private int FindQueuedSeatByProfileId(string profileId)
        {
            for (int i = 1; i < seats.Length; i++)
            {
                if (seats[i].PendingJoinProfile != null && string.Equals(seats[i].PendingJoinProfile.Id, profileId, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private int CountOccupiedSeats()
        {
            int count = 0;
            for (int i = 0; i < seats.Length; i++)
            {
                if (seats[i].IsOccupied)
                    count++;
            }

            return count;
        }

        private int CountEligiblePlayers()
        {
            int count = 0;
            for (int i = 0; i < seats.Length; i++)
            {
                if (seats[i].IsOccupied && seats[i].ChipStack > 0)
                    count++;
            }

            return count;
        }

        private int CountProjectedEligiblePlayers()
        {
            int count = 0;
            for (int i = 0; i < seats.Length; i++)
            {
                bool willLeave = seats[i].PendingLeave;
                bool willJoin = seats[i].PendingJoinProfile != null;

                if (seats[i].IsOccupied && !willLeave && seats[i].ChipStack > 0)
                    count++;
                else if (!seats[i].IsOccupied && willJoin)
                    count++;
                else if (seats[i].IsOccupied && willLeave && willJoin)
                    count++;
            }

            int emptyProjectedSeats = Math.Max(0, seats.Length - count);
            count += Math.Min(waitingRoom.Count, emptyProjectedSeats);

            return count;
        }

        private int GetProjectedOccupiedSeatCount()
        {
            int count = 0;
            for (int i = 0; i < seats.Length; i++)
            {
                if (seats[i].IsOccupied && !seats[i].PendingLeave)
                    count++;

                if (!seats[i].IsOccupied && seats[i].PendingJoinProfile != null)
                    count++;
                else if (seats[i].IsOccupied && seats[i].PendingLeave && seats[i].PendingJoinProfile != null)
                    count++;
            }

            count += Math.Min(waitingRoom.Count, Math.Max(0, seats.Length - count));

            return count;
        }

        private int CountPendingJoins()
        {
            int count = waitingRoom.Count;
            for (int i = 0; i < seats.Length; i++)
            {
                if (seats[i].PendingJoinProfile != null)
                    count++;
            }

            return count;
        }

        private int CountPendingLeaves()
        {
            int count = 0;
            for (int i = 0; i < seats.Length; i++)
            {
                if (seats[i].PendingLeave)
                    count++;
            }

            return count;
        }

        private string BuildPendingSeatSummary()
        {
            int joins = CountPendingJoins();
            int leaves = CountPendingLeaves();

            if (joins == 0 && leaves == 0)
                return string.Empty;

            if (handRunning)
            {
                if (joins > 0 && leaves > 0)
                    return joins + " bot(s) queued to join and " + leaves + " bot(s) marked to leave after showdown.";

                if (joins > 0)
                    return joins + " bot(s) queued to join after showdown.";

                return leaves + " bot(s) marked to leave after showdown.";
            }

            if (joins > 0 && leaves > 0)
                return joins + " bot(s) ready to join and " + leaves + " bot(s) ready to leave before the next hand.";

            if (joins > 0)
                return joins + " bot(s) ready to join before the next hand.";

            return leaves + " bot(s) ready to leave before the next hand.";
        }

        private string BuildSeatPendingStatus(SeatRuntimeState seat)
        {
            string timingLabel = handRunning ? "after showdown" : "before the next hand";

            if (seat.PendingLeave && seat.PendingJoinProfile != null)
                return seat.PlayerDefinition.Name + " leaves " + timingLabel + " and " + seat.PendingJoinProfile.DisplayName + " takes this seat.";

            if (seat.PendingLeave)
                return seat.PlayerDefinition.Name + " leaves " + timingLabel + ".";

            if (seat.PendingJoinProfile != null)
                return seat.PendingJoinProfile.DisplayName + " joins this seat " + timingLabel + ".";

            return string.Empty;
        }

        private string BuildSeatStatusLabel(SeatRuntimeState seat, PokerPlayerSnapshot handPlayer)
        {
            if (seat.PendingLeave && seat.PendingJoinProfile != null)
                return "Marked for swap";

            if (seat.PendingLeave)
                return "Marked for kick";

            if (seat.PendingJoinProfile != null && !seat.IsOccupied)
                return handRunning ? "Queued join" : "Reserved next";

            if (!seat.IsOccupied)
                return "Open seat";

            if (seat.IsBusted)
                return "Busted";

            if (handPlayer != null)
            {
                // ---> 1. LOGIC CHO NGƯỜI THẮNG (Sẽ in ra: WINNER: ACES FULL OF KINGS)
                if (handPlayer.ActionText == "WINNER")
                {
                    if (!string.IsNullOrWhiteSpace(handPlayer.BestHandText))
                        return "WINNER: " + handPlayer.BestHandText.ToUpper();
                    return "WINNER";
                }

                // ---> 2. LOGIC LÚC LẬT BÀI CHO NGƯỜI THUA (Sẽ in ra: PAIR OF TENS)
                if (currentHandSnapshot != null && 
                   (currentHandSnapshot.Phase == PokerGamePhase.Showdown || 
                    currentHandSnapshot.Phase == PokerGamePhase.HandComplete || 
                    currentHandSnapshot.Phase == PokerGamePhase.MatchComplete))
                {
                    // Nếu không Fold thì hiện tên bộ bài đang cầm
                    if (!handPlayer.IsFolded && !string.IsNullOrWhiteSpace(handPlayer.BestHandText))
                        return handPlayer.BestHandText.ToUpper();
                }
                
                if (handPlayer.IsCurrentTurn)
                    return "Acting";

                if (handPlayer.IsFolded)
                    return "Folded";

                if (!string.IsNullOrWhiteSpace(handPlayer.ActionText))
                    return handPlayer.ActionText;
            }

            return seat.IsHuman ? "Ready" : "Seated";
        }

        private string BuildSettingsStatusLabel()
        {
            if (handRunning)
                return "Table setting changes are saved for the next hand.";

            if (completedHandCount == 0)
                return "Table settings are ready for the first hand.";

            return "Table settings will be used when the next hand starts.";
        }

        private bool IsHandResolved(PokerGameSnapshot handSnapshot)
        {
            if (handSnapshot == null)
                return false;

            return handSnapshot.Phase == PokerGamePhase.HandComplete || handSnapshot.Phase == PokerGamePhase.MatchComplete;
        }

        private static string BuildInitials(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return "YO";

            string[] parts = displayName.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
                return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant();

            string left = parts[0].Substring(0, 1).ToUpperInvariant();
            string right = parts[parts.Length - 1].Substring(0, 1).ToUpperInvariant();
            return left + right;
        }

        private static PokerCardSnapshot CloneCardSnapshot(PokerCardSnapshot source)
        {
            return new PokerCardSnapshot
            {
                ResourceKey = source.ResourceKey,
                DisplayName = source.DisplayName,
                IsFaceUp = source.IsFaceUp,
                IsHighlighted = source.IsHighlighted
            };
        }
    }
}
