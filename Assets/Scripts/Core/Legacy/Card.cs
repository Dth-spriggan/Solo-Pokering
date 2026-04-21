using System;

namespace Holdem
{
    public enum RANK
    {
        TWO = 2,
        THREE,
        FOUR,
        FIVE,
        SIX,
        SEVEN,
        EIGHT,
        NINE,
        TEN,
        JACK,
        QUEEN,
        KING,
        ACE
    }

    public enum SUIT
    {
        DIAMONDS = 1,
        CLUBS,
        HEARTS,
        SPADES
    }

    /// <summary>
    /// Card data extracted from the desktop version.
    /// Unity UI can resolve the returned resource key into a Sprite later.
    /// </summary>
    public class Card
    {
        private const string FaceDownResourceKey = "Cards/bf";

        private int rank;
        private int suit;
        private bool faceUp;
        private bool highlight;

        public bool FaceUp
        {
            get { return faceUp; }
            set { faceUp = value; }
        }

        //default two of diamonds
        public Card()
        {
            rank = (int)RANK.TWO;
            suit = (int)SUIT.DIAMONDS;
            faceUp = false;
            highlight = false;
        }

        public Card(RANK rank, SUIT suit)
            : this((int)rank, (int)suit, false)
        {
        }

        public Card(int rank, int suit)
            : this(rank, suit, false)
        {
        }

        public Card(RANK rank, SUIT suit, bool faceUp)
            : this((int)rank, (int)suit, faceUp)
        {
        }

        public Card(int rank, int suit, bool faceUp)
        {
            ValidateCard(rank, suit);

            this.rank = rank;
            this.suit = suit;
            this.faceUp = faceUp;
            highlight = false;
        }

        public Card(Card card)
        {
            rank = card.rank;
            suit = card.suit;
            faceUp = card.faceUp;
            highlight = card.highlight;
        }

        public static string rankToString(int rank)
        {
            switch (rank)
            {
                case 11:
                    return "Jack";
                case 12:
                    return "Queen";
                case 13:
                    return "King";
                case 14:
                    return "Ace";
                default:
                    return rank.ToString();
            }
        }

        public static string suitToString(int suit)
        {
            switch (suit)
            {
                case 1:
                    return "Diamonds";
                case 2:
                    return "Clubs";
                case 3:
                    return "Hearts";
                default:
                    return "Spades";
            }
        }

        public int getRank()
        {
            return rank;
        }

        public int getSuit()
        {
            return suit;
        }

        // Legacy-compatible entry point for the old UI layer.
        // In this Unity project the card textures are imported as sub-sprites,
        // so UI should prefer Resources.LoadAll<Sprite>(card.getImage())[0].
        public string getImage()
        {
            return faceUp ? "Cards/" + GetSpriteKey() : FaceDownResourceKey;
        }

        public string GetSpriteKey()
        {
            if (!faceUp)
                return "bf";

            return GetSuitPrefix(suit) + GetRankIndex(rank).ToString();
        }

        public string GetImageFileName()
        {
            return GetSpriteKey() + ".png";
        }

        public string GetSpriteSubAssetName()
        {
            return GetSpriteKey() + "_0";
        }

        public void setRank(RANK rank)
        {
            this.rank = (int)rank;
        }

        public void setCard(RANK rank, SUIT suit)
        {
            this.rank = (int)rank;
            this.suit = (int)suit;
        }

        public void setCard(int rank, int suit)
        {
            ValidateCard(rank, suit);
            this.rank = rank;
            this.suit = suit;
        }

        public override string ToString()
        {
            if (faceUp)
                return rankToString(rank) + " of " + suitToString(suit);

            return "The card is facedown, you cannot see it!";
        }

        public void Highlight()
        {
            if (!faceUp)
                return;

            highlight = true;
        }

        public void UnHighlight()
        {
            if (!faceUp)
                return;

            highlight = false;
        }

        public bool isHighlighted()
        {
            return highlight;
        }

        //compare rank of cards
        public static bool operator ==(Card a, Card b)
        {
            if (ReferenceEquals(a, b))
                return true;

            if (ReferenceEquals(a, null) || ReferenceEquals(b, null))
                return false;

            return a.rank == b.rank;
        }

        public static bool operator !=(Card a, Card b)
        {
            return !(a == b);
        }

        public static bool operator <(Card a, Card b)
        {
            return a.rank < b.rank;
        }

        public static bool operator >(Card a, Card b)
        {
            return a.rank > b.rank;
        }

        public static bool operator <=(Card a, Card b)
        {
            return a.rank <= b.rank;
        }

        public static bool operator >=(Card a, Card b)
        {
            return a.rank >= b.rank;
        }

        private static void ValidateCard(int rank, int suit)
        {
            if (rank < (int)RANK.TWO || rank > (int)RANK.ACE || suit < (int)SUIT.DIAMONDS || suit > (int)SUIT.SPADES)
                throw new ArgumentOutOfRangeException();
        }

        private static int GetRankIndex(int rank)
        {
            if (rank == (int)RANK.ACE)
                return 0;

            return rank - 1;
        }

        private static string GetSuitPrefix(int suit)
        {
            switch (suit)
            {
                case (int)SUIT.HEARTS:
                    return "0";
                case (int)SUIT.DIAMONDS:
                    return "1";
                case (int)SUIT.CLUBS:
                    return "2";
                default:
                    return "3";
            }
        }
    }
}
