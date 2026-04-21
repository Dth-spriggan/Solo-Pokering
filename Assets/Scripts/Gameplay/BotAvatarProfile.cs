using System;
using System.Collections.Generic;
using Holdem;

namespace SoloPokering.Gameplay
{
    public sealed class BotAvatarProfile
    {
        private static readonly IReadOnlyList<BotAvatarProfile> Catalog = new List<BotAvatarProfile>
        {
            new BotAvatarProfile("ray-calculator", "Ray", "RY", "Numbers First", "A tight grinder who trusts math over drama.", "#5FB36D", "#163B24", PLAYINGSTYLE.TIGHT, DIFFICULTY.MEDIUM),
            new BotAvatarProfile("rachel-rampage", "Rachel", "RA", "Pressure Engine", "Aggressive and fearless when the pot starts swelling.", "#D86C52", "#4A1E18", PLAYINGSTYLE.AGRESSIVE, DIFFICULTY.HARD),
            new BotAvatarProfile("stewie-smirk", "Stewie", "ST", "Chaos Bluff", "Loves bluff lines and weird timing spots.", "#D4A13C", "#3C2A0C", PLAYINGSTYLE.BLUFFER, DIFFICULTY.MEDIUM),
            new BotAvatarProfile("ivy-quill", "Ivy", "IV", "Quiet Trap", "Plays compact ranges and waits for clean value.", "#65A0C8", "#182B3A", PLAYINGSTYLE.TIGHT, DIFFICULTY.EASY),
            new BotAvatarProfile("atlas-burn", "Atlas", "AT", "Heavy Hands", "Pushes edges hard and leans on bet pressure.", "#C95F7A", "#421621", PLAYINGSTYLE.AGRESSIVE, DIFFICULTY.MEDIUM),
            new BotAvatarProfile("nova-silk", "Nova", "NV", "Velvet Bluff", "Looks calm, bluffs wide, and never shuts up.", "#8C78D8", "#221A45", PLAYINGSTYLE.BLUFFER, DIFFICULTY.EASY),
            new BotAvatarProfile("bishop-lock", "Bishop", "BP", "Cold Reader", "Tight ranges, hard calls, and very little noise.", "#53B5A5", "#123A35", PLAYINGSTYLE.TIGHT, DIFFICULTY.HARD),
            new BotAvatarProfile("ember-jack", "Ember", "EM", "Fast Tempo", "Tries to win initiative early and keep it.", "#E68A2E", "#4B2305", PLAYINGSTYLE.AGRESSIVE, DIFFICULTY.EASY),
            new BotAvatarProfile("vega-glitch", "Vega", "VG", "Mixed Signals", "Mixes styles and likes putting people in weird spots.", "#B96BE2", "#2E123C", PLAYINGSTYLE.BLUFFER, DIFFICULTY.HARD),
            new BotAvatarProfile("mira-slate", "Mira", "MI", "Late Bloom", "Careful early, sharper after the flop.", "#7593B2", "#1E2C3C", PLAYINGSTYLE.TIGHT, DIFFICULTY.MEDIUM)
        };

        private readonly string searchableText;

        private BotAvatarProfile(
            string id,
            string displayName,
            string initials,
            string title,
            string description,
            string accentHex,
            string secondaryHex,
            PLAYINGSTYLE playingStyle,
            DIFFICULTY difficulty)
        {
            Id = id;
            DisplayName = displayName;
            Initials = initials;
            Title = title;
            Description = description;
            AccentHex = accentHex;
            SecondaryHex = secondaryHex;
            PlayingStyle = playingStyle;
            Difficulty = difficulty;
            searchableText = string.Join(" ", id, displayName, initials, title, description, playingStyle, difficulty).ToLowerInvariant();
        }

        public string Id { get; private set; }
        public string DisplayName { get; private set; }
        public string Initials { get; private set; }
        public string Title { get; private set; }
        public string Description { get; private set; }
        public string AccentHex { get; private set; }
        public string SecondaryHex { get; private set; }
        public PLAYINGSTYLE PlayingStyle { get; private set; }
        public DIFFICULTY Difficulty { get; private set; }

        public PokerPlayerDefinition ToPlayerDefinition()
        {
            return PokerPlayerDefinition.Bot(PlayingStyle, Difficulty, DisplayName);
        }

        public bool Matches(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;

            string normalized = query.Trim().ToLowerInvariant();
            return searchableText.Contains(normalized);
        }

        public static IReadOnlyList<BotAvatarProfile> GetCatalog()
        {
            return Catalog;
        }

        public static BotAvatarProfile GetById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            for (int i = 0; i < Catalog.Count; i++)
            {
                if (string.Equals(Catalog[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    return Catalog[i];
            }

            return null;
        }
    }
}
