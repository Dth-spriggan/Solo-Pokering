using System;
using System.Collections.Generic;
using Holdem;

namespace SoloPokering.Gameplay
{
    /// <summary>
    /// Unity-facing orchestration layer built on top of the legacy Holdem core.
    /// It preserves the old game rules while exposing a cleaner state/snapshot API for UI work.
    /// </summary>
    public sealed class OfflinePokerGame
    {
        private readonly PokerMatchSettings settings;
        private readonly List<PokerPlayerDefinition> playerDefinitions;
        private readonly List<int> configuredStartingChips;
        private readonly List<string> actionLog;
        private readonly Random random;

        private Table table;
        private PokerGamePhase phase;
        private string bannerMessage;
        private string winnerMessage;
        private bool waitingForHumanInput;
        private int handStepCounter;

        public OfflinePokerGame(PokerMatchSettings settings)
        {
            this.settings = settings ?? new PokerMatchSettings();
            this.settings.Validate();

            playerDefinitions = new List<PokerPlayerDefinition>();
            configuredStartingChips = new List<int>();
            actionLog = new List<string>();
            random = new Random();

            phase = PokerGamePhase.NotStarted;
            bannerMessage = string.Empty;
            winnerMessage = string.Empty;
            waitingForHumanInput = false;
            handStepCounter = 0;
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
            int humanChips = configuredStartingChips.Count > 0 ? configuredStartingChips[0] : settings.StartingBank;

            if (playerDefinitions.Count == 0)
                playerDefinitions.Add(human);
            else if (playerDefinitions[0].IsHuman)
                playerDefinitions[0] = human;
            else
                playerDefinitions.Insert(0, human);

            EnsureConfiguredChipCount();
            configuredStartingChips[0] = humanChips;
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
            playerDefinitions.Add(PokerPlayerDefinition.Human(settings.HumanPlayerName));

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

            EnsureConfiguredChipCount();
            configuredStartingChips[0] = settings.StartingBank;

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
                configuredStartingChips.Add(configuredPlayer.StartingChips);
            }

            if (playerDefinitions.Count < settings.MinimumPlayers || playerDefinitions.Count > settings.MaximumPlayers)
                throw new InvalidOperationException("Configured player count is outside the allowed range.");

            ResetRuntimeState();
        }

        public PokerGameSnapshot StartNewHand()
        {
            EnsureTableReady();

            actionLog.Clear();
            waitingForHumanInput = false;
            winnerMessage = string.Empty;
            bannerMessage = "New Round";
            handStepCounter = 0;

            table.startNextMatch();
            table.DealHoleCards();
            phase = PokerGamePhase.PreFlop;

            RecordAction(bannerMessage);

            return AutoPlayUntilHumanTurn();
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
            EnsureTableReady();

            if (phase == PokerGamePhase.NotStarted)
                return StartNewHand();

            if (!waitingForHumanInput && phase != PokerGamePhase.HandComplete && phase != PokerGamePhase.MatchComplete)
                AdvanceOneStepInternal();

            return GetSnapshot();
        }

        public PokerGameSnapshot HumanFold()
        {
            EnsureHumanTurn();

            table[0].Fold(table.getPot());
            RecordAction(table[0].Message);
            bannerMessage = table[0].Message;
            waitingForHumanInput = false;

            return AutoPlayUntilHumanTurn();
        }

        public PokerGameSnapshot HumanCheckOrCall()
        {
            EnsureHumanTurn();

            if (table[0].getAmountToCall(table.getPot()) != 0)
                table[0].Call(table.getPot());
            else
                table[0].Check(table.getPot());

            RecordAction(table[0].Message);
            bannerMessage = table[0].Message;
            waitingForHumanInput = false;

            return AutoPlayUntilHumanTurn();
        }

        public PokerGameSnapshot HumanRaiseOrBet(int amount)
        {
            EnsureHumanTurn();

            if (amount < table.getPot().MinimumRaise)
                throw new ArgumentOutOfRangeException("amount");

            int amountToCall = table[0].getAmountToCall(table.getPot());
            int maxRaise = Math.Max(0, table[0].ChipStack - amountToCall);
            if (amount > maxRaise)
                throw new ArgumentOutOfRangeException("amount");

            int aggressorIndex = table.decrementIndex(table.getCurrentIndex());
            if (amountToCall == 0)
                table[0].Bet(amount, table.getPot(), aggressorIndex);
            else
                table[0].Raise(amount, table.getPot(), aggressorIndex);

            RecordAction(table[0].Message);
            bannerMessage = table[0].Message;
            waitingForHumanInput = false;

            return AutoPlayUntilHumanTurn();
        }

        public PokerGameSnapshot HumanAllIn()
        {
            EnsureHumanTurn();

            table[0].AllIn(table.getPot(), table.decrementIndex(table.getCurrentIndex()));
            RecordAction(table[0].Message);
            bannerMessage = table[0].Message;
            waitingForHumanInput = false;

            return AutoPlayUntilHumanTurn();
        }

        public PokerGameSnapshot GetSnapshot()
        {
            PokerGameSnapshot snapshot = new PokerGameSnapshot
            {
                Phase = phase,
                BannerMessage = bannerMessage,
                WinnerMessage = winnerMessage,
                IsWaitingForHumanInput = waitingForHumanInput
            };

            snapshot.ActionLog.AddRange(actionLog);

            if (table == null)
            {
                snapshot.SmallBlind = settings.SmallBlind;
                snapshot.BigBlind = settings.BigBlind;

                for (int i = 0; i < playerDefinitions.Count; i++)
                {
                    PokerPlayerDefinition definition = playerDefinitions[i];
                    snapshot.Players.Add(new PokerPlayerSnapshot
                    {
                        SeatIndex = i,
                        Name = definition.Name,
                        IsHuman = definition.IsHuman,
                        ChipStack = i < configuredStartingChips.Count ? configuredStartingChips[i] : settings.StartingBank,
                        HoleCards = new List<PokerCardSnapshot>()
                    });
                }

                return snapshot;
            }

            snapshot.CurrentPlayerIndex = table.getCurrentIndex();
            snapshot.PotAmount = table.getPot().Amount;
            snapshot.SmallBlind = table.SmallBlind;
            snapshot.BigBlind = table.BigBlind;
            snapshot.HumanActionOptions = BuildHumanActionOptions();

            int sbIndex = table.incrementIndex(table.getDealerPosition());
            int bbIndex = table.incrementIndex(sbIndex);

            for (int i = 0; i < table.getCommunityCards().Count(); i++)
                snapshot.CommunityCards.Add(BuildCardSnapshot(table.getCommunityCards()[i]));

            for (int i = 0; i < table.getPlayers().Count; i++)
            {
                Player player = table[i];
                PokerPlayerSnapshot playerSnapshot = new PokerPlayerSnapshot
                {
                    SeatIndex = i,
                    Name = player.Name,
                    IsHuman = i == 0,
                    IsDealer = i == table.getDealerPosition(),
                    IsSmallBlind = i == sbIndex,
                    IsBigBlind = i == bbIndex,
                    IsCurrentTurn = i == table.getCurrentIndex(),
                    IsFolded = player.IsFolded(),
                    IsBusted = player.isbusted,
                    ChipStack = player.ChipStack,
                    AmountInPot = player.AmountInPot,
                    ActionText = player.SimplifiedMessage,
                    BestHandText = GetBestHandText(player, i),
                    HoleCards = new List<PokerCardSnapshot>()
                };

                int cardCount = Math.Min(2, player.getHand().Count());
                for (int j = 0; j < cardCount; j++)
                    playerSnapshot.HoleCards.Add(BuildCardSnapshot(player.getHand()[j]));

                snapshot.Players.Add(playerSnapshot);
            }

            return snapshot;
        }

        private void AdvanceOneStepInternal()
        {
            waitingForHumanInput = false;
            handStepCounter++;

            if (table.PlayerWon())
            {
                ResolveSinglePlayerWin();
                return;
            }

            if (table.beginNextTurn())
            {
                table.setCurrentIndex(table.incrementIndex(table.getCurrentIndex()));

                if (handStepCounter == 1)
                {
                    table.PaySmallBlind();
                    phase = PokerGamePhase.PostingBlinds;
                    bannerMessage = table[table.getCurrentIndex()].Message;
                    RecordAction(bannerMessage);
                    return;
                }

                if (handStepCounter == 2)
                {
                    table.PayBigBlind();
                    phase = PokerGamePhase.PostingBlinds;
                    bannerMessage = table[table.getCurrentIndex()].Message;
                    RecordAction(bannerMessage);
                    return;
                }

                phase = GetPhaseFromHandSize(table[0].getHand().Count());

                if (table.getCurrentIndex() == 0)
                {
                    waitingForHumanInput = true;
                    bannerMessage = table[0].Name + "'s turn";
                    return;
                }

                ProcessBotTurn();
                return;
            }

            table.TurnCount = 0;

            if (table[0].getHand().Count() == 2)
            {
                table.DealFlop();
                phase = PokerGamePhase.Flop;
                bannerMessage = "Dealing the Flop";
                RecordAction(bannerMessage);
                ResetBettingRoundToDealer();
                return;
            }

            if (table[0].getHand().Count() == 5)
            {
                table.DealTurn();
                phase = PokerGamePhase.Turn;
                bannerMessage = "Dealing the Turn";
                RecordAction(bannerMessage);
                ResetBettingRoundToDealer();
                return;
            }

            if (table[0].getHand().Count() == 6)
            {
                table.DealRiver();
                phase = PokerGamePhase.River;
                bannerMessage = "Dealing the River";
                RecordAction(bannerMessage);
                ResetBettingRoundToDealer();
                return;
            }

            ResolveShowdown();
        }

        private void ProcessBotTurn()
        {
            int seatIndex = table.getCurrentIndex();
            PokerPlayerDefinition definition = playerDefinitions[seatIndex];
            AIPlayer currentPlayer = (AIPlayer)table[seatIndex];

            if (definition.Difficulty == DIFFICULTY.HARD)
            {
                Hand humanHoleCards = new Hand();
                humanHoleCards.Add(table[0].getHand()[0]);
                humanHoleCards.Add(table[0].getHand()[1]);
                currentPlayer.CalculateHandValueHard(humanHoleCards, new Deck(table.getDeck()));
            }
            else if (definition.Difficulty == DIFFICULTY.MEDIUM)
            {
                currentPlayer.CalculateHandValue(CountPlayersCompeting());
            }

            currentPlayer.MakeADecision(table.getPot(), table.decrementIndex(seatIndex));
            table[seatIndex] = currentPlayer;

            bannerMessage = currentPlayer.Message;
            RecordAction(bannerMessage);
        }

        private void ResolveSinglePlayerWin()
        {
            table.setCurrentIndex(table.incrementIndexShowdown(table.getCurrentIndex()));
            table[table.getCurrentIndex()].CollectMoney(table.getPot());

            winnerMessage = table[table.getCurrentIndex()].Message;
            bannerMessage = winnerMessage;
            RecordAction(winnerMessage);

            FinalizeHand();
        }

        private void ResolveShowdown()
        {
            phase = PokerGamePhase.Showdown;
            RevealShowdownCards();

            table.ShowDown();
            winnerMessage = table.winnermessage;
            bannerMessage = winnerMessage;

            if (!string.IsNullOrWhiteSpace(winnerMessage))
                RecordAction(winnerMessage);

            FinalizeHand();
        }

        private void FinalizeHand()
        {
            MarkBustedPlayers();

            if (CountActivePlayers() <= 1)
            {
                phase = PokerGamePhase.MatchComplete;
                Player champion = GetChampion();
                if (champion != null)
                {
                    bannerMessage = champion.Name + " wins the match.";
                    RecordAction(bannerMessage);
                }
            }
            else
            {
                phase = PokerGamePhase.HandComplete;
            }

            waitingForHumanInput = false;
        }

        private void RevealShowdownCards()
        {
            for (int i = 0; i < table.getPlayers().Count; i++)
            {
                Player player = table[i];
                if (player.IsFolded() || player.isbusted || player.getHand().Count() < 2)
                    continue;

                player.getHand()[0].FaceUp = true;
                player.getHand()[1].FaceUp = true;

                for (int j = 0; j < player.getHand().Count(); j++)
                    player.getHand()[j].UnHighlight();

                Hand bestHand = HandCombination.getBestHand(new Hand(player.getHand()));
                for (int cardIndex = 0; cardIndex < player.getHand().Count(); cardIndex++)
                {
                    for (int bestIndex = 0; bestIndex < bestHand.Count(); bestIndex++)
                    {
                        Card handCard = player.getHand().getCard(cardIndex);
                        Card bestCard = bestHand.getCard(bestIndex);
                        if (bestCard == handCard && bestCard.getSuit() == handCard.getSuit())
                            handCard.Highlight();
                    }
                }
            }
        }

        private void MarkBustedPlayers()
        {
            for (int i = 0; i < table.getPlayers().Count; i++)
            {
                Player player = table[i];
                if (!player.isbusted && player.ChipStack == 0)
                {
                    player.Leave();
                    RecordAction(player.Message);
                }
            }
        }

        private void ResetBettingRoundToDealer()
        {
            table.setCurrentIndex(table.getDealerPosition());
            table.getPot().AgressorIndex = table.getDealerPosition();
        }

        private void EnsureTableReady()
        {
            if (playerDefinitions.Count == 0)
                ConfigureDefaultTable(settings.MinimumPlayers);

            if (table != null)
                return;

            PlayerList players = new PlayerList();
            for (int i = 0; i < playerDefinitions.Count; i++)
            {
                int chipStack = i < configuredStartingChips.Count ? configuredStartingChips[i] : settings.StartingBank;
                players.Add(playerDefinitions[i].BuildPlayer(chipStack));
            }

            table = new Table(players, settings.SmallBlind, settings.BigBlind);
            phase = PokerGamePhase.NotStarted;
            waitingForHumanInput = false;
            bannerMessage = string.Empty;
            winnerMessage = string.Empty;
        }

        private void EnsureHumanTurn()
        {
            EnsureTableReady();

            if (!waitingForHumanInput || table.getCurrentIndex() != 0)
                throw new InvalidOperationException("It is not the human player's turn.");
        }

        private PokerActionOptions BuildHumanActionOptions()
        {
            PokerActionOptions options = new PokerActionOptions();
            if (table == null || !waitingForHumanInput)
                return options;

            Player human = table[0];
            int amountToCall = human.getAmountToCall(table.getPot());
            int maxRaiseOrBet = Math.Max(0, human.ChipStack - amountToCall);

            options.CanFold = true;
            options.CanAllIn = human.ChipStack > 0;
            options.CanCheckOrCall = true;
            options.CanRaiseOrBet = table.getPot().MinimumRaise <= maxRaiseOrBet;
            options.AmountToCall = amountToCall;
            options.MinimumRaise = table.getPot().MinimumRaise;
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

        private string GetBestHandText(Player player, int seatIndex)
        {
            if (player.IsFolded() || player.isbusted || player.getHand().Count() < 5)
                return string.Empty;

            bool revealCards = seatIndex == 0 || phase == PokerGamePhase.HandComplete || phase == PokerGamePhase.MatchComplete || phase == PokerGamePhase.Showdown;
            if (!revealCards)
                return string.Empty;

            return HandCombination.getBestHand(new Hand(player.getHand())).ToString();
        }

        private PokerGamePhase GetPhaseFromHandSize(int handSize)
        {
            if (handSize <= 2)
                return PokerGamePhase.PreFlop;
            if (handSize == 5)
                return PokerGamePhase.Flop;
            if (handSize == 6)
                return PokerGamePhase.Turn;

            return PokerGamePhase.River;
        }

        private int CountPlayersCompeting()
        {
            int playersCompeting = 0;
            for (int i = 0; i < table.getPlayers().Count; i++)
            {
                if (table[i].isbusted || table[i].IsFolded())
                    continue;

                playersCompeting++;
            }

            return Math.Max(playersCompeting, 2);
        }

        private int CountActivePlayers()
        {
            int activePlayers = 0;
            for (int i = 0; i < table.getPlayers().Count; i++)
            {
                if (!table[i].isbusted)
                    activePlayers++;
            }

            return activePlayers;
        }

        private Player GetChampion()
        {
            for (int i = 0; i < table.getPlayers().Count; i++)
            {
                if (!table[i].isbusted)
                    return table[i];
            }

            return null;
        }

        private void RecordAction(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                actionLog.Add(message);
        }

        private void ResetRuntimeState()
        {
            table = null;
            phase = PokerGamePhase.NotStarted;
            waitingForHumanInput = false;
            bannerMessage = string.Empty;
            winnerMessage = string.Empty;
            handStepCounter = 0;
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
    }
}
