using System;
using System.Collections.Generic;
using System.Linq;
using Holdem;

namespace SoloPokering.Gameplay
{
    /// <summary>
    /// Texas Hold'em match orchestration used by the Unity-facing session/UI layer.
    /// This class owns turn order, betting flow, blind posting, showdown payout,
    /// and snapshot generation for a single offline table.
    /// </summary>
    public sealed class OfflinePokerGame
    {
        private sealed class RuntimePlayer
        {
            public RuntimePlayer(PokerPlayerDefinition definition, int chipStack)
            {
                Definition = definition.Clone();
                ChipStack = Math.Max(0, chipStack);
                HoleCards = new List<Card>(2);
                ResetForNewHand();
            }

            public PokerPlayerDefinition Definition { get; private set; }
            public List<Card> HoleCards { get; private set; }
            public int ChipStack { get; set; }
            public int TotalContribution { get; set; }
            public int RoundContribution { get; set; }
            public bool Folded { get; set; }
            public bool HasActedThisRound { get; set; }
            public string ActionText { get; set; }
            public string Message { get; set; }

            public bool IsHuman
            {
                get { return Definition.IsHuman; }
            }

            public bool IsAllIn
            {
                get { return !Folded && ChipStack == 0; }
            }

            public bool CanAct
            {
                get { return !Folded && ChipStack > 0; }
            }

            public string Name
            {
                get { return Definition.Name; }
            }

            public void ResetForNewHand()
            {
                HoleCards.Clear();
                TotalContribution = 0;
                RoundContribution = 0;
                Folded = false;
                HasActedThisRound = false;
                ActionText = string.Empty;
                Message = string.Empty;
            }
        }

        private sealed class ShowdownAward
        {
            public int Amount;
            public Hand WinningHand;
            public List<int> Winners = new List<int>();
        }

        private readonly PokerMatchSettings settings;
        private readonly List<PokerPlayerDefinition> playerDefinitions;
        private readonly List<int> configuredStartingChips;
        private readonly List<string> actionLog;
        private readonly List<RuntimePlayer> runtimePlayers;
        private readonly List<Card> communityCards;
        private readonly Random random;

        private Deck deck;
        private PokerGamePhase phase;
        private string bannerMessage;
        private string winnerMessage;
        private bool waitingForHumanInput;
        private int currentPlayerIndex;
        private int dealerIndex;
        private int smallBlindIndex;
        private int bigBlindIndex;
        private int currentBet;
        private int minimumRaise;
        private int blindPostingStep;
        private int? requestedDealerIndex;

        public OfflinePokerGame(PokerMatchSettings settings)
        {
            this.settings = settings ?? new PokerMatchSettings();
            this.settings.Validate();

            playerDefinitions = new List<PokerPlayerDefinition>();
            configuredStartingChips = new List<int>();
            actionLog = new List<string>();
            runtimePlayers = new List<RuntimePlayer>();
            communityCards = new List<Card>();
            random = new Random();

            ResetRuntimeState();
        }

        public PokerMatchSettings Settings
        {
            get { return settings; }
        }

        public PokerGamePhase Phase
        {
            get { return phase; }
        }

        public void SetHumanPlayer(string playerName)
        {
            PokerPlayerDefinition human = PokerPlayerDefinition.Human(playerName);
            int chipStack = configuredStartingChips.Count > 0 ? configuredStartingChips[0] : settings.StartingBank;

            if (playerDefinitions.Count == 0)
                playerDefinitions.Add(human);
            else if (playerDefinitions[0].IsHuman)
                playerDefinitions[0] = human;
            else
                playerDefinitions.Insert(0, human);

            EnsureConfiguredChipCount();
            configuredStartingChips[0] = chipStack;
            ResetRuntimeState();
        }

        public void AddBot(PLAYINGSTYLE style, DIFFICULTY difficulty, string customName = null)
        {
            if (playerDefinitions.Count == 0 || !playerDefinitions[0].IsHuman)
                SetHumanPlayer(settings.HumanPlayerName);

            if (playerDefinitions.Count >= settings.MaximumPlayers)
                throw new InvalidOperationException("The table is already full.");

            playerDefinitions.Add(PokerPlayerDefinition.Bot(style, difficulty, customName));
            configuredStartingChips.Add(settings.StartingBank);
            ResetRuntimeState();
        }

        public void ConfigureDefaultTable(int totalPlayers)
        {
            settings.Validate();

            if (totalPlayers < settings.MinimumPlayers || totalPlayers > settings.MaximumPlayers)
                throw new ArgumentOutOfRangeException("totalPlayers");

            playerDefinitions.Clear();
            configuredStartingChips.Clear();

            playerDefinitions.Add(PokerPlayerDefinition.Human(settings.HumanPlayerName));
            configuredStartingChips.Add(settings.StartingBank);

            List<PLAYINGSTYLE> styles = new List<PLAYINGSTYLE>
            {
                PLAYINGSTYLE.TIGHT,
                PLAYINGSTYLE.AGRESSIVE,
                PLAYINGSTYLE.BLUFFER
            };

            Shuffle(styles);

            for (int i = 1; i < totalPlayers; i++)
            {
                playerDefinitions.Add(PokerPlayerDefinition.Bot(styles[(i - 1) % styles.Count], settings.DefaultBotDifficulty));
                configuredStartingChips.Add(settings.StartingBank);
            }

            ResetRuntimeState();
        }

        public void ConfigurePlayers(IEnumerable<PokerConfiguredPlayer> configuredPlayers)
        {
            if (configuredPlayers == null)
                throw new ArgumentNullException("configuredPlayers");

            playerDefinitions.Clear();
            configuredStartingChips.Clear();

            foreach (PokerConfiguredPlayer configuredPlayer in configuredPlayers)
            {
                if (configuredPlayer == null)
                    continue;

                playerDefinitions.Add(configuredPlayer.Definition.Clone());
                configuredStartingChips.Add(Math.Max(0, configuredPlayer.StartingChips));
            }

            if (playerDefinitions.Count < settings.MinimumPlayers || playerDefinitions.Count > settings.MaximumPlayers)
                throw new InvalidOperationException("Configured player count is outside the allowed range.");

            ResetRuntimeState();
        }

        public void SetNextDealerIndex(int dealerPlayerIndex)
        {
            if (dealerPlayerIndex < 0)
                requestedDealerIndex = null;
            else
                requestedDealerIndex = dealerPlayerIndex;
        }

        public PokerGameSnapshot StartNewHand()
        {
            EnsurePlayersConfigured();
            BuildRuntimePlayersFromConfiguration();

            if (runtimePlayers.Count < settings.MinimumPlayers)
                throw new InvalidOperationException("At least two players with chips are required.");

            DetermineDealerIndex();
            AssignBlindIndexes();

            deck = new Deck();
            deck.Shuffle();
            communityCards.Clear();
            actionLog.Clear();
            waitingForHumanInput = false;
            winnerMessage = string.Empty;
            currentPlayerIndex = -1;
            currentBet = 0;
            minimumRaise = settings.BigBlind;
            blindPostingStep = 0;

            DealHoleCards();

            phase = PokerGamePhase.PostingBlinds;
            bannerMessage = runtimePlayers[dealerIndex].Name + " has the dealer button.";
            RecordAction(bannerMessage);

            return GetSnapshot();
        }

        public PokerGameSnapshot AutoPlayUntilHumanTurn()
        {
            int safetyCounter = 0;
            while (!waitingForHumanInput && phase != PokerGamePhase.HandComplete && phase != PokerGamePhase.MatchComplete)
            {
                AdvanceOneStepInternal();
                safetyCounter++;

                if (safetyCounter > 256)
                    throw new InvalidOperationException("The poker loop exceeded its safety limit.");
            }

            return GetSnapshot();
        }

        public PokerGameSnapshot AdvanceOneStep()
        {
            if (phase == PokerGamePhase.NotStarted)
                return StartNewHand();

            if (!waitingForHumanInput && phase != PokerGamePhase.HandComplete && phase != PokerGamePhase.MatchComplete)
                AdvanceOneStepInternal();

            return GetSnapshot();
        }

        public PokerGameSnapshot HumanFold()
        {
            if (!IsHumanTurn())
                return GetSnapshot();

            ApplyFold(0);
            AdvanceToNextActor();
            waitingForHumanInput = false;

            return GetSnapshot();
        }

        public PokerGameSnapshot HumanCheckOrCall()
        {
            if (!IsHumanTurn())
                return GetSnapshot();

            ApplyCheckOrCall(0);
            AdvanceToNextActor();
            waitingForHumanInput = false;

            return GetSnapshot();
        }

        public PokerGameSnapshot HumanRaiseOrBet(int amount)
        {
            if (!IsHumanTurn())
                return GetSnapshot();

            ApplyRaiseOrBet(0, amount);
            AdvanceToNextActor();
            waitingForHumanInput = false;

            return GetSnapshot();
        }

        public PokerGameSnapshot HumanAllIn()
        {
            if (!IsHumanTurn())
                return GetSnapshot();

            ApplyAllIn(0);
            AdvanceToNextActor();
            waitingForHumanInput = false;

            return GetSnapshot();
        }

        public PokerGameSnapshot GetSnapshot()
        {
            PokerGameSnapshot snapshot = new PokerGameSnapshot
            {
                Phase = phase,
                BannerMessage = bannerMessage,
                WinnerMessage = winnerMessage,
                IsWaitingForHumanInput = waitingForHumanInput,
                CurrentPlayerIndex = currentPlayerIndex,
                PotAmount = GetTotalPotAmount(),
                SmallBlind = settings.SmallBlind,
                BigBlind = settings.BigBlind
            };

            snapshot.ActionLog.AddRange(actionLog);

            if (runtimePlayers.Count == 0)
            {
                for (int i = 0; i < playerDefinitions.Count; i++)
                {
                    snapshot.Players.Add(new PokerPlayerSnapshot
                    {
                        SeatIndex = i,
                        Name = playerDefinitions[i].Name,
                        IsHuman = playerDefinitions[i].IsHuman,
                        ChipStack = i < configuredStartingChips.Count ? configuredStartingChips[i] : settings.StartingBank,
                        HoleCards = new List<PokerCardSnapshot>()
                    });
                }

                return snapshot;
            }

            ApplySnapshotHighlights();
            snapshot.HumanActionOptions = BuildHumanActionOptions();

            for (int i = 0; i < communityCards.Count; i++)
                snapshot.CommunityCards.Add(BuildCardSnapshot(communityCards[i]));

            for (int i = 0; i < runtimePlayers.Count; i++)
            {
                RuntimePlayer player = runtimePlayers[i];
                PokerPlayerSnapshot playerSnapshot = new PokerPlayerSnapshot
                {
                    SeatIndex = i,
                    Name = player.Name,
                    IsHuman = player.IsHuman,
                    IsDealer = i == dealerIndex,
                    IsSmallBlind = i == smallBlindIndex,
                    IsBigBlind = i == bigBlindIndex,
                    IsCurrentTurn = i == currentPlayerIndex,
                    IsFolded = player.Folded,
                    IsBusted = player.ChipStack <= 0 && (phase == PokerGamePhase.HandComplete || phase == PokerGamePhase.MatchComplete),
                    ChipStack = player.ChipStack,
                    AmountInPot = player.RoundContribution,
                    ActionText = player.ActionText,
                    BestHandText = GetBestHandText(player, i),
                    HoleCards = new List<PokerCardSnapshot>()
                };

                for (int j = 0; j < player.HoleCards.Count; j++)
                    playerSnapshot.HoleCards.Add(BuildCardSnapshot(player.HoleCards[j]));

                snapshot.Players.Add(playerSnapshot);
            }

            return snapshot;
        }

        private void AdvanceOneStepInternal()
        {
            waitingForHumanInput = false;

            if (blindPostingStep < 2)
            {
                PostNextBlind();
                return;
            }

            if (CountPlayersStillInHand() <= 1)
            {
                ResolveUncontestedPot();
                return;
            }

            if (currentPlayerIndex < 0)
            {
                AdvanceStreetOrShowdown();
                return;
            }

            RuntimePlayer currentPlayer = runtimePlayers[currentPlayerIndex];
            if (currentPlayer.IsHuman)
            {
                waitingForHumanInput = true;
                bannerMessage = currentPlayer.Name + "'s turn";
                return;
            }

            ProcessBotTurn(currentPlayerIndex);
            AdvanceToNextActor();
        }

        private void PostNextBlind()
        {
            if (blindPostingStep == 0)
            {
                PostBlind(smallBlindIndex, settings.SmallBlind, true);
                blindPostingStep = 1;
                return;
            }

            PostBlind(bigBlindIndex, settings.BigBlind, false);
            blindPostingStep = 2;
            StartPreFlopRound();
        }

        private void PostBlind(int playerIndex, int blindAmount, bool isSmallBlind)
        {
            RuntimePlayer player = runtimePlayers[playerIndex];
            int amount = Math.Min(blindAmount, player.ChipStack);
            if (amount <= 0)
            {
                player.ActionText = isSmallBlind ? "SMALL BLIND 0" : "BIG BLIND 0";
                player.Message = player.Name + " cannot cover the blind.";
            }
            else
            {
                player.ChipStack -= amount;
                player.TotalContribution += amount;
                player.RoundContribution += amount;
                currentBet = Math.Max(currentBet, player.RoundContribution);
                minimumRaise = settings.BigBlind;

                bool allIn = player.ChipStack == 0;
                if (isSmallBlind)
                {
                    player.ActionText = allIn ? "SMALL BLIND ALL IN" : "SMALL BLIND " + amount;
                    player.Message = player.Name + " posts the small blind" + (allIn ? " all-in." : ".");
                }
                else
                {
                    player.ActionText = allIn ? "BIG BLIND ALL IN" : "BIG BLIND " + amount;
                    player.Message = player.Name + " posts the big blind" + (allIn ? " all-in." : ".");
                }
            }

            bannerMessage = player.Message;
            RecordAction(bannerMessage);
        }

        private void StartPreFlopRound()
        {
            phase = PokerGamePhase.PreFlop;
            minimumRaise = settings.BigBlind;
            ResetRoundActionFlags();
            currentPlayerIndex = FindNextPlayerNeedingAction(bigBlindIndex);
        }

        private void StartPostFlopRound(PokerGamePhase nextPhase)
        {
            ResetBettingRound();
            phase = nextPhase;
            currentPlayerIndex = FindNextPlayerNeedingAction(dealerIndex);
        }

        private void ResetBettingRound()
        {
            currentBet = 0;
            minimumRaise = settings.BigBlind;
            for (int i = 0; i < runtimePlayers.Count; i++)
            {
                runtimePlayers[i].RoundContribution = 0;
                runtimePlayers[i].HasActedThisRound = false;
            }
        }

        private void ResetRoundActionFlags()
        {
            for (int i = 0; i < runtimePlayers.Count; i++)
                runtimePlayers[i].HasActedThisRound = false;
        }

        private void AdvanceStreetOrShowdown()
        {
            if (CountPlayersStillInHand() <= 1)
            {
                ResolveUncontestedPot();
                return;
            }

            if (communityCards.Count == 0)
            {
                DealCommunityCards(3);
                bannerMessage = "Dealing the Flop";
                RecordAction(bannerMessage);
                StartPostFlopRound(PokerGamePhase.Flop);
                return;
            }

            if (communityCards.Count == 3)
            {
                DealCommunityCards(1);
                bannerMessage = "Dealing the Turn";
                RecordAction(bannerMessage);
                StartPostFlopRound(PokerGamePhase.Turn);
                return;
            }

            if (communityCards.Count == 4)
            {
                DealCommunityCards(1);
                bannerMessage = "Dealing the River";
                RecordAction(bannerMessage);
                StartPostFlopRound(PokerGamePhase.River);
                return;
            }

            ResolveShowdown();
        }

        private void DealHoleCards()
        {
            for (int cardIndex = 0; cardIndex < 2; cardIndex++)
            {
                for (int playerIndex = 0; playerIndex < runtimePlayers.Count; playerIndex++)
                {
                    RuntimePlayer player = runtimePlayers[playerIndex];
                    Card card = deck.Deal(player.IsHuman);
                    player.HoleCards.Add(card);
                }
            }
        }

        private void DealCommunityCards(int count)
        {
            for (int i = 0; i < count; i++)
                communityCards.Add(deck.Deal(true));
        }

        private void ProcessBotTurn(int playerIndex)
        {
            RuntimePlayer player = runtimePlayers[playerIndex];
            int amountToCall = GetAmountToCall(playerIndex);
            double strength = EstimateHandStrength(playerIndex);
            double styleAggression = GetStyleAggression(player.Definition.PlayingStyle);
            double bluffChance = GetBluffChance(player.Definition.PlayingStyle, player.Definition.Difficulty);
            double pressure = amountToCall <= 0
                ? 0.0
                : Math.Min(1.0, (double)amountToCall / Math.Max(1, player.ChipStack + GetTotalPotAmount()));

            bool canMakeFullRaise = CanPlayerRaise(playerIndex);
            double raiseThreshold;
            double callThreshold;

            switch (player.Definition.Difficulty)
            {
                case DIFFICULTY.EASY:
                    raiseThreshold = 0.82 - styleAggression * 0.20;
                    callThreshold = 0.42 - styleAggression * 0.10;
                    strength += (random.NextDouble() - 0.5) * 0.20;
                    break;

                case DIFFICULTY.HARD:
                    raiseThreshold = 0.72 - styleAggression * 0.18;
                    callThreshold = 0.34 - styleAggression * 0.10;
                    strength += EvaluateBoardTextureBonus(playerIndex);
                    break;

                default:
                    raiseThreshold = 0.77 - styleAggression * 0.18;
                    callThreshold = 0.38 - styleAggression * 0.10;
                    break;
            }

            strength = Clamp01(strength);
            double bluffRoll = random.NextDouble();

            if (amountToCall > 0 && strength + styleAggression - pressure < callThreshold)
            {
                if (player.ChipStack <= amountToCall && strength > 0.45)
                    ApplyAllIn(playerIndex);
                else
                    ApplyFold(playerIndex);
                return;
            }

            if (canMakeFullRaise && (strength + styleAggression > raiseThreshold || (amountToCall == 0 && bluffRoll < bluffChance)))
            {
                ApplyRaiseOrBet(playerIndex, ChooseRaiseAmount(playerIndex, strength));
                return;
            }

            if (amountToCall > 0 && player.ChipStack <= amountToCall && strength > 0.55)
            {
                ApplyAllIn(playerIndex);
                return;
            }

            ApplyCheckOrCall(playerIndex);
        }

        private void ApplyFold(int playerIndex)
        {
            RuntimePlayer player = runtimePlayers[playerIndex];
            player.Folded = true;
            player.HasActedThisRound = true;
            player.ActionText = "FOLDED";
            player.Message = player.Name + " folds.";
            bannerMessage = player.Message;
            RecordAction(player.Message);
        }

        private void ApplyCheckOrCall(int playerIndex)
        {
            RuntimePlayer player = runtimePlayers[playerIndex];
            int amountToCall = GetAmountToCall(playerIndex);

            if (amountToCall <= 0)
            {
                player.HasActedThisRound = true;
                player.ActionText = "CHECK";
                player.Message = player.Name + " checks.";
                bannerMessage = player.Message;
                RecordAction(player.Message);
                return;
            }

            int contribution = Math.Min(amountToCall, player.ChipStack);
            player.ChipStack -= contribution;
            player.TotalContribution += contribution;
            player.RoundContribution += contribution;
            player.HasActedThisRound = true;

            if (player.ChipStack == 0)
            {
                player.ActionText = "ALL IN";
                player.Message = player.Name + " calls all-in for " + contribution + ".";
            }
            else
            {
                player.ActionText = "CALL " + contribution;
                player.Message = player.Name + " calls " + contribution + ".";
            }

            bannerMessage = player.Message;
            RecordAction(player.Message);
        }

        private void ApplyRaiseOrBet(int playerIndex, int requestedAmount)
        {
            RuntimePlayer player = runtimePlayers[playerIndex];
            int amountToCall = GetAmountToCall(playerIndex);
            int available = player.ChipStack;

            if (available <= 0)
            {
                ApplyCheckOrCall(playerIndex);
                return;
            }

            int amount;
            int previousBet = currentBet;
            bool fullRaise;

            if (amountToCall <= 0)
            {
                int minimumBet = Math.Max(settings.BigBlind, minimumRaise);
                amount = Math.Max(requestedAmount, Math.Min(minimumBet, available));
                amount = Math.Min(amount, available);

                player.ChipStack -= amount;
                player.TotalContribution += amount;
                player.RoundContribution += amount;
                player.HasActedThisRound = true;

                int increase = player.RoundContribution - previousBet;
                fullRaise = increase >= minimumRaise || previousBet == 0 && increase >= settings.BigBlind;
                if (player.RoundContribution > currentBet)
                    currentBet = player.RoundContribution;

                if (fullRaise)
                {
                    minimumRaise = Math.Max(settings.BigBlind, increase);
                    ResetOtherPlayersForNewBet(playerIndex);
                }

                if (player.ChipStack == 0)
                {
                    player.ActionText = "ALL IN";
                    player.Message = player.Name + " bets all-in for " + amount + ".";
                }
                else
                {
                    player.ActionText = "BET " + amount;
                    player.Message = player.Name + " bets " + amount + ".";
                }
            }
            else
            {
                int minimumLegalRaise = Math.Min(minimumRaise, Math.Max(0, available - amountToCall));
                int raiseAmount = requestedAmount;
                if (available - amountToCall > 0)
                    raiseAmount = Math.Max(requestedAmount, minimumLegalRaise);
                raiseAmount = Math.Min(raiseAmount, Math.Max(0, available - amountToCall));

                amount = Math.Min(available, amountToCall + raiseAmount);
                player.ChipStack -= amount;
                player.TotalContribution += amount;
                player.RoundContribution += amount;
                player.HasActedThisRound = true;

                int increase = player.RoundContribution - previousBet;
                fullRaise = player.RoundContribution > previousBet && increase >= minimumRaise;
                if (player.RoundContribution > currentBet)
                    currentBet = player.RoundContribution;

                if (fullRaise)
                {
                    minimumRaise = increase;
                    ResetOtherPlayersForNewBet(playerIndex);
                }

                if (player.ChipStack == 0)
                {
                    player.ActionText = "ALL IN";
                    player.Message = player.Name + " moves all-in for " + amount + ".";
                }
                else
                {
                    player.ActionText = "RAISE " + Math.Max(increase, 0);
                    player.Message = player.Name + " raises to " + player.RoundContribution + ".";
                }
            }

            bannerMessage = player.Message;
            RecordAction(player.Message);
        }

        private void ApplyAllIn(int playerIndex)
        {
            RuntimePlayer player = runtimePlayers[playerIndex];
            if (player.ChipStack <= 0)
            {
                ApplyCheckOrCall(playerIndex);
                return;
            }

            int previousBet = currentBet;
            int amount = player.ChipStack;

            player.ChipStack = 0;
            player.TotalContribution += amount;
            player.RoundContribution += amount;
            player.HasActedThisRound = true;

            if (player.RoundContribution > currentBet)
            {
                int increase = player.RoundContribution - currentBet;
                currentBet = player.RoundContribution;
                if (increase >= minimumRaise)
                {
                    minimumRaise = increase;
                    ResetOtherPlayersForNewBet(playerIndex);
                }
            }

            player.ActionText = "ALL IN";
            player.Message = player.Name + " is all-in for " + amount + ".";
            bannerMessage = player.Message;
            RecordAction(player.Message);
        }

        private void AdvanceToNextActor()
        {
            currentPlayerIndex = FindNextPlayerNeedingAction(currentPlayerIndex);
        }

        private int FindNextPlayerNeedingAction(int fromIndex)
        {
            if (runtimePlayers.Count == 0)
                return -1;

            for (int offset = 1; offset <= runtimePlayers.Count; offset++)
            {
                int candidate = NormalizeIndex(fromIndex + offset);
                if (PlayerNeedsAction(candidate))
                    return candidate;
            }

            return -1;
        }

        private bool PlayerNeedsAction(int playerIndex)
        {
            RuntimePlayer player = runtimePlayers[playerIndex];
            if (!player.CanAct)
                return false;

            return !player.HasActedThisRound || player.RoundContribution != currentBet;
        }

        private void ResolveUncontestedPot()
        {
            int winnerIndex = -1;
            for (int i = 0; i < runtimePlayers.Count; i++)
            {
                if (!runtimePlayers[i].Folded)
                {
                    winnerIndex = i;
                    break;
                }
            }

            if (winnerIndex < 0)
            {
                winnerMessage = "No winner could be determined.";
                bannerMessage = winnerMessage;
                RecordAction(winnerMessage);
                FinalizeHand();
                return;
            }

            RuntimePlayer winner = runtimePlayers[winnerIndex];
            int potAmount = GetTotalPotAmount();
            winner.ChipStack += potAmount;
            winner.ActionText = "WINNER";

            winnerMessage = winner.Name + " wins " + potAmount + " uncontested.";
            bannerMessage = winnerMessage;
            RecordAction(winnerMessage);

            ResetContributionsAfterAward();
            FinalizeHand();
        }

        private void ResolveShowdown()
        {
            phase = PokerGamePhase.Showdown;
            RevealShowdownCards();

            Dictionary<int, Hand> bestHands = new Dictionary<int, Hand>();
            for (int i = 0; i < runtimePlayers.Count; i++)
            {
                if (runtimePlayers[i].Folded)
                    continue;

                bestHands[i] = BuildBestHand(runtimePlayers[i]);
            }

            List<ShowdownAward> awards = AwardShowdownPots(bestHands);
            if (awards.Count == 0)
            {
                winnerMessage = "No winner could be determined.";
            }
            else
            {
                List<string> lines = new List<string>();
                for (int i = 0; i < awards.Count; i++)
                    lines.Add(BuildAwardMessage(awards[i]));

                winnerMessage = string.Join(Environment.NewLine, lines.ToArray());
            }

            bannerMessage = winnerMessage;
            if (!string.IsNullOrWhiteSpace(winnerMessage))
                RecordAction(winnerMessage);

            ResetContributionsAfterAward();
            FinalizeHand();
        }

        private List<ShowdownAward> AwardShowdownPots(Dictionary<int, Hand> bestHands)
        {
            List<ShowdownAward> awards = new List<ShowdownAward>();
            List<int> levels = runtimePlayers
                .Where(player => player.TotalContribution > 0)
                .Select(player => player.TotalContribution)
                .Distinct()
                .OrderBy(amount => amount)
                .ToList();

            int previousLevel = 0;
            foreach (int level in levels)
            {
                List<int> contributors = new List<int>();
                for (int i = 0; i < runtimePlayers.Count; i++)
                {
                    if (runtimePlayers[i].TotalContribution >= level)
                        contributors.Add(i);
                }

                int potAmount = (level - previousLevel) * contributors.Count;
                previousLevel = level;

                if (potAmount <= 0)
                    continue;

                List<int> eligible = new List<int>();
                for (int i = 0; i < contributors.Count; i++)
                {
                    int contributorIndex = contributors[i];
                    if (!runtimePlayers[contributorIndex].Folded)
                        eligible.Add(contributorIndex);
                }

                if (eligible.Count == 0)
                    continue;

                List<int> winners = DetermineWinnersForPot(eligible, bestHands);
                if (winners.Count == 0)
                    continue;

                DistributePotAmount(potAmount, winners);
                for (int i = 0; i < winners.Count; i++)
                    runtimePlayers[winners[i]].ActionText = "WINNER";

                awards.Add(new ShowdownAward
                {
                    Amount = potAmount,
                    WinningHand = bestHands[winners[0]],
                    Winners = winners
                });
            }

            return awards;
        }

        private List<int> DetermineWinnersForPot(List<int> eligiblePlayers, Dictionary<int, Hand> bestHands)
        {
            List<int> winners = new List<int>();
            Hand bestHand = null;

            for (int i = 0; i < eligiblePlayers.Count; i++)
            {
                int playerIndex = eligiblePlayers[i];
                Hand candidate = bestHands[playerIndex];

                if (ReferenceEquals(bestHand, null) || candidate > bestHand)
                {
                    winners.Clear();
                    winners.Add(playerIndex);
                    bestHand = candidate;
                }
                else if (!(candidate > bestHand) && !(candidate < bestHand))
                {
                    winners.Add(playerIndex);
                }
            }

            return winners;
        }

        private void DistributePotAmount(int potAmount, List<int> winners)
        {
            if (winners.Count == 0)
                return;

            int baseShare = potAmount / winners.Count;
            int remainder = potAmount % winners.Count;
            List<int> orderedWinners = OrderFromLeftOfDealer(winners);

            for (int i = 0; i < orderedWinners.Count; i++)
            {
                int payout = baseShare;
                if (i < remainder)
                    payout++;

                runtimePlayers[orderedWinners[i]].ChipStack += payout;
            }
        }

        private List<int> OrderFromLeftOfDealer(List<int> winners)
        {
            List<int> ordered = new List<int>();
            for (int offset = 1; offset <= runtimePlayers.Count; offset++)
            {
                int candidate = NormalizeIndex(dealerIndex + offset);
                if (winners.Contains(candidate))
                    ordered.Add(candidate);
            }

            return ordered;
        }

        private string BuildAwardMessage(ShowdownAward award)
        {
            string handName = !ReferenceEquals(award.WinningHand, null) ? award.WinningHand.ToString().ToUpperInvariant() : "SHOWDOWN";
            if (award.Winners.Count == 1)
            {
                return runtimePlayers[award.Winners[0]].Name + " wins " + award.Amount + " with " + handName + ".";
            }

            List<string> names = new List<string>();
            for (int i = 0; i < award.Winners.Count; i++)
                names.Add(runtimePlayers[award.Winners[i]].Name);

            return string.Join(", ", names.ToArray()) + " split " + award.Amount + " with " + handName + ".";
        }

        private void RevealShowdownCards()
        {
            for (int i = 0; i < runtimePlayers.Count; i++)
            {
                if (runtimePlayers[i].Folded)
                    continue;

                for (int j = 0; j < runtimePlayers[i].HoleCards.Count; j++)
                    runtimePlayers[i].HoleCards[j].FaceUp = true;
            }
        }

        private void ResetContributionsAfterAward()
        {
            for (int i = 0; i < runtimePlayers.Count; i++)
            {
                runtimePlayers[i].TotalContribution = 0;
                runtimePlayers[i].RoundContribution = 0;
            }
        }

        private void FinalizeHand()
        {
            SyncConfiguredStacksFromRuntimePlayers();
            RecordBustMessages();

            currentPlayerIndex = -1;
            waitingForHumanInput = false;
            blindPostingStep = 2;

            if (CountPlayersWithChips() <= 1 || GetHumanPlayer().ChipStack <= 0)
                phase = PokerGamePhase.MatchComplete;
            else
                phase = PokerGamePhase.HandComplete;
        }

        private void RecordBustMessages()
        {
            for (int i = 0; i < runtimePlayers.Count; i++)
            {
                if (runtimePlayers[i].ChipStack == 0)
                    RecordAction(runtimePlayers[i].Name + " busted out.");
            }
        }

        private void DetermineDealerIndex()
        {
            if (runtimePlayers.Count == 0)
            {
                dealerIndex = -1;
                return;
            }

            if (requestedDealerIndex.HasValue && requestedDealerIndex.Value >= 0 && requestedDealerIndex.Value < runtimePlayers.Count)
            {
                dealerIndex = requestedDealerIndex.Value;
            }
            else if (dealerIndex >= 0)
            {
                dealerIndex = NormalizeIndex(dealerIndex + 1);
            }
            else
            {
                dealerIndex = random.Next(runtimePlayers.Count);
            }

            requestedDealerIndex = null;
        }

        private void AssignBlindIndexes()
        {
            if (runtimePlayers.Count <= 2)
            {
                smallBlindIndex = dealerIndex;
                bigBlindIndex = NormalizeIndex(dealerIndex + 1);
                return;
            }

            smallBlindIndex = NormalizeIndex(dealerIndex + 1);
            bigBlindIndex = NormalizeIndex(smallBlindIndex + 1);
        }

        private void BuildRuntimePlayersFromConfiguration()
        {
            runtimePlayers.Clear();
            for (int i = 0; i < playerDefinitions.Count; i++)
            {
                int chipStack = i < configuredStartingChips.Count ? configuredStartingChips[i] : settings.StartingBank;
                if (chipStack <= 0)
                    continue;

                runtimePlayers.Add(new RuntimePlayer(playerDefinitions[i], chipStack));
            }
        }

        private void EnsurePlayersConfigured()
        {
            if (playerDefinitions.Count == 0)
                ConfigureDefaultTable(settings.MinimumPlayers);
        }

        private void ApplySnapshotHighlights()
        {
            ClearAllHighlights();

            RuntimePlayer humanPlayer = GetHumanPlayer();
            if (humanPlayer != null && !humanPlayer.Folded && communityCards.Count >= 3)
            {
                HighlightCardsForPlayer(humanPlayer, true);
            }

            if (phase == PokerGamePhase.Showdown || phase == PokerGamePhase.HandComplete || phase == PokerGamePhase.MatchComplete)
            {
                for (int i = 0; i < runtimePlayers.Count; i++)
                {
                    if (!runtimePlayers[i].Folded)
                        HighlightCardsForPlayer(runtimePlayers[i], false);
                }
            }
        }

        private void HighlightCardsForPlayer(RuntimePlayer player, bool includeCommunityCards)
        {
            Hand bestHand = BuildBestHand(player);
            if (bestHand.Count() == 0)
                return;

            for (int i = 0; i < player.HoleCards.Count; i++)
            {
                if (ContainsCard(bestHand, player.HoleCards[i]))
                    player.HoleCards[i].Highlight();
            }

            if (!includeCommunityCards)
                return;

            for (int i = 0; i < communityCards.Count; i++)
            {
                if (ContainsCard(bestHand, communityCards[i]))
                    communityCards[i].Highlight();
            }
        }

        private void ClearAllHighlights()
        {
            for (int i = 0; i < communityCards.Count; i++)
                communityCards[i].UnHighlight();

            for (int i = 0; i < runtimePlayers.Count; i++)
            {
                for (int j = 0; j < runtimePlayers[i].HoleCards.Count; j++)
                    runtimePlayers[i].HoleCards[j].UnHighlight();
            }
        }

        private bool ContainsCard(Hand hand, Card card)
        {
            for (int i = 0; i < hand.Count(); i++)
            {
                Card candidate = hand.getCard(i);
                if (ReferenceEquals(candidate, card))
                    return true;

                if (candidate.getRank() == card.getRank() && candidate.getSuit() == card.getSuit())
                    return true;
            }

            return false;
        }

        private PokerActionOptions BuildHumanActionOptions()
        {
            PokerActionOptions options = new PokerActionOptions();
            if (!IsHumanTurn())
                return options;

            RuntimePlayer human = runtimePlayers[0];
            int amountToCall = GetAmountToCall(0);
            int maxRaiseOrBet = Math.Max(0, human.ChipStack - amountToCall);

            options.CanFold = true;
            options.CanAllIn = human.ChipStack > 0;
            options.CanCheckOrCall = true;
            options.CanRaiseOrBet = CanPlayerRaise(0);
            options.AmountToCall = amountToCall;
            options.MinimumRaise = amountToCall == 0 ? Math.Max(settings.BigBlind, minimumRaise) : minimumRaise;
            options.MaximumRaise = maxRaiseOrBet;
            options.CheckOrCallLabel = amountToCall == 0 ? "Check" : "Call " + amountToCall;
            options.RaiseOrBetLabel = amountToCall == 0 ? "Bet" : "Raise";

            return options;
        }

        private PokerCardSnapshot BuildCardSnapshot(Card card)
        {
            return new PokerCardSnapshot
            {
                ResourceKey = card.getImage(),
                DisplayName = card.ToString(),
                IsFaceUp = card.FaceUp,
                IsHighlighted = card.isHighlighted()
            };
        }

        private string GetBestHandText(RuntimePlayer player, int playerIndex)
        {
            if (player.Folded || communityCards.Count < 3)
                return string.Empty;

            bool canReveal = player.IsHuman || phase == PokerGamePhase.Showdown || phase == PokerGamePhase.HandComplete || phase == PokerGamePhase.MatchComplete;
            if (!canReveal)
                return string.Empty;

            Hand bestHand = BuildBestHand(player);
            return bestHand.Count() == 0 ? string.Empty : bestHand.ToString();
        }

        private Hand BuildBestHand(RuntimePlayer player)
        {
            Hand combined = new Hand();
            for (int i = 0; i < player.HoleCards.Count; i++)
                combined.Add(player.HoleCards[i]);

            for (int i = 0; i < communityCards.Count; i++)
                combined.Add(communityCards[i]);

            return HandCombination.getBestHand(combined);
        }

        private int GetAmountToCall(int playerIndex)
        {
            return Math.Max(0, currentBet - runtimePlayers[playerIndex].RoundContribution);
        }

        private int GetTotalPotAmount()
        {
            int total = 0;
            for (int i = 0; i < runtimePlayers.Count; i++)
                total += runtimePlayers[i].TotalContribution;

            return total;
        }

        private bool CanPlayerRaise(int playerIndex)
        {
            RuntimePlayer player = runtimePlayers[playerIndex];
            int amountToCall = GetAmountToCall(playerIndex);
            int remaining = player.ChipStack - amountToCall;
            if (remaining <= 0)
                return false;

            return remaining >= (currentBet == 0 ? Math.Max(settings.BigBlind, minimumRaise) : minimumRaise);
        }

        private void ResetOtherPlayersForNewBet(int aggressorIndex)
        {
            for (int i = 0; i < runtimePlayers.Count; i++)
            {
                if (i == aggressorIndex || runtimePlayers[i].Folded || runtimePlayers[i].ChipStack == 0)
                    continue;

                runtimePlayers[i].HasActedThisRound = false;
            }
        }

        private int ChooseRaiseAmount(int playerIndex, double strength)
        {
            int pot = Math.Max(GetTotalPotAmount(), settings.BigBlind * 2);
            int amountToCall = GetAmountToCall(playerIndex);
            int maximum = Math.Max(0, runtimePlayers[playerIndex].ChipStack - amountToCall);
            if (maximum <= 0)
                return 0;

            int candidate;
            if (amountToCall == 0)
            {
                if (communityCards.Count == 0)
                    candidate = settings.BigBlind * (strength > 0.85 ? 4 : 3);
                else if (strength > 0.85)
                    candidate = pot;
                else
                    candidate = pot / 2;

                candidate = Math.Max(candidate, Math.Max(settings.BigBlind, minimumRaise));
            }
            else
            {
                if (strength > 0.90)
                    candidate = Math.Max(minimumRaise, pot);
                else if (strength > 0.75)
                    candidate = Math.Max(minimumRaise, pot / 2);
                else
                    candidate = minimumRaise;
            }

            return Math.Min(candidate, maximum);
        }

        private double EstimateHandStrength(int playerIndex)
        {
            RuntimePlayer player = runtimePlayers[playerIndex];
            if (communityCards.Count == 0)
                return EstimatePreFlopStrength(player);

            Hand bestHand = BuildBestHand(player);
            if (bestHand.Count() == 0 || bestHand.getValue().Count == 0)
                return 0.0;

            int category = bestHand.getValue()[0];
            double baseStrength;
            switch (category)
            {
                case 10:
                    baseStrength = 1.0;
                    break;
                case 9:
                    baseStrength = 0.98;
                    break;
                case 8:
                    baseStrength = 0.95;
                    break;
                case 7:
                    baseStrength = 0.90;
                    break;
                case 6:
                    baseStrength = 0.82;
                    break;
                case 5:
                    baseStrength = 0.75;
                    break;
                case 4:
                    baseStrength = 0.68;
                    break;
                case 3:
                    baseStrength = 0.56;
                    break;
                case 2:
                    baseStrength = 0.45;
                    break;
                default:
                    baseStrength = 0.22;
                    break;
            }

            if (communityCards.Count < 5)
                baseStrength += EvaluateDrawBonus(player);

            return Clamp01(baseStrength);
        }

        private double EstimatePreFlopStrength(RuntimePlayer player)
        {
            if (player.HoleCards.Count < 2)
                return 0.0;

            Card first = player.HoleCards[0];
            Card second = player.HoleCards[1];
            int high = Math.Max(first.getRank(), second.getRank());
            int low = Math.Min(first.getRank(), second.getRank());

            double strength = (high + low) / 28.0;
            bool pocketPair = high == low;
            bool suited = first.getSuit() == second.getSuit();
            bool connected = Math.Abs(high - low) <= 1;
            bool oneGap = Math.Abs(high - low) == 2;

            if (pocketPair)
                strength += 0.28 + high / 28.0;
            if (suited)
                strength += 0.08;
            if (connected)
                strength += 0.07;
            else if (oneGap)
                strength += 0.03;
            if (high >= (int)RANK.JACK)
                strength += 0.05;
            if (low >= (int)RANK.TEN)
                strength += 0.04;

            return Clamp01(strength);
        }

        private double EvaluateDrawBonus(RuntimePlayer player)
        {
            double bonus = 0.0;
            int[] suitCounts = new int[5];
            List<int> ranks = new List<int>();

            for (int i = 0; i < player.HoleCards.Count; i++)
            {
                suitCounts[player.HoleCards[i].getSuit()]++;
                ranks.Add(player.HoleCards[i].getRank());
            }

            for (int i = 0; i < communityCards.Count; i++)
            {
                suitCounts[communityCards[i].getSuit()]++;
                ranks.Add(communityCards[i].getRank());
            }

            for (int suit = 1; suit <= 4; suit++)
            {
                if (suitCounts[suit] == 4)
                    bonus += 0.08;
            }

            List<int> distinctRanks = ranks.Distinct().OrderBy(rank => rank).ToList();
            if (distinctRanks.Contains((int)RANK.ACE))
                distinctRanks.Insert(0, 1);

            int runLength = 1;
            for (int i = 1; i < distinctRanks.Count; i++)
            {
                if (distinctRanks[i] == distinctRanks[i - 1] + 1)
                {
                    runLength++;
                    if (runLength >= 4)
                        bonus = Math.Max(bonus, 0.06);
                }
                else if (distinctRanks[i] != distinctRanks[i - 1])
                {
                    runLength = 1;
                }
            }

            return bonus;
        }

        private double EvaluateBoardTextureBonus(int playerIndex)
        {
            RuntimePlayer player = runtimePlayers[playerIndex];
            double bonus = 0.0;

            if (communityCards.Count >= 3)
            {
                int pairedBoardCards = 0;
                Dictionary<int, int> boardRanks = new Dictionary<int, int>();
                for (int i = 0; i < communityCards.Count; i++)
                {
                    int rank = communityCards[i].getRank();
                    if (!boardRanks.ContainsKey(rank))
                        boardRanks.Add(rank, 0);

                    boardRanks[rank]++;
                }

                foreach (KeyValuePair<int, int> entry in boardRanks)
                {
                    if (entry.Value >= 2)
                        pairedBoardCards++;
                }

                if (pairedBoardCards == 0 && player.Definition.PlayingStyle == PLAYINGSTYLE.AGRESSIVE)
                    bonus += 0.04;
            }

            return bonus;
        }

        private double GetStyleAggression(PLAYINGSTYLE style)
        {
            switch (style)
            {
                case PLAYINGSTYLE.AGRESSIVE:
                    return 0.12;
                case PLAYINGSTYLE.BLUFFER:
                    return 0.08;
                default:
                    return -0.04;
            }
        }

        private double GetBluffChance(PLAYINGSTYLE style, DIFFICULTY difficulty)
        {
            double baseChance;
            switch (style)
            {
                case PLAYINGSTYLE.BLUFFER:
                    baseChance = 0.22;
                    break;
                case PLAYINGSTYLE.AGRESSIVE:
                    baseChance = 0.12;
                    break;
                default:
                    baseChance = 0.04;
                    break;
            }

            if (difficulty == DIFFICULTY.EASY)
                return baseChance + 0.04;

            if (difficulty == DIFFICULTY.HARD)
                return baseChance - 0.03;

            return baseChance;
        }

        private RuntimePlayer GetHumanPlayer()
        {
            for (int i = 0; i < runtimePlayers.Count; i++)
            {
                if (runtimePlayers[i].IsHuman)
                    return runtimePlayers[i];
            }

            return null;
        }

        private bool IsHumanTurn()
        {
            return waitingForHumanInput &&
                   currentPlayerIndex >= 0 &&
                   currentPlayerIndex < runtimePlayers.Count &&
                   runtimePlayers[currentPlayerIndex].IsHuman;
        }

        private int CountPlayersStillInHand()
        {
            int count = 0;
            for (int i = 0; i < runtimePlayers.Count; i++)
            {
                if (!runtimePlayers[i].Folded)
                    count++;
            }

            return count;
        }

        private int CountPlayersWithChips()
        {
            int count = 0;
            for (int i = 0; i < runtimePlayers.Count; i++)
            {
                if (runtimePlayers[i].ChipStack > 0)
                    count++;
            }

            return count;
        }

        private void SyncConfiguredStacksFromRuntimePlayers()
        {
            for (int i = 0; i < runtimePlayers.Count; i++)
            {
                if (i < configuredStartingChips.Count)
                    configuredStartingChips[i] = runtimePlayers[i].ChipStack;
            }
        }

        private int NormalizeIndex(int index)
        {
            if (runtimePlayers.Count == 0)
                return -1;

            while (index < 0)
                index += runtimePlayers.Count;

            while (index >= runtimePlayers.Count)
                index -= runtimePlayers.Count;

            return index;
        }

        private void RecordAction(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                actionLog.Add(message);
        }

        private void ResetRuntimeState()
        {
            runtimePlayers.Clear();
            communityCards.Clear();
            deck = null;
            phase = PokerGamePhase.NotStarted;
            bannerMessage = string.Empty;
            winnerMessage = string.Empty;
            waitingForHumanInput = false;
            currentPlayerIndex = -1;
            dealerIndex = -1;
            smallBlindIndex = -1;
            bigBlindIndex = -1;
            currentBet = 0;
            minimumRaise = settings.BigBlind;
            blindPostingStep = 0;
            actionLog.Clear();
        }

        private void EnsureConfiguredChipCount()
        {
            while (configuredStartingChips.Count < playerDefinitions.Count)
                configuredStartingChips.Add(settings.StartingBank);

            while (configuredStartingChips.Count > playerDefinitions.Count)
                configuredStartingChips.RemoveAt(configuredStartingChips.Count - 1);
        }

        private void Shuffle(List<PLAYINGSTYLE> styles)
        {
            for (int i = styles.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                PLAYINGSTYLE temp = styles[i];
                styles[i] = styles[swapIndex];
                styles[swapIndex] = temp;
            }
        }

        private static double Clamp01(double value)
        {
            if (value < 0.0)
                return 0.0;

            if (value > 1.0)
                return 1.0;

            return value;
        }
    }
}
