using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    // Upgrade model
    // ============================================================
    public class Upgrade
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public int Cost { get; }
        // optional gameplay effects (example)
        public int AddChips { get; }
        public int AddMult { get; }

        // Optional target suit — if non-null, the upgrade applies only to cards of that suit.
        // Example: "Spades"
        public string TargetSuit { get; }

        // Optional target rank — if non-null, the upgrade applies only to cards of that rank.
        // Example: "Jack", "Queen"
        public string TargetRank { get; }

        public Upgrade(string id, string name, string description, int cost, int addChips = 0, int addMult = 0, string targetSuit = null, string targetRank = null)
        {
            Id = id;
            Name = name;
            Description = description;
            Cost = cost;
            AddChips = addChips;
            AddMult = addMult;
            TargetSuit = targetSuit;
            TargetRank = targetRank;
        }

        public override string ToString() => $"{Name} ({Cost}$)";
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

        // ToString override
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
        // singleton instance to simplify UI wiring
        public static Game Instance { get; private set; }

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

        // Owned upgrades (observable so UI can bind / observe)
        public ObservableCollection<Upgrade> OwnedUpgrades { get; } = new ObservableCollection<Upgrade>();

        // Pool of possible upgrades (editable)
        public static List<Upgrade> AvailableUpgrades = new List<Upgrade>
        {
            //new Upgrade("chip+50", "+50 Chips", "Grants +50 chips bonus to plays (adds to chips sum)", 12, addChips: 50),
            //new Upgrade("mult+1", "+1 Mult", "Adds +1 to combined multiplier", 10, addMult: 1),
            //new Upgrade("hands+1", "+1 Hand", "Gives one extra play this round", 20),
            //new Upgrade("chip+10", "+10 Chips", "Adds +10 chips to sums", 12, addChips: 10),
            new Upgrade("mult+2", "+2 Mult", "Adds +2 to combined multiplier", 10, addMult: 2),

            // Spade-targeted upgrade: +50 chips per Spade card in the played selection
            new Upgrade("spade+50", "Spade +50", "Grants +50 chips to Spade cards (applies per Spade in selection)", 1, addChips: 50, addMult: 0, targetSuit: "Spades"),

            // Rank-targeted example: +50 chips for Jacks only (per Jack in selection)
            new Upgrade("jack+50", "Jack +50", "Grants +50 chips to Jack cards (applies per Jack in selection)", 1, addChips: 50, addMult: 0, targetSuit: null, targetRank: "Jack")
        };

        private readonly Random rnd = new Random();

        // event raised when upgrade choices are available at end of round
        public event Action<List<Upgrade>> UpgradeChoicesAvailable;

        public Game()
        {
            Instance = this;
            StartNewRound();
        }

        // return up to count choices (simple random sample, excluding duplicates)
        public List<Upgrade> GetChoices(int count)
        {
            var pool = AvailableUpgrades.ToList();
            var choices = new List<Upgrade>();
            while (choices.Count < count && pool.Count > 0)
            {
                int i = rnd.Next(pool.Count);
                choices.Add(pool[i]);
                pool.RemoveAt(i);
            }
            return choices;
        }

        // attempt to purchase upgrade; returns true on success
        // optionally allow free purchases (useful when granting upgrades after a win)
        public bool PurchaseUpgrade(Upgrade u, bool free = false)
        {
            if (u == null) return false;
            if (!free && Money < u.Cost) return false;

            if (!free)
                Money -= u.Cost;

            OwnedUpgrades.Add(u);

            // Apply lifetime effects (statistics)
            if (u.AddChips != 0) TotalChips += u.AddChips;
            if (u.AddMult != 0) TotalMult += u.AddMult;

            // NOTE:
            // Suit- or rank-targeted upgrades are applied during PlayCards per-card,
            // so we don't apply suit/rank-specific chips here at purchase time.

            return true;
        }

        // called when a round ends (plays exhausted) to pay out and raise upgrade UI
        // now accepts whether the round was won (beat the blind); only show upgrades on win
        public void EndRound(bool won)
        {
            // payout formula: 3 + (hands left * 3) - at end PlaysLeft usually zero
            int payout = 3 + (PlaysLeft * 3);
            Money += payout;

            // raise event with choices only when player won the round
            if (won)
            {
                var choices = GetChoices(3);
                UpgradeChoicesAvailable?.Invoke(choices);
            }
        }

        // start a fresh round (call this after upgrades dialog is handled)
        public void StartNewRound()
        {
            // increment round count
            RoundNumber++;

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

            // all suits and rank values for general checks
            var suits = selected.Select(c => c.Suit).ToList();
            var ranks = selected.Select(c => c.RankValue).OrderBy(x => x).ToList();

            // check for flush: any suit with at least 5 cards
            bool isFlush = suits.GroupBy(s => s).Any(g => g.Count() >= 5);

            // check for straight anywhere among selected (distinct ranks)
            bool isStraight = IsStraight(ranks);

            // detect straight-flush or royal flush by checking each suit separately
            bool isStraightFlush = false;
            bool isRoyal = false;
            foreach (var suitGroup in selected.GroupBy(c => c.Suit))
            {
                var suitRanks = suitGroup.Select(c => c.RankValue).OrderBy(x => x).ToList();
                if (suitRanks.Count >= 5 && IsStraight(suitRanks))
                {
                    isStraightFlush = true;
                    var uniq = suitRanks.Distinct().ToList();
                    if (uniq.Contains(14) && uniq.Contains(10))
                        isRoyal = true;
                    break;
                }
            }

            if (isRoyal) return ("royal flush", 100, 8);
            if (isStraightFlush) return ("straight flush", 100, 8);
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

        // Play cards: scoring updated to support suit- and rank-targeted upgrades.
        // final gained = (baseHandChips + sum(card.Chips) + globalUpgradesChips + sum(per-card suitUpgrades) + sum(per-card rankUpgrades)) *
        //                (baseHandMult + sum(card.Mult) + globalUpgradesMult + sum(per-card suitUpgradesMult) + sum(per-card rankUpgradesMult))
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

            // Owned upgrades: split into global (no target), suit-targeted and rank-targeted
            var globalUpgrades = OwnedUpgrades.Where(u => string.IsNullOrEmpty(u.TargetSuit) && string.IsNullOrEmpty(u.TargetRank)).ToList();
            var suitTargeted = OwnedUpgrades.Where(u => !string.IsNullOrEmpty(u.TargetSuit)).ToList();
            var rankTargeted = OwnedUpgrades.Where(u => !string.IsNullOrEmpty(u.TargetRank)).ToList();

            int globalUpgradesChips = globalUpgrades.Any() ? globalUpgrades.Sum(u => u.AddChips) : 0;
            int globalUpgradesMult = globalUpgrades.Any() ? globalUpgrades.Sum(u => u.AddMult) : 0;

            // For suit-targeted and rank-targeted upgrades we apply their AddChips/AddMult to matching cards in the selection.
            int suitSpecificChips = 0;
            int suitSpecificMult = 0;
            int rankSpecificChips = 0;
            int rankSpecificMult = 0;

            if (selected.Any())
            {
                foreach (var card in selected)
                {
                    if (suitTargeted.Any())
                    {
                        var perCardChips = suitTargeted.Where(u => string.Equals(u.TargetSuit, card.Suit, StringComparison.OrdinalIgnoreCase)).Sum(u => u.AddChips);
                        var perCardMult = suitTargeted.Where(u => string.Equals(u.TargetSuit, card.Suit, StringComparison.OrdinalIgnoreCase)).Sum(u => u.AddMult);

                        suitSpecificChips += perCardChips;
                        suitSpecificMult += perCardMult;
                    }

                    if (rankTargeted.Any())
                    {
                        var perCardChipsR = rankTargeted.Where(u => string.Equals(u.TargetRank, card.Rank, StringComparison.OrdinalIgnoreCase)).Sum(u => u.AddChips);
                        var perCardMultR = rankTargeted.Where(u => string.Equals(u.TargetRank, card.Rank, StringComparison.OrdinalIgnoreCase)).Sum(u => u.AddMult);

                        rankSpecificChips += perCardChipsR;
                        rankSpecificMult += perCardMultR;
                    }
                }
            }

            // Combined multiplier:
            int combinedMult = baseMult + cardMultSum + globalUpgradesMult + suitSpecificMult + rankSpecificMult;

            // Pre-multiply subtotal: base hand chips + sum of chips from cards + global upgrades + suit-specific chips + rank-specific chips (per matching card)
            int preMultiply = baseChips + sumCardChips + globalUpgradesChips + suitSpecificChips + rankSpecificChips;

            // Final gained value after applying the combined multiplier
            int gained = preMultiply * combinedMult;

            // Remove cards from hand
            RemovePlayedCards(selected);

            // Update lifetime stats: raw chips is hand + cards + global + suit/rank-specific (counted once per play)
            TotalChips += preMultiply;
            // Update lifetime multiplier stat (avoid zero product)
            TotalMult *= Math.Max(1, combinedMult);

            // Update per-round accumulators
            roundChips += gained;
            roundMult *= Math.Max(1, combinedMult);

            // evaluate whether player beat the blind now (use updated roundChips)
            double score = CurrentScore;
            bool beat = score >= BlindRequirement;

            if (beat)
            {
                // increase blind (game difficulty)
                BlindRequirement *= 1.15;

                // If player beat the blind, end the round immediately so payout and upgrade choices happen now.
                // Call EndRound once and return — do not continue refilling or end again below.
                EndRound(true);
                return (true, handType, baseChips, baseMult, gained);
            }

            // If round not won: if no plays left – round ended, trigger end-of-round flow (loss)
            if (PlaysLeft <= 0)
            {
                EndRound(false);
            }
            else
            {
                // when round not ended and not won, refill so player can continue trying
                RefillHand();
            }

            return (false, handType, baseChips, baseMult, gained);
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