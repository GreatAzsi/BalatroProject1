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
    // Upgrades
    // ============================================================
    public class Upgrade
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public int Cost { get; }
        public int AddChips { get; }
        public int AddMult { get; }
        public string TargetSuit { get; }
        public string TargetRank { get; }
        public string TargetHandType { get; }

        public Upgrade(string id, string name, string description, int cost, int addChips = 0, int addMult = 0, string targetSuit = null, string targetRank = null, string targetHandType = null)
        {
            Id = id;
            Name = name;
            Description = description;
            Cost = cost;
            AddChips = addChips;
            AddMult = addMult;
            TargetSuit = targetSuit;
            TargetRank = targetRank;
            TargetHandType = targetHandType;
        }

        public override string ToString() => $"{Name} ({Cost}$)";
    }

    // ============================================================
    // Cards
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
    // Deck
    // ============================================================
    public class Deck
    {
        public List<Card> cards;
        public static string[] Ranks =
            { "Ace", "King", "Queen", "Jack", "10", "9", "8", "7", "6", "5", "4", "3", "2" };

        public static string[] Suits = { "Spades", "Hearts", "Clubs", "Diamonds" };

        public Random rng;
       
        public static int[] RankChipValues = { 11, 10, 10, 10, 10, 9, 8, 7, 6, 5, 4, 3, 2 };

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
    // Game class 
    // ============================================================
    public class Game
    {
        public static Game Instance { get; private set; }

        public Deck Deck { get; private set; }
        public List<Card> PlayerHand { get; private set; } = new List<Card>();

        // lifetime trackers
        public double BlindRequirement { get; private set; } = 300;
        public int TotalChips { get; private set; } = 0; 
        public int TotalMult { get; private set; } = 1;  

        // player money
        public int Money { get; private set; } = 0;
        public int RoundNumber { get; private set; } = 0;
        public int Ante { get; private set; } = 1;

        public const int DefaultPlaysPerRound = 4;
        public const int DefaultDiscardsPerRound = 3;

        public int PlaysLeft { get; private set; } = DefaultPlaysPerRound;
        public int DiscardsLeft { get; private set; } = DefaultDiscardsPerRound;

        public int roundChips = 0;
        public int roundMult = 1;

        
        public int CurrentScore => roundChips;

        
        public ObservableCollection<Upgrade> OwnedUpgrades { get; } = new ObservableCollection<Upgrade>();

        //Upgrades
        public static List<Upgrade> AvailableUpgrades = new List<Upgrade>
        {   
            new Upgrade("mult+4", "Joker", "Adds +4 to combined multiplier", 2, addMult: 4),

            new Upgrade("Diamond +3 Mult", "Greedy Joker", "Played cards with Diamond suit give +3 Mult when scored", 5, addChips: 0, addMult: 3, targetSuit: "Diamonds"),
            new Upgrade("Heart +3 Mult", "Lusty Joker", "Played cards with Heart suit give +3 Mult when scored", 5, addChips: 0, addMult: 3, targetSuit: "Hearts"),
            new Upgrade("Spade +3 Mult", "Wrathful Joker", "Played cards with Spade suit give +3 Mult when scored", 5, addChips: 0, addMult: 3, targetSuit: "Spades"),
            new Upgrade("Club +3 Mult", "Gluttonous Joker", "Played cards with Club suit give +3 Mult when scored", 5, addChips: 0, addMult: 3, targetSuit: "Clubs"),
            new Upgrade("Spade +50 Chips", "Arrowhead", "Played cards with Spade suit give +50 Chips when scored", 7, addChips: 50, addMult: 0, targetSuit: "Spades"),
            new Upgrade("Club +7 Mult", "Onyx Agate", "Played cards with Club suit give +7 Mult when scored", 5, addChips: 0, addMult: 7, targetSuit: "Clubs"),

            new Upgrade("Ace +20 Chips +4 Mult", "Scholar", "Played Aces give +20 Chips and +4 Mult when scored", 4, addChips: 20, addMult: 4, targetHandType: "flush"),

            new Upgrade("Pair +8 Mult", "Jolly Joker", "Grants +8 mult when you play a Pair", 3, addChips: 0, addMult: 8, targetSuit: null, targetRank: null, targetHandType: "pair"),
            new Upgrade("Two Pair +10 Mult", "Mad Joker", "Grants +10 mult when you play Two Pair", 4, addChips: 0, addMult: 10, targetHandType: "two pair"),
            new Upgrade("Three of a kind +12 Mult", "Zany Joker", "Grants +12 mult when you play a Three of a kind", 4, addChips: 0, addMult: 12, targetSuit: null, targetRank: null, targetHandType: "three of a kind"),
            new Upgrade("Straight +12 Mult", "Crazy Joker", "Grants +12 mult when you play a Straight", 4, addChips: 0, addMult: 12, targetSuit: null, targetRank: null, targetHandType: "straight"),
            new Upgrade("Flush +10 Mult", "Droll Joker", "Grants +10 mult when you play Flush", 4, addChips: 0, addMult: 10, targetHandType: "flush"),

            new Upgrade("Pair +50 Chips", "Sly Joker", "Grants +50 Chips when you play a Pair", 3, addChips: 50, addMult: 0, targetSuit: null, targetRank: null, targetHandType: "pair"),
            new Upgrade("Two Pair +80 Chips", "Clever joker", "Grants +50 Chips when you play a Two Pair", 4, addChips: 80, addMult: 0, targetSuit: null, targetRank: null, targetHandType: "pair"),
            new Upgrade("Three of a kind +100 Chips", "Wily Joker", "Grants +100 Chips when you play a Three of a kind", 4, addChips: 100, addMult: 0, targetSuit: null, targetRank: null, targetHandType: "three of a kind"),
            new Upgrade("Straight +100 Chips", "Devious Joker", "Grants +100 Chips when you play a Straight", 4, addChips: 100, addMult: 0, targetSuit: null, targetRank: null, targetHandType: "straight"),
            new Upgrade("Flush +80 Chips", "Crafty Joker", "Grants +80 Chips when you play Flush", 4, addChips: 80, addMult: 0, targetHandType: "flush"),
        };

        private readonly Random rnd = new Random();

        
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
        // optionally allow free purchases (useful for testing)
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

            return true;
        }

        
        public void EndRound(bool won)
        {
            
            int payout = 3 + (PlaysLeft * 3);
            Money += payout;

            
            if (won)
            {
                var choices = GetChoices(3);
                UpgradeChoicesAvailable?.Invoke(choices);
            }
        }

        // start a fresh round (call this after upgrades dialog is handled)
        public void StartNewRound()
        {
            
            RoundNumber++;

            
            if (RoundNumber % 4 == 0)
            {
                Ante++;
            }

            
            PlaysLeft = DefaultPlaysPerRound;
            DiscardsLeft = DefaultDiscardsPerRound;

            
            roundChips = 0;
            roundMult = 1;

            Deck = new Deck();
            Deck.Shuffle();
            PlayerHand = Deck.DrawCards(9);
        }

        
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

        
        public void RefillHand()
        {
            int missing = 9 - PlayerHand.Count;
            if (missing > 0)
                PlayerHand.AddRange(Deck.DrawCards(missing));
        }

        
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

        // Remove played/discarded cards
        public void RemovePlayedCards(IEnumerable<Card> cards)
        {
            foreach (var c in cards.ToList())
            {
                PlayerHand.Remove(c);
            }
        }

        // Select best hand within the selected cards (simple poker-like evaluator)
        
        public (string handType, int baseChips, int baseMult) EvaluateHand(List<Card> selected)
        {
            var groups = selected.GroupBy(c => c.Rank)
                                 .Select(g => new { Rank = g.Key, Count = g.Count() })
                                 .OrderByDescending(g => g.Count)
                                 .ThenByDescending(g => RankToNumeric(g.Rank))
                                 .ToList();

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

        // Play cards: scoring updated to support suit-, rank- and hand-type-targeted upgrades.
        // final gained = (baseHandChips + sum(card.Chips) + globalUpgradesChips + sum(per-card suitUpgrades) + sum(per-card rankUpgrades) + handTypeUpgrades) *
        //                (baseHandMult + sum(card.Mult) + globalUpgradesMult + sum(per-card suitUpgradesMult) + sum(per-card rankUpgradesMult) + handTypeUpgradesMult)
        public (bool beatBlind, string handType, int baseChips, int baseMult, int gained) PlayCards(int[] indexes)
        {
            if (PlaysLeft <= 0)
                throw new InvalidOperationException("No plays left this round.");

            
            PlaysLeft--;

            
            var selected = SelectCards(indexes);

            
            var (handType, baseChips, baseMult) = EvaluateHand(selected);

            
            int sumCardChips = selected.Any() ? selected.Sum(c => c.Chips) : 0;
            
            int cardMultSum = selected.Sum(c => c.Mult);

            
            var globalUpgrades = OwnedUpgrades.Where(u => string.IsNullOrEmpty(u.TargetSuit) && string.IsNullOrEmpty(u.TargetRank) && string.IsNullOrEmpty(u.TargetHandType)).ToList();
            var suitTargeted = OwnedUpgrades.Where(u => !string.IsNullOrEmpty(u.TargetSuit)).ToList();
            var rankTargeted = OwnedUpgrades.Where(u => !string.IsNullOrEmpty(u.TargetRank)).ToList();
            var handTypeTargeted = OwnedUpgrades.Where(u => !string.IsNullOrEmpty(u.TargetHandType)).ToList();

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

            // Hand-type targeted upgrades apply once per play if the evaluated handType matches the upgrade's TargetHandType.
            int handTypeChips = 0;
            int handTypeMult = 0;
            if (!string.IsNullOrEmpty(handType) && handTypeTargeted.Any())
            {
                var matching = handTypeTargeted.Where(u => string.Equals(u.TargetHandType, handType, StringComparison.OrdinalIgnoreCase));
                if (matching.Any())
                {
                    handTypeChips = matching.Sum(u => u.AddChips);
                    handTypeMult = matching.Sum(u => u.AddMult);
                }
            }

            // Combined multiplier:
            int combinedMult = baseMult + cardMultSum + globalUpgradesMult + suitSpecificMult + rankSpecificMult + handTypeMult;

            // Pre-multiply subtotal: base hand chips + sum of chips from cards + global upgrades + suit-specific chips + rank-specific chips + hand-type chips
            int preMultiply = baseChips + sumCardChips + globalUpgradesChips + suitSpecificChips + rankSpecificChips + handTypeChips;

            // Final gained value after applying the combined multiplier
            int gained = preMultiply * combinedMult;

            // Remove cards from hand
            RemovePlayedCards(selected);

            // Update lifetime stats: raw chips is hand + cards + global + suit/rank/hand-type specific (counted once per play)
            TotalChips += preMultiply;
            // Update lifetime multiplier stat (avoid zero product)
            TotalMult *= Math.Max(1, combinedMult);

            // Update per-round accumulators
            roundChips += gained;
            roundMult *= Math.Max(1, combinedMult);

            // evaluate whether player beat the blind now 
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