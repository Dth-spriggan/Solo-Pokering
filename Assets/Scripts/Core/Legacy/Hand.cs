using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Holdem
{
    /// <summary>
    /// a class for a list of cards that forms a player's hand
    /// hands have a handValue which is determined using the static class HandCombinations
    /// only hands with a handValue can be compared.
    /// += operator can be used to append a hand to this hand
    /// </summary>
    public class Hand
    {
        private List<Card> myHand;
        private List<int> handValue;
        public Hand()
        {
            myHand = new List<Card>();
            handValue = new List<int>();
        }
        public Hand(Hand otherHand)
        {
            myHand = new List<Card>(otherHand.myHand);
            handValue = new List<int>();
        }
        public Card this[int index]
        {
            get
            {
                return myHand[index];
            }
            set
            {
                myHand[index] = value;
            }
        }
        public void Clear()
        {
            myHand.Clear();
            handValue.Clear();
        }
        public void Add(Card card)
        {
            myHand.Add(card);
        }
        public void Remove(int index)
        {
            myHand.RemoveAt(index);
        }
        public void Remove(Card card)
        {
            myHand.Remove(card);
        }
        public List<int> getValue()
        {
            return this.handValue;
        }
        public void setValue(int value)
        {
            handValue.Add(value);
        }
        public int Count()
        {
            return myHand.Count;
        }
        public Card getCard(int index)
        {
            if (index >= myHand.Count)
                throw new ArgumentOutOfRangeException();
            return myHand[index];
        }
        List<Card> QuickSortRank(List<Card> myCards)
        {
            return myCards
                .OrderByDescending(card => card.getRank())
                .ThenBy(card => card.getSuit())
                .ToList();
        }
        List<Card> QuickSortSuit(List<Card> myCards)
        {
            return myCards
                .OrderBy(card => card.getSuit())
                .ThenByDescending(card => card.getRank())
                .ToList();
        }
        public void sortByRank()
        {
            myHand = QuickSortRank(myHand);
        }
        public void sortBySuit()
        {
            myHand = QuickSortSuit(myHand);
        }
        public override string ToString()
        {
            if (this.handValue.Count() == 0)
                return "No Poker Hand is Found";
            switch (this.handValue[0])
            {
                case 1: return "High Card";
                case 2: return "One Pair";
                case 3: return "Two Pair";
                case 4: return "Three of a Kind";
                case 5: return "Straight";
                case 6: return "Flush";
                case 7: return "Full House";
                case 8: return "Four of a Kind";
                case 9: return "Straight Flush";
                default: return "Royal Flush";
            }
        }
        //check is the hands are equal, NOT their value
        public bool isEqual(Hand a)
        {
            for (int i = 0; i < a.Count(); i++)
            {
                if (a[i] != myHand[i] || a[i].getSuit() != myHand[i].getSuit())
                    return false;
            }
            return true;
        }
        //operator overloads for hand comparison, check if the hand values are equal
        public static bool operator ==(Hand a, Hand b)
        {
            if (a.getValue().Count == 0 || b.getValue().Count == 0)
                throw new NullReferenceException();
            for (int i = 0; i < a.getValue().Count(); i++)
            {
                if (a.getValue()[i] != b.getValue()[i])
                {
                    return false;
                }
            }
            return true;
        }
        
        public static bool operator !=(Hand a, Hand b)
        {
            if (a.getValue().Count == 0 || b.getValue().Count == 0)
                throw new NullReferenceException();
            for (int i = 0; i < a.getValue().Count(); i++)
            {
                if (a.getValue()[i] != b.getValue()[i])
                {
                    return true;
                }
            }
            return false;
        }
        public static bool operator <(Hand a, Hand b)
        {
            if (a.getValue().Count == 0 || b.getValue().Count == 0)
                throw new NullReferenceException();
            for (int i = 0; i < a.getValue().Count(); i++)
            {
                if (a.getValue()[i] < b.getValue()[i])
                {
                    return true;
                }
                if (a.getValue()[i] > b.getValue()[i])
                {
                    return false;
                }
            }
            return false;
        }
        public static bool operator >(Hand a, Hand b)
        {
            if (a.getValue().Count == 0 || b.getValue().Count == 0)
                throw new NullReferenceException();
            for (int i = 0; i < a.getValue().Count(); i++)
            {
                if (a.getValue()[i] > b.getValue()[i])
                {
                    return true;
                }
                if (a.getValue()[i] < b.getValue()[i])
                {
                    return false;
                }

            }
            return false;
        }
        public static bool operator <=(Hand a, Hand b)
        {
            if (a.getValue().Count == 0 || b.getValue().Count == 0)
                throw new NullReferenceException();
            for (int i = 0; i < a.getValue().Count(); i++)
            {
                if (a.getValue()[i] < b.getValue()[i])
                {
                    return true;
                }
                if (a.getValue()[i] > b.getValue()[i])
                {
                    return false;
                }

            }
            return true;
        }
        public static bool operator >=(Hand a, Hand b)
        {
            if (a.getValue().Count == 0 || b.getValue().Count == 0)
                throw new NullReferenceException();
            for (int i = 0; i < a.getValue().Count(); i++)
            {
                if (a.getValue()[i] > b.getValue()[i])
                {
                    return true;
                }
                if (a.getValue()[i] < b.getValue()[i])
                {
                    return false;
                }

            }
            return true;
        }
        public static Hand operator +(Hand a, Hand b)
        {
            for (int i = 0; i < b.Count(); i++)
            {
                a.Add(b[i]);
            }
            return a;
        }
    }
}
