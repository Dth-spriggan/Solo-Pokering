using System;
using System.Collections.Generic;

namespace Holdem
{
    /// <summary>
    /// custom made list to support circular structure
    /// all indexes over the count of list are wrapped around
    /// ex. if the count is 5, and index of 5 is passed, the index will be converted into 0
    /// </summary>
    public class PlayerList : IList<Player>
    {
        private readonly List<Player> list = new List<Player>();

        public PlayerList()
        {
        }

        public PlayerList(PlayerList playerList)
        {
            list.AddRange(playerList.list);
        }

        public int IndexOf(Player item)
        {
            return list.IndexOf(item);
        }

        public void Insert(int index, Player item)
        {
            list.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            list.RemoveAt(index);
        }

        public Player this[int index]
        {
            get
            {
                return list[NormalizeIndex(index)];
            }
            set
            {
                list[NormalizeIndex(index)] = value;
            }
        }

        public Player GetPlayer(ref int index)
        {
            index = NormalizeIndex(index);
            return list[index];
        }

        public void Add(Player item)
        {
            list.Add(item);
        }

        public void AddRange(PlayerList players)
        {
            list.AddRange(players.list);
        }

        public void Clear()
        {
            list.Clear();
        }

        public bool Contains(Player item)
        {
            return list.Contains(item);
        }

        public void CopyTo(Player[] array, int arrayIndex)
        {
            list.CopyTo(array, arrayIndex);
        }

        public int Count
        {
            get { return list.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(Player item)
        {
            return list.Remove(item);
        }

        public IEnumerator<Player> GetEnumerator()
        {
            return list.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return list.GetEnumerator();
        }

        //reset all players in list
        public void ResetPlayers()
        {
            foreach (Player player in this)
                player.Reset();
        }

        public void Sort()
        {
            list.Sort((left, right) => left.AmountInPot.CompareTo(right.AmountInPot));
        }

        private int NormalizeIndex(int index)
        {
            if (list.Count == 0)
                throw new InvalidOperationException("Player list is empty.");

            while (index > list.Count - 1)
                index -= list.Count;

            while (index < 0)
                index += list.Count;

            return index;
        }
    }
}
