using System;
using System.Collections.Generic;
using System.Linq;

namespace Balbasztro
{
    // ============================================================
    // Parser helper
    // ============================================================
    public static class Parser
    {
        // Parse indexes from a string like "0,1,4"
        // returns unique indexes in the order they appeared (validated later)
        public static int[] ParseIndexes(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return Array.Empty<int>();

            var tokens = input.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var list = new List<int>();

            foreach (var t in tokens)
            {
                if (int.TryParse(t.Trim(), out int idx))
                {
                    if (!list.Contains(idx)) // avoid duplicates
                        list.Add(idx);
                }
            }

            return list.ToArray();
        }
    }

    // ============================================================
    // Card class
    // ============================================================
    public class Card
    {
        public string Rank { get; }
        public string Suit { get; }
        // Chips is the scoring value used for high-card scoring (Ace = 11)
        public int Chips { get; }
        // RankValue is the numeric rank used for straights & comparisons (Ace = 14)
        public int RankValue { get; }
        public int Mult { get; set; }
        public bool Played { get; set; }

        public Card(string rank, string suit, int chips, int rankValue, int mult)
        {
            Rank = rank;
            Suit = suit;
            Chips = chips;
            RankValue = rankValue;
            Mult = mult;
            Played = false;
        }


        public override string ToString()
        {
            return $"{Rank} of {Suit} (Chips:{Chips}, RankValue:{RankValue}, Mult:{Mult})";
        }
    }

    // ============================================================
    // Deck class
    // ============================================================
    public class Deck
    {
        public List<Card> cards;
        public static string[] Ranks =
            { "Ace", "King", "Queen", "Jack", "10", "9", "8", "7", "6", "5", "4", "3", "2" };

        public static string[] Suits = { "Spades", "Hearts", "Clubs", "Diamonds" };
        public Random rng;

        // Chips used for scoring (Ace=11)
        public static int[] RankChipValues = { 11, 10, 10, 10, 10, 9, 8, 7, 6, 5, 4, 3, 2 };

        // Rank numeric values used for straight detection and comparisons 
        public static int[] RankNumericValues = { 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2 };

        public Deck()
        {
            rng = new Random();
            cards = new List<Card>();
            Generate();
        }

       
        public void Generate()
        {
            cards.Clear();
            for (int s = 0; s < Suits.Length; s++)
            {
                for (int r = 0; r < Ranks.Length; r++)
                {
                    // give both chip value and numeric rank value to the Card
                    cards.Add(new Card(Ranks[r], Suits[s], RankChipValues[r], RankNumericValues[r], 0));
                }
            }
        }

        public void Shuffle()
        {
            int n = cards.Count;
            for (int i = n - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var tmp = cards[i];
                cards[i] = cards[j];
                cards[j] = tmp;
            }
        }

        // Draw specified count (returns a new list)
        public List<Card> DrawCards(int count)
        {
            if (count <= 0) return new List<Card>();

            int actual = Math.Min(count, cards.Count);
            var drawn = cards.GetRange(0, actual);
            cards.RemoveRange(0, actual);
            return drawn;
        }
    }

    // ============================================================
    // Game class (core logic)
    // ============================================================
    public class Game
    {
        public Deck Deck { get; private set; }
        public List<Card> PlayerHand { get; private set; } = new List<Card>();

        // lifetime trackers
        public double BlindRequirement { get; private set; } = 300;
        public int TotalChips { get; private set; } = 0; // lifetime raw chips earned (for stats)
        public int TotalMult { get; private set; } = 1;  // lifetime multiplier (for stats)

        // player money updated at round end
        public int Money { get; private set; } = 0;

        // track rounds, ante, and per-round usage
        public int RoundNumber { get; private set; } = 0;
        public int Ante { get; private set; } = 1;

        public const int DefaultPlaysPerRound = 4;
        public const int DefaultDiscardsPerRound = 3;

        public int PlaysLeft { get; private set; } = DefaultPlaysPerRound;
        public int DiscardsLeft { get; private set; } = DefaultDiscardsPerRound;

        // Per-round accumulation: store already multiplied contributions
        public int roundChips = 0;
        public int roundMult = 1;

        // CurrentScore returns the accumulated round score 
        public int CurrentScore => roundChips;

        public Game()
        {
            StartNewRound();
        }

        // start a fresh round
        public void StartNewRound()
        {
            // increment round count
            RoundNumber++;

            // award end-of-round payout for the previous round 
            if (RoundNumber > 1)
            {
                int payout = 3 + (PlaysLeft * 3);
                Money += payout;
            }

            // every 4th round increase the ante by 1 (rounds 4,8,12,...)
            if (RoundNumber % 4 == 0)
            {
                Ante++;
            }

            // reset per-round counters
            PlaysLeft = DefaultPlaysPerRound;
            DiscardsLeft = DefaultDiscardsPerRound;

            // reset per-round accumulators
            roundChips = 0;
            roundMult = 1;

            Deck = new Deck();
            Deck.Shuffle();
            PlayerHand = Deck.DrawCards(9);
        }

        // reset the player's progress back to round 1 (loss condition) 
        // Not Implemented Yet 
        public void ResetToRoundOne()
        {
            RoundNumber = 0;
            Ante = 1;
            BlindRequirement = 300;
            TotalChips = 0;
            TotalMult = 1;
            Money = 0;

            roundChips = 0;
            roundMult = 1;

            StartNewRound();
        }

        // refill hand to 9 cards
        public void RefillHand()
        {
            int missing = 9 - PlayerHand.Count;
            if (missing > 0)
                PlayerHand.AddRange(Deck.DrawCards(missing));
        }

        // Validate a selection of indexes:
        // returns (isValid, error message)
        public (bool isValid, string error) ValidateSelection(int[] indexes)
        {
            if (indexes == null || indexes.Length == 0)
                return (false, "No indexes provided.");

            if (indexes.Length > 5)
                return (false, "Cannot select more than 5 cards.");

            if (indexes.Length != indexes.Distinct().Count())
                return (false, "Duplicate indexes are not allowed.");

            for (int i = 0; i < indexes.Length; i++)
            {
                if (indexes[i] < 0 || indexes[i] >= PlayerHand.Count)
                    return (false, $"Index {indexes[i]} is out of range (0..{PlayerHand.Count - 1}).");
            }

            return (true, null);
        }

        // Select cards by indexes (returns the Card objects)
        public List<Card> SelectCards(int[] indexes)
        {
            var validation = ValidateSelection(indexes);
            if (!validation.isValid)
                throw new ArgumentException(validation.error);

            var selected = new List<Card>();
            foreach (var i in indexes)
                selected.Add(PlayerHand[i]);

            return selected;
        }

        // Remove played/discarded cards safely
        public void RemovePlayedCards(IEnumerable<Card> cards)
        {
            foreach (var c in cards.ToList())
            {
                PlayerHand.Remove(c);
            }
        }

        // Evaluate best hand within the selected cards (simple poker-like evaluator)
        // returns (handType, baseChips, baseMult)
        public (string handType, int baseChips, int baseMult) EvaluateHand(List<Card> selected)
        {
            var groups = selected.GroupBy(c => c.Rank)
                                 .Select(g => new { Rank = g.Key, Count = g.Count() })
                                 .OrderByDescending(g => g.Count)
                                 .ThenByDescending(g => RankToNumeric(g.Rank))
                                 .ToList();

            var suits = selected.Select(c => c.Suit).ToList();
            // use RankValue for straight detection
            var ranks = selected.Select(c => c.RankValue).OrderBy(x => x).ToList();

            bool isFlush = selected.Count >= 5 && suits.Distinct().Count() == 1;
            bool isStraight = IsStraight(ranks);

            // robust royal detection: straight + flush and contains 10..A
            var uniqRanks = ranks.Distinct().ToList();
            bool isRoyal = isStraight && isFlush && uniqRanks.Contains(14) && uniqRanks.Contains(10);

            if (isRoyal) return ("royal flush", 100, 8);
            if (isStraight && isFlush) return ("straight flush", 100, 8);
            if (groups.Count > 0 && groups[0].Count == 4) return ("four of a kind", 60, 7);
            if (groups.Count >= 2 && groups[0].Count == 3 && groups[1].Count == 2) return ("full house", 40, 4);
            if (isFlush) return ("flush", 35, 4);
            if (isStraight) return ("straight", 30, 4);
            if (groups.Count > 0 && groups[0].Count == 3) return ("three of a kind", 30, 3);
            if (groups.Count > 1 && groups[0].Count == 2 && groups[1].Count == 2) return ("two pair", 20, 2);
            if (groups.Count > 0 && groups[0].Count == 2) return ("pair", 10, 2);

            return ("high card", 5, 1);
        }

        // helper: detect straight (consecutive ints), supports Ace-high and Ace-low
        public bool IsStraight(List<int> ranks)
        {
            if (ranks == null || ranks.Count < 5) return false;

            var uniq = ranks.Distinct().OrderBy(x => x).ToList();
            if (uniq.Count < 5) return false;

            // create candidate lists: normal and ace-as-1 (if Ace present)
            var candidates = new List<List<int>> { uniq };

            if (uniq.Contains(14))
            {
                var alt = uniq.ToList();
                // if 1 isn't already present, insert 1 at the start to allow A-2-3-4-5 detection
                if (!alt.Contains(1))
                    alt.Insert(0, 1);
                candidates.Add(alt);
            }

            foreach (var list in candidates)
            {
                for (int start = 0; start <= list.Count - 5; start++)
                {
                    bool ok = true;
                    for (int k = 1; k < 5; k++)
                    {
                        if (list[start + k] != list[start] + k)
                        {
                            ok = false;
                            break;
                        }
                    }
                    if (ok) return true;
                }
            }

            return false;
        }

        // map rank string to numeric for sorting tie-breakers (correct mapping)
        public int RankToNumeric(string rank)
        {
            switch (rank)
            {
                case "Ace": return 14;
                case "King": return 13;
                case "Queen": return 12;
                case "Jack": return 11;
                default: return int.TryParse(rank, out int v) ? v : 0;
            }
        }

        // Play cards: scoring updated to (sum(chips from cards) + Chips from played hand) * (handMult + sum(card.Mult))
        public (bool beatBlind, string handType, int baseChips, int baseMult, int gained) PlayCards(int[] indexes)
        {
            if (PlaysLeft <= 0)
                throw new InvalidOperationException("No plays left this round.");

            // consume a play
            PlaysLeft--;

            // select and validate
            var selected = SelectCards(indexes);

            // evaluate best hand inside the selected cards (only those selected matter)
            var (handType, baseChips, baseMult) = EvaluateHand(selected);

            // Sum chips from selected cards (not max)
            int sumCardChips = selected.Any() ? selected.Sum(c => c.Chips) : 0;
            // Sum card multipliers (Card.Mult defaults to 0)
            int cardMultSum = selected.Sum(c => c.Mult);

            // Combined multiplier: hand mult plus sum of card mults
            int combinedMult = baseMult + cardMultSum;

            // Pre-multiply subtotal: base hand chips + sum of chips from cards
            int preMultiply = baseChips + sumCardChips;

            // Final gained value after applying the combined multiplier
            int gained = preMultiply * combinedMult;

            // Remove cards from hand
            RemovePlayedCards(selected);

            // Update lifetime stats: raw chips is hand + cards
            TotalChips += preMultiply;
            // Update lifetime multiplier stat (avoid zero product)
            TotalMult *= Math.Max(1, combinedMult);

            // Update per-round accumulators
            roundChips += gained;
            roundMult *= Math.Max(1, combinedMult);

            double score = CurrentScore;
            bool beat = score >= BlindRequirement;
            if (beat)
            {
                // increase blind (game difficulty)
               BlindRequirement *= 1.15;
            }
            else
            {
                // refill hand so player can try again
                RefillHand();
            }

            return (beat, handType, baseChips, baseMult, gained);
        }

        // Discard cards (remove them and refill)
        public void DiscardCards(int[] indexes)
        {
            if (DiscardsLeft <= 0)
                throw new InvalidOperationException("No discards left this round.");

            // consume a discard
            DiscardsLeft--;

            var selected = SelectCards(indexes);
            RemovePlayedCards(selected);
            RefillHand();
        }
    }
}