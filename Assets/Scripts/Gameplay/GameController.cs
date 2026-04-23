using System;
using UnityEngine;

namespace SoloPokering.Gameplay
{
    /// <summary>
    /// Unity-facing controller wrapper for UI buttons, sliders, and startup/gameplay panels.
    /// Attach this to a root object and bind your future UI to the public methods.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        [Header("Startup Defaults")]
        [SerializeField] private PokerMatchSettings defaultSettings = new PokerMatchSettings();
        [SerializeField] private bool seedThreeBotsOnAwake = false;

        [Header("Runtime Snapshot")]
        [SerializeField] private PokerTableSessionSnapshot currentState;
        [SerializeField] private string lastFeedback;

        private PokerTableSession session;

        public PokerTableSessionSnapshot CurrentState
        {
            get { return currentState; }
        }

        public string LastFeedback
        {
            get { return lastFeedback; }
        }

        private void Awake()
        {
            EnsureSession();

            if (seedThreeBotsOnAwake)
            {
                session.QueueAddBot("ray-calculator", out lastFeedback);
                session.QueueAddBot("rachel-rampage", out lastFeedback);
                session.QueueAddBot("stewie-smirk", out lastFeedback);
            }

            RefreshSnapshot();
        }

        public void SetHumanPlayerName(string playerName)
        {
            EnsureSession();
            session.RenameHuman(playerName);
            RefreshSnapshot();
        }

        public void SetBotQuery(string query)
        {
            EnsureSession();
            session.SetBotQuery(query);
            RefreshSnapshot();
        }

        public void SetStartingBank(int amount)
        {
            EnsureSession();
            session.SetStartingBank(amount);
            RefreshSnapshot();
        }

        public void SetTurnTimer(int seconds)
        {
            EnsureSession();
            session.SetTurnTimer(seconds);
            RefreshSnapshot();
        }

        public void SetBlindValues(int smallBlind, int bigBlind)
        {
            EnsureSession();
            session.SetBlindValues(smallBlind, bigBlind);
            RefreshSnapshot();
        }

        public void SetBotMode(PokerBotMode mode)
        {
            EnsureSession();
            session.SetBotMode(mode);
            RefreshSnapshot();
        }

        public void QueueAddBot(string profileId)
        {
            EnsureSession();
            session.QueueAddBot(profileId, out lastFeedback);
            RefreshSnapshot();
        }

        public void ToggleKickBot(int seatIndex)
        {
            EnsureSession();
            session.ToggleKickBot(seatIndex, out lastFeedback);
            RefreshSnapshot();
        }

        public void StartNextHand()
        {
            EnsureSession();
            session.StartNextHand(out lastFeedback);
            RefreshSnapshot();
        }

        public void ApplyPendingSeatChanges()
        {
            EnsureSession();
            session.ApplyPendingSeatChangesNow(out lastFeedback);
            RefreshSnapshot();
        }

        public void HumanFold()
        {
            EnsureSession();
            currentState = session.HumanFold();
            UpdateLastFeedbackFromState();
        }

        public void HumanCheckOrCall()
        {
            EnsureSession();
            currentState = session.HumanCheckOrCall();
            UpdateLastFeedbackFromState();
        }

        public void HumanRaiseOrBet(int amount)
        {
            EnsureSession();
            currentState = session.HumanRaiseOrBet(amount);
            UpdateLastFeedbackFromState();
        }

        public void HumanAllIn()
        {
            EnsureSession();
            currentState = session.HumanAllIn();
            UpdateLastFeedbackFromState();
        }

        public void AdvanceAutoPlay()
        {
            EnsureSession();
            currentState = session.AdvanceAutoPlay();
            UpdateLastFeedbackFromState();
        }

        [ContextMenu("Refresh Snapshot")]
        public void RefreshSnapshot()
        {
            EnsureSession();
            currentState = session.GetSnapshot();
            UpdateLastFeedbackFromState();
        }
        public void ResetSession()
        {
            session = null;
            EnsureSession();
            RefreshSnapshot();
        }

        private void EnsureSession()
        {
            if (session != null)
                return;

            PokerMatchSettings settings = defaultSettings != null ? defaultSettings.CreateCopy() : new PokerMatchSettings();
            settings.MaximumPlayers = PokerMatchSettings.RecommendedSeatCount;
            session = new PokerTableSession(settings);
        }

        private void UpdateLastFeedbackFromState()
        {
            if (currentState != null && !string.IsNullOrWhiteSpace(currentState.BannerMessage))
                lastFeedback = currentState.BannerMessage;
        }
    }
}
