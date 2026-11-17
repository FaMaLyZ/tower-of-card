using System.Collections.Generic;
using System.Linq;

/// Poker hand types, from weakest to strongest.
public enum PokerHand
{
    HighCard, Pair, TwoPair, ThreeOfAKind, Straight, Flush, FullHouse,
    FourOfAKind, StraightFlush
}

public class PokerHandResult
{
    public PokerHand hand;
    public string displayName;
    public string shapeName;
    public bool isRoyal;                    // a Straight Flush from 10 up to Ace (shown as "Royal Flush")
    public List<int> scoringIndices = new List<int>();
}

/// Works out which poker hand a group of cards makes, and which cards score.
/// Pure logic - no Unity objects - so it is easy to test and edit.
public static class PokerHandEvaluator
{
    public static string GetName(PokerHand hand)
    {
        switch (hand)
        {
            case PokerHand.HighCard:      return "High Card";
            case PokerHand.Pair:          return "Pair";
            case PokerHand.TwoPair:       return "Two Pair";
            case PokerHand.ThreeOfAKind:  return "Three of a Kind";
            case PokerHand.Straight:      return "Straight";
            case PokerHand.Flush:         return "Flush";
            case PokerHand.FullHouse:     return "Full House";
            case PokerHand.FourOfAKind:   return "Four of a Kind";
            default:                      return "Straight Flush";
        }
    }

    /// The tower-themed name shown beside the poker name. Flavour only: it changes no rules.
    public static string GetShapeName(PokerHand hand)
    {
        switch (hand)
        {
            case PokerHand.HighCard:      return "Loose Cards";
            case PokerHand.Pair:          return "Tent";
            case PokerHand.TwoPair:       return "Twin Tents";
            case PokerHand.ThreeOfAKind:  return "Tripod";
            case PokerHand.Straight:      return "Staircase";
            case PokerHand.Flush:         return "Wall";
            case PokerHand.FullHouse:     return "House";
            case PokerHand.FourOfAKind:   return "Tower Block";
            default:                      return "Spire";
        }
    }

    /// Chips a card adds when it scores: 2-10 = face value, J/Q/K = 10, Ace = 11.
    public static int ChipValue(CardRank rank)
    {
        if (rank == CardRank.Ace) return 11;
        if (rank >= CardRank.Jack) return 10;
        return (int)rank;
    }

    // Ace counts high when sorting groups
    static int HighValue(int rank) => rank == 1 ? 14 : rank;

    /// Evaluates 1-5 cards. Straights and flushes need exactly 5 cards.
    public static PokerHandResult Evaluate(IList<CardData> cards)
    {
        int n = cards.Count;
        if (n == 0) return null;

        // group card indices by rank, biggest group first
        var byRank = new Dictionary<int, List<int>>();
        for (int i = 0; i < n; i++)
        {
            int rank = (int)cards[i].rank;
            if (!byRank.TryGetValue(rank, out var list)) byRank[rank] = list = new List<int>();
            list.Add(i);
        }

        var groups = byRank
            .OrderByDescending(kv => kv.Value.Count)
            .ThenByDescending(kv => HighValue(kv.Key))
            .Select(kv => kv.Value)
            .ToList();

        int largest = groups[0].Count;
        int second = groups.Count > 1 ? groups[1].Count : 0;

        bool flush = n == 5 && cards.All(c => c.suit == cards[0].suit);
        bool straight = n == 5 && IsStraight(byRank.Keys.ToList());

        // strongest first
        PokerHand hand;
        if (straight && flush) hand = PokerHand.StraightFlush;
        else if (largest >= 4) hand = PokerHand.FourOfAKind;
        else if (largest == 3 && second == 2) hand = PokerHand.FullHouse;
        else if (flush) hand = PokerHand.Flush;
        else if (straight) hand = PokerHand.Straight;
        else if (largest == 3) hand = PokerHand.ThreeOfAKind;
        else if (largest == 2 && second == 2) hand = PokerHand.TwoPair;
        else if (largest == 2) hand = PokerHand.Pair;
        else hand = PokerHand.HighCard;

        var result = new PokerHandResult { hand = hand, displayName = GetName(hand), shapeName = GetShapeName(hand) };

        // A-K-Q-J-10 of one suit has its own name
        if (hand == PokerHand.StraightFlush && byRank.ContainsKey(1) && byRank.ContainsKey(13))
        {
            result.isRoyal = true;
            result.displayName = "Royal Flush";
            result.shapeName = "Palace";
        }

        // Which cards score (kickers do not score, just like Balatro)
        var scoring = result.scoringIndices;
        switch (hand)
        {
            case PokerHand.HighCard:
                scoring.Add(HighestCardIndex(cards));
                break;
            case PokerHand.Pair:
            case PokerHand.ThreeOfAKind:
            case PokerHand.FourOfAKind:
                scoring.AddRange(groups[0]);
                break;
            case PokerHand.TwoPair:
                scoring.AddRange(groups[0]);
                scoring.AddRange(groups[1]);
                break;
            default:    // Straight, Flush, Full House, Straight Flush: all cards score
                for (int i = 0; i < n; i++) scoring.Add(i);
                break;
        }
        scoring.Sort();
        return result;
    }

    // 5 different ranks in a row. Ace can be low (A-2-3-4-5) or high (10-J-Q-K-A), not both.
    static bool IsStraight(List<int> ranks)
    {
        if (ranks.Count != 5) return false;
        if (Consecutive(ranks)) return true;
        if (ranks.Contains(1)) return Consecutive(ranks.Select(r => r == 1 ? 14 : r).ToList());
        return false;
    }

    static bool Consecutive(List<int> ranks)
    {
        var sorted = ranks.OrderBy(r => r).ToList();
        return sorted[sorted.Count - 1] - sorted[0] == 4;
    }

    static int HighestCardIndex(IList<CardData> cards)
    {
        int best = 0;
        for (int i = 1; i < cards.Count; i++)
            if (HighValue((int)cards[i].rank) > HighValue((int)cards[best].rank)) best = i;
        return best;
    }
}